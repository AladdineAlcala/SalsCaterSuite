using FluentValidation;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;

namespace SalsCaterSuite.Application.Features.Packages.Validators;

/// <summary>
/// The validation rules of the course-rules <b>preview</b> request (spec §3.6, BR-11, contract
/// §13.3).
/// <para>
/// A preview persists nothing, so its validation is the last line of defence: it refuses a dish
/// that does not exist (or is soft-deleted), a dish that does not belong to the course the
/// selection names, and the same dish twice in one selection.
/// </para>
/// <para>
/// The <c>selections</c> member is <b>nullable</b> on purpose: an absent or explicitly null member
/// is a rejected payload (<c>400</c>), never an empty selection.
/// </para>
/// <para>
/// A negative stress percentage is rejected; a <c>null</c> one means no stress. A negative headcount
/// is rejected; a headcount below the tier's floor is <b>allowed</b> and reported (spec §4.3).
/// </para>
/// </summary>
public class PackageCourseRulePreviewValidator : AbstractValidator<PackageCourseRulePreviewRequest>
{
    private readonly IMenuRepository _menus;

    public PackageCourseRulePreviewValidator(IMenuRepository menus)
    {
        _menus = menus;

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

        RuleFor(x => x.StressPercentage)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Stress percentage must be zero or greater.");

        RuleFor(x => x.Pax)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Pax must be at least 1.");
    }

    /// <summary>The first dish code the selection names more than once.</summary>
    private static string FirstRepeatedMenuId(PackageCourseRulePreviewRequest request) =>
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
