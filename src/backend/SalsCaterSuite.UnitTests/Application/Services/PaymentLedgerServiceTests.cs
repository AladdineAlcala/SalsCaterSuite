namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using Moq;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Queries;
using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Features.Payments.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// Application use-case tests (DEC-037): persistence is exercised through hand-written
/// repository fakes, never EF Core. DEC-015 — this is the single ledger implementation.
/// </summary>
public class PaymentLedgerServiceTests
{
    private readonly FakeSalsCaterSuiteDataStore _store;
    private readonly FakeBookingRepository _bookings;
    private readonly FakePaymentRepository _payments;
    private readonly Mock<ICurrentUserService> _currentUserMock;
    private readonly BookingAmountDueCalculator _calculator;
    private readonly PaymentLedgerService _service;

    public PaymentLedgerServiceTests()
    {
        _store = new FakeSalsCaterSuiteDataStore();
        _bookings = new FakeBookingRepository(_store);
        _payments = new FakePaymentRepository(_store);

        _currentUserMock = new Mock<ICurrentUserService>();
        _calculator = new BookingAmountDueCalculator();

        _currentUserMock.Setup(u => u.UserName).Returns("test_cashier");

        _service = new PaymentLedgerService(_bookings, _payments, _currentUserMock.Object, _calculator);
    }

    /// <summary>Bookings can only be created through the domain factory.</summary>
    private static Booking CreateTestBooking(int customerId = 1, int noOfPerson = 100, decimal pricePerHead = 450m)
    {
        var eventDateTime = DateTime.UtcNow.AddDays(14);
        return Booking.Create(
            customerId: customerId,
            noOfPerson: noOfPerson,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: DateOnly.FromDateTime(eventDateTime),
            startDate: eventDateTime,
            endDate: eventDateTime.AddHours(4),
            packageName: "Silver Buffet",
            pricePerHead: pricePerHead);
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenBookingNotFound_ShouldReturnFailure()
    {
        // Arrange
        var request = new RecordPaymentRequestDto
        {
            TransactionId = 999,
            AmountPaid = 5000m,
            Particular = "Downpayment",
            PaymentMeans = PaymentMeans.Cash
        };

        // Act
        var result = await _service.RecordPaymentAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Booking transaction not found.");
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenBookingExists_ShouldPersistPaymentAndReturnDto()
    {
        // Arrange
        var booking = CreateTestBooking();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var request = new RecordPaymentRequestDto
        {
            TransactionId = booking.TransactionId,
            AmountPaid = 15000m,
            Particular = "Initial 50% deposit",
            PaymentMeans = PaymentMeans.Check,
            CheckNo = "CHK-98765",
            Notes = "Bank of Commerce"
        };

        // Act
        var result = await _service.RecordPaymentAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TransactionId.Should().Be(booking.TransactionId);
        result.Data.AmountPaid.Should().Be(15000m);
        result.Data.PaymentMeans.Should().Be(PaymentMeans.Check);
        result.Data.CheckNo.Should().Be("CHK-98765");
        result.Data.CreatedByUser.Should().Be("test_cashier");
        result.Data.PaymentNo.Should().StartWith("OR-");

        // Verify state in the payment store
        var savedPayment = _payments.Payments.FirstOrDefault(p => p.TransactionId == booking.TransactionId);
        savedPayment.Should().NotBeNull();
        savedPayment!.AmountPaid.Should().Be(15000m);
        savedPayment.CreatedByUser.Should().Be("test_cashier");
    }

    [Fact]
    public async Task GetLedgerAsync_WhenBookingNotFound_ShouldReturnFailure()
    {
        // Act
        var result = await _service.GetLedgerAsync(404);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Booking not found.");
    }

    [Fact]
    public async Task GetLedgerAsync_WhenBookingHasPayments_ShouldReturnCompleteLedgerSummary()
    {
        // Arrange
        var customer = new Customer { CustomerId = 1, FirstName = "Maria", LastName = "Clara" };
        _store.Customers.Add(customer);

        var booking = CreateTestBooking(customerId: 1, noOfPerson: 100, pricePerHead: 450m);
        booking.Customer = customer;
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        _store.Payments.Add(new Payment
        {
            PaymentNo = "OR-20261018-0001",
            TransactionId = booking.TransactionId,
            AmountPaid = 20000m,
            Particular = "Deposit",
            DateOfPayment = DateTime.UtcNow,
            PaymentMeans = PaymentMeans.Cash
        });

        // Act
        var result = await _service.GetLedgerAsync(booking.TransactionId);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.CustomerName.Should().Be("Clara, Maria");
        result.Data.ContractAmount.Should().Be(45000m);
        result.Data.TotalPaid.Should().Be(20000m);
        result.Data.BalanceDue.Should().Be(25000m);
        result.Data.IsFullyPaid.Should().BeFalse();
        result.Data.Payments.Should().HaveCount(1);
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenBookingExists_ShouldRefreshAuthoritativeAmountPaidCache()
    {
        // Arrange
        var booking = CreateTestBooking();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var request = new RecordPaymentRequestDto
        {
            TransactionId = booking.TransactionId,
            AmountPaid = 15000m,
            Particular = "Deposit",
            PaymentMeans = PaymentMeans.Cash
        };

        // Act
        var result = await _service.RecordPaymentAsync(request);

        // Assert — the denormalized cache now equals the authoritative payment sum.
        result.Succeeded.Should().BeTrue();
        var reloaded = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        var ledgerSum = _payments.Payments.Where(p => p.TransactionId == booking.TransactionId).Sum(p => p.AmountPaid);
        reloaded.AmountPaid.Should().Be(15000m);
        reloaded.AmountPaid.Should().Be(ledgerSum);
        reloaded.Status.Should().Be(BookingStatus.DepositPaid);
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenBookingCancelled_ShouldReturnFailureAndNotThrow()
    {
        // Arrange
        var booking = CreateTestBooking();
        booking.Cancel();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var request = new RecordPaymentRequestDto
        {
            TransactionId = booking.TransactionId,
            AmountPaid = 5000m,
            Particular = "Late deposit",
            PaymentMeans = PaymentMeans.Cash
        };

        // Act
        var result = await _service.RecordPaymentAsync(request);

        // Assert — a domain rule violation becomes a failed result, never an exception.
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Cannot record a payment on a cancelled booking.");
        _payments.Payments.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordPaymentAsync_WhenAmountIsNotPositive_ShouldReturnFailureAndNotThrow()
    {
        // Arrange
        var booking = CreateTestBooking();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var request = new RecordPaymentRequestDto
        {
            TransactionId = booking.TransactionId,
            AmountPaid = 0m,
            Particular = "Zero payment",
            PaymentMeans = PaymentMeans.Cash
        };

        // Act
        var result = await _service.RecordPaymentAsync(request);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Payment amount must be positive.");
        _payments.Payments.Should().BeEmpty();
    }

    [Fact]
    public async Task LedgerEntryPoints_ShouldReturnIdenticalValues()
    {
        // Arrange — the two HTTP entry points are GET /api/payments/ledger/{id}
        // (service) and GET /api/bookings/{id}/ledger (query). DEC-015 requires
        // them to return identical ledger values.
        var customer = new Customer { CustomerId = 7, FirstName = "Liza", LastName = "Reyes" };
        _store.Customers.Add(customer);

        var booking = CreateTestBooking(customerId: 7, noOfPerson: 80, pricePerHead: 500m);
        booking.Customer = customer;
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        _store.Payments.AddRange(
            new Payment { PaymentNo = "OR-1", TransactionId = booking.TransactionId, AmountPaid = 10000m, Particular = "Deposit", DateOfPayment = DateTime.UtcNow.AddDays(-2), PaymentMeans = PaymentMeans.Cash },
            new Payment { PaymentNo = "OR-2", TransactionId = booking.TransactionId, AmountPaid = 8000m, Particular = "Second", DateOfPayment = DateTime.UtcNow.AddDays(-1), PaymentMeans = PaymentMeans.GCash });
        _store.Refunds.Add(new Refund
        {
            TransactionId = booking.TransactionId,
            RefundDate = DateTime.UtcNow,
            RefundAmount = 1500m,
            NetAmount = 1500m,
            Status = RefundStatus.Processed
        });

        var bookingQuery = new GetBookingLedgerQuery(_service);

        // Act
        var fromPayments = await _service.GetLedgerAsync(booking.TransactionId);
        var fromBookings = await bookingQuery.ExecuteAsync(booking.TransactionId);

        // Assert
        fromPayments.Succeeded.Should().BeTrue();
        fromBookings.Succeeded.Should().BeTrue();

        var a = fromPayments.Data!;
        var b = fromBookings.Data!;

        b.TransactionId.Should().Be(a.TransactionId);
        b.CustomerName.Should().Be(a.CustomerName);
        b.Occasion.Should().Be(a.Occasion);
        b.EventDate.Should().Be(a.EventDate);
        b.ContractAmount.Should().Be(a.ContractAmount);
        b.TotalPaid.Should().Be(a.TotalPaid);
        b.TotalRefunded.Should().Be(a.TotalRefunded);
        b.BalanceDue.Should().Be(a.BalanceDue);
        b.IsFullyPaid.Should().Be(a.IsFullyPaid);
        b.Payments.Should().HaveCount(a.Payments.Count);

        // Sanity on the values themselves.
        a.ContractAmount.Should().Be(40000m);
        a.TotalPaid.Should().Be(18000m);
        a.TotalRefunded.Should().Be(1500m);
        a.BalanceDue.Should().Be(22000m);   // refunds never reduce balance (DEC-013)
        a.IsFullyPaid.Should().BeFalse();
        a.Payments.Should().HaveCount(2);
    }
}
