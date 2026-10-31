using SalsCaterSuite.Application.Features.Bookings.DTOs.Request;
using SalsCaterSuite.Application.Features.Bookings.DTOs.Response;
using SalsCaterSuite.Application.Features.Bookings.Models;
using SalsCaterSuite.Application.Features.Packages.Services;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// The <b>one</b> projection of a dish selection onto the wire (contract §14.3, §15.1).
/// <para>
/// The booking's stored selection read, the quote's proposed selection and the two writes'
/// responses all describe the same rows through this projector, so the shape a client shows
/// after a save is the shape it was shown before it (DEC-134, BR-31).
/// </para>
/// <para>
/// The selection carries <b>no money</b> (BR-37). The two planning figures it does publish —
/// <c>selectedFoodCostPerPax</c> and <c>selectedGrossProfitPercentage</c> — are estimates from
/// the manually entered catalogue costs (DEC-062) and never enter a booking total.
/// </para>
/// </summary>
public static class BookingSelectionProjector
{
    /// <summary>
    /// The quote's <c>courses</c> block: the tier's own rules, one entry per rule, each with the
    /// <b>count</b> of live dishes in its qualified pool (contract §14.3).
    /// </summary>
    public static List<BookingQuoteCourseResponse> QuoteCourses(BookingMenuCourseSet courseSet) =>
        courseSet.Courses
            .Select(course => new BookingQuoteCourseResponse
            {
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                RequiredQuota = course.RequiredQuota,
                PortionGrams = course.PortionGrams,
                BatchMassKg = course.BatchMassKg,
                RuleDescription = course.RuleDescription,
                QualifiedPool = course.Pool.Count
            })
            .ToList();

    /// <summary>
    /// The quote's <c>selection</c> block — the request's proposed selection described exactly as
    /// <c>GET /api/bookings/{id}/menus</c> describes a stored one (contract §14.3).
    /// </summary>
    public static BookingQuoteSelectionResponse QuoteSelection(
        BookingMenuCourseSet courseSet,
        IReadOnlyList<ResolvedSelectionRow> selection,
        BookingMenuWindowState window,
        decimal pricePerHead)
    {
        var courses = selection
            .GroupBy(row => row.CourseId)
            .OrderBy(group => CourseName(courseSet, group.Key), StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key)
            .Select(group => new BookingQuoteSelectionCourseResponse
            {
                CourseId = group.Key,
                CourseName = CourseNameOrNull(courseSet, group.Key),
                SelectedMenus = group
                    .Select(row => ToSelectedMenu(courseSet, row))
                    .ToList()
            })
            .ToList();

        var selectedFoodCost = SelectedFoodCost(courseSet, selection);

        return new BookingQuoteSelectionResponse
        {
            IsLocked = window.IsLocked,
            LockDate = window.LockDate,
            Courses = courses,
            SelectedFoodCostPerPax = selectedFoodCost,
            SelectedGrossProfitPercentage = PackageCostingCalculator.BaseGrossProfitPercentage(
                pricePerHead, selectedFoodCost)
        };
    }

    /// <summary>
    /// Fills a <see cref="BookingMenuSelectionResponse"/>'s selection-derived members: the chosen
    /// dishes, the ruleset with its chosen keys, the compliance verdict and the planning figures.
    /// The caller owns the identity, window and add-on members.
    /// </summary>
    public static void ApplyTo(
        BookingMenuSelectionResponse dto,
        BookingMenuCourseSet courseSet,
        IReadOnlyList<ResolvedSelectionRow> selection,
        decimal pricePerHead)
    {
        var chosenMenuIds = selection
            .Select(row => row.MenuId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var courses = courseSet.Courses
            .Select(course => new BookingMenuCourseResponse
            {
                CourseId = course.CourseId,
                CourseName = course.CourseName,
                RequiredQuota = course.RequiredQuota,
                PortionGrams = course.PortionGrams,
                BatchMassKg = course.BatchMassKg,
                RuleDescription = course.RuleDescription,
                Pool = course.Pool,
                ChosenMenuIds = course.Pool
                    .Where(dish => chosenMenuIds.Contains(dish.MenuId))
                    .Select(dish => dish.MenuId)
                    .ToList()
            })
            .ToList();

        var compliance = PackageSelectionCompliance.Evaluate(
            courseSet.Rules.Select(rule => (rule.CourseId, rule.CourseName, rule.RequiredQuota)),
            courseSet.CourseNameById,
            selection.Select(row => (row.CourseId, row.MenuId)));

        // An empty selection is "no selection", not a breach: the booking runs the tier's
        // standard run (BR-35, DEC-131).
        dto.Selections = selection.Select(row => ToSelectionItem(courseSet, row)).ToList();
        dto.Courses = courses;
        dto.IsCompliant = selection.Count == 0 || PackageSelectionCompliance.IsSatisfied(compliance);
        dto.ComplianceMessage = selection.Count == 0
            ? null
            : PackageSelectionCompliance.BuildSelectionMessage(compliance);

        var selectedFoodCost = SelectedFoodCost(courseSet, selection);

        dto.SelectedFoodCostPerPax = selectedFoodCost;
        dto.SelectedGrossProfitPercentage = PackageCostingCalculator.BaseGrossProfitPercentage(
            pricePerHead, selectedFoodCost);
    }

    /// <summary>
    /// The sum of the chosen dishes' food costs. <c>null</c> when nothing is selected <b>or</b>
    /// when any chosen dish is not costed or no longer in the live catalogue (DEC-087).
    /// </summary>
    private static decimal? SelectedFoodCost(
        BookingMenuCourseSet courseSet,
        IReadOnlyList<ResolvedSelectionRow> selection)
    {
        if (selection.Count == 0)
        {
            return null;
        }

        return PackageCostingCalculator.SelectedFoodCostPerPax(
            selection.Select(row => courseSet.MenuById.TryGetValue(row.MenuId, out var menu)
                ? menu.FoodCost
                : (decimal?)null));
    }

    private static BookingMenuSelectionItemResponse ToSelectionItem(
        BookingMenuCourseSet courseSet,
        ResolvedSelectionRow row)
    {
        courseSet.MenuById.TryGetValue(row.MenuId, out var menu);

        return new BookingMenuSelectionItemResponse
        {
            CourseId = row.CourseId,
            CourseName = CourseNameOrNull(courseSet, row.CourseId),
            MenuId = row.MenuId,
            MenuName = menu?.MenuName,
            Note = menu?.Note,
            FoodCost = menu?.FoodCost,
            PrepInstruction = row.PrepInstruction
        };
    }

    private static BookingQuoteSelectedMenuResponse ToSelectedMenu(
        BookingMenuCourseSet courseSet,
        ResolvedSelectionRow row)
    {
        courseSet.MenuById.TryGetValue(row.MenuId, out var menu);

        return new BookingQuoteSelectedMenuResponse
        {
            MenuId = row.MenuId,
            MenuName = menu?.MenuName,
            Note = menu?.Note,
            PrepInstruction = row.PrepInstruction,
            FoodCost = menu?.FoodCost
        };
    }

    private static string CourseName(BookingMenuCourseSet courseSet, int courseId) =>
        CourseNameOrNull(courseSet, courseId) ?? string.Empty;

    private static string? CourseNameOrNull(BookingMenuCourseSet courseSet, int courseId) =>
        courseSet.CourseNameById.TryGetValue(courseId, out var name) ? name : null;
}
