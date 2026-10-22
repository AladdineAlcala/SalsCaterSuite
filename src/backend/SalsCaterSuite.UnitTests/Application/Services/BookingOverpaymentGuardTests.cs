namespace SalsCaterSuite.UnitTests.Application.Services;

using FluentAssertions;
using Moq;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Application.Features.Payments.DTOs;
using SalsCaterSuite.Application.Features.Payments.Services;
using SalsCaterSuite.Application.Services;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// BR-05's overpayment prohibition (DEC-118). A payment that would take the total above the
/// net amount due is refused with a failed result and <b>nothing</b> is written: no payment
/// row, no status change and no change to the cached <c>AmountPaid</c>.
/// </summary>
public class BookingOverpaymentGuardTests
{
    private readonly FakeSalsCaterSuiteDataStore _store = new();
    private readonly FakeBookingRepository _bookings;
    private readonly FakePaymentRepository _payments;
    private readonly FakeRefundRepository _refunds;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly PaymentLedgerService _ledger;
    private readonly BookingAmountDueCalculator _calculator = new();

    public BookingOverpaymentGuardTests()
    {
        _bookings = new FakeBookingRepository(_store);
        _payments = new FakePaymentRepository(_store);
        _refunds = new FakeRefundRepository(_store);

        _currentUser.Setup(user => user.UserName).Returns("test_cashier");

        _ledger = new PaymentLedgerService(_bookings, _payments, _currentUser.Object, _calculator);
    }

    private static Booking CreateBooking(decimal pricePerHead = 450m, int heads = 100)
    {
        var eventDate = DateTime.UtcNow.AddDays(14);

        return Booking.Create(
            customerId: 1,
            noOfPerson: heads,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: DateOnly.FromDateTime(eventDate),
            startDate: eventDate,
            endDate: eventDate.AddHours(4),
            packageName: "Silver Buffet",
            pricePerHead: pricePerHead);
    }

    [Fact]
    public async Task A_payment_above_the_net_amount_due_should_be_refused_and_nothing_should_be_written()
    {
        var booking = CreateBooking();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var before = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);

        var result = await _ledger.RecordPaymentAsync(new RecordPaymentRequestDto
        {
            TransactionId = booking.TransactionId,
            AmountPaid = 45_000.01m,
            Particular = "Overpayment",
            PaymentMeans = PaymentMeans.Cash
        });

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("The payment would take the total collected above the net amount due of the booking.");

        _payments.Payments.Should().BeEmpty("no payment row is written");

        var after = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        after.AmountPaid.Should().Be(before.AmountPaid, "the cached AmountPaid is unchanged");
        after.Status.Should().Be(before.Status, "the status is unchanged");
    }

    [Fact]
    public async Task The_ledger_should_still_show_the_same_remaining_balance_after_a_refused_overpayment()
    {
        var booking = CreateBooking();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        _store.Payments.Add(new Payment
        {
            PaymentNo = "OR-1",
            TransactionId = booking.TransactionId,
            AmountPaid = 20_000m,
            Particular = "Deposit",
            DateOfPayment = DateTime.UtcNow,
            PaymentMeans = PaymentMeans.Cash
        });

        var before = await _ledger.GetLedgerAsync(booking.TransactionId);

        await _ledger.RecordPaymentAsync(new RecordPaymentRequestDto
        {
            TransactionId = booking.TransactionId,
            AmountPaid = 30_000.01m,
            Particular = "Overpayment",
            PaymentMeans = PaymentMeans.Cash
        });

        var after = await _ledger.GetLedgerAsync(booking.TransactionId);

        after.Data!.BalanceDue.Should().Be(before.Data!.BalanceDue);
        after.Data.TotalPaid.Should().Be(before.Data.TotalPaid);
    }

    [Fact]
    public async Task A_payment_that_takes_the_total_exactly_to_the_net_amount_due_should_be_accepted()
    {
        var booking = CreateBooking();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var result = await _ledger.RecordPaymentAsync(new RecordPaymentRequestDto
        {
            TransactionId = booking.TransactionId,
            AmountPaid = 45_000m,
            Particular = "Settlement",
            PaymentMeans = PaymentMeans.Cash
        });

        result.Succeeded.Should().BeTrue();
        _payments.Payments.Should().HaveCount(1);
    }

    [Fact]
    public async Task The_guard_should_not_apply_to_a_refund()
    {
        var booking = CreateBooking();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        _store.Payments.Add(new Payment
        {
            PaymentNo = "OR-1",
            TransactionId = booking.TransactionId,
            AmountPaid = 20_000m,
            Particular = "Deposit",
            DateOfPayment = DateTime.UtcNow,
            PaymentMeans = PaymentMeans.Cash
        });

        var cancel = new CancelBookingCommand(_bookings, _refunds);

        // A refund of the whole collected amount is legitimate — the guard governs payments,
        // never refunds (BR-06, DEC-118).
        var result = await cancel.ExecuteAsync(booking.TransactionId, "Client request", 20_000m);

        result.Succeeded.Should().BeTrue();
        _store.Refunds.Should().HaveCount(1);
    }
}
