namespace SalsCaterSuite.UnitTests.Application.Dashboard;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Dashboard;
using Xunit;

/// <summary>
/// The transport-level validation of the <c>period</c> query string (contract §9).
/// </summary>
public class GetDashboardQueryTests
{
    [Fact]
    public async Task A_malformed_period_should_fail_with_the_contract_message()
    {
        var harness = new DashboardHarness();

        var result = await harness.GetAsync("banana");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Period must be in the format YYYY-MM.");
        result.Errors.Should().Contain(DashboardErrors.InvalidPeriod);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("2026-10-01")]
    [InlineData("October 2026")]
    public async Task Any_value_that_is_not_exactly_year_dash_month_should_be_rejected(string period)
    {
        var harness = new DashboardHarness();

        var result = await harness.GetAsync(period);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Be("Period must be in the format YYYY-MM.");
    }

    [Fact]
    public async Task A_valid_period_should_resolve_the_requested_month()
    {
        var harness = new DashboardHarness();

        var result = await harness.GetAsync("2026-03");

        result.Succeeded.Should().BeTrue();
        result.Data!.Period.Year.Should().Be(2026);
        result.Data.Period.Month.Should().Be(3);
        result.Data.Period.Label.Should().Be("March 2026");
    }

    [Fact]
    public async Task An_absent_period_should_default_to_the_current_utc_calendar_month()
    {
        var harness = new DashboardHarness();

        var result = await harness.GetAsync(null);

        result.Succeeded.Should().BeTrue();
        result.Data!.Period.Year.Should().Be(2026);
        result.Data.Period.Month.Should().Be(10);
        result.Data.Period.Label.Should().Be("October 2026");
    }
}
