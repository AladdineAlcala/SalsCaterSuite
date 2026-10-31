namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The coverage-set replacement and the attach action (contract §13.5, §13.6, DEC-085, DEC-088,
/// BR-11).
/// </summary>
public class PackageAreaCoverageCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakePackageAreaCoverageRepository _coverages;
    private readonly FakeAreaRepository _areas;
    private readonly ReplacePackageAreaCoverageCommand _replace;
    private readonly AttachPackageAreaCoverageCommand _attach;

    public PackageAreaCoverageCommandTests()
    {
        _store.Packages.Add(new Package
        {
            PackageId = 1,
            PackageType = "Wedding",
            Description = "Wedding Grand Tier",
            AmountPax = 1450m,
            MinPax = 150,
            IsActive = true,
            NumPaxId = 4,
        });

        _store.Areas.Add(new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m, LogisticsCost = 3200m });
        _store.Areas.Add(new Area { AreaId = 2, AreaDetails = "Batangas", AdditionalRate = 200m });

        _coverages = new FakePackageAreaCoverageRepository(_store);
        _areas = new FakeAreaRepository(_store);

        var rulesetQuery = new GetPackageCourseRulesetQuery(
            new FakePackageRepository(_store),
            new FakePackageCourseRuleRepository(_store),
            new FakeMenuRepository(_store),
            new FakeFoodCostBenchmarkProvider(35m),
            new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc)));

        _replace = new ReplacePackageAreaCoverageCommand(
            new FakePackageRepository(_store),
            _coverages,
            new PackageAreaCoverageRequestValidator(new PackageAreaCoverageEntryValidator(_areas)),
            new GetPackageAreaCoverageMatrixQuery(
                rulesetQuery,
                _coverages,
                new FakeDateTime(new DateTime(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc))));

        _attach = new AttachPackageAreaCoverageCommand(
            new FakePackageRepository(_store),
            _coverages,
            _areas);
    }

    private static PackageAreaCoverageRequest Request(params PackageAreaCoverageEntryRequest[] coverages) =>
        new(coverages);

    [Fact]
    public async Task A_rejected_payload_should_persist_nothing()
    {
        var result = await _replace.ExecuteAsync(1, Request(new PackageAreaCoverageEntryRequest(99, true)));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Area 99 does not exist.");
        _store.PackageAreaCoverages.Should().BeEmpty();
        _coverages.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task A_coverage_removed_from_the_submitted_set_should_be_soft_deleted_and_not_destroyed()
    {
        _store.PackageAreaCoverages.Add(new PackageAreaCoverage
        {
            PackageAreaCoverageId = 1,
            PackageId = 1,
            AreaId = 1,
            IsActive = true
        });

        var result = await _replace.ExecuteAsync(1, Request(new PackageAreaCoverageEntryRequest(2, true)));

        result.Succeeded.Should().BeTrue();

        // The row survives for audit — it is flagged, not destroyed (DEC-085).
        _store.PackageAreaCoverages.Should().HaveCount(2);
        _store.PackageAreaCoverages.Single(coverage => coverage.AreaId == 1).IsDeleted.Should().BeTrue();
        _store.PackageAreaCoverages.Single(coverage => coverage.AreaId == 2).IsDeleted.Should().BeFalse();

        result.Data!.Areas.Should().ContainSingle();
        result.Data.Areas[0].AreaId.Should().Be(2);
    }

    [Fact]
    public async Task An_empty_set_should_clear_the_tier_by_soft_deleting_every_live_row()
    {
        _store.PackageAreaCoverages.Add(new PackageAreaCoverage
        {
            PackageAreaCoverageId = 1,
            PackageId = 1,
            AreaId = 1,
            IsActive = true
        });

        var result = await _replace.ExecuteAsync(1, Request());

        result.Succeeded.Should().BeTrue();
        _store.PackageAreaCoverages.Should().ContainSingle();
        _store.PackageAreaCoverages[0].IsDeleted.Should().BeTrue();
        result.Data!.CoverageCount.Should().Be(0);
    }

    [Fact]
    public async Task A_re_added_municipality_should_resurrect_its_soft_deleted_row()
    {
        _store.PackageAreaCoverages.Add(new PackageAreaCoverage
        {
            PackageAreaCoverageId = 1,
            PackageId = 1,
            AreaId = 1,
            IsActive = true
        });

        await _replace.ExecuteAsync(1, Request());
        _store.PackageAreaCoverages[0].IsDeleted.Should().BeTrue();

        var result = await _replace.ExecuteAsync(1, Request(new PackageAreaCoverageEntryRequest(1, true)));

        result.Succeeded.Should().BeTrue();
        _store.PackageAreaCoverages.Should().ContainSingle();
        _store.PackageAreaCoverages[0].PackageAreaCoverageId.Should().Be(1);
        _store.PackageAreaCoverages[0].IsDeleted.Should().BeFalse();
        result.Data!.CoverageCount.Should().Be(1);
    }

    [Fact]
    public async Task A_soft_deleted_tier_should_be_refused_and_no_write_should_touch_it()
    {
        _store.Packages[0].IsDeleted = true;

        var result = await _replace.ExecuteAsync(1, Request(new PackageAreaCoverageEntryRequest(1, true)));

        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<PackageNotFoundResult<PackageAreaCoverageMatrixResponse>>();
        _store.PackageAreaCoverages.Should().BeEmpty();
        _coverages.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task The_attach_action_should_add_a_municipality()
    {
        var result = await _attach.ExecuteAsync(1, new PackageAreaCoverageAttachRequest(1, true));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
        _store.PackageAreaCoverages.Should().ContainSingle();
        _store.PackageAreaCoverages[0].AreaId.Should().Be(1);
        _store.PackageAreaCoverages[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task The_attach_action_should_refuse_an_unknown_municipality()
    {
        var result = await _attach.ExecuteAsync(1, new PackageAreaCoverageAttachRequest(99, true));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Area 99 does not exist.");
        _store.PackageAreaCoverages.Should().BeEmpty();
    }

    [Fact]
    public async Task The_attach_action_should_refuse_a_municipality_the_tier_already_covers()
    {
        _store.PackageAreaCoverages.Add(new PackageAreaCoverage
        {
            PackageAreaCoverageId = 1,
            PackageId = 1,
            AreaId = 1,
            IsActive = true
        });

        var result = await _attach.ExecuteAsync(1, new PackageAreaCoverageAttachRequest(1, true));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Area 1 is already covered by this package.");
        _store.PackageAreaCoverages.Should().ContainSingle();
    }

    [Fact]
    public async Task The_attach_action_should_refuse_a_soft_deleted_tier()
    {
        _store.Packages[0].IsDeleted = true;

        var result = await _attach.ExecuteAsync(1, new PackageAreaCoverageAttachRequest(1, true));

        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<PackageNotFoundResult<int>>();
        _store.PackageAreaCoverages.Should().BeEmpty();
    }
}
