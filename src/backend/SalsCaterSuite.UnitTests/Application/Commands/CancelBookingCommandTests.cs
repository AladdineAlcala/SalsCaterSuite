namespace SalsCaterSuite.UnitTests.Application.Commands;

using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.Commands;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.UnitTests.Common.Fakes;
using Xunit;

/// <summary>
/// Application use-case tests (DEC-037): persistence is exercised through hand-written
/// repository fakes, never EF Core.
/// </summary>
public class CancelBookingCommandTests
{
    private readonly FakeSalsCaterSuiteDataStore _store;
    private readonly FakeBookingRepository _bookings;
    private readonly FakeRefundRepository _refunds;
    private readonly CancelBookingCommand _command;

    public CancelBookingCommandTests()
    {
        _store = new FakeSalsCaterSuiteDataStore();
        _bookings = new FakeBookingRepository(_store);
        _refunds = new FakeRefundRepository(_store);
        _command = new CancelBookingCommand(_bookings, _refunds);
    }

    /// <summary>Bookings can only be created through the domain factory.</summary>
    private static Booking CreateTestBooking(int customerId = 1, string? occasion = null)
    {
        var eventDateTime = DateTime.UtcNow.AddDays(30);
        return Booking.Create(
            customerId: customerId,
            noOfPerson: 50,
            bookType: BookingType.Inside,
            typeOfService: ServiceType.Catering,
            eventDate: DateOnly.FromDateTime(eventDateTime),
            startDate: eventDateTime,
            endDate: eventDateTime.AddHours(4),
            occasion: occasion);
    }

    /// <summary>
    /// Persists a booking plus its authoritative Payment rows (DEC-012). The
    /// denormalized <see cref="Booking.AmountPaid"/> cache is intentionally left at
    /// zero so the tests prove the command uses the Payment rows, not the cache.
    /// </summary>
    private async Task<Booking> SeedBookingWithPaymentsAsync(params decimal[] payments)
    {
        var booking = CreateTestBooking(occasion: "Refund Policy Test");
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        var index = 1;
        foreach (var amount in payments)
        {
            _store.Payments.Add(new Payment
            {
                PaymentNo = $"OR-{booking.TransactionId:D6}-{index++}",
                TransactionId = booking.TransactionId,
                AmountPaid = amount,
                Particular = "Deposit",
                DateOfPayment = DateTime.UtcNow,
                PaymentMeans = PaymentMeans.Cash
            });
        }

        return booking;
    }

    [Fact]
    public async Task ExecuteAsync_WhenBookingDoesNotExist_ShouldReturnFailure()
    {
        // Act
        var result = await _command.ExecuteAsync(999, "Client request", null);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Booking transaction not found.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenBookingAlreadyCancelled_ShouldReturnFailure()
    {
        // Arrange
        var booking = CreateTestBooking(occasion: "Party");
        booking.Cancel();
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "Second cancel attempt", null);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Booking is already cancelled.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCancellationWithoutRefund_ShouldUpdateBookingStatus()
    {
        // Arrange
        var booking = CreateTestBooking(occasion: "Seminar");
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "Schedule Conflict", null);

        // Assert
        result.Succeeded.Should().BeTrue();
        var updated = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        updated.Status.Should().Be(BookingStatus.Cancelled);
        updated.UpdatedDate.Should().NotBeNull();
        _refunds.Refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCancellationWithRefund_ShouldCreateRefundEntity()
    {
        // Arrange — a refund is only valid up to the payments received (DEC-024);
        // this booking has a 15,000 payment so a 10,000 refund is permitted.
        var booking = await SeedBookingWithPaymentsAsync(15000m);

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "Force Majeure - Typhoon", 10000m);

        // Assert
        result.Succeeded.Should().BeTrue();
        var updated = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        updated.Status.Should().Be(BookingStatus.Cancelled);

        var refunds = _refunds.Refunds.Where(r => r.TransactionId == booking.TransactionId).ToList();
        refunds.Should().HaveCount(1);
        var refund = refunds[0];
        refund.RefundAmount.Should().Be(10000m);
        refund.NetAmount.Should().Be(10000m);
        refund.RefundReason.Should().Be("Force Majeure - Typhoon");
        refund.Status.Should().Be(RefundStatus.Processed);
    }

    // ─── DEC-024 / F7: refund cannot exceed payments received ────────────────

    [Fact]
    public async Task ExecuteAsync_WhenRefundExceedsPaymentsReceived_ShouldFailAndLeaveBookingUncancelled()
    {
        // Arrange — authoritative payments total 15,000; the refund asks for more.
        var booking = await SeedBookingWithPaymentsAsync(10000m, 5000m);

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "Over-refund", 15000.01m);

        // Assert — rejected before any mutation.
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Refund amount cannot exceed the total payments received.");

        var persisted = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        persisted.Status.Should().NotBe(BookingStatus.Cancelled);
        _refunds.Refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefundEqualsPaymentsReceived_ShouldCancelAndCreateRefund()
    {
        // Arrange
        var booking = await SeedBookingWithPaymentsAsync(20000m);

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "Full refund", 20000m);

        // Assert
        result.Succeeded.Should().BeTrue();

        var persisted = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        persisted.Status.Should().Be(BookingStatus.Cancelled);

        var refunds = _refunds.Refunds.Where(r => r.TransactionId == booking.TransactionId).ToList();
        refunds.Should().HaveCount(1);
        refunds[0].RefundAmount.Should().Be(20000m);
        refunds[0].NetAmount.Should().Be(20000m);
        refunds[0].Deduction.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefundIsZero_ShouldCancelWithoutCreatingARefund()
    {
        // Arrange — a zero refund means "no refund" (DEC-024).
        var booking = await SeedBookingWithPaymentsAsync(20000m);

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "No refund", 0m);

        // Assert
        result.Succeeded.Should().BeTrue();

        var persisted = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        persisted.Status.Should().Be(BookingStatus.Cancelled);
        _refunds.Refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefundIsNull_ShouldCancelWithoutCreatingARefund()
    {
        // Arrange
        var booking = await SeedBookingWithPaymentsAsync(20000m);

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "No refund supplied", null);

        // Assert
        result.Succeeded.Should().BeTrue();

        var persisted = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        persisted.Status.Should().Be(BookingStatus.Cancelled);
        _refunds.Refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefundIsNegative_ShouldFailAndLeaveBookingUncancelled()
    {
        // Arrange
        var booking = await SeedBookingWithPaymentsAsync(10000m);

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "Negative refund", -1m);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Refund amount must not be negative.");

        var persisted = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        persisted.Status.Should().NotBe(BookingStatus.Cancelled);
        _refunds.Refunds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefundRequestedOnBookingWithNoPayments_ShouldFail()
    {
        // Arrange — no Payment rows, so the cap is zero.
        var booking = CreateTestBooking(occasion: "Unpaid");
        await _bookings.AddAsync(booking);
        await _bookings.SaveChangesAsync();

        // Act
        var result = await _command.ExecuteAsync(booking.TransactionId, "Refund with no payments", 1m);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Refund amount cannot exceed the total payments received.");

        var persisted = _bookings.Bookings.Single(b => b.TransactionId == booking.TransactionId);
        persisted.Status.Should().NotBe(BookingStatus.Cancelled);
        _refunds.Refunds.Should().BeEmpty();
    }
}
