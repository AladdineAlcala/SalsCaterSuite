namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>DEC-037: the package read use cases are repository-backed (hand-written fake).</summary>
public class PackageQueryTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakePackageRepository _packages;
    private readonly GetPackagesQuery _list;
    private readonly GetPackageByIdQuery _byId;

    public PackageQueryTests()
    {
        _packages = new FakePackageRepository(_store);
        _list = new GetPackagesQuery(_packages);
        _byId = new GetPackageByIdQuery(_packages);
    }

    [Fact]
    public async Task GetPackagesQuery_ShouldReturnAllPackages()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 });
        _store.Packages.Add(new Package { PackageId = 2, PackageType = "Plated", Description = "B", IsActive = false, NumPaxId = 4 });

        // Act
        var result = await _list.ExecuteAsync(null);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPackagesQuery_WhenActiveOnly_ShouldExcludeInactivePackages()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 });
        _store.Packages.Add(new Package { PackageId = 2, PackageType = "Plated", Description = "B", IsActive = false, NumPaxId = 4 });

        // Act
        var result = await _list.ExecuteAsync(activeOnly: true);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.PackageId.Should().Be(1);
    }

    /// <summary>
    /// The catalogue's scoped population (DEC-074): a soft-deleted tier is not part of the
    /// catalogue, so the list the use case returns excludes it.
    /// </summary>
    [Fact]
    public async Task GetPackagesQuery_ShouldExcludeASoftDeletedTier()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 1, PackageType = "Buffet", Description = "A", IsActive = true, NumPaxId = 4 });
        _store.Packages.Add(new Package { PackageId = 2, PackageType = "Ghost", Description = "B", IsActive = true, IsDeleted = true, NumPaxId = 4 });

        // Act
        var result = await _list.ExecuteAsync(null);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.PackageId.Should().Be(1);
    }

    [Fact]
    public async Task GetPackageByIdQuery_WhenPackageExists_ShouldReturnProjection()
    {
        // Arrange
        _store.Packages.Add(new Package { PackageId = 7, PackageType = "Silver Buffet", Description = "Silver", AmountPax = 500m, MinPax = 50, IsActive = true, NumPaxId = 4 });

        // Act
        var result = await _byId.ExecuteAsync(7);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.PackageId.Should().Be(7);
        result.Data.AmountPax.Should().Be(500m);
    }

    [Fact]
    public async Task GetPackageByIdQuery_WhenPackageDoesNotExist_ShouldReturnFailure()
    {
        // Act
        var result = await _byId.ExecuteAsync(999);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Package not found.");
    }
}
