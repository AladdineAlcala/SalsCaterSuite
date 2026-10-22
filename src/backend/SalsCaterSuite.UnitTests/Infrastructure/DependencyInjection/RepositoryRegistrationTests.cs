namespace SalsCaterSuite.UnitTests.Infrastructure.DependencyInjection;

using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Calendar.Queries;
using SalsCaterSuite.Application.Features.Customers.Queries;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.Application.Features.Dashboard.Queries;
using SalsCaterSuite.Application.Features.Dashboard.Services;
using SalsCaterSuite.Application.Features.Payments.Services;
using SalsCaterSuite.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using Xunit;

/// <summary>
/// DEC-037 wiring proof: every use case migrated to repositories must resolve from the
/// real Application + Infrastructure service container. The integration suite exercises
/// five of the six through live HTTP requests; the calendar query has no integration
/// coverage, so it is asserted here.
/// </summary>
public class RepositoryRegistrationTests
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

        // Registered by the WebApi host in production.
        services.AddScoped<ICurrentUserService, StubCurrentUserService>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void MigratedUseCases_ShouldResolveFromTheRealContainer()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var migrated = new[]
        {
            typeof(CreateBookingCommand),
            typeof(CancelBookingCommand),
            typeof(GetBookingDetailsQuery),
            typeof(GetBookingLedgerQuery),
            typeof(GetCalendarEventsQuery),
            typeof(IPaymentLedgerService),
            typeof(IDashboardService),
            typeof(GetDashboardQuery),
            typeof(GetCustomerDirectoryQuery),
            typeof(GetCustomerStatementQuery),
            typeof(ICustomerAgingService),
            // The Package Configuration Workbench (REQ-PKG-02).
            typeof(SalsCaterSuite.Application.Features.Packages.Queries.GetPackageCourseRulesetQuery),
            typeof(SalsCaterSuite.Application.Features.Packages.Queries.GetPackageAreaCoverageMatrixQuery),
            typeof(SalsCaterSuite.Application.Features.Packages.Queries.GetPackageSpecSheetQuery),
            typeof(SalsCaterSuite.Application.Features.Packages.Queries.PreviewPackageCourseRuleQuery),
            typeof(SalsCaterSuite.Application.Features.Packages.Queries.PreviewPackageLocationQuery),
            typeof(SalsCaterSuite.Application.Features.Packages.Queries.GetAreasQuery),
            typeof(SalsCaterSuite.Application.Features.Packages.Commands.ReplacePackageCourseRulesCommand),
            typeof(SalsCaterSuite.Application.Features.Packages.Commands.ReplacePackageAreaCoverageCommand),
            typeof(SalsCaterSuite.Application.Features.Packages.Commands.AttachPackageAreaCoverageCommand),
            typeof(SalsCaterSuite.Application.Features.Packages.Commands.CreateAreaCommand),
            typeof(SalsCaterSuite.Application.Features.Packages.Commands.UpdateAreaCommand),
        };

        // Act + Assert
        foreach (var serviceType in migrated)
        {
            var instance = scope.ServiceProvider.GetRequiredService(serviceType);
            instance.Should().NotBeNull($"{serviceType.Name} must be resolvable from the real container");
        }
    }

    [Fact]
    public void RepositoryAbstractions_ShouldResolveToInfrastructureImplementations()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act + Assert — the contract (Application) maps onto the EF Core implementation (Infrastructure).
        scope.ServiceProvider.GetRequiredService<IBookingRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.BookingRepository>();
        scope.ServiceProvider.GetRequiredService<IPaymentRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.PaymentRepository>();
        scope.ServiceProvider.GetRequiredService<IRefundRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.RefundRepository>();
        scope.ServiceProvider.GetRequiredService<ICustomerRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.CustomerRepository>();
        scope.ServiceProvider.GetRequiredService<IPackageRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.PackageRepository>();
        scope.ServiceProvider.GetRequiredService<IMenuRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.MenuRepository>();
        scope.ServiceProvider.GetRequiredService<ICourseCategoryRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.CourseCategoryRepository>();
        scope.ServiceProvider.GetRequiredService<IPackageCourseRuleRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.PackageCourseRuleRepository>();
        scope.ServiceProvider.GetRequiredService<IPackageAreaCoverageRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.PackageAreaCoverageRepository>();
        scope.ServiceProvider.GetRequiredService<IAreaRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.AreaRepository>();
        scope.ServiceProvider.GetRequiredService<IAuthenticationRepository>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.AuthenticationRepository>();
        scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>()
            .Should().BeOfType<SalsCaterSuite.Infrastructure.Persistence.Repositories.DatabaseMigrator>();
    }

    /// <summary>
    /// The login endpoint's only dependencies are the neutral Application contract and
    /// configuration — never a UserManager/SignInManager (DEC-037).
    /// </summary>
    [Fact]
    public void AuthController_ShouldResolveWithoutAnyIdentityTypeOnItsConstructor()
    {
        // Arrange
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        // Act
        var controller = ActivatorUtilities.CreateInstance<SalsCaterSuite.WebApi.Controllers.AuthController>(
            scope.ServiceProvider);

        // Assert
        controller.Should().NotBeNull();

        var parameterTypes = typeof(SalsCaterSuite.WebApi.Controllers.AuthController)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToArray();

        parameterTypes.Should().BeEquivalentTo(
            new[] { typeof(IAuthenticationRepository), typeof(IConfiguration) },
            "the WebApi layer must not depend on ASP.NET Core Identity or EF Core (DEC-037)");
    }

    [Fact]
    public void RepositoryAbstractions_ShouldShareOneScopedContext()
    {
        // Arrange — the per-repository SaveChangesAsync contract is only atomic because all
        // repositories share the request-scoped SalsCaterSuiteDbContext instance.
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        // Act
        var repositories = new object[]
        {
            scope.ServiceProvider.GetRequiredService<IBookingRepository>(),
            scope.ServiceProvider.GetRequiredService<IPaymentRepository>(),
            scope.ServiceProvider.GetRequiredService<IRefundRepository>(),
            scope.ServiceProvider.GetRequiredService<ICustomerRepository>(),
            scope.ServiceProvider.GetRequiredService<IPackageRepository>(),
            scope.ServiceProvider.GetRequiredService<IMenuRepository>(),
            scope.ServiceProvider.GetRequiredService<ICourseCategoryRepository>(),
            scope.ServiceProvider.GetRequiredService<IPackageCourseRuleRepository>(),
            scope.ServiceProvider.GetRequiredService<IPackageAreaCoverageRepository>(),
            scope.ServiceProvider.GetRequiredService<IAreaRepository>(),
            scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>(),
        };

        // Assert — each repository was constructed with the very same DbContext instance,
        // so any one of them commits the whole pending change set.
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

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public string? UserId => "di-validation";

        public string? UserName => "di-validation";

        public bool IsAuthenticated => true;

        public bool IsInRole(string role) => true;
    }
}
