using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Menus.Services;
using SalsCaterSuite.Infrastructure.Exports;
using SalsCaterSuite.Infrastructure.Identity;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.Infrastructure.Services;

namespace SalsCaterSuite.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // QuestPDF License
        QuestPDF.Settings.License = LicenseType.Community;

        // EF Core - SQL Server
        services.AddDbContext<SalsCaterSuiteDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("SalsCaterSuiteDb"),
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(SalsCaterSuiteDbContext).Assembly.FullName)
            ));

        // ASP.NET Core Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = false;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<SalsCaterSuiteDbContext>()
        .AddDefaultTokenProviders();

        // Override the default password hasher with legacy-compatible hasher
        services.AddScoped<IPasswordHasher<ApplicationUser>, LegacyPasswordHasher<ApplicationUser>>();

        // Repositories (DEC-037). Every repository shares the request-scoped
        // SalsCaterSuiteDbContext above, so any one SaveChangesAsync commits the whole
        // pending change set (atomic multi-step writes) — no separate unit-of-work is needed.
        services.AddScoped<IBookingRepository, Persistence.Repositories.BookingRepository>();
        services.AddScoped<IPaymentRepository, Persistence.Repositories.PaymentRepository>();
        services.AddScoped<IRefundRepository, Persistence.Repositories.RefundRepository>();
        services.AddScoped<ICustomerRepository, Persistence.Repositories.CustomerRepository>();
        services.AddScoped<IPackageRepository, Persistence.Repositories.PackageRepository>();
        services.AddScoped<IMenuRepository, Persistence.Repositories.MenuRepository>();
        services.AddScoped<ICourseCategoryRepository, Persistence.Repositories.CourseCategoryRepository>();

        // The Package Configuration Workbench repositories (REQ-PKG-02, DEC-077). They share the
        // same request-scoped context, so a ruleset replacement commits as one change set.
        services.AddScoped<IPackageCourseRuleRepository, Persistence.Repositories.PackageCourseRuleRepository>();
        services.AddScoped<IPackageAreaCoverageRepository, Persistence.Repositories.PackageAreaCoverageRepository>();
        services.AddScoped<IAreaRepository, Persistence.Repositories.AreaRepository>();

        // The discount catalogue (REQ-BKG-05, DEC-114). It is read by code, and the code
        // carries no unique index — the Application layer decides that an unknown and an
        // ambiguous code are both a 400.
        services.AddScoped<IDiscountRepository, Persistence.Repositories.DiscountRepository>();

        // The read-only add-on master (REQ-BKG-07, DEC-142). No write exists in this cycle
        // (RSK-066); it shares the request-scoped context like every other repository.
        services.AddScoped<IAddonRepository, Persistence.Repositories.AddonRepository>();

        // The read-only department lookup (the owner's 2026-10-24 add-on administration). It
        // shares the request-scoped context; the department master itself has no write surface
        // in this cycle (BR-13).
        services.AddScoped<IDepartmentRepository, Persistence.Repositories.DepartmentRepository>();

        // Identity credential lookup (DEC-037). The WebApi login endpoint resolves this
        // neutral Application contract; UserManager/SignInManager stay inside the
        // repositories layer.
        services.AddScoped<IAuthenticationRepository, Persistence.Repositories.AuthenticationRepository>();

        // Schema migration (DEC-020) — the only place that calls DbContext.Database.MigrateAsync.
        services.AddScoped<IDatabaseMigrator, Persistence.Repositories.DatabaseMigrator>();

        // Database initialization (migrations + system-required seeding) (DEC-018, DEC-020).
        // Invoked from startup only when the Database:ApplyMigrationsOnStartup /
        // Database:SeedOnStartup flags are enabled; both are false by default.
        // The seeder itself lives in the repositories layer (DEC-037).
        services.AddScoped<IDatabaseSeeder, Persistence.Repositories.DatabaseSeeder>();

        // Application Services
        services.AddScoped<IDateTime, DateTimeService>();
        services.AddScoped<IMenuCostingService, MenuCostingService>();

        // The deployment-configured food-cost benchmark (DEC-040). A single provider feeds
        // both the menu-costing service and the operations dashboard, so the two can never
        // report a different target percentage (AC-B8).
        services.AddSingleton<IFoodCostBenchmarkProvider, ConfigurationFoodCostBenchmarkProvider>();

        // The deployment-configured booking policy (DEC-101, DEC-102) — the required deposit
        // percentage and the commissary capacity. Both are absent by default; an absent key
        // means "unknown", never zero.
        services.AddSingleton<IBookingPolicyProvider, ConfigurationBookingPolicyProvider>();

        // Document generators (transient - created per use)
        services.AddTransient<CateringMonthlyReportExport>();
        services.AddTransient<CollectionSummaryExport>();

        // The Customer Master directory export (spec §5.5).
        services.AddTransient<CustomerDirectoryExport>();

        // The Menu & Food Costing Master export (spec §6.3).
        services.AddTransient<MenuFoodCostingExport>();

        // The Bookings & Events Register export (contract §14.6).
        services.AddTransient<BookingRegisterExport>();

        return services;
    }
}
