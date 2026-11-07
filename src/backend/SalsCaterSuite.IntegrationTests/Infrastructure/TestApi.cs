using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace SalsCaterSuite.IntegrationTests.Infrastructure;

/// <summary>Payload for <c>POST /api/bookings</c>. Enum values travel as string names (contract §8).</summary>
internal sealed record BookingPayload(
    int CustomerId,
    int NoOfPerson,
    decimal OverridePricePerHead,
    string BookType = "Inside",
    string TypeOfService = "Catering",
    string Occasion = "Integration Event",
    string Venue = "Integration Hall",
    int? PackageId = null)
{
    public DateTimeOffset StartDate { get; init; } = new DateTimeOffset(DateTime.Now.Date.AddDays(30).AddHours(10), TimeSpan.FromHours(8));
    public DateTimeOffset EndDate { get; init; } = new DateTimeOffset(DateTime.Now.Date.AddDays(30).AddHours(14), TimeSpan.FromHours(8));

    // ── The REQ-BKG-05 extensions (contract §14.4) ────────────────────────────
    public int? ExtendedAreaId { get; init; }
    public bool ApplyExtendedAmount { get; init; }
    public TimeSpan? CallTime { get; init; }
    public TimeSpan? PackoutTime { get; init; }
    public string? DiscountCode { get; init; }
    public decimal? DepositAmount { get; init; }
    public bool Confirm { get; init; }
    public InitialPaymentPayload? InitialPayment { get; init; }
}

/// <summary>The initial payment of a confirmed booking (contract §14.4).</summary>
internal sealed record InitialPaymentPayload(
    decimal AmountPaid,
    string PaymentMeans = "Cash",
    string? CheckNo = null,
    string? Notes = null,
    string? Particular = "Initial payment");

internal sealed record PaymentPayload(
    int TransactionId,
    decimal AmountPaid,
    string PaymentMeans = "Cash",
    string Particular = "Integration payment");

internal sealed record CustomerPayload(
    string LastName = "Integration",
    string FirstName = "Customer",
    string? Middle = null,
    string? Address = "1 Integration Way",
    string? Contact1 = "09170000000",
    string? Contact2 = null,
    string? Company = null);

internal sealed record CancelPayload(string Reason = "Integration cancellation", decimal? RefundAmount = null);

/// <summary>
/// Thin helpers over the real HTTP contract. Nothing here fabricates responses — every call goes
/// through the API surface exactly as a client would.
/// </summary>
internal static class TestApi
{
    public static async Task<int> CreateCustomerAsync(
        this HttpClient client,
        string lastName = "Integration",
        string firstName = "Customer")
    {
        var response = await client.PostAsJsonAsync("/api/customers", new CustomerPayload(lastName, firstName));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("customerId").GetInt32();
    }

    public static async Task<HttpResponseMessage> PostBookingAsync(
        this HttpClient client,
        BookingPayload payload) =>
        await client.PostAsJsonAsync("/api/bookings", payload);

    /// <summary>
    /// The automatic headcount-band discount of BR-38 (DEC-136), computed here with the test's
    /// own arithmetic so an expectation never round-trips through production code.
    /// <para>
    /// The bands are half-open and the 90-99 gap is real: 80-89 heads earn 10.00 per head and
    /// 100-999 heads earn 20.00 per head. Nothing else earns anything.
    /// </para>
    /// </summary>
    public static decimal BandDiscountFor(int noOfPerson) => noOfPerson switch
    {
        >= 80 and <= 89 => 10m * noOfPerson,
        >= 100 and <= 999 => 20m * noOfPerson,
        _ => 0m
    };

    /// <summary>
    /// The frozen net amount due a booking is created with: the headcount times the rate per
    /// guest, less the automatic band discount (BR-38). There is no surcharge and no discount
    /// code on the bookings these helpers create.
    /// </summary>
    public static decimal NetAmountDueFor(int noOfPerson, decimal pricePerHead) =>
        noOfPerson * pricePerHead - BandDiscountFor(noOfPerson);

    /// <summary>
    /// Creates a booking whose frozen net amount due is
    /// <see cref="NetAmountDueFor"/> of <paramref name="noOfPerson"/> and
    /// <paramref name="pricePerHead"/>, and returns its transaction id.
    /// </summary>
    public static async Task<int> CreateBookingAsync(
        this HttpClient client,
        int customerId,
        int noOfPerson,
        decimal pricePerHead)
    {
        var response = await client.PostBookingAsync(new BookingPayload(customerId, noOfPerson, pricePerHead));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("transactionId").GetInt32();
    }

    /// <summary>
    /// Creates a booking with an explicit event date, so a test can control the booking's
    /// position inside a reporting period.
    /// </summary>
    public static async Task<int> CreateBookingAsync(
        this HttpClient client,
        int customerId,
        int noOfPerson,
        decimal pricePerHead,
        DateOnly eventDate)
    {
        var payload = new BookingPayload(customerId, noOfPerson, pricePerHead)
        {
            StartDate = new DateTimeOffset(DateTime.SpecifyKind(eventDate.ToDateTime(new TimeOnly(10, 0)), DateTimeKind.Unspecified), TimeSpan.FromHours(8)),
            EndDate = new DateTimeOffset(DateTime.SpecifyKind(eventDate.ToDateTime(new TimeOnly(14, 0)), DateTimeKind.Unspecified), TimeSpan.FromHours(8))
        };

        var response = await client.PostBookingAsync(payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("transactionId").GetInt32();
    }

    public static async Task<HttpResponseMessage> RecordPaymentAsync(
        this HttpClient client,
        int transactionId,
        decimal amount,
        string paymentMeans = "Cash") =>
        await client.PostAsJsonAsync(
            "/api/payments",
            new PaymentPayload(transactionId, amount, paymentMeans));

    public static async Task<HttpResponseMessage> CancelBookingAsync(
        this HttpClient client,
        int transactionId,
        decimal? refundAmount,
        string reason = "Integration cancellation") =>
        await client.PostAsJsonAsync(
            $"/api/bookings/{transactionId}/cancel",
            new CancelPayload(reason, refundAmount));

    public static async Task<JsonElement> GetBookingDetailsAsync(this HttpClient client, int transactionId)
    {
        var response = await client.GetAsync($"/api/bookings/{transactionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> GetCustomerByIdFromListAsync(
        this HttpClient client,
        int customerId)
    {
        var customers = await client.GetJsonAsync("/api/customers");
        foreach (var customer in customers.EnumerateArray())
        {
            if (customer.GetProperty("customerId").GetInt32() == customerId)
            {
                return customer;
            }
        }

        throw new InvalidOperationException(
            $"Customer {customerId} was not returned by GET /api/customers.");
    }

    public static async Task<string> DescribeAsync(this HttpResponseMessage response) =>
        $"Expected a successful response but the API returned {(int)response.StatusCode} " +
        $"({response.StatusCode}). Body: {await response.Content.ReadAsStringAsync()}";

    /// <summary>Reads the uniform <c>{ "errors": [ ... ] }</c> failure contract.</summary>
    public static async Task<string[]> ReadErrorsAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();
    }
}
