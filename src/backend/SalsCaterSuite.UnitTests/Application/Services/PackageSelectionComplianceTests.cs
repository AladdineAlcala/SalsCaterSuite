namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Services;
using Xunit;

/// <summary>
/// The one BR-15 quota verdict of a dish selection (REQ-PKG-02, REQ-BKG-06).
/// <para>
/// The verdict is <b>exact</b>: every course rule must be met with exactly its quota, and a
/// selection that names a course the tier holds no rule for is a breach. The booking's read, the
/// quote, the create and the two writes all call this evaluator, so a selection the packages
/// simulator accepts can never be refused by the booking write for a different reason.
/// </para>
/// </summary>
public class PackageSelectionComplianceTests
{
    private static readonly (int, string, int)[] Rules =
    {
        (1, "Appetizer", 1),
        (2, "Main Course", 2)
    };

    private static readonly Dictionary<int, string> CourseNames = new()
    {
        [1] = "Appetizer",
        [2] = "Main Course",
        [3] = "Dessert"
    };

    [Fact]
    public void A_selection_that_meets_every_quota_exactly_should_be_compliant()
    {
        var compliance = PackageSelectionCompliance.Evaluate(
            Rules,
            CourseNames,
            new[] { (1, "APP-001"), (2, "MAIN-001"), (2, "MAIN-002") });

        compliance.Should().HaveCount(2);
        compliance.Should().OnlyContain(entry => entry.IsCompliant);
        compliance.Should().OnlyContain(entry => entry.HasRule);
        PackageSelectionCompliance.IsSatisfied(compliance).Should().BeTrue();
        PackageSelectionCompliance.BuildBreachMessage(compliance).Should().BeNull();
    }

    [Fact]
    public void An_under_quota_course_should_be_a_breach_that_names_the_quota()
    {
        var compliance = PackageSelectionCompliance.Evaluate(
            Rules,
            CourseNames,
            new[] { (1, "APP-001"), (2, "MAIN-001") });

        compliance.Single(entry => entry.CourseId == 2).SelectedCount.Should().Be(1);
        compliance.Single(entry => entry.CourseId == 2).IsCompliant.Should().BeFalse();
        PackageSelectionCompliance.IsSatisfied(compliance).Should().BeFalse();

        PackageSelectionCompliance.BuildBreachMessage(compliance).Should().Be(
            "The 'Main Course' course requires exactly 2 dish(es); 1 selected.");
    }

    [Fact]
    public void An_over_quota_course_should_be_a_breach()
    {
        var compliance = PackageSelectionCompliance.Evaluate(
            Rules,
            CourseNames,
            new[] { (1, "APP-001"), (1, "APP-002"), (2, "MAIN-001"), (2, "MAIN-002") });

        compliance.Single(entry => entry.CourseId == 1).SelectedCount.Should().Be(2);
        compliance.Single(entry => entry.CourseId == 1).IsCompliant.Should().BeFalse();
        PackageSelectionCompliance.BuildBreachMessage(compliance).Should().Contain("'Appetizer'");
    }

    /// <summary>
    /// A selection that names a course the tier holds <b>no</b> rule for is a breach — the tier's
    /// ruleset is what makes a selection valid (BR-15).
    /// </summary>
    [Fact]
    public void An_unruled_course_should_be_a_breach_that_names_the_course()
    {
        var compliance = PackageSelectionCompliance.Evaluate(
            Rules,
            CourseNames,
            new[] { (1, "APP-001"), (2, "MAIN-001"), (2, "MAIN-002"), (3, "DES-001") });

        compliance.Should().HaveCount(3);

        var unruled = compliance.Single(entry => entry.CourseId == 3);
        unruled.HasRule.Should().BeFalse();
        unruled.RequiredQuota.Should().Be(0);
        unruled.SelectedCount.Should().Be(1);
        unruled.IsCompliant.Should().BeFalse();

        PackageSelectionCompliance.BuildBreachMessage(compliance).Should().Be(
            "The selected dishes name the 'Dessert' course, which the tier holds no course rule " +
            "for. Remove them or add the course to the tier.");
    }

    [Fact]
    public void A_course_with_no_selection_and_no_rule_should_not_be_reported()
    {
        var compliance = PackageSelectionCompliance.Evaluate(
            Rules,
            CourseNames,
            new[] { (1, "APP-001"), (2, "MAIN-001"), (2, "MAIN-002") });

        compliance.Should().NotContain(entry => entry.CourseId == 3);
    }

    /// <summary>
    /// An <b>empty</b> selection is "no selection": it is vacuously compliant and publishes no
    /// message, because the booking then runs the tier's standard run (BR-35, DEC-131).
    /// </summary>
    [Fact]
    public void An_empty_selection_should_be_vacuously_compliant()
    {
        var compliance = PackageSelectionCompliance.Evaluate(Rules, CourseNames, Array.Empty<(int, string)>());

        compliance.Should().HaveCount(2);
        compliance.Should().OnlyContain(entry => entry.SelectedCount == 0);
        compliance.Should().OnlyContain(entry => !entry.IsCompliant, "the rules demand dishes");

        PackageSelectionCompliance.BuildBreachMessage(compliance).Should().NotBeNull();

        // ...but the published message treats "nothing selected" as "no selection".
        PackageSelectionCompliance.BuildSelectionMessage(new List<PackageCourseComplianceDto>())
            .Should().BeNull();
    }

    [Fact]
    public void The_selection_message_should_repeat_the_first_breach()
    {
        var compliance = PackageSelectionCompliance.Evaluate(
            Rules,
            CourseNames,
            new[] { (2, "MAIN-001") });

        PackageSelectionCompliance.BuildSelectionMessage(compliance).Should().Be(
            PackageSelectionCompliance.BuildBreachMessage(compliance));
    }
}
