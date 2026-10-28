using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Addons.Commands;
using SalsCaterSuite.Application.Features.Addons.Queries;
using SalsCaterSuite.Application.Features.Bookings;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Features.Calendar.Queries;
using SalsCaterSuite.Application.Features.Customers.Commands;
using SalsCaterSuite.Application.Features.Customers.Queries;
using SalsCaterSuite.Application.Features.Customers.Services;
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

        // The Bookings & Events Register and the Reservation Workbench (REQ-BKG-05). The
        // validators are picked up by AddValidatorsFromAssembly above; the use cases are
        // registered by hand.
        services.AddScoped<BookingPricingService>();
        services.AddScoped<BookingTierEstimateService>();
        services.AddScoped<BookingPlanningEstimateService>();
        services.AddScoped<BookingRegisterPopulationService>();
        services.AddScoped<GetBookingRegisterQuery>();
        services.AddScoped<GetBookingRegisterSummaryQuery>();
        services.AddScoped<PreviewBookingQuoteCommand>();

        // The collaborators the extended create path needs. They are gathered in one object so
        // the create command keeps a single, stable constructor (DEC-114).
        services.AddScoped<BookingCreateCapabilities>(provider => new BookingCreateCapabilities
        {
            Areas = provider.GetRequiredService<IAreaRepository>(),
            Discounts = provider.GetRequiredService<IDiscountRepository>(),
            Payments = provider.GetRequiredService<IPaymentRepository>(),
            Policy = provider.GetRequiredService<IBookingPolicyProvider>(),
            Pricing = provider.GetRequiredService<BookingPricingService>(),
            LineItems = provider.GetRequiredService<BookingLineItemResolver>()
        });

        // The Booking Dish Selection & Add-Ons surface (REQ-BKG-06 / 07, contract §15 / §16).
        // The one course-set builder and the one line-item resolver are shared by the read, the
        // quote, the create and the two full-replace writes, so the shapes can never diverge
        // (BR-31, DEC-153).
        services.AddScoped<BookingMenuCourseSetBuilder>();
        services.AddScoped<BookingLineItemResolver>();
        services.AddScoped<BookingMenuSelectionPresenter>();
        services.AddScoped<BookingAddonSetPresenter>();
        services.AddScoped<GetBookingMenuSelectionQuery>();
        services.AddScoped<ReplaceBookingMenuSelectionCommand>();
        services.AddScoped<GetBookingAddonSetQuery>();
        services.AddScoped<ReplaceBookingAddonsCommand>();
        services.AddScoped<GetAddonMasterQuery>();
        services.AddScoped<GetAddonCategoriesQuery>();

        // The add-on master administration (the owner's 2026-10-24 Settings → Add-ons request).
        // The master is no longer read-only: the create, update and soft-delete commands live
        // beside the read queries and the booking picker keeps using the active-only read.
        services.AddScoped<GetAddonManagementListQuery>();
        services.AddScoped<GetAddonManagementDetailQuery>();
        services.AddScoped<GetDepartmentsQuery>();
        services.AddScoped<CreateAddonCommand>();
        services.AddScoped<UpdateAddonCommand>();
        services.AddScoped<DeleteAddonCommand>();

        // Calendar
        services.AddScoped<GetCalendarEventsQuery>();

        // Customers
        services.AddScoped<GetCustomersQuery>();
        services.AddScoped<GetCustomerByIdQuery>();
        services.AddScoped<GetCustomerStatementQuery>();
        services.AddScoped<GetCustomerDirectoryQuery>();
        services.AddScoped<CreateCustomerCommand>();
        services.AddScoped<UpdateCustomerCommand>();

        // The one aging vocabulary shared by the customer directory and the customer
        // statement (spec §3, DEC-052). It resolves "today" from IDateTime, so the Customer
        // Master and the operations dashboard age the same bookings on the same day.
        services.AddScoped<ICustomerAgingService, CustomerAgingService>();

        // Menus
        services.AddScoped<GetMenusQuery>();
        services.AddScoped<GetCoursesQuery>();
        services.AddScoped<GetMenuByIdQuery>();
        services.AddScoped<GetMenuMasterQuery>();
        services.AddScoped<PreviewMenuCostingQuery>();
        services.AddScoped<CreateMenuCommand>();
        services.AddScoped<UpdateMenuCommand>();
        services.AddScoped<DeleteMenuCommand>();

        // Packages
        services.AddScoped<GetPackagesQuery>();
        services.AddScoped<GetPackageByIdQuery>();
        services.AddScoped<GetPackageCatalogueSummaryQuery>();
        services.AddScoped<CreatePackageCommand>();
        services.AddScoped<UpdatePackageCommand>();
        services.AddScoped<TogglePackageActiveCommand>();

        // The read-only applicable-pax classification lookup (REQ-PKG-04, DEC-173).
        services.AddScoped<GetPackagePaxApplicablesQuery>();

        // The Package Configuration Workbench (REQ-PKG-02). The validators are picked up by
        // AddValidatorsFromAssembly above; the use cases are registered by hand.
        services.AddScoped<GetPackageCourseRulesetQuery>();
        services.AddScoped<GetPackageAreaCoverageMatrixQuery>();
        services.AddScoped<GetPackageSpecSheetQuery>();
        services.AddScoped<PreviewPackageCourseRuleQuery>();
        services.AddScoped<PreviewPackageLocationQuery>();
        services.AddScoped<ReplacePackageCourseRulesCommand>();
        services.AddScoped<ReplacePackageAreaCoverageCommand>();
        services.AddScoped<AttachPackageAreaCoverageCommand>();

        // The municipality catalogue (REQ-PKG-02).
        services.AddScoped<GetAreasQuery>();
        services.AddScoped<CreateAreaCommand>();
        services.AddScoped<UpdateAreaCommand>();

        // Dashboard (REQ-DSH-01) — the read-only operations aggregate.
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<GetDashboardQuery>();

        return services;
    }
}
