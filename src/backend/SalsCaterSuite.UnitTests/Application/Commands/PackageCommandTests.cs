namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs.Request;
using SalsCaterSuite.Application.Features.Packages.DTOs.Response;
using SalsCaterSuite.Application.Features.Packages.Models;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>DEC-037: the package write use cases are repository-backed (hand-written fake).</summary>
public class PackageCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakePackageRepository _packages;
    private readonly FakePackagePaxApplicableRepository _paxApplicables;
    private readonly CreatePackageCommand _create;
    private readonly UpdatePackageCommand _update;
    private readonly TogglePackageActiveCommand _toggle;

    public PackageCommandTests()
    {
        _packages = new FakePackageRepository(_store);
        _paxApplicables = new FakePackagePaxApplicableRepository(_store);

        // The five fixed classification rows are seeded by the schema migration (DEC-173), so
        // the fake store carries them too.
        _store.SeedPackagePaxApplicables();

        _create = new CreatePackageCommand(_packages, _paxApplicables, new PackageRequestValidator());
        _update = new UpdatePackageCommand(_packages, _paxApplicables, new PackageRequestValidator());
        _toggle = new TogglePackageActiveCommand(_packages);
    }

    private static CreatePackageRequest Request() =>
        new("Buffet Premium", "Premium buffet package", 650m, 100, 4);

    [Fact]
    public async Task Create_ShouldPersistActivePackageAndReturnAssignedId()
    {
        // Act
        var result = await _create.ExecuteAsync(Request());

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeGreaterThan(0);
        _store.Packages.Should().ContainSingle();
        _store.Packages[0].PackageType.Should().Be("Buffet Premium");
        _store.Packages[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_ShouldGenerateTheFrozenPackageCode()
    {
        // Act
        await _create.ExecuteAsync(new CreatePackageRequest("Wedding", "Wedding Grand", 1450m, 150, 4));
        await _create.ExecuteAsync(new CreatePackageRequest("Wedding", "Wedding Two", 1500m, 150, 4));

        // Assert — the per-type sequence starts at 01 and increments; nothing else is counted.
        _store.Packages[0].PackageCode.Should().Be("PKG-WED-01");
        _store.Packages[1].PackageCode.Should().Be("PKG-WED-02");
    }

    [Fact]
    public async Task Update_ShouldNotRenumberTheFrozenCode()
    {
        // Arrange
        _store.Packages.Add(new Package
        {
            PackageId = 3,
            PackageCode = "PKG-REG-01",
            PackageType = "Regular",
            Description = "Old",
            AmountPax = 100m,
            MinPax = 10,
            NumPaxId = 4,
        });

        // Act — the type changes, but the code must not.
        var result = await _update.ExecuteAsync(3, Request());

        // Assert
        result.Succeeded.Should().BeTrue();
        _store.Packages.Single(p => p.PackageId == 3).PackageCode.Should().Be("PKG-REG-01");
    }

    [Fact]
    public async Task Create_ShouldStoreTheExtendedLocationAmountForAnEligibleTier()
    {
        // Act
        var result = await _create.ExecuteAsync(
            new CreatePackageRequest("Regular", "Regular Feast", 950m, 80, 4, 125m));

        // Assert
        result.Succeeded.Should().BeTrue();
        _store.Packages[0].ExtendedLocationAmount.Should().Be(125m);
    }

    [Fact]
    public async Task Update_ShouldClearTheExtendedLocationAmountWhenTheTypeLeavesRegularOrPremier()
    {
        // Arrange
        _store.Packages.Add(new Package
        {
            PackageId = 3,
            PackageCode = "PKG-REG-01",
            PackageType = "Regular",
            Description = "Old",
            AmountPax = 100m,
            MinPax = 10,
            ExtendedLocationAmount = 125m,
            NumPaxId = 4
        });

        // Act — the operator changes the type to Wedding and sends no amount.
        var result = await _update.ExecuteAsync(
            3, new CreatePackageRequest("Wedding", "Wedding Grand", 1450m, 150, 4, null));

        // Assert
        result.Succeeded.Should().BeTrue();
        _store.Packages.Single(p => p.PackageId == 3).ExtendedLocationAmount.Should().BeNull();
    }

    [Fact]
    public async Task Update_WhenPackageExists_ShouldApplyChangesAndSave()
    {
        // Arrange
        _store.Packages.Add(new Package{ PackageId = 3, PackageType = "Old", Description = "Old", AmountPax = 100m, MinPax = 10 ,
 NumPaxId = 4,
});

        // Act
        var result = await _update.ExecuteAsync(3, Request());

        // Assert
        result.Succeeded.Should().BeTrue();
        var saved = _store.Packages.Single(p => p.PackageId == 3);
        saved.PackageType.Should().Be("Buffet Premium");
        saved.AmountPax.Should().Be(650m);
        saved.MinPax.Should().Be(100);
    }

    [Fact]
    public async Task Update_WhenPackageDoesNotExist_ShouldReturnNotFoundFailure()
    {
        // Act
        var result = await _update.ExecuteAsync(999, Request());

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Package not found.");
        // The API distinguishes "no such tier" (404) from "rejected payload" (400) by the
        // result type, never by the message text (DEC-069).
        result.Should().BeOfType<PackageNotFoundResult>();
    }

    [Fact]
    public async Task Toggle_WhenPackageActive_ShouldDeactivateAndReturnNewState()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 3, PackageType = "Buffet", IsActive = true, NumPaxId = 4 });

        // Act
        var result = await _toggle.ExecuteAsync(3);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeFalse();
        _store.Packages.Single(p => p.PackageId == 3).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Toggle_WhenPackageDoesNotExist_ShouldReturnNotFoundFailure()
    {
        // Act
        var result = await _toggle.ExecuteAsync(999);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Package not found.");
    }

    // ─────────────────────────── the write validation (BR-11, DEC-069) ───────────────────────────

    [Fact]
    public async Task Create_WhenThePayloadIsRejected_ShouldPersistNothingAndReturnTheMessages()
    {
        // Arrange
        var request = new CreatePackageRequest("   ", string.Empty, -1m, 0, 0);

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert — nothing reached the store.
        result.Succeeded.Should().BeFalse();
        _store.Packages.Should().BeEmpty();

        result.Errors.Should().Contain("Package type is required.");
        result.Errors.Should().Contain("Package description is required.");
        result.Errors.Should().Contain("Rate per guest must be zero or greater.");
        result.Errors.Should().Contain("Minimum pax must be at least 1.");
        result.Errors.Should().Contain("Applicable pax classification is required.");
    }

    [Fact]
    public async Task Create_WhenTheDescriptionIsBlank_ShouldReturnTheDescriptionMessageOnly()
    {
        // Act
        var result = await _create.ExecuteAsync(new CreatePackageRequest("Buffet", "   ", 100m, 10, 4));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Package description is required.");
        _store.Packages.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_WhenThePayloadIsRejected_ShouldPersistNothingAndReturnTheMessages()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 3, PackageType = "Old", Description = "Old", AmountPax = 100m, MinPax = 10, NumPaxId = 4 });

        // Act
        var result = await _update.ExecuteAsync(3, new CreatePackageRequest("Buffet", string.Empty, -5m, 10, 4));

        // Assert — the stored tier is untouched.
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Package description is required.");
        result.Errors.Should().Contain("Rate per guest must be zero or greater.");

        var saved = _store.Packages.Single(p => p.PackageId == 3);
        saved.Description.Should().Be("Old");
        saved.AmountPax.Should().Be(100m);
    }

    [Fact]
    public async Task Update_WhenThePayloadIsRejected_ShouldNotBeReportedAsNotFound()
    {
        // Act
        var result = await _update.ExecuteAsync(999, new CreatePackageRequest("Buffet", string.Empty, 100m, 10, 4));

        // Assert — a rejected payload is a 400, not a 404, even for an unknown tier.
        result.Succeeded.Should().BeFalse();
        result.Should().NotBeOfType<PackageNotFoundResult>();
        result.Errors.Should().Contain("Package description is required.");
    }

    // ─────────────────── the applicable-pax classification (REQ-PKG-04, DEC-173) ───────────────────

    [Fact]
    public async Task Create_ShouldStoreTheSelectedApplicablePaxClassification()
    {
        // Act
        var result = await _create.ExecuteAsync(
            new CreatePackageRequest("Buffet Premium", "Premium buffet package", 650m, 100, 2));

        // Assert
        result.Succeeded.Should().BeTrue();
        _store.Packages[0].NumPaxId.Should().Be(2);
    }

    [Fact]
    public async Task Create_WhenTheClassificationIsMissing_ShouldPersistNothing()
    {
        // Act — the identifier is omitted, so it binds the C# default of zero.
        var result = await _create.ExecuteAsync(
            new CreatePackageRequest("Buffet Premium", "Premium buffet package", 650m, 100, 0));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Applicable pax classification is required.");
        _store.Packages.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheClassificationIsUnknown_ShouldPersistNothing()
    {
        // Act — a positive identifier that references no lookup row.
        var result = await _create.ExecuteAsync(
            new CreatePackageRequest("Buffet Premium", "Premium buffet package", 650m, 100, 999));

        // Assert — the referential half of the validation (DEC-173).
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Applicable pax classification was not found.");
        _store.Packages.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WhenTheClassificationIsSoftDeleted_ShouldPersistNothing()
    {
        // Arrange — a removed classification is no longer a selectable option.
        _store.PackagePaxApplicables.Single(o => o.NumPaxId == 3).IsDeleted = true;

        // Act
        var result = await _create.ExecuteAsync(
            new CreatePackageRequest("Buffet Premium", "Premium buffet package", 650m, 100, 3));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Applicable pax classification was not found.");
        _store.Packages.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ShouldStoreTheSelectedApplicablePaxClassification()
    {
        // Arrange
        _store.Packages.Add(new Package
        {
            PackageId = 3,
            PackageCode = "PKG-REG-01",
            PackageType = "Regular",
            Description = "Old",
            AmountPax = 100m,
            MinPax = 10,
            NumPaxId = 4
        });

        // Act
        var result = await _update.ExecuteAsync(
            3, new CreatePackageRequest("Regular", "New", 100m, 10, 5));

        // Assert
        result.Succeeded.Should().BeTrue();
        _store.Packages.Single(p => p.PackageId == 3).NumPaxId.Should().Be(5);
    }

    [Fact]
    public async Task Update_WhenTheClassificationIsUnknown_ShouldChangeNothing()
    {
        // Arrange
        _store.Packages.Add(new Package
        {
            PackageId = 3,
            PackageCode = "PKG-REG-01",
            PackageType = "Regular",
            Description = "Old",
            AmountPax = 100m,
            MinPax = 10,
            NumPaxId = 4
        });

        // Act
        var result = await _update.ExecuteAsync(
            3, new CreatePackageRequest("Regular", "New", 100m, 10, 999));

        // Assert — the stored tier keeps its previous classification and description.
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Applicable pax classification was not found.");

        var saved = _store.Packages.Single(p => p.PackageId == 3);
        saved.NumPaxId.Should().Be(4);
        saved.Description.Should().Be("Old");
    }

    [Fact]
    public async Task Update_WhenTheClassificationIsMissing_ShouldChangeNothing()
    {
        // Arrange
        _store.Packages.Add(new Package
        {
            PackageId = 3,
            PackageCode = "PKG-REG-01",
            PackageType = "Regular",
            Description = "Old",
            AmountPax = 100m,
            MinPax = 10,
            NumPaxId = 4
        });

        // Act
        var result = await _update.ExecuteAsync(
            3, new CreatePackageRequest("Regular", "New", 100m, 10, 0));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Applicable pax classification is required.");

        var saved = _store.Packages.Single(p => p.PackageId == 3);
        saved.NumPaxId.Should().Be(4);
        saved.Description.Should().Be("Old");
    }
}
