namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// The municipality catalogue commands (contract §13.10, §13.11, BR-11, DEC-077).
/// </summary>
public class AreaCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeAreaRepository _areas;
    private readonly CreateAreaCommand _create;
    private readonly UpdateAreaCommand _update;

    public AreaCommandTests()
    {
        _areas = new FakeAreaRepository(_store);
        _create = new CreateAreaCommand(_areas, new AreaRequestValidator());
        _update = new UpdateAreaCommand(_areas, new AreaRequestValidator());
    }

    [Fact]
    public async Task A_valid_payload_should_create_a_municipality()
    {
        var result = await _create.ExecuteAsync(new AreaRequestDto(
            "Antipolo",
            150m,
            42,
            "Reefer Truck & Mobile Gen",
            3200m));

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);

        _store.Areas.Should().ContainSingle();
        _store.Areas[0].AreaDetails.Should().Be("Antipolo");
        _store.Areas[0].AdditionalRate.Should().Be(150m);
        _store.Areas[0].DistanceKm.Should().Be(42);
        _store.Areas[0].LogisticsNote.Should().Be("Reefer Truck & Mobile Gen");
        _store.Areas[0].LogisticsCost.Should().Be(3200m);
    }

    [Fact]
    public async Task A_rejected_payload_should_persist_nothing()
    {
        var result = await _create.ExecuteAsync(new AreaRequestDto("   ", -1m, -1, new string('x', 151), -1m));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Area name is required.");
        result.Errors.Should().Contain("Additional rate must be zero or greater.");
        result.Errors.Should().Contain("Distance must be zero or greater.");
        result.Errors.Should().Contain("Logistics note must not exceed 150 characters.");
        result.Errors.Should().Contain("Logistics cost must be zero or greater.");
        _store.Areas.Should().BeEmpty();
        _areas.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task A_municipality_without_a_stated_rate_should_be_created()
    {
        var result = await _create.ExecuteAsync(new AreaRequestDto("Lucena", null, null, null, null));

        result.Succeeded.Should().BeTrue();
        _store.Areas.Should().ContainSingle();
        _store.Areas[0].AdditionalRate.Should().BeNull();
        _store.Areas[0].LogisticsCost.Should().BeNull();
    }

    [Fact]
    public async Task The_update_should_change_the_municipality()
    {
        _store.Areas.Add(new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m });

        var result = await _update.ExecuteAsync(1, new AreaRequestDto("Antipolo City", 200m, 44, "Van", 1500m));

        result.Succeeded.Should().BeTrue();
        _store.Areas[0].AreaDetails.Should().Be("Antipolo City");
        _store.Areas[0].AdditionalRate.Should().Be(200m);
        _store.Areas[0].DistanceKm.Should().Be(44);
        _store.Areas[0].LogisticsNote.Should().Be("Van");
        _store.Areas[0].LogisticsCost.Should().Be(1500m);
    }

    [Fact]
    public async Task An_update_to_a_missing_municipality_should_be_a_not_found()
    {
        var result = await _update.ExecuteAsync(99, new AreaRequestDto("Antipolo", null, null, null, null));

        result.Succeeded.Should().BeFalse();
        result.Should().BeOfType<AreaNotFoundResult>();
        result.Errors.Should().ContainSingle().Which.Should().Be("Area not found.");
    }

    [Fact]
    public async Task A_rejected_update_should_persist_nothing()
    {
        _store.Areas.Add(new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m });

        var result = await _update.ExecuteAsync(1, new AreaRequestDto("", -5m, null, null, null));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Area name is required.");
        _store.Areas[0].AreaDetails.Should().Be("Antipolo");
        _store.Areas[0].AdditionalRate.Should().Be(150m);
        _areas.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task The_catalogue_should_publish_the_live_municipalities_only()
    {
        _store.Areas.Add(new Area { AreaId = 1, AreaDetails = "Antipolo", AdditionalRate = 150m });
        _store.Areas.Add(new Area { AreaId = 2, AreaDetails = "Retired", IsDeleted = true });

        var query = new GetAreasQuery(_areas);

        var catalogue = (await query.ExecuteAsync()).Data!;

        catalogue.Should().ContainSingle();
        catalogue[0].AreaDetails.Should().Be("Antipolo");
    }
}
