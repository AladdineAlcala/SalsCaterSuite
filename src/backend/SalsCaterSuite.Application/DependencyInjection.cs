using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Calendar.Queries;
using SalsCaterSuite.Application.Features.Customers.Commands;
using SalsCaterSuite.Application.Features.Customers.Queries;
using SalsCaterSuite.Application.Features.Dashboard.Queries;
using SalsCaterSuite.Application.Features.Dashboard.Services;
using SalsCaterSuite.Application.Features.Menus.Commands;
using SalsCaterSuite.Application.Features.Menus.Queries;
using SalsCaterSuite.Application.Features.Packages.Commands;
using SalsCaterSuite.Application.Features.Packages.Queries;
using SalsCaterSuite.Application.Features.Payments.Services;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton<BookingAmountDueCalculator>();

        // Payments / ledger — the single authoritative implementation (DEC-015).
        services.AddScoped<IPaymentLedgerService, PaymentLedgerService>();

        // Bookings
        services.AddScoped<CreateBookingCommand>();
        services.AddScoped<CancelBookingCommand>();
        services.AddScoped<GetBookingDetailsQuery>();
        services.AddScoped<GetBookingLedgerQuery>();

        // Calendar
        services.AddScoped<GetCalendarEventsQuery>();

        // Customers
        services.AddScoped<GetCustomersQuery>();
        services.AddScoped<GetCustomerByIdQuery>();
        services.AddScoped<GetCustomerStatementQuery>();
        services.AddScoped<CreateCustomerCommand>();
        services.AddScoped<UpdateCustomerCommand>();

        // Menus
        services.AddScoped<GetMenusQuery>();
        services.AddScoped<GetCoursesQuery>();
        services.AddScoped<GetMenuByIdQuery>();
        services.AddScoped<CreateMenuCommand>();
        services.AddScoped<UpdateMenuCommand>();
        services.AddScoped<DeleteMenuCommand>();

        // Packages
        services.AddScoped<GetPackagesQuery>();
        services.AddScoped<GetPackageByIdQuery>();
        services.AddScoped<CreatePackageCommand>();
        services.AddScoped<UpdatePackageCommand>();
        services.AddScoped<TogglePackageActiveCommand>();

        // Dashboard (REQ-DSH-01) — the read-only operations aggregate.
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<GetDashboardQuery>();

        return services;
    }
}
