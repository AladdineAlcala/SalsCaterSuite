using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Services;

namespace SalsCaterSuite.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton<BookingAmountDueCalculator>();
        services.AddScoped<CreateBookingCommand>();
        services.AddScoped<CancelBookingCommand>();
        services.AddScoped<GetBookingDetailsQuery>();
        services.AddScoped<GetBookingLedgerQuery>();

        return services;
    }
}

