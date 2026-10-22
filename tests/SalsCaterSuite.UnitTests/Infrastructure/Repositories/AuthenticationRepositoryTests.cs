namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Infrastructure.Identity;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.UnitTests.Common;
using Xunit;

/// <summary>
/// DEC-037: credential lookup now lives behind <see cref="AuthenticationRepository"/>.
/// These tests run the real ASP.NET Core Identity <see cref="UserManager{TUser}"/> and
/// <see cref="SignInManager{TUser}"/> over the EF Core InMemory provider (never SQL Server)
/// and pin the behaviour the login contract depends on:
/// unknown user ⇒ unavailable, inactive account ⇒ unavailable <em>before</em> the password
/// is examined, wrong password ⇒ invalid, success ⇒ identity fields plus roles, and failed
/// attempts still count towards lockout (<c>lockoutOnFailure: true</c>).
/// </summary>
public class AuthenticationRepositoryTests : IDisposable
{
    private const string ValidPassword = "Admin@12345";

    private readonly SalsCaterSuiteDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly AuthenticationRepository _repository;

    public AuthenticationRepositoryTests()
    {
        _context = TestDbContextFactory.Create();

        var identityOptions = Options.Create(new IdentityOptions
        {
            Password = new PasswordOptions
            {
                RequireDigit = true,
                RequiredLength = 8,
                RequireUppercase = false,
                RequireNonAlphanumeric = false
            },
            User = new UserOptions { RequireUniqueEmail = false },
            Lockout = new LockoutOptions
            {
                AllowedForNewUsers = true,
                MaxFailedAccessAttempts = 5,
                DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
            }
        });

        var normalizer = new UpperInvariantLookupNormalizer();

        _userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser, ApplicationRole, SalsCaterSuiteDbContext>(_context),
            identityOptions,
            new LegacyPasswordHasher<ApplicationUser>(),
            new[] { new UserValidator<ApplicationUser>() },
            new IPasswordValidator<ApplicationUser>[] { new PasswordValidator<ApplicationUser>() },
            normalizer,
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        _roleManager = new RoleManager<ApplicationRole>(
            new RoleStore<ApplicationRole, SalsCaterSuiteDbContext>(_context),
            new[] { new RoleValidator<ApplicationRole>() },
            normalizer,
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<ApplicationRole>>.Instance);

        var signInManager = new SignInManager<ApplicationUser>(
            _userManager,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            identityOptions,
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<ApplicationUser>>().Object);

        _repository = new AuthenticationRepository(_userManager, signInManager);
    }

    private async Task<ApplicationUser> CreateUserAsync(
        string userName = "alice",
        bool isActive = true,
        string? role = null)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@salscatersuite.test",
            Firstname = "Alice",
            Lastname = "Reyes",
            IsActive = isActive,
            EmailConfirmed = true,
            // Identity enables lockout for new users by default; state it explicitly so the
            // lockout assertions below do not depend on that default.
            LockoutEnabled = true,
            DateCreated = DateTime.UtcNow
        };

        (await _userManager.CreateAsync(user, ValidPassword)).Succeeded
            .Should().BeTrue("the test user must be provisioned");

        if (role is not null)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                (await _roleManager.CreateAsync(new ApplicationRole(role))).Succeeded.Should().BeTrue();
            }

            (await _userManager.AddToRoleAsync(user, role)).Succeeded
                .Should().BeTrue("the test role must be assigned");
        }

        return user;
    }

    [Fact]
    public async Task AuthenticateAsync_WhenNoSuchUser_ReturnsUserUnavailable()
    {
        // Act
        var attempt = await _repository.AuthenticateAsync("ghost", ValidPassword);

        // Assert
        attempt.Status.Should().Be(AuthenticationStatus.UserUnavailable);
        attempt.User.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAccountIsInactive_ReturnsUserUnavailableEvenWithTheCorrectPassword()
    {
        // Arrange — the inactive check is deliberately evaluated before the password, exactly
        // as the login endpoint always did: a correct password must never authenticate.
        await CreateUserAsync(userName: "retired", isActive: false);

        // Act
        var attempt = await _repository.AuthenticateAsync("retired", ValidPassword);

        // Assert
        attempt.Status.Should().Be(AuthenticationStatus.UserUnavailable);
        attempt.User.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenPasswordIsWrong_ReturnsInvalidPassword()
    {
        // Arrange
        await CreateUserAsync();

        // Act
        var attempt = await _repository.AuthenticateAsync("alice", "not-the-password");

        // Assert
        attempt.Status.Should().Be(AuthenticationStatus.InvalidPassword);
        attempt.User.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenCredentialsVerify_ReturnsIdentityFieldsAndRoles()
    {
        // Arrange
        var user = await CreateUserAsync(role: Roles.Administrator);

        // Act
        var attempt = await _repository.AuthenticateAsync("alice", ValidPassword);

        // Assert
        attempt.Status.Should().Be(AuthenticationStatus.Succeeded);
        attempt.User.Should().NotBeNull();
        attempt.User!.UserId.Should().Be(user.Id);
        attempt.User.UserName.Should().Be("alice");
        attempt.User.FullName.Should().Be("Reyes, Alice");
        attempt.User.IsActive.Should().BeTrue();
        attempt.User.Roles.Should().Equal(Roles.Administrator);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenUserHasNoRole_ReturnsNoRoles()
    {
        // Act
        await CreateUserAsync();
        var attempt = await _repository.AuthenticateAsync("alice", ValidPassword);

        // Assert
        attempt.Status.Should().Be(AuthenticationStatus.Succeeded);
        attempt.User!.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task AuthenticateAsync_WithLockoutOnFailure_CountsFailedAttemptsAndLocksTheAccount()
    {
        // Arrange — five consecutive failures reach LockoutOptions.MaxFailedAccessAttempts.
        await CreateUserAsync();

        for (var i = 0; i < 5; i++)
        {
            var failed = await _repository.AuthenticateAsync("alice", "not-the-password");
            failed.Status.Should().Be(AuthenticationStatus.InvalidPassword);
        }

        var lockedUser = await _userManager.FindByNameAsync("alice");
        lockedUser.Should().NotBeNull();

        var lockoutEnd = await _userManager.GetLockoutEndDateAsync(lockedUser!);
        lockoutEnd.Should().NotBeNull();
        lockoutEnd!.Value.Should().BeAfter(
            DateTimeOffset.UtcNow,
            "lockoutOnFailure must still lock the account");

        // Act — the correct password is now rejected while the account is locked out.
        var lockedOut = await _repository.AuthenticateAsync("alice", ValidPassword);

        // Assert — a locked-out account reports a plain credential failure, never success.
        lockedOut.Status.Should().Be(AuthenticationStatus.InvalidPassword);
        lockedOut.User.Should().BeNull();
    }

    public void Dispose()
    {
        _userManager.Dispose();
        TestDbContextFactory.Destroy(_context);
    }
}
