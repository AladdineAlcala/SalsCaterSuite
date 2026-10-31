namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>One submitted rule (contract §13.2).</summary>
/// <para>
/// The retired <c>inclusionTag</c> and <c>upgradeAddonId</c> members are gone (DEC-172). A
/// client that still sends them is <b>ignored</b>, never rejected — the default
/// <c>System.Text.Json</c> behaviour for an unmapped member.
/// </para>
public record PackageCourseRuleRequest(
    int CourseId = 0,
    int RequiredQuota = 0,
    int? PortionGrams = null,
    string RuleDescription = "");
