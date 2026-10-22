namespace SalsCaterSuite.UnitTests.Infrastructure.DependencyInjection;

using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.Infrastructure.Services;
using Xunit;

/// <summary>
/// The DEC-037 wiring proof for the Bookings &amp; Events Register (REQ-BKG-05, AC-BKG-39).
/// <para>
/// Every new use case must resolve from the real Application + Infrastructure container, the
/// new repository contract must map onto its EF Core implementation, the new repositories must
/// share the one request-scoped context, and the Application project must reference neither
/// Entity Framework Core nor Identity.
/// </para>
/// </summary>
public class BookingsRegisterRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never opened: the container is only built, not queried.
                ["ConnectionStrings:SalsCaterSuiteDb"] =
                    @"Server=(localdb)\mssqllocaldb;Database=SalsCaterSuiteDb_DiValidation;Trusted_Connection=True"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

        services.AddScoped<ICurrentUserService, StubCurrentUserService>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void TheRegisterUseCases_ShouldResolveFromTheRealContainer()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var services = new[]
        {
            typeof(GetBookingRegisterQuery),
            typeof(GetBookingRegisterSummaryQuery),
            typeof(PreviewBookingQuoteCommand),
            typeof(BookingPricingService),
            typeof(BookingTierEstimateService),
            typeof(BookingRegisterPopulationService),
            typeof(BookingCreateCapabilities),
            typeof(IBookingPolicyProvider)
        };

        foreach (var serviceType in services)
        {
            var instance = scope.ServiceProvider.GetRequiredService(serviceType);
            instance.Should().NotBeNull($"{serviceType.Name} must be resolvable from the real container");
        }
    }

    [Fact]
    public void TheDiscountRepository_ShouldResolveToItsInfrastructureImplementation()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IDiscountRepository>()
            .Should().BeOfType<DiscountRepository>();
    }

    [Fact]
    public void TheBookingPolicyProvider_ShouldResolveToTheConfigurationImplementation()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IBookingPolicyProvider>()
            .Should().BeOfType<ConfigurationBookingPolicyProvider>();
    }

    [Fact]
    public void TheNewRepositories_ShouldShareTheOneScopedContext()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        var repositories = new object[]
        {
            scope.ServiceProvider.GetRequiredService<IBookingRepository>(),
            scope.ServiceProvider.GetRequiredService<IDiscountRepository>()
        };

        foreach (var repository in repositories)
        {
            var field = repository.GetType()
                .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);

            field.Should().NotBeNull($"{repository.GetType().Name} must hold a SalsCaterSuiteDbContext");
            field!.GetValue(repository).Should().BeSameAs(
                context,
                $"{repository.GetType().Name} must share the request-scoped DbContext (DEC-037 atomicity)");
        }
    }

    /// <summary>
    /// AC-BKG-39 / DEC-037: the Application project references neither Entity Framework Core nor
    /// Identity, and no repository contract exposes a <c>DbSet</c>, an <c>IQueryable</c> or a
    /// <c>DbContext</c>.
    /// </summary>
    [Fact]
    public void TheApplicationProject_ShouldReferenceNeitherEfCoreNorIdentity()
    {
        var referenced = typeof(BookingAmountDueCalculator).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToList();

        referenced.Should().NotContain(name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        referenced.Should().NotContain(name => name.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRepositoryContracts_ShouldNotExposeAnyEfCoreType()
    {
        var efCoreTypeNames = new[]
        {
            "Microsoft.EntityFrameworkCore.DbSet`1",
            "Microsoft.EntityFrameworkCore.IQueryable`1",
            "Microsoft.EntityFrameworkCore.DbContext"
        };

        var contracts = typeof(IBookingRepository).Assembly
            .GetTypes()
            .Where(type => type.IsInterface && type.Namespace == "SalsCaterSuite.Application.Common.Interfaces")
            .ToList();

        contracts.Should().NotBeEmpty();

        foreach (var contract in contracts)
        {
            foreach (var method in contract.GetMethods())
            {
                var parameterTypes = method.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Concat(new[] { method.ReturnType })
                    .ToList();

                foreach (var parameterType in parameterTypes)
                {
                    efCoreTypeNames.Should().NotContain(
                        parameterType.FullName,
                        $"{contract.Name}.{method.Name} must not expose an EF Core type (DEC-037)");
                }
            }
        }
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public string? UserId => "di-validation";

        public string? UserName => "di-validation";

        public bool IsAuthenticated => true;

        public bool IsInRole(string role) => true;
    }
}
