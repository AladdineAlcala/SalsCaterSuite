using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalsCaterSuite.Application;
using SalsCaterSuite.Infrastructure;
using SalsCaterSuite.Infrastructure.BackgroundJobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.json",
    optional: true,
    reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// BG-04 runs in the Worker host only (DEC-180). Disabled configurations do not poll.
builder.Services.Configure<BookingLifecycleOptions>(
    builder.Configuration.GetSection(BookingLifecycleOptions.SectionName));
builder.Services.AddHostedService<BookingLifecycleHostedService>();

var host = builder.Build();
await host.RunAsync();































































































































