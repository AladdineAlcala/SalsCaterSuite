using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Packages.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>One resolved, validated dish selection row (contract §15.2).</summary>
/// <param name="CourseId">The course the dish was chosen for — frozen on the row (RSK-063).</param>
/// <param name="MenuId">The catalogue key of the dish.</param>
/// <param name="PrepInstruction">
/// The chef's per-event instruction, trimmed; <c>null</c> = not stated (DEC-146, DEC-155).
/// </param>
public sealed record ResolvedSelectionRow(int CourseId, string MenuId, string? PrepInstruction);

/// <summary>One resolved, validated add-on line with its <b>frozen</b> values (contract §16.3).</summary>
public sealed record ResolvedAddonLine(
    int AddonId,
    string Description,
    decimal Amount,
    string? Unit,
    AddonPricingBasis PricingBasis,
    int? AddonCategoryId,
    string? AddonCategoryName);

/// <summary>
/// The validated line items of a booking — the dishes and the add-on lines, with their frozen
/// values and the frozen add-on total (contract §15.3, §16.0).
/// </summary>
public sealed record ResolvedBookingLineItems(
    IReadOnlyList<ResolvedSelectionRow> Selections,
    IReadOnlyList<ResolvedAddonLine> Addons,
    decimal AddonsTotal);

/// <summary>
/// The <b>one</b> resolver of a booking's line items (BR-15, BR-35, BR-39, DEC-158).
/// <para>
/// The quote (<c>POST /api/bookings/quote</c>) and the create (<c>POST /api/bookings</c>) price
/// the same request shapes through this resolver, so the figure a workbench quotes and the
/// figure a confirmed booking freezes are produced by the same code — an add-on total or a
/// selection that one accepts can never be refused by the other (BR-31, DEC-153).
/// </para>
/// </summary>
public class BookingLineItemResolver
{
    /// <summary>The maximum length of a per-event prep instruction (DEC-146).</summary>
    public const int MaxPrepInstructionLength = 500;

    private readonly BookingMenuCourseSetBuilder _courseSets;
    private readonly IAddonRepository _addons;

    public BookingLineItemResolver(BookingMenuCourseSetBuilder courseSets, IAddonRepository addons)
    {
        _courseSets = courseSets;
        _addons = addons;
    }

    /// <summary>
    /// Resolves and validates the request's selection and add-ons.
    /// </summary>
    /// <param name="packageId">
    /// The tier; <c>null</c> for a bespoke booking. A selection on a bespoke booking is a
    /// <c>400</c>, because there is no ruleset to validate it against.
    /// </param>
    /// <param name="numberOfHeads">The headcount the defaults are derived from.</param>
    /// <param name="selections">
    /// The submitted dishes. <c>null</c> means "no selection". An explicitly <b>empty</b> array is
    /// the caller's decision: a <c>400</c> on create and a clear on replace (contract §15.3).
    /// </param>
    /// <param name="addons">The submitted add-on lines. <c>null</c> means "no add-ons".</param>
    public async Task<Result<ResolvedBookingLineItems>> ResolveAsync(
        int? packageId,
        int numberOfHeads,
        IReadOnlyList<BookingMenuSelectionEntryRequest>? selections,
        IReadOnlyList<BookingAddonRequest>? addons,
        CancellationToken cancellationToken = default)
    {
        var addonResult = await ResolveAddonsAsync(numberOfHeads, addons, cancellationToken);

        if (!addonResult.Succeeded)
        {
            return Result<ResolvedBookingLineItems>.Failure(addonResult.Errors);
        }

        var selectionResult = await ResolveSelectionsAsync(
            packageId, numberOfHeads, selections, cancellationToken);

        if (!selectionResult.Succeeded)
        {
            return Result<ResolvedBookingLineItems>.Failure(selectionResult.Errors);
        }

        return Result<ResolvedBookingLineItems>.Success(new ResolvedBookingLineItems(
            selectionResult.Data!,
            addonResult.Data!,
            BookingAmountDueCalculator.Round2(addonResult.Data!.Sum(line => line.Amount))));
    }

    /// <summary>
    /// Validates and freezes the submitted dishes against the tier's live ruleset (BR-15,
    /// BR-35): a dish must be a <b>live</b> member of its course's qualified pool, it may appear
    /// only once, and every rule's quota must be satisfied <b>exactly</b>.
    /// </summary>
    private async Task<Result<List<ResolvedSelectionRow>>> ResolveSelectionsAsync(
        int? packageId,
        int numberOfHeads,
        IReadOnlyList<BookingMenuSelectionEntryRequest>? selections,
        CancellationToken cancellationToken)
    {
        var rows = new List<ResolvedSelectionRow>();

        if (selections is null || selections.Count == 0)
        {
            return Result<List<ResolvedSelectionRow>>.Success(rows);
        }

        if (!packageId.HasValue)
        {
            return Result<List<ResolvedSelectionRow>>.Failure(
                "A dish selection requires a catering package.");
        }

        var courseSet = await _courseSets.BuildAsync(packageId, numberOfHeads, cancellationToken);

        var duplicate = selections
            .GroupBy(entry => entry.MenuId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            return Result<List<ResolvedSelectionRow>>.Failure(
                $"The dish '{duplicate.Key}' is selected more than once.");
        }

        var rulesByCourse = courseSet.Rules.ToDictionary(rule => rule.CourseId);

        foreach (var entry in selections)
        {
            if (string.IsNullOrWhiteSpace(entry.MenuId))
            {
                return Result<List<ResolvedSelectionRow>>.Failure(
                    "Every selected dish must name a menu.");
            }

            var menuId = entry.MenuId.Trim();

            if (!courseSet.MenuById.TryGetValue(menuId, out var menu))
            {
                return Result<List<ResolvedSelectionRow>>.Failure(
                    $"The dish '{menuId}' was not found in the menu catalogue.");
            }

            // The dish must be a live member of the pool of the course it is submitted for. A
            // dish the catalogue has since re-assigned belongs to another pool, so it is not a
            // member of this one (BR-15).
            if (menu.CourseId != entry.CourseId)
            {
                var courseName = rulesByCourse.TryGetValue(entry.CourseId, out var rule)
                    ? rule.CourseName
                    : courseSet.CourseNameById.TryGetValue(entry.CourseId, out var name)
                        ? name
                        : "unnamed";

                return Result<List<ResolvedSelectionRow>>.Failure(
                    $"The dish '{menuId}' is not a live member of the '{courseName}' course's qualified pool.");
            }

            var prepInstruction = NormalisePrepInstruction(entry.PrepInstruction);

            if (prepInstruction is not null && prepInstruction.Length > MaxPrepInstructionLength)
            {
                return Result<List<ResolvedSelectionRow>>.Failure(
                    $"The prep instruction for dish '{menuId}' must not exceed " +
                    $"{MaxPrepInstructionLength} characters.");
            }

            rows.Add(new ResolvedSelectionRow(entry.CourseId, menuId, prepInstruction));
        }

        var compliance = PackageSelectionCompliance.Evaluate(
            courseSet.Rules.Select(rule => (rule.CourseId, rule.CourseName, rule.RequiredQuota)),
            courseSet.CourseNameById,
            rows.Select(row => (row.CourseId, row.MenuId)));

        var breach = PackageSelectionCompliance.BuildBreachMessage(compliance);

        return breach is null
            ? Result<List<ResolvedSelectionRow>>.Success(rows)
            : Result<List<ResolvedSelectionRow>>.Failure(breach);
    }

    /// <summary>
    /// Validates and freezes the submitted add-on lines (BR-39, DEC-158): an add-on must be an
    /// <b>active</b> catalogue row, it may appear only once, and its line total may not be
    /// negative. When the request states no amount the server derives the default —
    /// <c>catalogueAmount x heads</c> for a <c>PerGuest</c> add-on, the catalogue amount for a
    /// <c>Flat</c> one.
    /// </summary>
    private async Task<Result<List<ResolvedAddonLine>>> ResolveAddonsAsync(
        int numberOfHeads,
        IReadOnlyList<BookingAddonRequest>? addons,
        CancellationToken cancellationToken)
    {
        var lines = new List<ResolvedAddonLine>();

        if (addons is null || addons.Count == 0)
        {
            return Result<List<ResolvedAddonLine>>.Success(lines);
        }

        var duplicate = addons
            .GroupBy(addon => addon.AddonId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            return Result<List<ResolvedAddonLine>>.Failure(
                $"The add-on with ID {duplicate.Key} is listed more than once.");
        }

        var catalogue = (await _addons.GetLiveByIdsAsync(
                addons.Select(addon => addon.AddonId), cancellationToken))
            .ToDictionary(addon => addon.AddonId);

        foreach (var request in addons)
        {
            if (!catalogue.TryGetValue(request.AddonId, out var addon))
            {
                return Result<List<ResolvedAddonLine>>.Failure(
                    $"The add-on with ID {request.AddonId} was not found.");
            }

            if (!addon.IsActive)
            {
                return Result<List<ResolvedAddonLine>>.Failure(
                    $"The add-on '{addon.Description}' is not active in the catalogue.");
            }

            // DEC-158: the request's amount IS the line total. When it is absent the server
            // derives the default from the catalogue's unit amount and the basis.
            var amount = request.Amount.HasValue
                ? BookingAmountDueCalculator.Round2(request.Amount.Value)
                : BookingAmountDueCalculator.Round2(addon.LineAmountFor(numberOfHeads));

            if (amount < 0m)
            {
                return Result<List<ResolvedAddonLine>>.Failure(
                    $"The line total of the add-on '{addon.Description}' must be zero or greater.");
            }

            lines.Add(new ResolvedAddonLine(
                AddonId: addon.AddonId,
                Description: addon.Description,
                Amount: amount,
                Unit: addon.Unit,
                PricingBasis: addon.EffectivePricingBasis,
                AddonCategoryId: addon.AddonCategoryId,
                AddonCategoryName: addon.AddonCategory?.AddonCategoryName));
        }

        return Result<List<ResolvedAddonLine>>.Success(lines);
    }

    /// <summary>A blank instruction is "not stated" (DEC-155): trimmed, empty becomes null.</summary>
    public static string? NormalisePrepInstruction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
