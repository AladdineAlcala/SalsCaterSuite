namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The coverage-matrix read (contract §13.4, AC-P2-29, REQ-PKG-02) over a seeded population.
/// </summary>
public class PackageAreaCoverageQueryTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly GetPackageAreaCoverageMatrixQuery _query;

    public PackageAreaCoverageQueryTests()
    {
        _store.Packages.Add(new Package
        {
            PackageId = 1,
            PackageType = "Wedding",
            Description = "Wedding Grand Tier",
            AmountPax = 1000m,
            MinPax = 50,
            IsActive = true
        });

        _store.CourseCategories.Add(new CourseCategory { CourseId = 1, CourseName = "Main Course" });
        _store.Menus.Add(new Menu { MenuId = "DS-1", CourseId = 1, MenuName = "Beef", Price = 500m, FoodCost = 100m });
        _store.PackageCourseRules.Add(new PackageCourseRule
        {
            PackageCourseRuleId = 1,
            PackageId = 1,
            CourseId = 1,
            RequiredQuota = 1,
            RuleDescription = "Exactly one main course"
        });

        _store.Areas.Add(new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m, DistanceKm = 42, LogisticsNote = "Reefer", LogisticsCost = 3200m });
        _store.Areas.Add(new Area { AreaId = 2, AreaDetails = "Batangas", AdditionalRate = 200m, LogisticsCost = null });
        _store.Areas.Add(new Area { AreaId = 3, AreaDetails = "Lucena", AdditionalRate = null, LogisticsCost = null });

        _store.PackageAreaCoverages.Add(new PackageAreaCoverage { PackageAreaCoverageId = 1, PackageId = 1, AreaId = 1, IsActive = true });
        _store.PackageAreaCoverages.Add(new PackageAreaCoverage { PackageAreaCoverageId = 2, PackageId = 1, AreaId = 2, IsActive = true });
        _store.PackageAreaCoverages.Add(new PackageAreaCoverage { PackageAreaCoverageId = 3, PackageId = 1, AreaId = 3, IsActive = false });

        _query = new GetPackageAreaCoverageMatrixQuery(
            new GetPackageCourseRulesetQuery(
                new FakePackageRepository(_store),
                new FakePackageCourseRuleRepository(_store),
                new FakeMenuRepository(_store),
                new FakeFoodCostBenchmarkProvider(35m),
                new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc))),
            new FakePackageAreaCoverageRepository(_store),
            new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task The_matrix_should_publish_the_tier_identity_and_its_derived_floor()
    {
        var matrix = (await _query.ExecuteAsync(1)).Data!;

        matrix.PackageId.Should().Be(1);
        matrix.PackageName.Should().Be("Wedding Grand Tier");
        matrix.AmountPax.Should().Be(1000m);
        matrix.MinPax.Should().Be(50);

        // 1000 × 50 = 50 000.00 (BR-16).
        matrix.MinimumContractValue.Should().Be(50000.00m);
        matrix.StandardRunFoodCostPerPax.Should().Be(100.00m);
        matrix.BaseGrossProfitPercentage.Should().Be(90.00m);
    }

    [Fact]
    public async Task The_matrix_should_count_the_coverage_and_the_active_coverage()
    {
        var matrix = (await _query.ExecuteAsync(1)).Data!;

        matrix.CoverageCount.Should().Be(3);
        matrix.ActiveCoverageCount.Should().Be(2);
    }

    [Fact]
    public async Task The_rate_range_should_be_measured_over_the_active_coverage_only()
    {
        var matrix = (await _query.ExecuteAsync(1)).Data!;

        // The active rows carry 150 and 200; the inactive row carries no rate at all.
        matrix.LowestAdditionalRate.Should().Be(150.00m);
        matrix.HighestAdditionalRate.Should().Be(200.00m);
    }

    [Fact]
    public async Task The_gross_profit_range_should_exclude_a_row_without_a_stated_logistics_cost()
    {
        var matrix = (await _query.ExecuteAsync(1)).Data!;

        // Only the Antipolo row yields a figure: (1000 + 150) × 50 = 57 500.00, less the food
        // cost of 100 × 50 = 5 000.00, less the logistics cost of 3 200.00 → 49 300.00 → 85.74 %.
        matrix.GrossProfitCoverageCount.Should().Be(1);
        matrix.LowestGrossProfitPercentage.Should().Be(85.74m);
        matrix.HighestGrossProfitPercentage.Should().Be(85.74m);
    }

    [Fact]
    public async Task The_gross_profit_range_should_be_null_when_the_tier_has_no_floor()
    {
        _store.Packages[0].MinPax = null;

        var matrix = (await _query.ExecuteAsync(1)).Data!;

        matrix.GrossProfitCoverageCount.Should().Be(0);
        matrix.LowestGrossProfitPercentage.Should().BeNull();
        matrix.HighestGrossProfitPercentage.Should().BeNull();
    }

    [Fact]
    public async Task The_gross_profit_range_should_be_null_when_the_tier_has_no_standard_run()
    {
        _store.PackageCourseRules.Clear();

        var matrix = (await _query.ExecuteAsync(1)).Data!;

        matrix.GrossProfitCoverageCount.Should().Be(0);
        matrix.LowestGrossProfitPercentage.Should().BeNull();
    }

    [Fact]
    public async Task The_rows_should_carry_the_municipalitys_own_rate()
    {
        var matrix = (await _query.ExecuteAsync(1)).Data!;

        var antipolo = matrix.Areas.Single(area => area.AreaId == 1);
        antipolo.AreaName.Should().Be("Antipolo");
        antipolo.AdditionalRate.Should().Be(150.00m);
        antipolo.DistanceKm.Should().Be(42);
        antipolo.LogisticsNote.Should().Be("Reefer");
        antipolo.LogisticsCost.Should().Be(3200.00m);
        antipolo.IsActive.Should().BeTrue();

        // The municipality with no stated rate publishes null — never zero (DEC-086).
        var lucena = matrix.Areas.Single(area => area.AreaId == 3);
        lucena.AdditionalRate.Should().BeNull();
        lucena.LogisticsCost.Should().BeNull();
        lucena.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task A_coverage_row_of_a_soft_deleted_municipality_should_be_excluded()
    {
        _store.Areas[1].IsDeleted = true;

        var matrix = (await _query.ExecuteAsync(1)).Data!;

        matrix.CoverageCount.Should().Be(2);
        matrix.ActiveCoverageCount.Should().Be(1);
        matrix.Areas.Should().NotContain(area => area.AreaId == 2);
    }

    [Fact]
    public async Task A_soft_deleted_tier_should_not_be_found()
    {
        _store.Packages[0].IsDeleted = true;

        var result = await _query.ExecuteAsync(1);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Package not found.");
    }
}
