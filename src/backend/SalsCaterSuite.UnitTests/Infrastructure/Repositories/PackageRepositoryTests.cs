namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using FluentAssertions;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.UnitTests.Common;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// Infrastructure-layer tests for the package repository (DEC-037). The EF-specific
/// projection, active-only filtering and catalogue-aggregate behaviour is proven here
/// against EF Core.
/// </summary>
public class PackageRepositoryTests : IDisposable
{
    private readonly SalsCaterSuiteDbContext _context;
    private readonly PackageRepository _repository;

    public PackageRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new PackageRepository(_context);
    }

    [Fact]
    public async Task AddAsync_And_SaveChangesAsync_ShouldAssignIdentityAndPersist()
    {
        // Arrange
        var package = new Package { PackageType = "Buffet Premium", Description = "Premium", AmountPax = 650m, MinPax = 100, IsActive = true, NumPaxId = 4 };

        // Act
        await _repository.AddAsync(package);
        await _repository.SaveChangesAsync();

        // Assert
        package.PackageId.Should().BeGreaterThan(0);
        (await _context.Packages.FindAsync(package.PackageId)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetProjectionListAsync_WhenActiveOnly_ShouldExcludeInactivePackages()
    {
        // Arrange
        await _context.Packages.AddRangeAsync(
            new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 2, PackageType = "Plated", Description = "B", IsActive = false, NumPaxId = 4 });
        await _context.SaveChangesAsync();

        // Act
        var all = await _repository.GetProjectionListAsync(null);
        var active = await _repository.GetProjectionListAsync(activeOnly: true);

        // Assert
        all.Should().HaveCount(2);
        active.Should().ContainSingle().Which.PackageId.Should().Be(1);
    }

    [Fact]
    public async Task GetProjectionByIdAsync_WhenPackageDoesNotExist_ShouldReturnNull()
    {
        (await _repository.GetProjectionByIdAsync(999)).Should().BeNull();
    }

    /// <summary>
    /// The applicable-pax classification is published by both catalogue read projections
    /// (REQ-PKG-04, DEC-173). A projection that omitted it would answer the default value and
    /// the client would render the wrong option.
    /// </summary>
    [Fact]
    public async Task The_read_projections_should_publish_the_applicable_pax_classification()
    {
        // Arrange
        await _context.Packages.AddRangeAsync(
            new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 2, PackageType = "Plated", Description = "B", IsActive = true, NumPaxId = 2 });
        await _context.SaveChangesAsync();

        // Act
        var list = await _repository.GetProjectionListAsync(null);
        var detail = await _repository.GetProjectionByIdAsync(1);

        // Assert
        list.Single(p => p.PackageId == 1).NumPaxId.Should().Be(4);
        list.Single(p => p.PackageId == 2).NumPaxId.Should().Be(2);
        detail!.NumPaxId.Should().Be(4);
    }

    /// <summary>
    /// The catalogue's scoped population (DEC-074): the read projections exclude a
    /// soft-deleted tier, while the entity fetch keeps returning it because the booking path
    /// resolves the package's rate per head from it (BR-04).
    /// </summary>
    [Fact]
    public async Task The_read_projections_should_exclude_a_soft_deleted_tier_but_the_entity_fetch_should_not()
    {
        // Arrange
        await _context.Packages.AddRangeAsync(
            new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 2, PackageType = "Ghost", Description = "B", IsActive = true, IsDeleted = true, NumPaxId = 4 });
        await _context.SaveChangesAsync();

        // Act
        var list = await _repository.GetProjectionListAsync(null);
        var detail = await _repository.GetProjectionByIdAsync(2);
        var entity = await _repository.GetByIdAsync(2);

        // Assert
        list.Should().ContainSingle().Which.PackageId.Should().Be(1);
        detail.Should().BeNull();
        entity.Should().NotBeNull();
        entity!.PackageId.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTrackedEntityForInPlaceUpdates()
    {
        // Arrange
        await _context.Packages.AddAsync(new Package { PackageId = 5, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 });
        await _context.SaveChangesAsync();

        // Act — mutate then persist, as the toggle-active use case does.
        var package = await _repository.GetByIdAsync(5);
        package.Should().NotBeNull();
        package!.IsActive = !package.IsActive;
        await _repository.SaveChangesAsync();

        // Assert
        var reloaded = await _context.Packages.FindAsync(5);
        reloaded!.IsActive.Should().BeFalse();
    }

    // ─────────────────────────── the catalogue aggregate (DEC-068) ───────────────────────────

    [Fact]
    public async Task GetCatalogueSummaryAsync_ShouldMeasureTheCatalogueOverTheNonDeletedPopulation()
    {
        // Arrange — (950 + 1200 + 1450) ÷ 3 = 1 200.00
        await _context.Packages.AddRangeAsync(
            new Package { PackageId = 1, PackageType = "Buffet", Description = "A", AmountPax = 950m, MinPax = 80, IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 2, PackageType = "Buffet", Description = "B", AmountPax = 1200m, MinPax = 100, IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 3, PackageType = "Plated", Description = "C", AmountPax = 1450m, MinPax = 150, IsActive = false, NumPaxId = 4 },
            new Package { PackageId = 4, PackageType = "Plated", Description = "D", AmountPax = null, MinPax = null, IsActive = true, NumPaxId = 4 },
            // A soft-deleted tier is excluded by the query itself (DEC-045 / DEC-050).
            new Package { PackageId = 5, PackageType = "Ghost", Description = "E", AmountPax = 9999m, MinPax = 999, IsActive = true, IsDeleted = true, NumPaxId = 4 });
        await _context.SaveChangesAsync();

        // Act
        var summary = await _repository.GetCatalogueSummaryAsync();

        // Assert
        summary.TotalPackages.Should().Be(4);
        summary.ActivePackages.Should().Be(3);
        summary.InactivePackages.Should().Be(1);
        summary.PricedPackages.Should().Be(3);
        summary.AverageAmountPax.Should().Be(1200.00m);
        summary.LowestAmountPax.Should().Be(950m);
        summary.HighestAmountPax.Should().Be(1450m);
        summary.LowestMinPax.Should().Be(80);
        summary.HighestMinPax.Should().Be(150);
    }

    [Fact]
    public async Task GetCatalogueSummaryAsync_WhenTheCatalogueIsEmpty_ShouldReturnZeroCountsAndNullAggregates()
    {
        // Act
        var summary = await _repository.GetCatalogueSummaryAsync();

        // Assert
        summary.TotalPackages.Should().Be(0);
        summary.ActivePackages.Should().Be(0);
        summary.InactivePackages.Should().Be(0);
        summary.PricedPackages.Should().Be(0);
        summary.AverageAmountPax.Should().BeNull();
        summary.LowestAmountPax.Should().BeNull();
        summary.HighestAmountPax.Should().BeNull();
        summary.LowestMinPax.Should().BeNull();
        summary.HighestMinPax.Should().BeNull();
    }

    /// <summary>
    /// The SQL aggregate and the hand-written in-memory mirror must publish the same figures
    /// for the same population — the same guard the menu costing's rounding consistency tests
    /// apply (DEC-003, DEC-042). A divergence here would let the API and the unit-tested
    /// Application layer describe two different catalogues.
    /// </summary>
    [Fact]
    public async Task GetCatalogueSummaryAsync_ShouldAgreeWithTheHandWrittenFake()
    {
        // Arrange — the same population in the database and in the fake's store.
        var population = new[]
        {
            new Package { PackageId = 1, PackageType = "Buffet", Description = "A", AmountPax = 950m, MinPax = 80, IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 2, PackageType = "Buffet", Description = "B", AmountPax = 1200m, MinPax = 100, IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 3, PackageType = "Plated", Description = "C", AmountPax = 1450m, MinPax = 150, IsActive = false, NumPaxId = 4 },
            new Package { PackageId = 4, PackageType = "Plated", Description = "D", AmountPax = null, MinPax = null, IsActive = true, NumPaxId = 4 },
            new Package { PackageId = 5, PackageType = "Ghost", Description = "E", AmountPax = 9999m, MinPax = 999, IsActive = true, IsDeleted = true, NumPaxId = 4 }
        };

        await _context.Packages.AddRangeAsync(population);
        await _context.SaveChangesAsync();

        var store = new FakeSalsCaterSuiteDataStore();
        store.Packages.AddRange(population.Select(p => new Package
        {
            PackageId = p.PackageId,
            PackageType = p.PackageType,
            Description = p.Description,
            AmountPax = p.AmountPax,
            MinPax = p.MinPax,
            IsActive = p.IsActive,
            IsDeleted = p.IsDeleted,
            NumPaxId = p.NumPaxId
        }));

        var fake = new FakePackageRepository(store);

        // Act
        var fromDatabase = await _repository.GetCatalogueSummaryAsync();
        var fromFake = await fake.GetCatalogueSummaryAsync();

        // Assert
        fromDatabase.TotalPackages.Should().Be(fromFake.TotalPackages);
        fromDatabase.ActivePackages.Should().Be(fromFake.ActivePackages);
        fromDatabase.InactivePackages.Should().Be(fromFake.InactivePackages);
        fromDatabase.PricedPackages.Should().Be(fromFake.PricedPackages);
        fromDatabase.AverageAmountPax.Should().Be(fromFake.AverageAmountPax);
        fromDatabase.LowestAmountPax.Should().Be(fromFake.LowestAmountPax);
        fromDatabase.HighestAmountPax.Should().Be(fromFake.HighestAmountPax);
        fromDatabase.LowestMinPax.Should().Be(fromFake.LowestMinPax);
        fromDatabase.HighestMinPax.Should().Be(fromFake.HighestMinPax);
    }

    public void Dispose()
    {
        TestDbContextFactory.Destroy(_context);
    }

    // ─────────────────────────── the workbench projections (REQ-PKG-02) ───────────────────────────

    /// <summary>
    /// The course-rule projection over the seeded population: the live rules and their course
    /// names (contract §13.1).
    /// <para>
    /// The rule no longer resolves an upgrade add-on: DEC-172 retired that coupling, so the
    /// projection publishes the rule's own fields and the course name alone.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_course_rule_projection_should_resolve_the_live_rules_and_their_course_names()
    {
        // Arrange
        await _context.CourseCategories.AddRangeAsync(
            new CourseCategory { CourseId = 1, CourseName = "Main Course" },
            new CourseCategory { CourseId = 2, CourseName = "Dessert", IsDeleted = true });

        await _context.Packages.AddAsync(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 });

        await _context.PackageCourseRules.AddRangeAsync(
            new PackageCourseRule { PackageCourseRuleId = 1, PackageId = 1, CourseId = 1, RequiredQuota = 1, RuleDescription = "One main course" },
            new PackageCourseRule { PackageCourseRuleId = 2, PackageId = 1, CourseId = 2, RequiredQuota = 1, RuleDescription = "One dessert" },
            new PackageCourseRule { PackageCourseRuleId = 3, PackageId = 1, CourseId = 1, RequiredQuota = 2, RuleDescription = "Removed rule", IsDeleted = true });

        await _context.SaveChangesAsync();

        var repository = new PackageCourseRuleRepository(_context);

        // Act
        var live = await repository.GetLiveRulesAsync(1);
        var all = await repository.GetAllRulesAsync(1);

        // Assert
        live.Should().HaveCount(2);
        all.Should().HaveCount(3);

        var rule = live.Single(candidate => candidate.PackageCourseRuleId == 1);
        rule.CourseName.Should().Be("Main Course");
        rule.RequiredQuota.Should().Be(1);
    }

    /// <summary>
    /// The coverage projection over the seeded population: a coverage row whose municipality is
    /// soft-deleted is excluded, because the municipality catalogue is the population
    /// (contract §13.4).
    /// </summary>
    [Fact]
    public async Task The_coverage_projection_should_exclude_a_soft_deleted_municipality()
    {
        // Arrange
        await _context.Areas.AddRangeAsync(
            new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m, DistanceKm = 42, LogisticsNote = "Reefer", LogisticsCost = 3200m },
            new Area { AreaId = 2, AreaDetails = "Retired municipality", IsDeleted = true });

        await _context.Packages.AddAsync(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 });

        await _context.PackageAreaCoverages.AddRangeAsync(
            new PackageAreaCoverage { PackageAreaCoverageId = 1, PackageId = 1, AreaId = 1, IsActive = true },
            new PackageAreaCoverage { PackageAreaCoverageId = 2, PackageId = 1, AreaId = 2, IsActive = true },
            new PackageAreaCoverage { PackageAreaCoverageId = 3, PackageId = 1, AreaId = 1, IsActive = false, IsDeleted = true });

        await _context.SaveChangesAsync();

        var repository = new PackageAreaCoverageRepository(_context);

        // Act
        var live = await repository.GetLiveCoveragesAsync(1);
        var all = await repository.GetAllCoveragesAsync(1);

        // Assert
        live.Should().ContainSingle();

        var row = live.Single();
        row.AreaId.Should().Be(1);
        row.AreaName.Should().Be("Antipolo");
        row.AdditionalRate.Should().Be(150m);
        row.DistanceKm.Should().Be(42);
        row.LogisticsNote.Should().Be("Reefer");
        row.LogisticsCost.Should().Be(3200m);
        row.IsActive.Should().BeTrue();

        all.Should().HaveCount(3);
    }

    /// <summary>The municipality catalogue excludes the soft-deleted municipalities.</summary>
    [Fact]
    public async Task The_municipality_catalogue_should_exclude_the_soft_deleted_municipalities()
    {
        // Arrange
        await _context.Areas.AddRangeAsync(
            new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m },
            new Area { AreaId = 2, AreaDetails = "Retired municipality", IsDeleted = true });

        await _context.SaveChangesAsync();

        var repository = new AreaRepository(_context);

        // Act
        var catalogue = await repository.GetCatalogueAsync();

        // Assert
        catalogue.Should().ContainSingle();
        catalogue[0].AreaDetails.Should().Be("Antipolo");
        catalogue[0].AdditionalRate.Should().Be(150m);
        (await repository.GetLiveByIdAsync(1)).Should().NotBeNull();
        (await repository.GetLiveByIdAsync(2)).Should().BeNull();
        (await repository.ExistsAsync(1)).Should().BeTrue();
        (await repository.ExistsAsync(2)).Should().BeFalse();
    }
}
