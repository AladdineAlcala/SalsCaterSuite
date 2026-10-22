using System.Net;
using FluentAssertions;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 9 — Accounts receivable reconciliation on real SQL Server (Scenario 14 / DEC-025).
///
/// This test is the proof that the derived-table receivables projection in
/// <c>GetCustomersQuery</c> translates to SQL Server: EF Core cannot client-evaluate a projection
/// containing a correlated <c>SUM</c> over a navigation collection, so an untranslatable query
/// would throw and surface as <c>500</c>. A <c>200</c> with the correct total therefore proves the
/// aggregation ran in the database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ReceivablesTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public ReceivablesTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Receivables_keep_only_unpaid_balances_of_non_cancelled_bookings()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Receivables", "Scenario14");

        // A — net amount due ₱50,000.00, ₱20,000.00 collected  → ₱30,000.00 receivable
        var bookingA = await client.CreateBookingAsync(customerId, 100, 500m);
        // B — net amount due ₱30,000.00, fully settled          → contributes ₱0.00
        var bookingB = await client.CreateBookingAsync(customerId, 100, 300m);
        // C — net amount due ₱40,000.00, nothing collected      → ₱40,000.00 receivable
        var bookingC = await client.CreateBookingAsync(customerId, 100, 400m);

        (await client.RecordPaymentAsync(bookingA, 20_000m)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.RecordPaymentAsync(bookingB, 30_000m)).StatusCode.Should().Be(HttpStatusCode.OK);

        // D — cancelled event with ₱10,000.00 collected → must be excluded entirely
        var bookingD = await client.CreateBookingAsync(customerId, 100, 100m);
        (await client.RecordPaymentAsync(bookingD, 10_000m)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.CancelBookingAsync(bookingD, refundAmount: 0m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetAsync("/api/customers");
        listResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the receivables projection must translate to SQL Server — a client-evaluated query " +
            "would throw and surface as 500");

        var customerElement = await client.GetCustomerByIdFromListAsync(customerId);
        customerElement.GetProperty("totalReceivables").GetDecimal().Should().Be(
            70_000m, "₱30,000.00 (A) + ₱0.00 (B) + ₱40,000.00 (C); the cancelled booking (D) is excluded (DEC-025)");

        // The total must equal the sum of the contributing bookings' ledger balance due.
        var ledgerBalances = new List<decimal>();
        foreach (var transactionId in new[] { bookingA, bookingB, bookingC })
        {
            var ledger = await client.GetJsonAsync($"/api/bookings/{transactionId}/ledger");
            ledgerBalances.Add(ledger.GetProperty("balanceDue").GetDecimal());
        }

        ledgerBalances.Sum().Should().Be(
            customerElement.GetProperty("totalReceivables").GetDecimal(),
            "dashboard/statement totals must reconcile with the per-booking ledger (DEC-025)");

        ledgerBalances.Should().OnlyContain(b => b >= 0m);
        customerElement.GetProperty("totalBookings").GetInt32().Should().Be(3);
    }
}
