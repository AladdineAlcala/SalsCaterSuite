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

        // Document generators (transient - created per use)
        services.AddTransient<CateringMonthlyReportExport>();
        services.AddTransient<CollectionSummaryExport>();

        return services;
    }
}
