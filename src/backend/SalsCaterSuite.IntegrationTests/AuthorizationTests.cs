using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 7 — Role-based authorization (Scenario 10 / DEC-017).
/// The backend enforces access; a Cashier cannot cancel, a Manager can, a Cashier can record a
/// payment, and anonymous callers are rejected with the uniform error contract.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AuthorizationTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public AuthorizationTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Cancellation_is_forbidden_for_a_cashier_and_allowed_for_a_manager()
    {
        var adminClient = await _fixture.CreateAdministratorClientAsync();
        var customerId = await adminClient.CreateCustomerAsync("Authorization", "Scenario10");
        var transactionId = await adminClient.CreateBookingAsync(customerId, 100, 450m);

        // ── Cashier is not authorized to cancel (403) ────────────────────────
        var cashierClient = await _fixture.CreateCashierClientAsync();
        var forbidden = await cashierClient.CancelBookingAsync(transactionId, refundAmount: 0m);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await forbidden.ReadErrorsAsync())
            .Should().Equal("You are not authorized to perform this action.");

        var afterForbidden = await adminClient.GetBookingDetailsAsync(transactionId);
        afterForbidden.GetProperty("status").GetString()
            .Should().Be("Pending", "a forbidden cancellation must not change the booking status");

        // ── Manager is authorized to cancel (200) ────────────────────────────
        var managerClient = await _fixture.CreateManagerClientAsync();
        var allowed = await managerClient.CancelBookingAsync(transactionId, refundAmount: 0m);

        allowed.StatusCode.Should().Be(HttpStatusCode.OK, await allowed.DescribeAsync());

        var afterAllowed = await adminClient.GetBookingDetailsAsync(transactionId);
        afterAllowed.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    [Fact]
    public async Task A_cashier_can_record_a_payment()
    {
        var adminClient = await _fixture.CreateAdministratorClientAsync();
        var customerId = await adminClient.CreateCustomerAsync("Cashier", "Payment");
        var transactionId = await adminClient.CreateBookingAsync(customerId, 100, 450m);

        var cashierClient = await _fixture.CreateCashierClientAsync();
        var response = await cashierClient.RecordPaymentAsync(transactionId, 5_000m);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());
    }

    [Fact]
    public async Task Anonymous_access_to_a_business_endpoint_is_rejected_with_401()
    {
        using var anonymousClient = _fixture.CreateClient();

        var response = await anonymousClient.GetAsync("/api/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.ReadErrorsAsync()).Should().Equal("Authentication is required.");

        // The same contract protects a protected write endpoint.
        var writeResponse = await anonymousClient.PostAsJsonAsync(
            "/api/bookings",
            new BookingPayload(1, 10, 100m));

        writeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await writeResponse.ReadErrorsAsync()).Should().Equal("Authentication is required.");
    }
}
