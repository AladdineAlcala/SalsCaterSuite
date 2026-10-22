namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.UnitTests.Common;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// DEC-025: Accounts Receivable is the outstanding balance,
/// <c>Σ max(0, NetAmountDue − PaymentsReceived)</c> over non-cancelled bookings —
/// not the sum of collections (the old defect).
/// <para>
/// Moved here from <c>Application/Queries/CustomerReceivablesQueryTests</c> by DEC-037:
/// the projection now lives in <see cref="CustomerRepository"/>, so it is an
/// Infrastructure concern and is exercised directly against EF Core. Every original
/// assertion is preserved, including the SQL Server translatability probe.
/// </para>
/// <para>
/// The Customer Master directory projection (spec §5.1) is exercised here as well: the
/// AR, the contracted value, the soft-delete exclusion and the reconciliation between the
/// summary and the per-customer balances (B1, B2, B3, B9, B12, B13).
/// </para>
/// </summary>
public class CustomerRepositoryTests : IDisposable
{
    /// <summary>The fixed "today" the aging assertions are anchored on (DEC-041).</summary>
    private static readonly DateOnly Today = new(2026, 10, 19);

    private readonly SalsCaterSuiteDbContext _context;
    private readonly CustomerRepository _repository;

    public CustomerRepositoryTests()
    {
        _context = TestDbContextFactory.Create();

        // One clock for the whole test: the aging service and the aggregate's generatedAt
        // must read the same instant.
        var clock = new FakeDateTime(
            DateTime.SpecifyKind(Today.ToDateTime(new TimeOnly(9, 15)), DateTimeKind.Utc));

        _repository = new CustomerRepository(
            _context,
            new BookingAmountDueCalculator(),
            new CustomerAgingService(clock),
            clock);
    }

    /// <summary>
    /// Creates a booking whose frozen <c>AmountDue</c> equals <paramref name="amountDue"/>
    /// (pricePerHead × headcount is snapshotted at creation).
    /// </summary>
    private static Booking CreateBooking(int customerId, decimal amountDue)
    {
        var eventDateTime = DateTime.UtcNow.AddDays(30);
        return Booking.Create(
            customerId: customerId,
            noOfPerson: 100,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: DateOnly.FromDateTime(eventDateTime),
            startDate: eventDateTime,
            endDate: eventDateTime.AddHours(4),
            pricePerHead: amountDue / 100m);
    }

    /// <summary>A booking with an explicit event date, so the aging bucket is deterministic.</summary>
    private static Booking CreateBookingOn(int customerId, decimal amountDue, DateOnly eventDate)
    {
        var start = eventDate.ToDateTime(new TimeOnly(10, 0));
        return Booking.Create(
            customerId: customerId,
            noOfPerson: 100,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: start,
            endDate: start.AddHours(4),
            pricePerHead: amountDue / 100m);
    }

    private static Payment PaymentFor(Booking booking, string paymentNo, decimal amount)
        => new()
        {
            PaymentNo = paymentNo,
            TransactionId = booking.TransactionId,
            AmountPaid = amount,
            Particular = "Payment",
            DateOfPayment = DateTime.UtcNow,
            PaymentMeans = PaymentMeans.Cash
        };

    /// <summary>
    /// Customer 1 with three active bookings:
    /// A (due 50,000 / paid 20,000), B (due 30,000 / paid 30,000, fully paid),
    /// C (due 40,000 / paid 0). AR = 30,000 + 0 + 40,000 = 70,000.
    /// The old collections bug would have returned 50,000.
    /// </summary>
    private async Task SeedBaselineCustomerAsync()
    {
        await _context.Customers.AddAsync(new Customer { CustomerId = 1, FirstName = "Maria", LastName = "Santos" });

        var a = CreateBooking(1, 50000m);
        var b = CreateBooking(1, 30000m);
        var c = CreateBooking(1, 40000m);
        await _context.Bookings.AddRangeAsync(a, b, c);
        await _context.SaveChangesAsync();

        await _context.Payments.AddRangeAsync(
            PaymentFor(a, "OR-A-1", 20000m),
            PaymentFor(b, "OR-B-1", 30000m));
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetListProjectionAsync_TotalReceivables_ShouldBeOutstandingBalanceNotCollections()
    {
        // Arrange
        await SeedBaselineCustomerAsync();

        // Act
        var customers = await _repository.GetListProjectionAsync(null);

        // Assert
        var customer = customers.Single(c => c.CustomerId == 1);
        customer.TotalReceivables.Should().Be(70000m);
        customer.TotalReceivables.Should().NotBe(50000m); // the old "sum of payments" defect
        customer.TotalBookings.Should().Be(3);              // unchanged by the correction
    }

    [Fact]
    public async Task BothProjections_ShouldReportTheSameTotalReceivables()
    {
        // Arrange
        await SeedBaselineCustomerAsync();

        // Act
        var list = await _repository.GetListProjectionAsync(null);
        var byId = await _repository.GetProjectionByIdAsync(1);

        // Assert
        byId.Should().NotBeNull();
        byId!.TotalReceivables.Should().Be(70000m);
        byId.TotalReceivables.Should().Be(list.Single(c => c.CustomerId == 1).TotalReceivables);
    }

    [Fact]
    public async Task TotalReceivables_ShouldExcludeCancelledBookingsEvenWhenTheyHavePayments()
    {
        // Arrange — booking D has an outstanding 20,000 (due 25,000 / paid 5,000)
        // but is cancelled, so it must not contribute to AR.
        await SeedBaselineCustomerAsync();

        var d = CreateBooking(1, 25000m);
        await _context.Bookings.AddAsync(d);
        await _context.SaveChangesAsync();
        await _context.Payments.AddAsync(PaymentFor(d, "OR-D-1", 5000m));
        await _context.SaveChangesAsync();

        d.Cancel();
        await _context.SaveChangesAsync();

        // Act
        var list = await _repository.GetListProjectionAsync(null);
        var byId = await _repository.GetProjectionByIdAsync(1);

        // Assert — still 70,000, and D is excluded from the booking count too.
        list.Single(c => c.CustomerId == 1).TotalReceivables.Should().Be(70000m);
        list.Single(c => c.CustomerId == 1).TotalBookings.Should().Be(3);
        byId!.TotalReceivables.Should().Be(70000m);
    }

    [Fact]
    public async Task TotalReceivables_ShouldClampAnOverpaidBookingToZero()
    {
        // Arrange — booking E is overpaid (due 10,000 / paid 15,000) and must
        // contribute 0, never a negative amount.
        await SeedBaselineCustomerAsync();

        var e = CreateBooking(1, 10000m);
        await _context.Bookings.AddAsync(e);
        await _context.SaveChangesAsync();
        await _context.Payments.AddAsync(PaymentFor(e, "OR-E-1", 15000m));
        await _context.SaveChangesAsync();

        // Act
        var list = await _repository.GetListProjectionAsync(null);
        var byId = await _repository.GetProjectionByIdAsync(1);

        // Assert
        list.Single(c => c.CustomerId == 1).TotalReceivables.Should().Be(70000m);
        byId!.TotalReceivables.Should().Be(70000m);
    }

    /// <summary>
    /// The receivables projection must remain a single server-side aggregate
    /// (no client-side <c>AsEnumerable()</c> fallback). This mirrors the exact projection
    /// used by <see cref="CustomerRepository.GetListProjectionAsync"/> and
    /// <see cref="CustomerRepository.GetProjectionByIdAsync"/>, and asserts the SQL Server
    /// provider can generate SQL for it; <c>ToQueryString()</c> compiles the query without
    /// opening a connection. The end-to-end proof that the real query path translates
    /// remains the integration <c>ReceivablesTests</c> on live SQL Server (DEC-025).
    /// </summary>
    [Fact]
    public void TotalReceivablesProjection_ShouldBeTranslatableBySqlServer()
    {
        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuite_QueryTranslationProbe;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        using var context = new SalsCaterSuiteDbContext(options);

        var query = context.Customers
            .AsNoTracking()
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new
            {
                c.CustomerId,
                TotalReceivables = c.Bookings!
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .Select(b => new
                    {
                        b.AmountDue,
                        AmountPaid = b.Payments!.Sum(p => p.AmountPaid)
                    })
                    .Where(x => x.AmountDue > x.AmountPaid)
                    .Sum(x => x.AmountDue - x.AmountPaid)
            });

        var sql = query.ToQueryString();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("SUM");
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // The Customer Master directory projection (spec §5.1)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// B1: the directory's AR is <c>Σ max(0, NetAmountDue − Σ payments)</c> over the
    /// customer's non-cancelled bookings.
    /// </summary>
    [Fact]
    public async Task The_directory_projection_AR_should_beTheOutstandingBalanceOfTheNonCancelledBookings()
    {
        // Arrange
        await SeedBaselineCustomerAsync();

        // Act
        var directory = await _repository.GetDirectoryProjectionAsync(null, null, null, 1, 10);

        // Assert
        var item = directory.Items.Single(i => i.CustomerId == 1);
        item.OutstandingBalance.Should().Be(70000m, "30,000 (A) + 0 (B) + 40,000 (C)");
        item.TotalContractedValue.Should().Be(120000m, "50,000 + 30,000 + 40,000");
        item.TotalPaid.Should().Be(50000m, "20,000 (A) + 30,000 (B)");
    }

    /// <summary>
    /// B2: a fully paid booking contributes 0 and a cancelled booking contributes nothing
    /// at all.
    /// </summary>
    [Fact]
    public async Task The_directory_projection_ShouldExcludeFullyPaidAndCancelledBookings()
    {
        // Arrange — the baseline already holds a fully paid booking (B).
        await SeedBaselineCustomerAsync();

        var cancelled = CreateBooking(1, 25000m);
        await _context.Bookings.AddAsync(cancelled);
        await _context.SaveChangesAsync();
        await _context.Payments.AddAsync(PaymentFor(cancelled, "OR-D-1", 5000m));
        await _context.SaveChangesAsync();
        cancelled.Cancel();
        await _context.SaveChangesAsync();

        // Act
        var directory = await _repository.GetDirectoryProjectionAsync(null, null, null, 1, 10);

        // Assert
        var item = directory.Items.Single(i => i.CustomerId == 1);
        item.OutstandingBalance.Should().Be(70000m);
        item.TotalContractedValue.Should().Be(120000m, "the cancelled booking contributes nothing");
        item.TotalPaid.Should().Be(50000m, "the cancelled booking's payment is not counted");
        item.BookingCounts.Total.Should().Be(3);
        item.BookingCounts.Cancelled.Should().Be(1);
    }

    /// <summary>B3: an overpaid booking clamps to 0.00 — never a negative balance.</summary>
    [Fact]
    public async Task The_directory_projection_ShouldClampAnOverpaidBookingToZero()
    {
        // Arrange
        await SeedBaselineCustomerAsync();

        var overpaid = CreateBooking(1, 10000m);
        await _context.Bookings.AddAsync(overpaid);
        await _context.SaveChangesAsync();
        await _context.Payments.AddAsync(PaymentFor(overpaid, "OR-E-1", 15000m));
        await _context.SaveChangesAsync();

        // Act
        var directory = await _repository.GetDirectoryProjectionAsync(null, null, null, 1, 10);

        // Assert
        var item = directory.Items.Single(i => i.CustomerId == 1);
        item.OutstandingBalance.Should().Be(70000m);
        item.OutstandingBalance.Should().BeGreaterThanOrEqualTo(0m);
    }

    /// <summary>B12: the soft-deleted customers are excluded from the rows and the summary.</summary>
    [Fact]
    public async Task The_directory_projection_ShouldExcludeSoftDeletedCustomers()
    {
        // Arrange
        await SeedBaselineCustomerAsync();
        await _context.Customers.AddAsync(new Customer
        {
            CustomerId = 2,
            FirstName = "Deleted",
            LastName = "Cruz",
            IsDeleted = true
        });
        await _context.SaveChangesAsync();

        // Act
        var directory = await _repository.GetDirectoryProjectionAsync(null, null, null, 1, 10);

        // Assert
        directory.Items.Should().ContainSingle().Which.CustomerId.Should().Be(1);
        directory.TotalCount.Should().Be(1);
        directory.Summary.TotalClients.Should().Be(1);
    }

    /// <summary>
    /// B13: the summary's active AR equals the sum of the per-customer balances over the
    /// whole directory, and the summary describes the whole filtered set — not the page.
    /// </summary>
    [Fact]
    public async Task The_summary_activeAR_shouldEqualTheSumOfThePerCustomerBalances()
    {
        // Arrange — three customers, each owing 10,000.
        for (var id = 1; id <= 3; id++)
        {
            await _context.Customers.AddAsync(new Customer
            {
                CustomerId = id,
                FirstName = $"First{id}",
                LastName = $"Last{id}"
            });

            var booking = CreateBookingOn(id, 10000m, Today.AddDays(10));
            await _context.Bookings.AddAsync(booking);
        }

        await _context.SaveChangesAsync();

        // Act
        var directory = await _repository.GetDirectoryProjectionAsync(null, null, null, 1, 2);

        // Assert
        directory.Items.Should().HaveCount(2, "the page holds at most pageSize rows");
        directory.TotalCount.Should().Be(3);
        directory.Summary.TotalClients.Should().Be(3, "the summary covers the whole filtered set");
        directory.Summary.ActiveArAmount.Should().Be(30000m);
        directory.Summary.ClientsWithBalance.Should().Be(3);
        directory.Summary.AverageValuePerClient.Should().Be(10000m);

        // The whole set's balances sum to the summary's active AR.
        var allRows = await _repository.GetDirectoryItemsAsync(null, null, null);
        allRows.Sum(r => r.OutstandingBalance).Should().Be(directory.Summary.ActiveArAmount);
    }

    /// <summary>
    /// The directory's aging state is the worst bucket among the customer's positive
    /// balances, and the label comes from the server (DEC-041 / DEC-052).
    /// </summary>
    [Fact]
    public async Task The_directory_projection_shouldDeriveTheAgingStateAndTheClassification()
    {
        // Arrange
        await _context.Customers.AddAsync(new Customer
        {
            CustomerId = 1,
            FirstName = "Liza",
            LastName = "Tan",
            Company = "Apex Global Technologies Inc."
        });

        var recent = CreateBookingOn(1, 10000m, Today.AddDays(-5));    // Days1To30
        var old = CreateBookingOn(1, 20000m, Today.AddDays(-100));     // Over90Days
        await _context.Bookings.AddRangeAsync(recent, old);
        await _context.SaveChangesAsync();

        // Act
        var directory = await _repository.GetDirectoryProjectionAsync(null, null, null, 1, 10);

        // Assert
        var item = directory.Items.Single();
        item.CustomerType.Should().Be("Corporate");
        item.LedgerState.Should().Be("Over90Days");
        item.LedgerStateLabel.Should().Be("Overdue 90+ days");
        item.OutstandingBalance.Should().Be(30000m);
    }

    /// <summary>The classification filter narrows the rows; an unknown value is ignored.</summary>
    [Fact]
    public async Task The_directory_projection_shouldApplyTheClassificationAndTheARStateFilters()
    {
        // Arrange
        await _context.Customers.AddAsync(new Customer { CustomerId = 1, FirstName = "Maria", LastName = "Santos" });
        await _context.Customers.AddAsync(new Customer { CustomerId = 2, FirstName = "Jose", LastName = "Cruz", Company = "Acme" });

        var corporateBooking = CreateBookingOn(2, 10000m, Today.AddDays(-45));  // 45 days overdue
        await _context.Bookings.AddAsync(corporateBooking);
        await _context.SaveChangesAsync();

        // Act
        var corporate = await _repository.GetDirectoryProjectionAsync(null, "Corporate", null, 1, 10);
        var privateAccounts = await _repository.GetDirectoryProjectionAsync(null, "Private", null, 1, 10);
        var overdue30 = await _repository.GetDirectoryProjectionAsync(null, null, "Overdue30", 1, 10);
        var ignored = await _repository.GetDirectoryProjectionAsync(null, "banana", "banana", 1, 10);

        // Assert
        corporate.Items.Should().ContainSingle().Which.CustomerId.Should().Be(2);
        privateAccounts.Items.Should().ContainSingle().Which.CustomerId.Should().Be(1);
        overdue30.Items.Should().ContainSingle().Which.CustomerId.Should().Be(2);
        ignored.Items.Should().HaveCount(2, "an unrecognised filter value is ignored");
    }

    /// <summary>
    /// The quick-view pills' live counts (spec §4.3, contract §10.1): they describe the
    /// search-filtered population with the type and the AR-state filters ignored, and they
    /// are produced in the same pass — no extra query, and never one query per customer.
    /// </summary>
    [Fact]
    public async Task The_directory_projection_should_carry_the_quick_view_counts_and_the_generated_timestamp()
    {
        // Arrange — a corporate customer with an open balance, a private customer with an
        // open balance 45 days overdue, and a private customer who is settled.
        await _context.Customers.AddAsync(new Customer
        {
            CustomerId = 1,
            FirstName = "Liza",
            LastName = "Tan",
            Company = "Apex Global Technologies Inc."
        });
        await _context.Customers.AddAsync(new Customer { CustomerId = 2, FirstName = "Ana", LastName = "Reyes" });
        await _context.Customers.AddAsync(new Customer { CustomerId = 3, FirstName = "Jose", LastName = "Cruz" });

        await _context.Bookings.AddAsync(CreateBookingOn(1, 10000m, Today.AddDays(10)));   // open, not yet due
        await _context.Bookings.AddAsync(CreateBookingOn(2, 20000m, Today.AddDays(-45)));  // open, 45 days overdue
        await _context.Bookings.AddAsync(CreateBookingOn(3, 30000m, Today.AddDays(-10)));  // settled
        await _context.SaveChangesAsync();

        var settled = await _context.Bookings.Where(b => b.CustomerId == 3).FirstAsync();
        await _context.Payments.AddAsync(PaymentFor(settled, "OR-SETTLED", 30000m));
        await _context.SaveChangesAsync();

        // Act
        var unfiltered = await _repository.GetDirectoryProjectionAsync(null, null, null, 1, 10);
        var corporate = await _repository.GetDirectoryProjectionAsync(null, "Corporate", null, 1, 10);
        var overdue30 = await _repository.GetDirectoryProjectionAsync(null, null, "Overdue30", 1, 10);
        var searched = await _repository.GetDirectoryProjectionAsync("Tan", null, null, 1, 10);

        // Assert — the pills describe the population, not the active scope.
        unfiltered.QuickViewCounts.All.Should().Be(3);
        unfiltered.QuickViewCounts.Corporate.Should().Be(1);
        unfiltered.QuickViewCounts.Private.Should().Be(2);
        unfiltered.QuickViewCounts.WithOpenBalance.Should().Be(2);
        unfiltered.QuickViewCounts.Overdue30.Should().Be(1);
        (unfiltered.QuickViewCounts.Corporate + unfiltered.QuickViewCounts.Private)
            .Should().Be(unfiltered.QuickViewCounts.All);

        corporate.Summary.TotalClients.Should().Be(1, "the summary follows the active filter");
        corporate.QuickViewCounts.Should().BeEquivalentTo(unfiltered.QuickViewCounts);

        overdue30.Summary.TotalClients.Should().Be(1);
        overdue30.QuickViewCounts.Should().BeEquivalentTo(unfiltered.QuickViewCounts);

        // The search narrows the population the pills describe.
        searched.QuickViewCounts.All.Should().Be(1);
        searched.QuickViewCounts.Corporate.Should().Be(1);
        searched.QuickViewCounts.Private.Should().Be(0);

        // The aggregate carries the server's UTC instant.
        unfiltered.GeneratedAt.Should().Be(
            DateTime.SpecifyKind(Today.ToDateTime(new TimeOnly(9, 15)), DateTimeKind.Utc));
        unfiltered.GeneratedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    /// <summary>
    /// B9: the database-side half of the directory projection is a single translatable
    /// statement — the customer filter, the soft-delete exclusion and the ordering are all
    /// applied by SQL Server, never client-side.
    /// <para>
    /// The money figures are composed in the Application layer through the single shared
    /// <c>BookingAmountDueCalculator</c> overload (the "load the rows, compose them
    /// server-side" pattern), because a C# service method cannot be called inside a LINQ
    /// <c>Select</c> and the persisted <c>Booking.AmountDue</c> column must never be used
    /// (DEC-012, DEC-035, DEC-046).
    /// </para>
    /// </summary>
    [Fact]
    public void TheDirectoryProjection_ShouldBeTranslatableBySqlServer()
    {
        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuite_QueryTranslationProbe;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        using var context = new SalsCaterSuiteDbContext(options);

        var term = "san".ToLower();

        var query = context.Customers
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .Where(c =>
                c.LastName.ToLower().Contains(term) ||
                c.FirstName.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)) ||
                (c.Contact1 != null && c.Contact1.ToLower().Contains(term)))
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ThenBy(c => c.CustomerId)
            .Select(c => new
            {
                c.CustomerId,
                c.LastName,
                c.FirstName,
                c.Company
            });

        var sql = query.ToQueryString();

        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("is_deleted");
    }

    public void Dispose()
    {
        TestDbContextFactory.Destroy(_context);
    }
}
