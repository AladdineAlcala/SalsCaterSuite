using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of the <b>location</b> preview request (spec §3.6, BR-11, contract
/// §13.7).
/// <para>
/// The headcount must be at least one. A headcount <b>below</b> the tier's floor is allowed and
/// reported — the preview is a what-if, not a booking (spec §4.3). No maximum is ruled, so none is
/// enforced.
/// </para>
/// <para>
/// The selection rules are the course-rules preview's rules: the <c>selections</c> member must be
/// present, a dish must exist and must not be soft-deleted, a dish must belong to the course the
/// selection names, and the same dish may not be named twice. An <b>empty</b> array is a valid
/// "no selection" request; a <c>null</c> member is a rejected payload.
/// </para>
/// </summary>
public class PackageLocationPreviewValidator : AbstractValidator<PackageLocationPreviewRequest>
{
    private readonly IMenuRepository _menus;

    public PackageLocationPreviewValidator(IMenuRepository menus)
    {
        _menus = menus;

        RuleFor(x => x.Pax)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Pax must be at least 1.");

        RuleFor(x => x.Selections)
            .NotNull()
            .WithMessage("Selections must be supplied.");

        // The same dish twice in one selection.
        RuleFor(x => x.Selections)
            .Must(selections => selections is null
                || selections
                    .Select(selection => selection.MenuId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() == selections.Count)
            .WithMessage(request => $"Dish {FirstRepeatedMenuId(request)} is selected more than once.");

        // The dish must exist and must not be soft-deleted.
        RuleForEach(x => x.Selections!)
            .MustAsync(async (selection, cancellationToken) =>
                await DishExistsAsync(selection.MenuId, cancellationToken))
            .WithMessage((request, selection) => $"Dish {selection.MenuId} does not exist.");

        // The dish's own course must equal the course the selection names. An unknown dish is
        // reported by the rule above only, so it is not reported twice.
        RuleForEach(x => x.Selections!)
            .MustAsync(async (selection, cancellationToken) =>
                await DishBelongsToCourseAsync(selection.CourseId, selection.MenuId, cancellationToken))
            .WithMessage((request, selection) =>
                $"Dish {selection.MenuId} does not belong to course {selection.CourseId}.");
    }

    /// <summary>The first dish code the selection names more than once.</summary>
    private static string FirstRepeatedMenuId(PackageLocationPreviewRequest request) =>
        request.Selections?
            .GroupBy(selection => selection.MenuId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?
            .Key ?? string.Empty;

    private async Task<bool> DishExistsAsync(string menuId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(menuId))
        {
            return false;
        }

        return await _menus.GetByIdAsync(menuId, cancellationToken) is not null;
    }

    private async Task<bool> DishBelongsToCourseAsync(
        int courseId,
        string menuId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(menuId))
        {
            return true;
        }

        var menu = await _menus.GetByIdAsync(menuId, cancellationToken);

        // An unknown dish is reported once, by the existence rule.
        return menu is null || menu.CourseId == courseId;
    }
}
