namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// The dashboard's repository queries must be translatable by the SQL Server provider
/// (DEC-037, AC-B11). EF Core cannot client-evaluate a filtered aggregate, so an
/// untranslatable query would throw at runtime and surface as a <c>500</c>.
/// <para>
/// <c>ToQueryString()</c> compiles the query without opening a connection. This probe
/// mirrors the exact query shapes of the dashboard repository methods; the authoritative
/// proof that they execute on real SQL Server is the integration
/// <c>DashboardEndpointTests</c>.
/// </para>
/// </summary>
public class DashboardRepositoryTranslatabilityTests
{
    private static SalsCaterSuiteDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuite_QueryTranslationProbe;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        return new SalsCaterSuiteDbContext(options);
    }

    [Fact]
    public void The_dashboard_booking_populations_shouldBeTranslatableBySqlServer()
    {
        using var context = CreateContext();

        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 31);

        var periodPopulation = context.Bookings
            .AsNoTracking()
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Expired &&
                b.EventDate >= from && b.EventDate <= to)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId);

        var receivablesPopulation = context.Bookings
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.TransactionId);

        var upcomingPopulation = context.Bookings
            .AsNoTracking()
            .Where(b =>
                !b.IsDeleted &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.Expired &&
                b.EventDate >= from)
            .OrderBy(b => b.EventDate)
            .ThenBy(b => b.TransactionId);

        foreach (var sql in new[]
        {
            periodPopulation.ToQueryString(),
            receivablesPopulation.ToQueryString(),
            upcomingPopulation.ToQueryString()
        })
        {
            sql.Should().NotBeNullOrWhiteSpace();
            sql.Should().Contain("is_deleted", "the dashboard-scoped soft-delete filter must be in the SQL");
            sql.Should().Contain("booking_status", "the status filter must be in the SQL");
        }
    }

    [Fact]
    public void The_payment_range_aggregate_shouldBeTranslatableBySqlServer()
    {
        using var context = CreateContext();

        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc);

        var query = context.Payments
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.DateOfPayment >= from && p.DateOfPayment <= to);

        var sql = query.ToQueryString();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("dateofPayment");
        sql.Should().Contain("is_deleted");
    }

    [Fact]
    public void The_menu_costing_aggregates_shouldBeTranslatableBySqlServer()
    {
        using var context = CreateContext();

        var costingAggregate = context.Menus
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .GroupBy(m => m.CourseId)
            .Select(g => new MenuCourseCostAggregate
            {
                CourseId = g.Key,
                FoodCostAmount = g.Sum(m => m.FoodCost) ?? 0m,
                MenuCount = g.Count()
            });

        var sql = costingAggregate.ToQueryString();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("SUM", "the per-course aggregation must run in the database");
        sql.Should().Contain("GROUP BY");
        sql.Should().Contain("is_deleted");
    }
}
