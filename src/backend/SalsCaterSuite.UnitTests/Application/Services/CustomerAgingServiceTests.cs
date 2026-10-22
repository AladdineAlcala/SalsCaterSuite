namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The one aging vocabulary of the Customer Master (KPI-13 / DEC-041 / DEC-052).
/// <para>
/// The boundary table below is the specification's table: the due date is the event date
/// (DEC-041) and the days overdue are counted from it.
/// </para>
/// </summary>
public class CustomerAgingServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    private static CustomerAgingService CreateService() =>
        new(new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue)));

    [Theory]
    // The event date is today or later — nothing is overdue yet.
    [InlineData(0, "NotYetDue")]
    [InlineData(1, "NotYetDue")]
    [InlineData(30, "NotYetDue")]
    // 1–30 days overdue.
    [InlineData(-1, "Days1To30")]
    [InlineData(-30, "Days1To30")]
    // 31–60 days overdue.
    [InlineData(-31, "Days31To60")]
    [InlineData(-60, "Days31To60")]
    // 61–90 days overdue.
    [InlineData(-61, "Days61To90")]
    [InlineData(-90, "Days61To90")]
    // More than 90 days overdue.
    [InlineData(-91, "Over90Days")]
    [InlineData(-365, "Over90Days")]
    public void GetBucketKey_ShouldFollowTheDec041BoundaryTable(int daysFromToday, string expected)
    {
        // Act
        var bucket = CreateService().GetBucketKey(Today.AddDays(daysFromToday), Today);

        // Assert
        bucket.Should().Be(expected);
    }

    [Fact]
    public void GetBucketKey_Today_ShouldBeNotYetDue()
    {
        CreateService().GetBucketKey(Today, Today).Should().Be("NotYetDue");
    }

    [Fact]
    public void GetBucketKey_Tomorrow_ShouldBeNotYetDue()
    {
        CreateService().GetBucketKey(Today.AddDays(1), Today).Should().Be("NotYetDue");
    }

    [Fact]
    public void GetBucketKey_Yesterday_ShouldBeDays1To30()
    {
        CreateService().GetBucketKey(Today.AddDays(-1), Today).Should().Be("Days1To30");
    }

    [Theory]
    [InlineData("Clean", "Paid in full")]
    [InlineData("NotYetDue", "Open · not yet due")]
    [InlineData("Days1To30", "Overdue 1–30 days")]
    [InlineData("Days31To60", "Overdue 31–60 days")]
    [InlineData("Days61To90", "Overdue 61–90 days")]
    [InlineData("Over90Days", "Overdue 90+ days")]
    public void GetBucketLabel_ShouldReturnTheServerSideLabel(string key, string expected)
    {
        CreateService().GetBucketLabel(key).Should().Be(expected);
    }

    [Fact]
    public void GetWorstBucketKey_WithoutAnyBalance_ShouldBeClean()
    {
        // Arrange — a settled booking carries no balance, so the customer is clean.
        var balances = new[]
        {
            new CustomerAgingBalance(Today.AddDays(-100), 0m),
            new CustomerAgingBalance(Today.AddDays(10), 0m)
        };

        // Act
        var state = CreateService().GetWorstBucketKey(balances, Today);

        // Assert
        state.Should().Be("Clean");
    }

    [Fact]
    public void GetWorstBucketKey_ShouldReturnTheMostOverdueBucket()
    {
        // Arrange
        var balances = new[]
        {
            new CustomerAgingBalance(Today.AddDays(5), 1000m),    // NotYetDue
            new CustomerAgingBalance(Today.AddDays(-10), 2000m),  // Days1To30
            new CustomerAgingBalance(Today.AddDays(-45), 3000m),  // Days31To60
            new CustomerAgingBalance(Today.AddDays(-70), 100m)    // Days61To90
        };

        // Act
        var state = CreateService().GetWorstBucketKey(balances, Today);

        // Assert
        state.Should().Be("Days61To90");
    }

    [Fact]
    public void GetWorstBucketKey_ShouldIgnoreTheBookingsWithoutAPositiveBalance()
    {
        // Arrange — the 200-day-old booking is settled, so it does not age the customer.
        var balances = new[]
        {
            new CustomerAgingBalance(Today.AddDays(-200), 0m),
            new CustomerAgingBalance(Today.AddDays(-5), 500m)
        };

        // Act
        var state = CreateService().GetWorstBucketKey(balances, Today);

        // Assert
        state.Should().Be("Days1To30");
    }

    [Theory]
    [InlineData("Days31To60", true, false)]
    [InlineData("Days61To90", true, true)]
    [InlineData("Over90Days", true, true)]
    [InlineData("Days1To30", false, false)]
    [InlineData("NotYetDue", false, false)]
    [InlineData("Clean", false, false)]
    public void TheOverduePredicates_ShouldFollowTheKpi13Thresholds(
        string bucketKey, bool overdue30, bool overdue60)
    {
        var service = CreateService();

        service.IsOverdue30(bucketKey).Should().Be(overdue30);
        service.IsOverdue60(bucketKey).Should().Be(overdue60);
    }

    [Fact]
    public void Today_ShouldComeFromTheInjectedClock()
    {
        CreateService().Today.Should().Be(Today);
    }
}
