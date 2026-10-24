using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Application.Features.Bookings.Services;

/// <summary>
/// A tier's <b>live</b> ruleset together with the qualified pools and the catalogues needed to
/// validate and describe a booking's dish selection (contract §15.1, BR-15, BR-35).
/// </summary>
public sealed class BookingMenuCourseSet
{
    /// <summary>The tier; <c>null</c> for a bespoke booking (no tier, therefore no ruleset).</summary>
    public int? PackageId { get; init; }

    /// <summary>The headcount the batch mass is derived from (DEC-145).</summary>
    public int NumberOfHeads { get; init; }

    /// <summary>The tier's live rules, in the tier's own order.</summary>
    public IReadOnlyList<PackageCourseRuleProjection> Rules { get; init; } =
        Array.Empty<PackageCourseRuleProjection>();

    /// <summary>The ruleset with its qualified pools, in the tier's order.</summary>
    public IReadOnlyList<BookingMenuCourseDto> Courses { get; init; } =
        Array.Empty<BookingMenuCourseDto>();

    /// <summary>The live dishes, keyed by catalogue key.</summary>
    public IReadOnlyDictionary<string, Menu> MenuById { get; init; } =
        new Dictionary<string, Menu>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The course names of the whole catalogue, so an unruled course can be named.</summary>
    public IReadOnlyDictionary<int, string> CourseNameById { get; init; } =
        new Dictionary<int, string>();

    /// <summary>Whether the tier holds at least one course rule.</summary>
    public bool HasRules => Rules.Count > 0;
}

/// <summary>
/// Builds the <see cref="BookingMenuCourseSet"/> — the <b>one</b> derivation of a tier's
/// qualified pools, so the booking selection read, the quote, the create and the two writes
/// can never disagree about which dish belongs to which pool (BR-15, BR-35, DEC-083).
/// <para>
/// A qualified pool is <b>derived, never stored</b>: it is the live dishes of the rule's course.
/// The catalogue is read <b>once</b> per request.
/// </para>
/// </summary>
public class BookingMenuCourseSetBuilder
{
    private readonly IPackageCourseRuleRepository _rules;
    private readonly IMenuRepository _menus;

    public BookingMenuCourseSetBuilder(IPackageCourseRuleRepository rules, IMenuRepository menus)
    {
        _rules = rules;
        _menus = menus;
    }

    /// <summary>
    /// The set of the tier at the given headcount. A booking with no tier yields an empty
    /// ruleset — a bespoke booking carries no course rules, so no selection can be valid on it.
    /// </summary>
    public async Task<BookingMenuCourseSet> BuildAsync(
        int? packageId,
        int numberOfHeads,
        CancellationToken cancellationToken = default)
    {
        var courseNameById = (await _menus.GetCourseProjectionsAsync(cancellationToken))
            .ToDictionary(course => course.CourseId, course => course.CourseName ?? string.Empty);

        var menus = await _menus.GetOrderedByNameAsync(cancellationToken);

        var menuById = menus
            .GroupBy(menu => menu.MenuId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (!packageId.HasValue)
        {
            return new BookingMenuCourseSet
            {
                PackageId = null,
                NumberOfHeads = numberOfHeads,
                MenuById = menuById,
                CourseNameById = courseNameById
            };
        }

        var rules = await _rules.GetLiveRulesAsync(packageId.Value, cancellationToken);

        var pools = menus
            .Where(menu => menu.CourseId.HasValue)
            .GroupBy(menu => menu.CourseId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var courses = rules
            .OrderBy(rule => rule.CourseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.CourseId)
            .Select(rule => MapCourse(rule, pools, numberOfHeads))
            .ToList();

        return new BookingMenuCourseSet
        {
            PackageId = packageId,
            NumberOfHeads = numberOfHeads,
            Rules = rules,
            Courses = courses,
            MenuById = menuById,
            CourseNameById = courseNameById
        };
    }

    private static BookingMenuCourseDto MapCourse(
        PackageCourseRuleProjection rule,
        IReadOnlyDictionary<int, List<Menu>> pools,
        int numberOfHeads)
    {
        var pool = pools.TryGetValue(rule.CourseId, out var dishes)
            ? dishes
            : new List<Menu>();

        return new BookingMenuCourseDto
        {
            CourseId = rule.CourseId,
            CourseName = rule.CourseName,
            RequiredQuota = rule.RequiredQuota,
            PortionGrams = rule.PortionGrams,
            BatchMassKg = BatchMassKg(rule.PortionGrams, numberOfHeads),
            RuleDescription = rule.RuleDescription,
            InclusionTag = rule.InclusionTag,
            Pool = pool
                .OrderBy(menu => menu.MenuName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(menu => menu.MenuId, StringComparer.OrdinalIgnoreCase)
                .Select(menu => new BookingMenuCourseDishDto
                {
                    MenuId = menu.MenuId,
                    MenuName = menu.MenuName,
                    Note = menu.Note,
                    FoodCost = menu.FoodCost,
                    Price = menu.Price
                })
                .ToList()
        };
    }

    /// <summary>
    /// <c>PortionGrams x NumberOfHeads / 1000</c> (DEC-145). <c>null</c> when the rule states no
    /// portion or a portion of zero — "not stated" is null, never zero (BR-20, M3). It is a
    /// <b>planning figure</b>.
    /// </summary>
    public static decimal? BatchMassKg(int? portionGrams, int numberOfHeads)
    {
        if (!portionGrams.HasValue || portionGrams.Value <= 0 || numberOfHeads <= 0)
        {
            return null;
        }

        return BookingAmountDueCalculator.Round2(portionGrams.Value * numberOfHeads / 1000m);
    }
}
