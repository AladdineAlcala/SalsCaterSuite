namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The out-of-town pricing simulator (contract §13.7, BR-17, BR-18, DEC-081, DEC-086).
/// </summary>
public class PackageLocationPreviewTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly PreviewPackageLocationQuery _query;

    public PackageLocationPreviewTests()
    {
        _store.Packages.Add(new Package
        {
            PackageId = 1,
            PackageType = "Wedding",
            Description = "Wedding Grand Tier",
            AmountPax = 1000m,
            MinPax = 50,
            IsActive = true,
            NumPaxId = 4,
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

        // A municipality with a rate and a stated logistics cost.
        _store.Areas.Add(new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m, DistanceKm = 42, LogisticsCost = 3200m });

        // A municipality with a rate but no stated logistics cost.
        _store.Areas.Add(new Area { AreaId = 2, AreaDetails = "Batangas", AdditionalRate = 200m });

        // A municipality with no stated rate at all.
        _store.Areas.Add(new Area { AreaId = 3, AreaDetails = "Lucena" });

        _query = new PreviewPackageLocationQuery(
            new GetPackageCourseRulesetQuery(
                new FakePackageRepository(_store),
                new FakePackageCourseRuleRepository(_store),
                new FakeMenuRepository(_store),
                new FakeFoodCostBenchmarkProvider(35m),
                new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc))),
            new FakeAreaRepository(_store),
            new FakeMenuRepository(_store),
            new PackageLocationPreviewValidator(new FakeMenuRepository(_store)),
            new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task The_breakdown_at_a_known_headcount_should_be_the_servers_figures()
    {
        var request = new PackageLocationPreviewRequest(50, 1, Array.Empty<PackageCourseSelectionDto>());

        var preview = (await _query.ExecuteAsync(1, request)).Data!;

        preview.PackageId.Should().Be(1);
        preview.Pax.Should().Be(50);
        preview.AmountPax.Should().Be(1000m);
        preview.AreaId.Should().Be(1);
        preview.AreaName.Should().Be("Antipolo");
        preview.AdditionalRate.Should().Be(150.00m);

        // 1000 × 50 = 50 000.00 and 150 × 50 = 7 500.00.
        preview.BaseTotal.Should().Be(50000.00m);
        preview.ExtendedTotal.Should().Be(7500.00m);
        preview.EffectiveRatePerPax.Should().Be(1150.00m);
        preview.TotalContractValue.Should().Be(57500.00m);

        // No selection was supplied, so the food cost is the standard run (100.00).
        preview.FoodCostPerPax.Should().Be(100.00m);
        preview.FoodCostTotal.Should().Be(5000.00m);
        preview.LogisticsCost.Should().Be(3200.00m);

        // 57 500.00 − 5 000.00 − 3 200.00 = 49 300.00 → 85.74 %.
        preview.GrossProfit.Should().Be(49300.00m);
        preview.GrossProfitPercentage.Should().Be(85.74m);
    }

    [Fact]
    public async Task A_municipality_without_a_stated_logistics_cost_should_omit_the_band()
    {
        var request = new PackageLocationPreviewRequest(50, 2, Array.Empty<PackageCourseSelectionDto>());

        var preview = (await _query.ExecuteAsync(1, request)).Data!;

        preview.LogisticsCost.Should().BeNull();

        // The gross profit is unknown rather than overstated (DEC-081).
        preview.GrossProfit.Should().BeNull();
        preview.GrossProfitPercentage.Should().BeNull();
    }

    [Fact]
    public async Task A_municipality_without_a_stated_rate_should_still_yield_a_complete_contract_value()
    {
        var request = new PackageLocationPreviewRequest(50, 3, Array.Empty<PackageCourseSelectionDto>());

        var preview = (await _query.ExecuteAsync(1, request)).Data!;

        // The rate is published as null — "not stated" (DEC-086)…
        preview.AdditionalRate.Should().BeNull();

        // …but the money chain treats the absent surcharge as zero.
        preview.ExtendedTotal.Should().Be(0.00m);
        preview.EffectiveRatePerPax.Should().Be(1000.00m);
        preview.TotalContractValue.Should().Be(50000.00m);
    }

    [Fact]
    public async Task A_selection_should_replace_the_standard_run_in_the_food_cost()
    {
        var request = new PackageLocationPreviewRequest(
            50,
            1,
            new[] { new PackageCourseSelectionDto(1, "DS-1") });

        var preview = (await _query.ExecuteAsync(1, request)).Data!;

        preview.FoodCostPerPax.Should().Be(100.00m);
        preview.FoodCostTotal.Should().Be(5000.00m);
    }

    [Fact]
    public async Task A_headcount_below_one_should_be_rejected()
    {
        var request = new PackageLocationPreviewRequest(0, 1, Array.Empty<PackageCourseSelectionDto>());

        var result = await _query.ExecuteAsync(1, request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Pax must be at least 1.");
    }

    [Fact]
    public async Task A_null_selection_member_should_be_rejected()
    {
        var request = new PackageLocationPreviewRequest(50, 1, null);

        var result = await _query.ExecuteAsync(1, request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Selections must be supplied.");
    }

    [Fact]
    public async Task A_soft_deleted_tier_should_not_be_found()
    {
        _store.Packages[0].IsDeleted = true;

        var request = new PackageLocationPreviewRequest(50, 1, Array.Empty<PackageCourseSelectionDto>());

        var result = await _query.ExecuteAsync(1, request);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Package not found.");
    }
}
