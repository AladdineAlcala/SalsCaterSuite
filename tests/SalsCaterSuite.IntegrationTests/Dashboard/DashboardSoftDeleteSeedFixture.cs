using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests.Dashboard;

/// <summary>
/// The known dataset of the soft-delete exclusion test. It owns its own disposable database
/// so the assertions are not disturbed by the other integration tests' data.
/// </summary>
public sealed class DashboardSoftDeleteSeedFixture : IAsyncLifetime
{
    public IntegrationDatabaseFixture Database { get; } = new();

    public int CustomerId { get; private set; }

    public int LiveBookingId { get; private set; }

    public int SoftDeletedBookingId { get; private set; }

    public int BookingWithSoftDeletedPaymentId { get; private set; }

    public string Period { get; private set; } = string.Empty;

    public DateOnly PeriodStart { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();

        var now = DateTime.UtcNow;
        Period = $"{now.Year:0000}-{now.Month:00}";
        PeriodStart = new DateOnly(now.Year, now.Month, 1);

        var client = await Database.CreateAdministratorClientAsync();
        CustomerId = await client.CreateCustomerAsync("SoftDelete", "Integration");

        // A live booking with a live payment.
        LiveBookingId = await client.CreateBookingAsync(CustomerId, 100, 500m, PeriodStart);
        (await client.RecordPaymentAsync(LiveBookingId, 20_000m)).StatusCode
            .Should().Be(System.Net.HttpStatusCode.OK);

        // A soft-deleted booking — excluded from the population and from the receivables.
        SoftDeletedBookingId = await client.CreateBookingAsync(CustomerId, 100, 500m, PeriodStart);
        await SoftDeleteBookingAsync(SoftDeletedBookingId);

        // A live booking whose only payment is soft-deleted — the payment must not count.
        BookingWithSoftDeletedPaymentId = await client.CreateBookingAsync(CustomerId, 100, 500m, PeriodStart);
        await InsertSoftDeletedPaymentAsync(BookingWithSoftDeletedPaymentId, 10_000m);

        await SeedMenusAsync();
    }

    public Task DisposeAsync() => Database.DisposeAsync();

    private async Task SoftDeleteBookingAsync(int transactionId)
    {
        using var scope = Database.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        var booking = await context.Bookings.FirstAsync(b => b.TransactionId == transactionId);
        booking.IsDeleted = true;

        await context.SaveChangesAsync();
    }

    private async Task InsertSoftDeletedPaymentAsync(int transactionId, decimal amount)
    {
        using var scope = Database.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        context.Payments.Add(new Payment
        {
            PaymentNo = $"OR-SOFTDEL-{transactionId:D5}",
            TransactionId = transactionId,
            DateOfPayment = DateTime.UtcNow,
            Particular = "Soft-deleted payment",
            PaymentMeans = PaymentMeans.Cash,
            AmountPaid = amount,
            IsDeleted = true,
            CreatedByUser = "integration-seed"
        });

        await context.SaveChangesAsync();
    }

    private async Task SeedMenusAsync()
    {
        using var scope = Database.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        var mainCourse = await context.CourseCategories.FirstAsync(c => c.CourseName == "Main Course");

        context.Menus.AddRange(
            new Menu { MenuId = "LIVE-001", CourseId = mainCourse.CourseId, MenuName = "Live Dish", Price = 350m, FoodCost = 125m },
            new Menu { MenuId = "LIVE-002", CourseId = mainCourse.CourseId, MenuName = "Uncosted Dish", Price = 150m, FoodCost = null },
            new Menu { MenuId = "SOFT-001", CourseId = mainCourse.CourseId, MenuName = "Soft-deleted Dish", Price = 300m, FoodCost = 90m, IsDeleted = true });

        await context.SaveChangesAsync();
    }
}
