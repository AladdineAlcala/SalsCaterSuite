using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalsCaterSuite.Infrastructure;
using SalsCaterSuite.Worker.BackgroundServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.json",
    optional: true,
    reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<BookingLifecycleWorker>();

var host = builder.Build();
await host.RunAsync();































































































































