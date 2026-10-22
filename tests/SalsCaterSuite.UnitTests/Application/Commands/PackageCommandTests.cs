namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.DTOs;
using SalsCaterSuite.Application.Features.Packages.Validators;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>DEC-037: the package write use cases are repository-backed (hand-written fake).</summary>
public class PackageCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakePackageRepository _packages;
    private readonly CreatePackageCommand _create;
    private readonly UpdatePackageCommand _update;
    private readonly TogglePackageActiveCommand _toggle;

    public PackageCommandTests()
    {
        _packages = new FakePackageRepository(_store);
        _create = new CreatePackageCommand(_packages, new PackageRequestValidator());
        _update = new UpdatePackageCommand(_packages, new PackageRequestValidator());
        _toggle = new TogglePackageActiveCommand(_packages);
    }

    private static CreatePackageRequestDto Request() =>
        new("Buffet Premium", "Premium buffet package", 650m, 100);

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
    public async Task Update_WhenPackageExists_ShouldApplyChangesAndSave()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 3, PackageType = "Old", Description = "Old", AmountPax = 100m, MinPax = 10 });

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
        _store.Packages.Add(new Package { PackageId = 3, PackageType = "Buffet", IsActive = true });

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
        var request = new CreatePackageRequestDto("   ", string.Empty, -1m, 0);

        // Act
        var result = await _create.ExecuteAsync(request);

        // Assert — nothing reached the store.
        result.Succeeded.Should().BeFalse();
        _store.Packages.Should().BeEmpty();

        result.Errors.Should().Contain("Package type is required.");
        result.Errors.Should().Contain("Package description is required.");
        result.Errors.Should().Contain("Rate per guest must be zero or greater.");
        result.Errors.Should().Contain("Minimum pax must be at least 1.");
    }

    [Fact]
    public async Task Create_WhenTheDescriptionIsBlank_ShouldReturnTheDescriptionMessageOnly()
    {
        // Act
        var result = await _create.ExecuteAsync(new CreatePackageRequestDto("Buffet", "   ", 100m, 10));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Package description is required.");
        _store.Packages.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_WhenThePayloadIsRejected_ShouldPersistNothingAndReturnTheMessages()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 3, PackageType = "Old", Description = "Old", AmountPax = 100m, MinPax = 10 });

        // Act
        var result = await _update.ExecuteAsync(3, new CreatePackageRequestDto("Buffet", string.Empty, -5m, 10));

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
        var result = await _update.ExecuteAsync(999, new CreatePackageRequestDto("Buffet", string.Empty, 100m, 10));

        // Assert — a rejected payload is a 400, not a 404, even for an unknown tier.
        result.Succeeded.Should().BeFalse();
        result.Should().NotBeOfType<PackageNotFoundResult>();
        result.Errors.Should().Contain("Package description is required.");
    }
}
