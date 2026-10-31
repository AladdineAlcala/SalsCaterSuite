using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
namespace SalsCaterSuite.Application.Features.Packages.Services;

/// <summary>
/// The <b>one</b> BR-15 quota verdict of a dish selection (REQ-PKG-02, REQ-BKG-06, BR-15).
/// <para>
/// The selection simulator, the booking's selection read, the quote and the two selection
/// writes all call this evaluator, so a selection the simulator accepts can never be refused
/// by the booking write for a different reason — the rule exists in exactly one place.
/// </para>
/// <para>
/// The verdict is <b>exact</b>: for every course rule of the tier the chosen count is
/// <b>exactly</b> <see cref="PackageCourseRuleProjection.RequiredQuota"/>, and a selection that
/// names a course the tier holds <b>no</b> rule for is a breach (BR-15).
/// </para>
/// </summary>
public static class PackageSelectionCompliance
{
    /// <summary>
    /// Evaluates the selection against the tier's rules: one entry per rule, plus one per
    /// <b>unruled</b> course the selection names.
    /// </summary>
    /// <param name="rules">
    /// The tier's rules as <c>(CourseId, CourseName, RequiredQuota)</c> triples, so the
    /// evaluator serves both the ruleset DTO and the repository projection without either
    /// having to reshape itself.
    /// </param>
    /// <param name="courseNameById">Every course's name, so an unruled course can be named.</param>
    /// <param name="selections">The chosen <c>(CourseId, MenuId)</c> pairs.</param>
    public static List<PackageCourseComplianceResponse> Evaluate(
        IEnumerable<(int CourseId, string CourseName, int RequiredQuota)> rules,
        IReadOnlyDictionary<int, string> courseNameById,
        IEnumerable<(int CourseId, string MenuId)> selections)
    {
        var ruleList = rules.ToList();
        var selected = selections.ToList();

        var selectedCounts = selected
            .GroupBy(entry => entry.CourseId)
            .ToDictionary(group => group.Key, group => group.Count());

        var compliance = new List<PackageCourseComplianceResponse>();

        foreach (var rule in ruleList)
        {
            var selectedCount = selectedCounts.TryGetValue(rule.CourseId, out var count) ? count : 0;

            compliance.Add(new PackageCourseComplianceResponse
            {
                CourseId = rule.CourseId,
                CourseName = rule.CourseName,
                RequiredQuota = rule.RequiredQuota,
                SelectedCount = selectedCount,
                IsCompliant = selectedCount == rule.RequiredQuota,
                HasRule = true
            });
        }

        var ruledCourseIds = ruleList.Select(rule => rule.CourseId).ToHashSet();

        foreach (var group in selected.GroupBy(entry => entry.CourseId))
        {
            if (ruledCourseIds.Contains(group.Key))
            {
                continue;
            }

            // The selection names a course the tier holds no rule for (BR-15).
            compliance.Add(new PackageCourseComplianceResponse
            {
                CourseId = group.Key,
                CourseName = courseNameById.TryGetValue(group.Key, out var courseName)
                    ? courseName
                    : string.Empty,
                RequiredQuota = 0,
                SelectedCount = group.Count(),
                IsCompliant = false,
                HasRule = false
            });
        }

        return compliance;
    }

    /// <summary>
    /// Whether <b>every</b> entry is satisfied. An <b>empty</b> selection is vacuously compliant
    /// when the caller treats it as "no selection" — a selection is an override, never a
    /// requirement (BR-35, DEC-131).
    /// </summary>
    public static bool IsSatisfied(IEnumerable<PackageCourseComplianceResponse> compliance) =>
        compliance.All(entry => entry.IsCompliant);

    /// <summary>
    /// The <c>400</c> message of the first breach, naming the course and its quota; <c>null</c>
    /// when nothing is breached.
    /// </summary>
    public static string? BuildBreachMessage(IEnumerable<PackageCourseComplianceResponse> compliance)
    {
        var breach = compliance.FirstOrDefault(entry => !entry.IsCompliant);

        if (breach is null)
        {
            return null;
        }

        if (!breach.HasRule)
        {
            return $"The selected dishes name the '{Describe(breach.CourseName)}' course, " +
                   "which the tier holds no course rule for. Remove them or add the course to the tier.";
        }

        return $"The '{Describe(breach.CourseName)}' course requires exactly " +
               $"{breach.RequiredQuota} dish(es); {breach.SelectedCount} selected.";
    }

    /// <summary>
    /// The published <c>complianceMessage</c>: <c>null</c> when the selection satisfies BR-15,
    /// otherwise the first breach. A selection that is <b>empty</b> is "no selection" and is
    /// therefore published as compliant — the booking runs the tier's standard run (DEC-131).
    /// </summary>
    public static string? BuildSelectionMessage(IReadOnlyList<PackageCourseComplianceResponse> compliance) =>
        compliance.Count == 0 ? null : BuildBreachMessage(compliance);

    private static string Describe(string courseName) =>
        string.IsNullOrWhiteSpace(courseName) ? "unnamed" : courseName;
}
