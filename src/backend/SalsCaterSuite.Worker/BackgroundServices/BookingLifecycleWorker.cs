using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Persistence;

namespace SalsCaterSuite.Worker.BackgroundServices;

public class BookingLifecycleWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingLifecycleWorker> _logger;

    public BookingLifecycleWorker(IServiceScopeFactory scopeFactory, ILogger<BookingLifecycleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledTransitionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Booking lifecycle worker failed while processing scheduled transitions.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessScheduledTransitionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();
        var now = DateTime.UtcNow;

        var bookings = await context.Bookings
            .Where(b => !b.IsDeleted)
            .ToListAsync(cancellationToken);

        var changed = false;

        foreach (var booking in bookings)
        {
            if (ShouldAdvanceToInProgress(booking, now))
            {
                booking.MarkInProgress();
                changed = true;
                _logger.LogInformation(
                    "Booking {BookingNumber} transitioned from Confirmed to InProgress on event date {EventDate}.",
                    booking.BookingNumber,
                    booking.EventDate);
            }

            if (ShouldAutoComplete(booking, now))
            {
                booking.MarkCompleted();
                changed = true;
                _logger.LogInformation(
                    "Booking {BookingNumber} transitioned from InProgress to Completed after pack-out at {PackoutTime}.",
                    booking.BookingNumber,
                    booking.PackoutTime);
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public static bool ShouldAdvanceToInProgress(Booking booking, DateTime now)
    {
        if (booking.Status != BookingStatus.Confirmed)
        {
            return false;
        }

        var eventDate = booking.EventDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return now.Date == eventDate.Date;
    }

    public static bool ShouldAutoComplete(Booking booking, DateTime now)
    {
        if (booking.Status != BookingStatus.InProgress)
        {
            return false;
        }

        if (booking.PackoutTime is null)
        {
            return false;
        }

        var eventDateTime = booking.EventDate.ToDateTime(TimeOnly.FromTimeSpan(booking.PackoutTime.Value), DateTimeKind.Utc);
        var completionThreshold = eventDateTime.AddHours(1);

        return now >= completionThreshold;
    }
}










































































































































