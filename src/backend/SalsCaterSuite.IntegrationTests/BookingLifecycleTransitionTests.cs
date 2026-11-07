using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Application;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// A payment must not change lifecycle status (DEC-177). Uses the shared disposable host.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BookingPaymentStateEndpointTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public BookingPaymentStateEndpointTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Recording_a_payment_sets_payment_state_and_leaves_lifecycle_status_unchanged()
    {
        var client = await _fixture.CreateManagerClientAsync();
        var customerId = await client.CreateCustomerAsync("Lifecycle", "PaymentSplit");
        var transactionId = await client.CreateBookingAsync(customerId, 10, 100m);

        var created = await client.GetBookingDetailsAsync(transactionId);
        created.GetProperty("status").GetString().Should().Be("Pending");
        created.GetProperty("paymentState").GetString().Should().Be("PendingPayment");
        created.GetProperty("eventEndDate").GetString().Should().NotBeNullOrWhiteSpace();

        var partial = await client.RecordPaymentAsync(transactionId, 200m);
        partial.StatusCode.Should().Be(HttpStatusCode.OK, await partial.DescribeAsync());

        var afterPartial = await client.GetBookingDetailsAsync(transactionId);
        afterPartial.GetProperty("status").GetString().Should().Be("Pending");
        afterPartial.GetProperty("paymentState").GetString().Should().Be("DepositPaid");

        var remainder = TestApi.NetAmountDueFor(10, 100m) - 200m;
        var settled = await client.RecordPaymentAsync(transactionId, remainder);
        settled.StatusCode.Should().Be(HttpStatusCode.OK, await settled.DescribeAsync());

        var afterSettled = await client.GetBookingDetailsAsync(transactionId);
        afterSettled.GetProperty("status").GetString().Should().Be("Pending");
        afterSettled.GetProperty("paymentState").GetString().Should().Be("FullyPaid");
    }
}

/// <summary>
/// BG-04 due query, ordered catch-up and idempotence on a private LocalDB database.
/// The hosted poller is not used; the transition service is called directly.
/// </summary>
public class BookingLifecycleTransitionTests
{
    [Fact]
    public async Task The_due_query_commits_ordered_catch_up_and_leaves_excluded_rows_and_payments_unchanged()
    {
        var databaseName = $"SalsCaterSuiteDb_LifecycleDue_{Guid.NewGuid():N}";
        var connectionString = IntegrationTestWebApplicationFactory.BuildConnectionString(databaseName);
        var utcNow = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SalsCaterSuiteDb"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);
        services.AddScoped<IDateTime>(_ => new FixedClock(utcNow));

        await using var provider = services.BuildServiceProvider();
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();
                await context.Database.MigrateAsync();
                await SeedAsync(context);
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IBookingLifecycleTransitionService>();
                var first = await service.ProcessDueBatchAsync(100);
                first.Committed.Should().Be(2, "the catch-up booking and the in-progress booking are due");
                first.Selected.Should().Be(2, "pending, cancelled and future confirmed rows are not selected");

                var second = await service.ProcessDueBatchAsync(100);
                second.Committed.Should().Be(0);
                second.Selected.Should().Be(0);
            }

            await using var verify = provider.CreateAsyncScope();
            var saved = verify.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();
            var bookings = await saved.Bookings
                .Include(booking => booking.StatusHistory)
                .OrderBy(booking => booking.TransactionId)
                .ToListAsync();

            var catchUp = bookings.Single(booking => booking.Occasion == "BK-CATCHUP");
            catchUp.Status.Should().Be(BookingStatus.Completed);
            catchUp.PaymentState.Should().Be(PaymentState.DepositPaid);
            catchUp.AmountPaid.Should().Be(100m);
            catchUp.StatusHistory.Select(row => row.StatusName).Should().Equal("InProgress", "Completed");
            catchUp.StatusHistory.Should().OnlyContain(row => row.ActorSource == BookingStatusHistory.Bg04ActorSource);

            var inProgress = bookings.Single(booking => booking.Occasion == "BK-INPROGRESS");
            inProgress.Status.Should().Be(BookingStatus.Completed);
            inProgress.StatusHistory.Should().ContainSingle()
                .Which.StatusName.Should().Be("Completed");

            bookings.Single(booking => booking.Occasion == "BK-FUTURE").Status.Should().Be(BookingStatus.Confirmed);
            bookings.Single(booking => booking.Occasion == "BK-CANCELLED").Status.Should().Be(BookingStatus.Cancelled);
            bookings.Single(booking => booking.Occasion == "BK-PENDING").Status.Should().Be(BookingStatus.Pending);
            bookings.Where(booking => booking.Occasion != "BK-CATCHUP" && booking.Occasion != "BK-INPROGRESS")
                .Should().OnlyContain(booking => booking.StatusHistory.Count == 0);
        }
        finally
        {
            await DropAsync(databaseName);
        }
    }

    private static async Task SeedAsync(SalsCaterSuiteDbContext context)
    {
        var customer = new Customer { FirstName = "Due", LastName = "Probe" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        context.Bookings.Add(BookingFor(customer.CustomerId, "BK-CATCHUP", new DateOnly(2026, 10, 27), new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0), paid: true, BookingStatus.Confirmed));
        context.Bookings.Add(BookingFor(customer.CustomerId, "BK-INPROGRESS", new DateOnly(2026, 10, 27), new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0), null, paid: false, BookingStatus.InProgress));
        context.Bookings.Add(BookingFor(customer.CustomerId, "BK-FUTURE", new DateOnly(2026, 10, 29), new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0), paid: false, BookingStatus.Confirmed));
        context.Bookings.Add(BookingFor(customer.CustomerId, "BK-CANCELLED", new DateOnly(2026, 10, 27), new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0), paid: false, BookingStatus.Cancelled));
        context.Bookings.Add(BookingFor(customer.CustomerId, "BK-PENDING", new DateOnly(2026, 10, 27), new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0), paid: false, BookingStatus.Pending));
        await context.SaveChangesAsync();
    }

    private static Booking BookingFor(
        int customerId,
        string occasion,
        DateOnly eventDate,
        TimeSpan start,
        TimeSpan end,
        TimeSpan? packout,
        bool paid,
        BookingStatus status)
    {
        var booking = Booking.Create(
            customerId,
            10,
            BookingType.Inside,
            ServiceType.Catering,
            eventDate,
            eventDate.ToDateTime(TimeOnly.FromTimeSpan(start)),
            eventDate.ToDateTime(TimeOnly.FromTimeSpan(end)),
            occasion: occasion,
            packageName: "Probe",
            pricePerHead: 100m,
            packoutTime: packout);

        typeof(Booking).GetProperty(nameof(Booking.BookingNumber))!
            .SetValue(booking, occasion);

        if (paid)
        {
            booking.RecordPayment(100m);
            booking.RefreshPaymentStatus(100m, booking.AmountDue);
        }

        switch (status)
        {
            case BookingStatus.Confirmed:
                booking.Confirm();
                break;
            case BookingStatus.InProgress:
                booking.Confirm();
                booking.MarkInProgress();
                break;
            case BookingStatus.Cancelled:
                booking.Cancel();
                break;
        }

        return booking;
    }

    private static async Task DropAsync(string databaseName)
    {
        try
        {
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(
                "Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"IF DB_ID('{databaseName}') IS NOT NULL " +
                $"BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{databaseName}]; END";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTime
    {
        public DateTime Now => utcNow;
        public DateTime UtcNow => utcNow;
    }
}
