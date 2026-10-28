namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The Packages &amp; Buffets catalogue aggregate (spec §3.2, DEC-068, KPI-25..KPI-28).
/// <para>
/// The aggregate is measured over the hand-written fake's population — no EF Core is involved
/// (DEC-037). The <see langword="null"/> cases are the important ones: a catalogue with no
/// priced tier publishes <see langword="null"/> and never <c>0</c> (contract §9.2 rule 6).
/// </para>
/// </summary>
public class PackageCatalogueSummaryTests
{
    private static readonly DateTime GeneratedAtUtc = new(2026, 10, 21, 9, 15, 0, DateTimeKind.Utc);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakePackageRepository _packages;
    private readonly GetPackageCatalogueSummaryQuery _summary;

    public PackageCatalogueSummaryTests()
    {
        _packages = new FakePackageRepository(_store);
        _summary = new GetPackageCatalogueSummaryQuery(_packages, new FakeDateTime(GeneratedAtUtc));
    }

    private void SeedTheKnownPopulation()
    {
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", AmountPax = 950m, MinPax = 80, IsActive = true, NumPaxId = 4 });
        _store.Packages.Add(new Package { PackageId = 2, PackageType = "Buffet", Description = "B", AmountPax = 1200m, MinPax = 100, IsActive = true, NumPaxId = 4 });
        _store.Packages.Add(new Package { PackageId = 3, PackageType = "Plated", Description = "C", AmountPax = 1450m, MinPax = 150, IsActive = false, NumPaxId = 4 });
        // Priced, but without a headcount floor.
        _store.Packages.Add(new Package { PackageId = 4, PackageType = "Plated", Description = "D", AmountPax = null, MinPax = null, IsActive = true, NumPaxId = 4 });
    }

    [Fact]
    public async Task The_aggregate_should_publish_the_counts_of_the_catalogue()
    {
        // Arrange
        SeedTheKnownPopulation();

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.TotalPackages.Should().Be(4);
        result.Data.ActivePackages.Should().Be(3);
        result.Data.InactivePackages.Should().Be(1);
        result.Data.PricedPackages.Should().Be(3);
    }

    [Fact]
    public async Task The_aggregate_should_publish_the_rate_average_and_range()
    {
        // Arrange — (950 + 1200 + 1450) ÷ 3 = 1 200.00
        SeedTheKnownPopulation();

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Data!.AverageAmountPax.Should().Be(1200.00m);
        result.Data.LowestAmountPax.Should().Be(950m);
        result.Data.HighestAmountPax.Should().Be(1450m);
    }

    [Fact]
    public async Task The_aggregate_should_publish_the_headcount_floor_range()
    {
        // Arrange
        SeedTheKnownPopulation();

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert — the tier without a floor does not narrow the range.
        result.Data!.LowestMinPax.Should().Be(80);
        result.Data.HighestMinPax.Should().Be(150);
    }

    [Fact]
    public async Task The_aggregate_should_stamp_the_server_timestamp()
    {
        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Data!.GeneratedAt.Should().Be(GeneratedAtUtc);
    }

    [Fact]
    public async Task A_soft_deleted_tier_should_be_excluded_from_the_aggregate()
    {
        // Arrange
        SeedTheKnownPopulation();
        _store.Packages.Add(new Package { PackageId = 5, PackageType = "Ghost", Description = "E", AmountPax = 9999m, MinPax = 999, IsActive = true, IsDeleted = true, NumPaxId = 4 });

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Data!.TotalPackages.Should().Be(4);
        result.Data.PricedPackages.Should().Be(3);
        result.Data.HighestAmountPax.Should().Be(1450m);
        result.Data.HighestMinPax.Should().Be(150);
    }

    [Fact]
    public async Task A_catalogue_without_a_priced_tier_should_publish_nulls_and_not_zeros()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", AmountPax = null, MinPax = 80, IsActive = true, NumPaxId = 4 });
        _store.Packages.Add(new Package { PackageId = 2, PackageType = "Buffet", Description = "B", AmountPax = null, MinPax = 100, IsActive = true, NumPaxId = 4 });

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert — the counts are real measurements; the rate aggregate is unknown.
        result.Data!.TotalPackages.Should().Be(2);
        result.Data.PricedPackages.Should().Be(0);
        result.Data.AverageAmountPax.Should().BeNull();
        result.Data.LowestAmountPax.Should().BeNull();
        result.Data.HighestAmountPax.Should().BeNull();

        // The floor range is still known.
        result.Data.LowestMinPax.Should().Be(80);
        result.Data.HighestMinPax.Should().Be(100);
    }

    [Fact]
    public async Task A_catalogue_without_a_headcount_floor_should_publish_nulls()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", AmountPax = 950m, MinPax = null, IsActive = true, NumPaxId = 4 });

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Data!.LowestMinPax.Should().BeNull();
        result.Data.HighestMinPax.Should().BeNull();
        result.Data.AverageAmountPax.Should().Be(950m);
    }

    [Fact]
    public async Task An_empty_catalogue_should_publish_zero_counts_and_null_aggregates()
    {
        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Data!.TotalPackages.Should().Be(0);
        result.Data.ActivePackages.Should().Be(0);
        result.Data.InactivePackages.Should().Be(0);
        result.Data.PricedPackages.Should().Be(0);
        result.Data.AverageAmountPax.Should().BeNull();
        result.Data.LowestAmountPax.Should().BeNull();
        result.Data.HighestAmountPax.Should().BeNull();
        result.Data.LowestMinPax.Should().BeNull();
        result.Data.HighestMinPax.Should().BeNull();
    }

    /// <summary>
    /// DEC-003: the mean is rounded <b>away from the midpoint</b>. 1 000.00 and 1 000.01 give a
    /// mean of exactly 1 000.005 — away from zero it is 1 000.01, half to even it would be
    /// 1 000.00. The two modes disagree only here, so this is the only place that proves the
    /// convention.
    /// </summary>
    [Fact]
    public async Task The_average_should_be_rounded_away_from_the_midpoint()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", AmountPax = 1000.00m, IsActive = true, NumPaxId = 4 });
        _store.Packages.Add(new Package { PackageId = 2, PackageType = "Buffet", Description = "B", AmountPax = 1000.01m, IsActive = true, NumPaxId = 4 });

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Data!.AverageAmountPax.Should().Be(1000.01m);
    }

    [Fact]
    public async Task A_catalogue_of_one_tier_should_publish_that_tier_s_own_figures()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", AmountPax = 777.77m, MinPax = 50, IsActive = false, NumPaxId = 4 });

        // Act
        var result = await _summary.ExecuteAsync();

        // Assert
        result.Data!.TotalPackages.Should().Be(1);
        result.Data.ActivePackages.Should().Be(0);
        result.Data.InactivePackages.Should().Be(1);
        result.Data.PricedPackages.Should().Be(1);
        result.Data.AverageAmountPax.Should().Be(777.77m);
        result.Data.LowestAmountPax.Should().Be(777.77m);
        result.Data.HighestAmountPax.Should().Be(777.77m);
        result.Data.LowestMinPax.Should().Be(50);
        result.Data.HighestMinPax.Should().Be(50);
    }
}
