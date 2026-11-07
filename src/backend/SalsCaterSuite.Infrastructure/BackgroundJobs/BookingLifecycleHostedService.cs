using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalsCaterSuite.Application.Features.Bookings.Services;

namespace SalsCaterSuite.Infrastructure.BackgroundJobs;

/// <summary>
/// Scheduled in-process BG-04 host (DEC-180). Registered by the Worker host only.
/// No SignalR, no scheduler package, and no outbox.
/// </summary>
public sealed class BookingLifecycleHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BookingLifecycleOptions> _options;
    private readonly ILogger<BookingLifecycleHostedService> _logger;

    public BookingLifecycleHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BookingLifecycleOptions> options,
        ILogger<BookingLifecycleHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("BG-04 booking lifecycle polling is disabled.");
            return;
        }

        _logger.LogInformation(
            "BG-04 booking lifecycle polling started. Interval {PollIntervalMinutes} minute(s), batch size {BatchSize}.",
            Math.Max(1, options.PollIntervalMinutes),
            Math.Max(1, options.BatchSize));

        await ProcessUntilQuietAsync(stoppingToken);

        using var timer = new PeriodicTimer(options.PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessUntilQuietAsync(stoppingToken);
        }
    }

    private async Task ProcessUntilQuietAsync(CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(1, _options.Value.BatchSize);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IBookingLifecycleTransitionService>();
                var result = await service.ProcessDueBatchAsync(batchSize, cancellationToken);
                if (result.Committed > 0)
                {
                    _logger.LogInformation(
                        "BG-04 committed {Committed} booking lifecycle transition(s) from a due batch of {Selected}.",
                        result.Committed,
                        result.Selected);
                }

                if (!result.BatchWasFull)
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BG-04 failed while processing a due batch. The next poll will retry.");
                return;
            }
        }
    }
}
