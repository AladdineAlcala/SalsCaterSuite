using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Infrastructure.BackgroundJobs;

namespace SalsCaterSuite.Worker.BackgroundServices;

/// <summary>
/// BG-04 scheduled lifecycle transitions hosted in the Worker (DEC-180).
/// Delegates to the shared transition service and uses row-version concurrency.
/// </summary>
public class BookingLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BookingLifecycleOptions> _options;
    private readonly ILogger<BookingLifecycleWorker> _logger;

    public BookingLifecycleWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BookingLifecycleOptions> options,
        ILogger<BookingLifecycleWorker> logger)
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










































































































































