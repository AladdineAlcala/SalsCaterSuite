using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 6 — Invalid requests return 400, never 500 (Scenario 9 / DEC-014).
/// A rejected request must also leave no booking row behind.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BookingValidationTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public BookingValidationTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("noOfPerson = 0")]
    [InlineData("endDate earlier than startDate")]
    [InlineData("occasion longer than 150 characters")]
    public async Task Invalid_payloads_return_400_with_an_errors_array_and_create_no_booking(string caseName)
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("Validation", "Scenario9");

        var start = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

        var payload = caseName switch
        {
            "noOfPerson = 0" => new BookingPayload(customerId, NoOfPerson: 0, OverridePricePerHead: 450m),
            "endDate earlier than startDate" => new BookingPayload(customerId, NoOfPerson: 100, OverridePricePerHead: 450m)
            {
                StartDate = start,
                EndDate = start.AddHours(-2)
            },
            "occasion longer than 150 characters" => new BookingPayload(
                customerId, NoOfPerson: 100, OverridePricePerHead: 450m)
            {
                Occasion = new string('X', 151)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, null)
        };

        var before = await _fixture.QueryAsync(context => context.Bookings.CountAsync());

        var response = await client.PostBookingAsync(payload);

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            $"{caseName} must be rejected as a client error, never an unhandled 500");

        var errors = await response.ReadErrorsAsync();
        errors.Should().NotBeEmpty("the error contract returns an \"errors\" array (DEC-014)");

        var after = await _fixture.QueryAsync(context => context.Bookings.CountAsync());
        after.Should().Be(before, $"a rejected request must not persist a booking row ({caseName})");
    }

    [Fact]
    public async Task A_payload_with_a_non_existent_customer_is_rejected_without_persisting()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var missingCustomerId = await _fixture.QueryAsync(
            context => context.Customers.MaxAsync(c => (int?)c.CustomerId)) ?? 0;

        var before = await _fixture.QueryAsync(context => context.Bookings.CountAsync());

        var response = await client.PostBookingAsync(new BookingPayload(
            missingCustomerId + 1_000_000, NoOfPerson: 100, OverridePricePerHead: 450m));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errors = await response.ReadErrorsAsync();
        errors.Should().Contain(e => e.Contains("Customer not found", StringComparison.Ordinal));

        (await _fixture.QueryAsync(context => context.Bookings.CountAsync()))
            .Should().Be(before);
    }
}
