using Microsoft.Extensions.Hosting;
namespace SalsCaterSuite.Worker.BackgroundServices;

public class BookingLifecycleWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public BookingLifecycleWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}
