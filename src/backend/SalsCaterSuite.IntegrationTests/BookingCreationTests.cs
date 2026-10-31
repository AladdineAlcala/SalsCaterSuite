using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 3 — Booking creation persists a unique, formatted booking number (Scenario 6 / DEC-016).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BookingCreationTests
{
    private static readonly Regex BookingNumberPattern = new(@"^BK-\d{8}-\d{5}$", RegexOptions.Compiled);

    private readonly IntegrationDatabaseFixture _fixture;

    public BookingCreationTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Creating_a_booking_returns_201_with_a_transaction_id_and_a_unique_formatted_number()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync("BookingNumber", "Scenario6");

        var response = await client.PostBookingAsync(
            new BookingPayload(customerId, NoOfPerson: 100, OverridePricePerHead: 450m));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());
        response.Headers.Location.Should().NotBeNull("Scenario 6 requires a Location header");

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        // The documented 201 body is the named CreateBookingResponse. REQ-BKG-05 extended
        // it additively with the booking number (BR-09) and the status (contract §1 and
        // §14.4); the transactionId field is unchanged.
        body.EnumerateObject().Select(p => p.Name).Should().Equal(
            new[] { "transactionId", "bookingNumber", "status" },
            "the documented 201 body is exactly { transactionId, bookingNumber, status }");

        var firstTransactionId = body.GetProperty("transactionId").GetInt32();
        body.GetProperty("bookingNumber").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("status").GetString().Should().Be("Pending", "a booking is created as a draft by default");

        var firstDetails = await client.GetBookingDetailsAsync(firstTransactionId);
        var firstNumber = firstDetails.GetProperty("bookingNumber").GetString();

        firstNumber.Should().NotBeNullOrWhiteSpace();
        firstNumber.Should().MatchRegex(
            BookingNumberPattern.ToString(), "DEC-016 fixes the format BK-YYYYMMDD-NNNNN");
        firstNumber.Should().EndWith($"-{firstTransactionId:D5}");

        // A second booking on the same day must receive its own number.
        var secondTransactionId = await client.CreateBookingAsync(customerId, 100, 450m);
        var secondDetails = await client.GetBookingDetailsAsync(secondTransactionId);
        var secondNumber = secondDetails.GetProperty("bookingNumber").GetString();

        secondNumber.Should().MatchRegex(BookingNumberPattern.ToString());
        secondNumber.Should().NotBe(firstNumber, "the booking number is unique by construction (DEC-016)");
    }
}
