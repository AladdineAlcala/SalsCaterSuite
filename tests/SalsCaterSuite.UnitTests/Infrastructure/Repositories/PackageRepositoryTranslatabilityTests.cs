namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// The packages catalogue aggregate must be translatable by the SQL Server provider
/// (DEC-037, DEC-068). EF Core cannot client-evaluate a filtered aggregate, so an
/// untranslatable query would throw at runtime and surface as a <c>500</c>.
/// <para>
/// <c>ToQueryString()</c> compiles the query without opening a connection. This probe mirrors
/// the exact query shape of
/// <c>PackageRepository.GetCatalogueSummaryAsync</c>; the authoritative proof that it executes
/// on real SQL Server is the integration <c>PackageCatalogueEndpointTests</c>.
/// </para>
/// </summary>
public class PackageRepositoryTranslatabilityTests
{
    private static SalsCaterSuiteDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuite_QueryTranslationProbe;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        return new SalsCaterSuiteDbContext(options);
    }

    [Fact]
    public void The_catalogue_aggregate_shouldBeTranslatableBySqlServer()
    {
        using var context = CreateContext();

        var aggregate = context.Packages
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalPackages = g.Count(),
                ActivePackages = g.Count(p => p.IsActive),
                PricedPackages = g.Count(p => p.AmountPax != null),
                SumAmountPax = g.Sum(p => p.AmountPax),
                LowestAmountPax = g.Min(p => p.AmountPax),
                HighestAmountPax = g.Max(p => p.AmountPax),
                LowestMinPax = g.Min(p => p.MinPax),
                HighestMinPax = g.Max(p => p.MinPax)
            });

        var sql = aggregate.ToQueryString();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("is_deleted", "the catalogue-scoped soft-delete filter must be in the SQL");
        sql.Should().Contain("COUNT(", "the counts must run in the database");
        sql.Should().Contain("SUM(", "the rate sum must run in the database");
        sql.Should().Contain("MIN(", "the extremes must run in the database");
        sql.Should().Contain("MAX(", "the extremes must run in the database");
    }
}
