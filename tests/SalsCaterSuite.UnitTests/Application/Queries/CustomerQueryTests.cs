namespace SalsCaterSuite.UnitTests.Application.Queries;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Customers.Queries;
using SalsCaterSuite.Application.Features.Customers.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// DEC-037: the customer read use cases are repository-backed. These Application tests use
/// the hand-written fake; the authoritative DEC-025 receivables aggregate is asserted
/// against EF Core in <c>CustomerRepositoryTests</c>.
/// </summary>
public class CustomerQueryTests
{
    /// <summary>The fixed "today" the aging assertions are anchored on (DEC-041).</summary>
    private static readonly DateOnly Today = new(2026, 10, 19);

    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeCustomerRepository _customers;
    private readonly GetCustomersQuery _list;
    private readonly GetCustomerByIdQuery _byId;
    private readonly GetCustomerStatementQuery _statement;
    private readonly BookingAmountDueCalculator _calculator = new();

    public CustomerQueryTests()
    {
        _customers = new FakeCustomerRepository(
            _store,
            _calculator,
            new CustomerAgingService(new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue))));

        _list = new GetCustomersQuery(_customers);
        _byId = new GetCustomerByIdQuery(_customers);
        _statement = new GetCustomerStatementQuery(
            _customers,
            _calculator,
            new CustomerAgingService(new FakeDateTime(Today.ToDateTime(TimeOnly.MinValue))));
    }

    private Booking AddBooking(int customerId, decimal pricePerHead, int daysFromNow)
    {
        var eventDateTime = DateTime.UtcNow.AddDays(daysFromNow);
        var booking = Booking.Create(
            customerId: customerId,
            noOfPerson: 100,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: DateOnly.FromDateTime(eventDateTime),
            startDate: eventDateTime,
            endDate: eventDateTime.AddHours(4),
            pricePerHead: pricePerHead);

        _store.Bookings.Add(booking);
        _store.AssignTransactionId(booking);
        return booking;
    }

    /// <summary>A booking with an explicit event date, so the aging bucket is deterministic.</summary>
    private Booking AddBookingOn(int customerId, decimal pricePerHead, DateOnly eventDate)
    {
        var start = eventDate.ToDateTime(new TimeOnly(10, 0));
        var booking = Booking.Create(
            customerId: customerId,
            noOfPerson: 100,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: eventDate,
            startDate: start,
            endDate: start.AddHours(4),
            pricePerHead: pricePerHead);

        _store.Bookings.Add(booking);
        _store.AssignTransactionId(booking);
        return booking;
    }

    private static Payment PaymentFor(Booking booking, string paymentNo, decimal amount) => new()
    {
        PaymentNo = paymentNo,
        TransactionId = booking.TransactionId,
        AmountPaid = amount,
        Particular = "Payment",
        DateOfPayment = DateTime.UtcNow,
        PaymentMeans = PaymentMeans.Cash
    };

    [Fact]
    public async Task GetCustomersQuery_ShouldReturnTheRepositoryProjection()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria" });
        var booking = AddBooking(1, 500m, 30);          // due 50,000
        _store.Payments.Add(PaymentFor(booking, "OR-0001", 20000m));

        // Act
        var result = await _list.ExecuteAsync(null);

        // Assert
        result.Succeeded.Should().BeTrue();
        var customer = result.Data!.Single(c => c.CustomerId == 1);
        customer.TotalBookings.Should().Be(1);
        customer.TotalReceivables.Should().Be(30000m);
    }

    [Fact]
    public async Task GetCustomersQuery_ShouldFilterByTheSearchTerm()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria" });
        _store.Customers.Add(new Customer { CustomerId = 2, LastName = "Cruz", FirstName = "Jose", Company = "Acme" });

        // Act
        var result = await _list.ExecuteAsync("acme");

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().ContainSingle().Which.CustomerId.Should().Be(2);
    }

    [Fact]
    public async Task GetCustomerByIdQuery_WhenCustomerExists_ShouldReturnTheProjection()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 5, LastName = "Reyes", FirstName = "Ana" });

        // Act
        var result = await _byId.ExecuteAsync(5);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data!.CustomerId.Should().Be(5);
        result.Data.FullName.Should().Be("Reyes, Ana");
    }

    [Fact]
    public async Task GetCustomerByIdQuery_WhenCustomerDoesNotExist_ShouldReturnFailure()
    {
        // Act
        var result = await _byId.ExecuteAsync(404);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Customer not found.");
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// The single-customer projection carries the Customer Master's extended fields
    /// (spec §5.2).
    /// </summary>
    [Fact]
    public async Task GetCustomerByIdQuery_ShouldCarryTheCustomerMasterFields()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 5, LastName = "Reyes", FirstName = "Ana", Company = "Acme" });
        var booking = AddBookingOn(5, 500m, Today.AddDays(10));   // due 50,000
        _store.Payments.Add(PaymentFor(booking, "OR-0005", 20000m));

        // Act
        var result = await _byId.ExecuteAsync(5);

        // Assert
        var customer = result.Data!;
        customer.CustomerType.Should().Be("Corporate");
        customer.BookingCounts!.Total.Should().Be(1);
        customer.BookingCounts.Upcoming.Should().Be(1);
        customer.BookingCounts.Past.Should().Be(0);
        customer.TotalContractedValue.Should().Be(50000m);
        customer.TotalPaid.Should().Be(20000m);
        customer.OutstandingBalance.Should().Be(30000m);
        customer.LedgerState.Should().Be("NotYetDue");
        customer.LedgerStateLabel.Should().Be("Open · not yet due");
        customer.UpcomingBooking.Should().NotBeNull();
        customer.UpcomingBooking!.TransactionId.Should().Be(booking.TransactionId);
        customer.UpcomingBooking.AmountDue.Should().Be(50000m);
    }

    [Fact]
    public async Task GetCustomerStatementQuery_ShouldBuildStatementExcludingCancelledBookings()
    {
        // Arrange — two active bookings plus one cancelled (must be excluded).
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria", Contact1 = "09171234567" });

        var a = AddBooking(1, 500m, 20);   // billed 50,000 / paid 20,000 → balance 30,000
        var b = AddBooking(1, 300m, 10);   // billed 30,000 / paid 30,000 → balance 0
        var cancelled = AddBooking(1, 100m, 5); // billed 10,000 — excluded

        _store.Payments.Add(PaymentFor(a, "OR-A", 20000m));
        _store.Payments.Add(PaymentFor(b, "OR-B", 30000m));
        _store.Payments.Add(PaymentFor(cancelled, "OR-C", 10000m));
        cancelled.Cancel();

        // Act
        var result = await _statement.ExecuteAsync(1);

        // Assert
        result.Succeeded.Should().BeTrue();
        var statement = result.Data!;
        statement.CustomerName.Should().Be("Santos, Maria");
        statement.Contact.Should().Be("09171234567");
        statement.TotalBilled.Should().Be(80000m);
        statement.TotalPaid.Should().Be(50000m);
        statement.OutstandingBalance.Should().Be(30000m);
        statement.Transactions.Should().HaveCount(2);
        // Ordered by event date descending — booking A (sooner-to-later) is the later event.
        statement.Transactions[0].TransactionId.Should().Be(a.TransactionId);
        // spec §7.2: the line's status is the approved BR-10 lifecycle name — the retired
        // "Served" / "Upcoming" vocabulary is not used any more.
        statement.Transactions[0].Status.Should().Be("Pending");
        statement.Transactions.Should().NotContain(t => t.TransactionId == cancelled.TransactionId);
    }

    [Fact]
    public async Task GetCustomerStatementQuery_WhenCustomerDoesNotExist_ShouldReturnFailure()
    {
        // Act
        var result = await _statement.ExecuteAsync(404);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Customer not found.");
        result.Data.Should().BeNull();
    }

    // ── spec §7.1 / §7.2 — the corrected statement (B4, B5) ──────────────────────────

    /// <summary>
    /// B4 / F11: the statement's amount is the ledger's net amount due — not the frozen
    /// <c>Booking.PackageAmount</c> and not the persisted <c>Booking.AmountDue</c>.
    /// </summary>
    [Fact]
    public async Task GetCustomerStatementQuery_ShouldUseTheLedgerNetAmountDue()
    {
        // Arrange — a 5,000 discount makes the three money notions differ:
        // PackageAmount = 50,000 · AmountDue = 45,000 · net amount due = 45,000.
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria" });
        var booking = AddBookingOn(1, 500m, Today.AddDays(5));
        booking.RecalculateFinancials(extendedAreaAmount: 0m, addonsAmount: 0m, discountAmount: 5000m, taxAmount: 0m);
        _store.Payments.Add(PaymentFor(booking, "OR-A", 10000m));

        // Act
        var result = await _statement.ExecuteAsync(1);

        // Assert
        var line = result.Data!.Transactions.Single();
        line.Amount.Should().Be(45000m, "the statement uses the shared calculator's net amount due");
        line.Amount.Should().NotBe(50000m, "the frozen PackageAmount is not the amount due");
        line.Paid.Should().Be(10000m);
        line.Balance.Should().Be(35000m);
        result.Data.TotalBilled.Should().Be(45000m);
        result.Data.OutstandingBalance.Should().Be(35000m);
    }

    /// <summary>B5: the statement's status is a BR-10 booking lifecycle name.</summary>
    [Fact]
    public async Task GetCustomerStatementQuery_ShouldUseTheBookingLifecycleStatus()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria" });
        var completed = AddBookingOn(1, 500m, Today.AddDays(-10));
        completed.MarkCompleted();
        var confirmed = AddBookingOn(1, 500m, Today.AddDays(20));
        confirmed.Confirm();

        // Act
        var result = await _statement.ExecuteAsync(1);

        // Assert
        var statuses = result.Data!.Transactions.Select(t => t.Status).ToList();
        statuses.Should().BeEquivalentTo(new[] { "Completed", "Confirmed" });
        statuses.Should().NotContain("Served");
        statuses.Should().NotContain("Upcoming");
    }

    /// <summary>BR-03: an overpaid booking clamps to zero — the balance is never negative.</summary>
    [Fact]
    public async Task GetCustomerStatementQuery_ShouldClampAnOverpaidBookingToZero()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria" });
        var booking = AddBookingOn(1, 500m, Today.AddDays(5));   // due 50,000
        _store.Payments.Add(PaymentFor(booking, "OR-A", 60000m));  // overpaid

        // Act
        var result = await _statement.ExecuteAsync(1);

        // Assert
        var line = result.Data!.Transactions.Single();
        line.Balance.Should().Be(0m);
        line.Balance.Should().BeGreaterThanOrEqualTo(0m);
        result.Data.OutstandingBalance.Should().Be(0m);
    }

    /// <summary>DEC-013: the refunds are reported separately and never reduce the balance.</summary>
    [Fact]
    public async Task GetCustomerStatementQuery_ShouldReportRefundsSeparately()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria" });
        var booking = AddBookingOn(1, 500m, Today.AddDays(5));
        _store.Payments.Add(PaymentFor(booking, "OR-A", 20000m));
        _store.Refunds.Add(new Refund
        {
            TransactionId = booking.TransactionId,
            RefundAmount = 3000m,
            Deduction = 500m,
            NetAmount = 2500m,
            RefundDate = DateTime.UtcNow
        });

        // Act
        var result = await _statement.ExecuteAsync(1);

        // Assert
        var statement = result.Data!;
        statement.TotalRefunded.Should().Be(2500m, "the net refund amount is reported on its own");
        statement.OutstandingBalance.Should().Be(30000m, "a refund never reduces the balance due");
    }

    /// <summary>DEC-041 / DEC-053: the line carries the aging bucket of its event date.</summary>
    [Fact]
    public async Task GetCustomerStatementQuery_ShouldAddTheAgingBucketToEveryLine()
    {
        // Arrange
        _store.Customers.Add(new Customer { CustomerId = 1, LastName = "Santos", FirstName = "Maria" });
        var overdue = AddBookingOn(1, 500m, Today.AddDays(-45));   // 45 days overdue
        var settled = AddBookingOn(1, 300m, Today.AddDays(-5));    // fully paid — no bucket
        _store.Payments.Add(PaymentFor(overdue, "OR-A", 10000m));
        _store.Payments.Add(PaymentFor(settled, "OR-B", 30000m));

        // Act
        var result = await _statement.ExecuteAsync(1);

        // Assert
        var statement = result.Data!;
        var overdueLine = statement.Transactions.Single(t => t.TransactionId == overdue.TransactionId);
        var settledLine = statement.Transactions.Single(t => t.TransactionId == settled.TransactionId);

        overdueLine.AgingBucket.Should().Be("Days31To60");
        overdueLine.AgingBucketLabel.Should().Be("Overdue 31–60 days");
        settledLine.AgingBucket.Should().BeNull("a line without a balance does not age");
        settledLine.AgingBucketLabel.Should().BeNull();

        statement.LedgerState.Should().Be("Days31To60");
        statement.LedgerStateLabel.Should().Be("Overdue 31–60 days");
    }
}
