using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Tests 4, 5 and 10 — Authoritative payment figures (Scenarios 7, 8 and 15; DEC-012, DEC-015).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PaymentLedgerReconciliationTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public PaymentLedgerReconciliationTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>Test 4 — Scenario 7: booking details and ledger reconcile after a payment.</summary>
    [Fact]
    public async Task Recording_a_payment_keeps_booking_details_and_the_ledger_reconciled()
    {
        var client = await _fixture.CreateManagerClientAsync();
        var customerId = await client.CreateCustomerAsync("Payment", "Scenario7");

        // 100 pax × ₱450.00 = ₱45,000.00 net amount due.
        var transactionId = await client.CreateBookingAsync(customerId, 100, 450m);

        var paymentResponse = await client.RecordPaymentAsync(transactionId, 15_000m, "GCash");
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK, await paymentResponse.DescribeAsync());

        var receipt = await paymentResponse.Content.ReadFromJsonAsync<JsonElement>();
        receipt.GetProperty("paymentNo").GetString().Should().MatchRegex(
            @"^OR-\d{8}-\d{4}$", "Scenario 7 requires the OR-YYYYMMDD-XXXX receipt format");

        var details = await client.GetBookingDetailsAsync(transactionId);
        details.GetProperty("amountPaid").GetDecimal().Should().Be(15_000m);
        details.GetProperty("balance").GetDecimal().Should().Be(30_000m);
        details.GetProperty("status").GetString().Should().Be("DepositPaid");

        var ledger = await client.GetJsonAsync($"/api/bookings/{transactionId}/ledger");
        ledger.GetProperty("totalPaid").GetDecimal().Should().Be(15_000m);
        ledger.GetProperty("balanceDue").GetDecimal().Should().Be(30_000m);

        details.GetProperty("amountPaid").GetDecimal()
            .Should().Be(ledger.GetProperty("totalPaid").GetDecimal(),
                "the sum of Payment rows is the single authoritative amount paid (DEC-012)");
    }

    /// <summary>Test 5 — Scenario 8: both ledger endpoints return identical authoritative values.</summary>
    [Fact]
    public async Task Both_ledger_endpoints_return_the_same_authoritative_values()
    {
        var client = await _fixture.CreateManagerClientAsync();
        var customerId = await client.CreateCustomerAsync("Ledger", "Scenario8");

        var transactionId = await client.CreateBookingAsync(customerId, 100, 500m);

        var paymentResponse = await client.RecordPaymentAsync(transactionId, 10_000m);
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK, await paymentResponse.DescribeAsync());

        var refundOf = 5_000m;
        var cancelResponse = await client.CancelBookingAsync(transactionId, refundOf);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK, await cancelResponse.DescribeAsync());

        var bookingsLedger = await client.GetJsonAsync($"/api/bookings/{transactionId}/ledger");
        var paymentsLedger = await client.GetJsonAsync($"/api/payments/ledger/{transactionId}");

        bookingsLedger.GetProperty("contractAmount").GetDecimal()
            .Should().Be(paymentsLedger.GetProperty("contractAmount").GetDecimal());
        bookingsLedger.GetProperty("totalPaid").GetDecimal()
            .Should().Be(paymentsLedger.GetProperty("totalPaid").GetDecimal());
        bookingsLedger.GetProperty("totalRefunded").GetDecimal()
            .Should().Be(paymentsLedger.GetProperty("totalRefunded").GetDecimal())
            .And.Be(refundOf);
        bookingsLedger.GetProperty("balanceDue").GetDecimal()
            .Should().Be(paymentsLedger.GetProperty("balanceDue").GetDecimal());
        bookingsLedger.GetProperty("isFullyPaid").GetBoolean()
            .Should().Be(paymentsLedger.GetProperty("isFullyPaid").GetBoolean());

        bookingsLedger.GetProperty("payments").GetArrayLength()
            .Should().Be(1).And.Be(paymentsLedger.GetProperty("payments").GetArrayLength());
    }

    /// <summary>
    /// Test 10 — Scenario 15: an overpayment is <b>refused</b>, so it can never produce a
    /// negative balance.
    /// <para>
    /// REQ-BKG-05 enforces BR-05's overpayment prohibition (DEC-118, which closes RSK-057):
    /// a payment that would take the total above the net amount due is refused with a
    /// <c>400</c> and <b>nothing</b> is written — no payment row, no status change and no
    /// change to the cached <c>AmountPaid</c>. The balance therefore stays at the amount that
    /// is actually owed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_overpayment_leaves_no_negative_balance()
    {
        var client = await _fixture.CreateCashierClientAsync();
        var customerId = await client.CreateCustomerAsync("Overpayment", "Scenario15");

        // 100 pax × ₱200.00 = ₱20,000.00 net amount due; ₱25,000.00 would exceed it.
        var transactionId = await client.CreateBookingAsync(customerId, 100, 200m);

        var paymentResponse = await client.RecordPaymentAsync(transactionId, 25_000m);
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await paymentResponse.ReadErrorsAsync()).Should().Contain(
            "The payment would take the total collected above the net amount due of the booking.");

        var ledger = await client.GetJsonAsync($"/api/bookings/{transactionId}/ledger");

        ledger.GetProperty("totalPaid").GetDecimal().Should().Be(0m, "nothing is written");
        ledger.GetProperty("balanceDue").GetDecimal().Should().Be(20_000m, "a balance is never negative");
        ledger.GetProperty("isFullyPaid").GetBoolean().Should().BeFalse();
        ledger.GetProperty("payments").GetArrayLength().Should().Be(0, "no payment row is written");
    }
}
