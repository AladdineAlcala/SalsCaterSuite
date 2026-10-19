namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalsCaterSuite.Application.Common.Authorization;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Identity;

/// <summary>
/// Controlled, idempotent seeding of system-required data (DEC-018).
///
/// Scope is deliberately narrow — exactly three things:
///   1. Identity roles (DEC-017): Administrator, Manager, Cashier, Accountant.
///   2. One bootstrap administrator user, resolved from <c>Seed:Admin</c>.
///   3. Standard <c>CourseCategory</c> reference values used by the Menu catalog.
///
/// No fabricated business data (customers, bookings, payments, menus, packages, areas,
/// add-ons, discounts) is ever created. Every lookup is by natural key and no existing
/// row is mutated, so running the routine any number of times is a no-op after the first.
///
/// Lives in <c>Infrastructure.Persistence.Repositories</c> (DEC-037): every database call it
/// makes goes through a repository abstraction — <see cref="ICourseCategoryRepository"/> for
/// reference data and <see cref="IDatabaseMigrator"/> for schema migration. Identity's
/// <see cref="RoleManager{TRole}"/> and <see cref="UserManager{TUser}"/> are the user-store
/// access that DEC-037 also confines to this layer.
/// </summary>
public class DatabaseSeeder : IDatabaseSeeder
{
    /// <summary>Development-only fallback credential. Never used outside Development.</summary>
    private const string DevelopmentFallbackPassword = "Admin@12345";

    private const string DefaultAdminEmail = "admin@salscatersuite.local";
    private const string DefaultAdminFirstName = "System";
    private const string DefaultAdminLastName = "Administrator";

    /// <summary>Canonical role names — taken from the single approved source (DEC-017).</summary>
    private static readonly string[] RequiredRoles =
    {
        Roles.Administrator,
        Roles.Manager,
        Roles.Cashier,
        Roles.Accountant
    };

    /// <summary>Standard Menu course categories (DEC-018).</summary>
    private static readonly string[] StandardCourseCategories =
    {
        "Appetizer",
        "Soup",
        "Salad",
        "Main Course",
        "Dessert",
        "Beverage"
    };

    private readonly ICourseCategoryRepository _courseCategories;
    private readonly IDatabaseMigrator _migrator;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ICourseCategoryRepository courseCategories,
        IDatabaseMigrator migrator,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<DatabaseSeeder> logger)
    {
        _courseCategories = courseCategories;
        _migrator = migrator;
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying pending EF Core migrations to the database.");
        return _migrator.MigrateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedBootstrapAdministratorAsync();
        await SeedCourseCategoriesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in RequiredRoles)
        {
            // Natural key = role name. Never modified once present.
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new ApplicationRole(roleName));
            if (result.Succeeded)
            {
                _logger.LogInformation("Seeded identity role '{Role}'.", roleName);
            }
            else
            {
                _logger.LogError(
                    "Failed to seed identity role '{Role}': {Errors}",
                    roleName,
                    Describe(result));
            }
        }
    }

    private async Task SeedBootstrapAdministratorAsync()
    {
        var section = _configuration.GetSection("Seed:Admin");

        var email = FirstNonBlank(section["Email"], DefaultAdminEmail);
        var userName = FirstNonBlank(section["UserName"], email);
        var firstName = FirstNonBlank(section["FirstName"], DefaultAdminFirstName);
        var lastName = FirstNonBlank(section["LastName"], DefaultAdminLastName);

        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!_environment.IsDevelopment())
            {
                // Do not invent a credential outside Development.
                _logger.LogWarning(
                    "Seed:Admin:Password is not configured and the environment is '{Environment}'. " +
                    "Skipping bootstrap administrator creation.",
                    _environment.EnvironmentName);
                return;
            }

            password = DevelopmentFallbackPassword;
            _logger.LogWarning(
                "==============================================================================\n" +
                "DEVELOPMENT ONLY: Seed:Admin:Password is not configured. A well-known fallback\n" +
                "password is being used for the bootstrap administrator ('{Email}'). This credential\n" +
                "MUST NOT be used outside local development and MUST be changed before any shared,\n" +
                "staging or production deployment.\n" +
                "==============================================================================",
                email);
        }

        // Idempotency: match by email (primary natural key), falling back to user name so a
        // pre-existing row can never trigger a duplicate-key failure. Never reset a password.
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            existing = await _userManager.FindByNameAsync(userName);
        }

        if (existing is not null)
        {
            _logger.LogInformation(
                "Bootstrap administrator already present ('{UserName}'). Seeding skipped; the existing account is left untouched.",
                existing.UserName);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            Firstname = firstName,
            Lastname = lastName,
            IsActive = true,
            // DEC-033: the bootstrap administrator is a system-provisioned account, not a
            // self-registered one, so its email is confirmed by provisioning. Without this,
            // enabling confirmed-email sign-in would lock the owner out of a fresh install.
            EmailConfirmed = true,
            DateCreated = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            _logger.LogError(
                "Failed to create bootstrap administrator '{Email}': {Errors}",
                email,
                Describe(createResult));
            return;
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, Roles.Administrator);
        if (!addRoleResult.Succeeded)
        {
            _logger.LogError(
                "Created bootstrap administrator '{Email}' but failed to assign role '{Role}': {Errors}",
                email,
                Roles.Administrator,
                Describe(addRoleResult));
            return;
        }

        _logger.LogInformation(
            "Seeded bootstrap administrator '{UserName}' with role '{Role}'.",
            userName,
            Roles.Administrator);
    }

    private async Task SeedCourseCategoriesAsync(CancellationToken cancellationToken)
    {
        foreach (var courseName in StandardCourseCategories)
        {
            // Natural key = course name. Never updated once present.
            if (await _courseCategories.ExistsByCourseNameAsync(courseName, cancellationToken))
            {
                continue;
            }

            await _courseCategories.AddAsync(
                new CourseCategory { CourseName = courseName },
                cancellationToken);
            await _courseCategories.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded course category '{Course}'.", courseName);
        }
    }

    private static string FirstNonBlank(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
