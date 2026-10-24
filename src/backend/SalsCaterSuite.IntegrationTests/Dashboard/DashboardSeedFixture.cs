using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests.Dashboard;

/// <summary>One seeded booking and the figures the dashboard must report for it.</summary>
public sealed record SeededBooking(
    string Key,
    int TransactionId,
    DateOnly EventDate,
    int Heads,
    decimal PricePerHead,
    BookingStatus Status,
    decimal NetAmountDue,
    decimal AmountPaid)
{
    /// <summary>The balance the ledger and the dashboard must both report (BR-03).</summary>
    public decimal Balance => Math.Max(0m, NetAmountDue - AmountPaid);
}

/// <summary>One seeded menu dish.</summary>
public sealed record SeededMenu(
    string MenuId,
    int? CourseId,
    string? CourseName,
    decimal? Price,
    decimal? FoodCost)
{
    /// <summary>A dish is "costed" when its food cost is known and greater than zero.</summary>
    public bool IsCosted => FoodCost.HasValue && FoodCost.Value > 0m;

    /// <summary>A dish carries a margin only when it is costed and has a selling price.</summary>
    public bool IsInMarginPopulation => IsCosted && Price.HasValue && Price.Value > 0m;
}

/// <summary>
/// The known dataset the dashboard integration test asserts against.
/// <para>
/// The bookings are seeded through the real HTTP API wherever the API allows it (creation and
/// payment), and the handful of lifecycle states the API does not expose yet (DEC-036) are
/// written straight to the database. Every figure the test expects is derived from this
/// table with plain arithmetic — never from production code.
/// </para>
/// </summary>
public sealed class DashboardSeedFixture : IAsyncLifetime
{
    private static readonly PropertyInfo StatusProperty =
        typeof(Booking).GetProperty(nameof(Booking.Status))!;

    public IntegrationDatabaseFixture Database { get; } = new();

    public List<SeededBooking> Bookings { get; } = new();

    public List<SeededMenu> Menus { get; } = new();

    public int CustomerId { get; private set; }

    /// <summary>The UTC date the dashboard treats as "today" when the request is served.</summary>
    public DateOnly Today { get; private set; }

    /// <summary>The first day of the active reporting period (the current UTC calendar month).</summary>
    public DateOnly PeriodStart { get; private set; }

    /// <summary>The last day of the active reporting period.</summary>
    public DateOnly PeriodEnd { get; private set; }

    /// <summary>The active reporting period in the <c>YYYY-MM</c> query-string format.</summary>
    public string Period { get; private set; } = string.Empty;

    /// <summary>The payments that were recorded inside the active period.</summary>
    public decimal PeriodPaymentTotal { get; private set; }

    /// <summary>The number of payment rows inside the active period.</summary>
    public int PeriodPaymentCount { get; private set; }

    /// <summary>The payment total of the preceding calendar month.</summary>
    public decimal PreviousPeriodPaymentTotal { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();

        var now = DateTime.UtcNow;
        Today = DateOnly.FromDateTime(now);
        PeriodStart = new DateOnly(now.Year, now.Month, 1);
        PeriodEnd = new DateOnly(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
        Period = $"{now.Year:0000}-{now.Month:00}";

        var client = await Database.CreateAdministratorClientAsync();
        CustomerId = await client.CreateCustomerAsync("Dashboard", "Integration");

        await SeedBookingsAsync(client);
        await SeedMenusAsync();
    }

    public Task DisposeAsync() => Database.DisposeAsync();

    /// <summary>
    /// The bookings that make up the active period, in the order they were created.
    /// </summary>
    public IEnumerable<SeededBooking> PeriodBookings =>
        Bookings.Where(b => b.EventDate >= PeriodStart && b.EventDate <= PeriodEnd);

    /// <summary>
    /// The receivables population: every booking that is not cancelled. The dashboard's AR is
    /// measured over exactly this population.
    /// </summary>
    public IEnumerable<SeededBooking> ReceivableBookings =>
        Bookings.Where(b => b.Status != BookingStatus.Cancelled);

    private async Task SeedBookingsAsync(HttpClient client)
    {
        // ── The period population: the first day of the active month ──────────────────
        var pending = await CreateBookingAsync(client, "A", PeriodStart, 100, 500m);
        var depositPaid = await CreateBookingAsync(client, "B", PeriodStart, 100, 300m);
        var fullyPaid = await CreateBookingAsync(client, "C", PeriodStart, 50, 400m);
        var confirmed = await CreateBookingAsync(client, "D", PeriodStart, 80, 250m);
        var completed = await CreateBookingAsync(client, "E", PeriodStart, 60, 600m);
        var cancelled = await CreateBookingAsync(client, "F", PeriodStart, 200, 500m);
        var expired = await CreateBookingAsync(client, "G", PeriodStart, 100, 500m);

        // ── The bookings outside the period, one per aging situation ───────────────────
        var days31To60 = await CreateBookingAsync(client, "H", Today.AddDays(-45), 40, 100m);
        var days61To90 = await CreateBookingAsync(client, "I", Today.AddDays(-75), 40, 100m);
        var over90 = await CreateBookingAsync(client, "J", Today.AddDays(-120), 40, 100m);
        var notYetDue = await CreateBookingAsync(client, "K", Today.AddDays(40), 40, 100m);
        var over90WithHistory = await CreateBookingAsync(client, "L", Today.AddDays(-150), 50, 700m);
        var future45 = await CreateBookingAsync(client, "M", Today.AddDays(45), 30, 500m);
        var future50 = await CreateBookingAsync(client, "N", Today.AddDays(50), 20, 450m);

        // ── The payments recorded inside the active period ────────────────────────────
        await RecordPaymentAsync(client, depositPaid, 20_000m);
        await RecordPaymentAsync(client, fullyPaid, 20_000m);
        await RecordPaymentAsync(client, cancelled, 50_000m);

        PeriodPaymentTotal = 90_000m;
        PeriodPaymentCount = 3;

        // ── The payment of the preceding calendar month ───────────────────────────────
        var previousMonth = PeriodStart.AddMonths(-1);
        var previousPaymentDate = new DateTime(previousMonth.Year, previousMonth.Month, 15, 10, 0, 0, DateTimeKind.Utc);
        await InsertPaymentAsync(over90WithHistory, 30_000m, previousPaymentDate);
        PreviousPeriodPaymentTotal = 30_000m;

        // ── The lifecycle states the API does not expose yet (DEC-036) ─────────────────
        await SetStatusAsync(confirmed, BookingStatus.Confirmed);
        await SetStatusAsync(completed, BookingStatus.Completed);
        await SetStatusAsync(expired, BookingStatus.Expired);

        // ── The cancelled booking leaves the receivables but keeps its cash ───────────
        (await client.CancelBookingAsync(cancelled.TransactionId, refundAmount: 0m))
            .StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // The records are built once every booking carries its final state and its final
        // payment total, so the expected figures below are the seeded truth.
        Bookings.Add(pending with { Status = BookingStatus.Pending });
        Bookings.Add(depositPaid with { Status = BookingStatus.DepositPaid, AmountPaid = 20_000m });
        Bookings.Add(fullyPaid with { Status = BookingStatus.FullyPaid, AmountPaid = 20_000m });
        Bookings.Add(confirmed with { Status = BookingStatus.Confirmed });
        Bookings.Add(completed with { Status = BookingStatus.Completed });
        Bookings.Add(cancelled with { Status = BookingStatus.Cancelled, AmountPaid = 50_000m });
        Bookings.Add(expired with { Status = BookingStatus.Expired });
        Bookings.Add(days31To60);
        Bookings.Add(days61To90);
        Bookings.Add(over90);
        Bookings.Add(notYetDue);
        Bookings.Add(over90WithHistory with { AmountPaid = 30_000m });
        Bookings.Add(future45);
        Bookings.Add(future50);
    }

    private async Task<SeededBooking> CreateBookingAsync(
        HttpClient client,
        string key,
        DateOnly eventDate,
        int heads,
        decimal pricePerHead)
    {
        var transactionId = await client.CreateBookingAsync(CustomerId, heads, pricePerHead, eventDate);

        return new SeededBooking(
            key,
            transactionId,
            eventDate,
            heads,
            pricePerHead,
            BookingStatus.Pending,
            // The create path freezes the automatic headcount band into the net (BR-38).
            TestApi.NetAmountDueFor(heads, pricePerHead),
            0m);
    }

    private static async Task RecordPaymentAsync(HttpClient client, SeededBooking booking, decimal amount)
    {
        var response = await client.RecordPaymentAsync(booking.TransactionId, amount);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, await response.DescribeAsync());
    }

    private async Task InsertPaymentAsync(SeededBooking booking, decimal amount, DateTime dateOfPayment)
    {
        using var scope = Database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        context.Payments.Add(new Payment
        {
            PaymentNo = $"OR-PREV-{booking.TransactionId:D5}",
            TransactionId = booking.TransactionId,
            DateOfPayment = dateOfPayment,
            Particular = "Previous period settlement",
            PaymentMeans = PaymentMeans.LDDAP,
            AmountPaid = amount,
            CreatedByUser = "integration-seed"
        });

        await context.SaveChangesAsync();
    }

    private async Task SetStatusAsync(SeededBooking booking, BookingStatus status)
    {
        using var scope = Database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        var entity = await context.Bookings.FirstAsync(b => b.TransactionId == booking.TransactionId);

        // The lifecycle status is a single source of truth with a private setter; the test
        // seeds the states the API does not expose yet (DEC-036).
        StatusProperty.SetValue(entity, status);

        await context.SaveChangesAsync();
    }

    private async Task SeedMenusAsync()
    {
        using var scope = Database.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuite.Infrastructure.Persistence.SalsCaterSuiteDbContext>();

        var courses = await context.CourseCategories.ToDictionaryAsync(c => c.CourseName, c => c.CourseId);

        var mainCourse = courses["Main Course"];
        var appetizer = courses["Appetizer"];
        var dessert = courses["Dessert"];
        var beverage = courses["Beverage"];

        var menus = new[]
        {
            new Menu { MenuId = "DSH-001", CourseId = mainCourse, MenuName = "Roast Beef", Price = 350m, FoodCost = 125m },
            new Menu { MenuId = "DSH-002", CourseId = mainCourse, MenuName = "Beef Steak", Price = 300m, FoodCost = 90m },
            new Menu { MenuId = "DSH-003", CourseId = appetizer, MenuName = "Lumpia", Price = 200m, FoodCost = 60m },
            new Menu { MenuId = "DSH-004", CourseId = dessert, MenuName = "Leche Flan", Price = 150m, FoodCost = null },
            new Menu { MenuId = "DSH-005", CourseId = beverage, MenuName = "Soft Drinks", Price = 100m, FoodCost = 0m },
            new Menu { MenuId = "DSH-006", CourseId = null, MenuName = "Bespoke Platter", Price = 120m, FoodCost = 40m }
        };

        context.Menus.AddRange(menus);
        await context.SaveChangesAsync();

        Menus.Add(new SeededMenu("DSH-001", mainCourse, "Main Course", 350m, 125m));
        Menus.Add(new SeededMenu("DSH-002", mainCourse, "Main Course", 300m, 90m));
        Menus.Add(new SeededMenu("DSH-003", appetizer, "Appetizer", 200m, 60m));
        Menus.Add(new SeededMenu("DSH-004", dessert, "Dessert", 150m, null));
        Menus.Add(new SeededMenu("DSH-005", beverage, "Beverage", 100m, 0m));
        Menus.Add(new SeededMenu("DSH-006", null, null, 120m, 40m));
    }
}
