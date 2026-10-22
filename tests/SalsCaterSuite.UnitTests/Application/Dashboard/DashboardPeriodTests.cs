namespace SalsCaterSuite.UnitTests.Application.Dashboard;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Dashboard.Models;
using Xunit;

/// <summary>
/// The reporting-period model (spec §5, contract §9). The server owns the boundaries; a
/// malformed <c>period</c> is rejected rather than coerced.
/// </summary>
public class DashboardPeriodTests
{
    [Fact]
    public void ForMonth_ShouldResolveTheCalendarMonthBoundaries()
    {
        var period = DashboardPeriod.ForMonth(2026, 10);

        period.StartDate.Should().Be(new DateOnly(2026, 10, 1));
        period.EndDate.Should().Be(new DateOnly(2026, 10, 31));
        period.Label.Should().Be("October 2026");
    }

    [Fact]
    public void ForMonth_ShouldRespectTheLengthOfAShortMonth()
    {
        var period = DashboardPeriod.ForMonth(2026, 2);

        period.StartDate.Should().Be(new DateOnly(2026, 2, 1));
        period.EndDate.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void FromUtcNow_ShouldResolveTheContainingMonth()
    {
        var period = DashboardPeriod.FromUtcNow(new DateTime(2026, 10, 19, 23, 59, 59, DateTimeKind.Utc));

        period.Year.Should().Be(2026);
        period.Month.Should().Be(10);
        period.StartDate.Should().Be(new DateOnly(2026, 10, 1));
        period.EndDate.Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void PreviousPeriod_ShouldBeTheImmediatelyPrecedingCalendarMonth()
    {
        var previous = DashboardPeriod.ForMonth(2026, 10).PreviousPeriod;

        previous.Year.Should().Be(2026);
        previous.Month.Should().Be(9);
        previous.StartDate.Should().Be(new DateOnly(2026, 9, 1));
        previous.EndDate.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void PreviousPeriod_ShouldCrossTheYearBoundary()
    {
        var previous = DashboardPeriod.ForMonth(2026, 1).PreviousPeriod;

        previous.Year.Should().Be(2025);
        previous.Month.Should().Be(12);
        previous.StartDate.Should().Be(new DateOnly(2025, 12, 1));
        previous.EndDate.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Theory]
    [InlineData("2026-10")]
    [InlineData("2026-01")]
    [InlineData("2026-12")]
    public void TryParse_ShouldAcceptAWellFormedPeriod(string value)
    {
        DashboardPeriod.TryParse(value, out var period).Should().BeTrue();

        period.StartDate.Should().Be(new DateOnly(int.Parse(value[..4]), int.Parse(value[5..]), 1));
    }

    [Theory]
    [InlineData("banana")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("2026-1")]
    [InlineData("2026-10-01")]
    [InlineData("10-2026")]
    [InlineData("2026/10")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_ShouldRejectAnythingThatIsNotExactlyYearDashMonth(string? value)
    {
        DashboardPeriod.TryParse(value, out _).Should().BeFalse();
    }

    [Fact]
    public void StartAndEndDateTime_ShouldCoverTheWholeCalendarMonth()
    {
        var period = DashboardPeriod.ForMonth(2026, 10);

        period.StartDateTime.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0));
        period.EndDateTime.Should().Be(new DateTime(2026, 10, 31, 23, 59, 59).AddTicks(TimeSpan.TicksPerSecond - 1));
    }
}
