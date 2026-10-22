using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 8 — Cancellation refund guard (Scenario 13 / DEC-024).
/// A refund may never exceed the authoritative payments received; a zero refund creates no row.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RefundGuardTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public RefundGuardTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_refund_above_payments_received_is_rejected_and_leaves_the_booking_uncancelled()
    {
        var client = await _fixture.CreateManagerClientAsync();
        var (transactionId, _) = await CreateBookingWithPaymentAsync(client, paid: 10_000m);

        var response = await client.CancelBookingAsync(transactionId, refundAmount: 15_000m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.DescribeAsync());
        (await response.ReadErrorsAsync())
            .Should().Equal("Refund amount cannot exceed the total payments received.");

        var details = await client.GetBookingDetailsAsync(transactionId);
        details.GetProperty("status").GetString().Should().NotBe(
            "Cancelled", "a rejected refund must leave the booking uncancelled (DEC-024)");

        var refundCount = await _fixture.QueryAsync(context =>
            context.Refunds.CountAsync(r => r.TransactionId == transactionId));

        refundCount.Should().Be(0, "no refund row may be created for a rejected refund");
    }

    [Fact]
    public async Task A_refund_equal_to_payments_received_is_allowed_and_recorded_as_processed()
    {
        var client = await _fixture.CreateManagerClientAsync();
        var (transactionId, _) = await CreateBookingWithPaymentAsync(client, paid: 10_000m);

        var response = await client.CancelBookingAsync(transactionId, refundAmount: 10_000m);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var details = await client.GetBookingDetailsAsync(transactionId);
        details.GetProperty("status").GetString().Should().Be("Cancelled");

        var refunds = await _fixture.QueryAsync(context => context.Refunds
            .AsNoTracking()
            .Where(r => r.TransactionId == transactionId)
            .ToListAsync());

        refunds.Should().ContainSingle();
        refunds[0].NetAmount.Should().Be(10_000m);
        refunds[0].Status.Should().Be(RefundStatus.Processed);

        var ledger = await client.GetJsonAsync($"/api/bookings/{transactionId}/ledger");
        ledger.GetProperty("totalRefunded").GetDecimal().Should().Be(10_000m);
    }

    [Fact]
    public async Task A_zero_refund_cancels_the_booking_and_creates_no_refund_row()
    {
        var client = await _fixture.CreateManagerClientAsync();
        var (transactionId, _) = await CreateBookingWithPaymentAsync(client, paid: 10_000m);

        var response = await client.CancelBookingAsync(transactionId, refundAmount: 0m);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var details = await client.GetBookingDetailsAsync(transactionId);
        details.GetProperty("status").GetString().Should().Be("Cancelled");

        var refundCount = await _fixture.QueryAsync(context =>
            context.Refunds.CountAsync(r => r.TransactionId == transactionId));

        refundCount.Should().Be(0, "a zero refund means \"no refund\" (DEC-024)");
    }

    private async Task<(int TransactionId, int CustomerId)> CreateBookingWithPaymentAsync(
        HttpClient client,
        decimal paid)
    {
        var customerId = await client.CreateCustomerAsync("Refund", "Scenario13");
        var transactionId = await client.CreateBookingAsync(customerId, 100, 500m);

        var payment = await client.RecordPaymentAsync(transactionId, paid);
        payment.StatusCode.Should().Be(HttpStatusCode.OK, await payment.DescribeAsync());

        return (transactionId, customerId);
    }
}
