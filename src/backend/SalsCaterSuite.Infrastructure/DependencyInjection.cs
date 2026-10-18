using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Menus.Services;
using SalsCaterSuite.Application.Features.Payments.Services;
using SalsCaterSuite.Infrastructure.Exports;
using SalsCaterSuite.Infrastructure.Identity;
using SalsCaterSuite.Infrastructure.Persistence;
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

        services.AddScoped<ISalsCaterSuiteDbContext>(provider =>
            provider.GetRequiredService<SalsCaterSuiteDbContext>());

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

        // Repositories
        services.AddScoped<IBookingRepository, Persistence.Repositories.BookingRepository>();

        // Application Services
        services.AddScoped<IDateTime, DateTimeService>();
        services.AddScoped<IPaymentLedgerService, PaymentLedgerService>();
        services.AddScoped<IMenuCostingService, MenuCostingService>();

        // Document generators (transient - created per use)
        services.AddTransient<CateringMonthlyReportExport>();
        services.AddTransient<CollectionSummaryExport>();

        return services;
    }
}
