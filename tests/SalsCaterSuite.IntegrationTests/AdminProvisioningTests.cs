using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.IntegrationTests.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 11 — Admin provisioning (DEC-033). The system-provisioned bootstrap administrator is
/// created with <c>EmailConfirmed = true</c> and the Administrator role.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AdminProvisioningTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public AdminProvisioningTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task The_seeded_bootstrap_administrator_has_a_confirmed_email()
    {
        var admin = await _fixture.QueryAsync(context => context.Users
            .AsNoTracking()
            .SingleAsync(u => u.Email == IntegrationTestWebApplicationFactory.AdminEmail));

        admin.EmailConfirmed.Should().BeTrue(
            "a system-provisioned account must not lock the owner out of a fresh install (DEC-033)");
        admin.IsActive.Should().BeTrue();

        var roles = await _fixture.QueryAsync(async context =>
            await (from userRole in context.UserRoles
                   join role in context.Roles on userRole.RoleId equals role.Id
                   where userRole.UserId == admin.Id
                   select role.Name!).ToListAsync());

        roles.Should().ContainSingle().Which.Should().Be(Roles.Administrator);
    }
}
