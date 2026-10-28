namespace SalsCaterSuite.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.IntegrationTests.Infrastructure;
using SalsCaterSuite.WebApi.Middleware;

/// <summary>
/// Verifies the operational log against the <b>real</b> API (REQ-OPS-01, specification §7).
/// Every call goes through the real HTTP surface of the test host, which runs against a
/// disposable SQL Server LocalDB database (DEC-019) — never the developer database.
/// <para>
/// The assertions read the rolling JSON log file the host wrote during the run, so they prove
/// what an operator would actually see in the file the Datadog Agent tails.
/// </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RequestLoggingEndpointTests
{
    private readonly IntegrationDatabaseFixture _fixture;

    public RequestLoggingEndpointTests(IntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    // ── AC-OPS-03 — the login call's password, username and JWT never reach the log ─────

    [Fact]
    public async Task Login_IsLoggedWithoutThePasswordTheUsernameAndTheJwt()
    {
        var client = _fixture.CreateClient();

        var before = CountEvents(e => Path(e) == "/api/auth/login" && Method(e) == "POST");

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = IntegrationTestWebApplicationFactory.AdminUserName, password = IntegrationTestWebApplicationFactory.AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var payload = await response.Content.ReadAsStringAsync();
        var token = JsonDocument.Parse(payload).RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrEmpty();

        var events = await WaitForEventsAsync(e => Path(e) == "/api/auth/login" && Method(e) == "POST", before + 1);
        var log = ReadLogText();

        // BR-21 / DEC-096 — the credential and the token are redacted by property name, and so is
        // the account identifier: in this deployment the username IS the administrator's e-mail
        // address. The sensitive-name set applies to the RECORD's own field names as well as to
        // the body's property names (BR-21.3), so the record's "UserName" field is redacted too.
        // The assertion is on the COMPLETE file content: the value must not appear in ANY field.
        log.Should().NotContain(IntegrationTestWebApplicationFactory.AdminPassword);
        log.Should().NotContain(IntegrationTestWebApplicationFactory.AdminUserName);
        log.Should().NotContain(token!);
        log.Should().Contain("***REDACTED***");

        var loginEvent = events
            .Where(e => Path(e) == "/api/auth/login" && Method(e) == "POST")
            .OrderByDescending(e => Timestamp(e))
            .First();

        loginEvent.GetProperty("EventType").GetString().Should().Be("HttpRequestCompleted");
        loginEvent.GetProperty("StatusCode").GetInt32().Should().Be(200);
        loginEvent.GetProperty("RequestBody").GetString().Should().Contain("***REDACTED***");
        loginEvent.GetProperty("RequestBody").GetString().Should().NotContain(IntegrationTestWebApplicationFactory.AdminUserName);
        loginEvent.GetProperty("ResponseBody").GetString().Should().Contain("***REDACTED***");
        loginEvent.GetProperty("ResponseBody").GetString().Should().NotContain(IntegrationTestWebApplicationFactory.AdminUserName);
        loginEvent.GetProperty("HasAuthorization").GetBoolean().Should().BeFalse();
    }

    // ── AC-OPS-01 — every endpoint produces exactly one record ──────────────

    [Fact]
    public async Task Dashboard_ProducesExactlyOneCompletedRecordWithTheRequestContext()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var before = CountEvents(e => Path(e) == "/api/dashboard");

        var response = await client.GetAsync("/api/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var events = await WaitForEventsAsync(e => Path(e) == "/api/dashboard", before + 1);

        var dashboardEvents = events.Where(e => Path(e) == "/api/dashboard").ToList();
        (dashboardEvents.Count - before).Should().Be(1, "exactly one record is written per request");

        var logEvent = dashboardEvents.OrderByDescending(e => Timestamp(e)).First();
        logEvent.GetProperty("EventType").GetString().Should().Be("HttpRequestCompleted");
        logEvent.GetProperty("Method").GetString().Should().Be("GET");
        logEvent.GetProperty("StatusCode").GetInt32().Should().Be(200);
        logEvent.GetProperty("TraceId").GetString().Should().NotBeNullOrEmpty();
        logEvent.GetProperty("ElapsedMs").GetDouble().Should().BeGreaterThanOrEqualTo(0d);
        logEvent.GetProperty("UserId").GetString().Should().NotBeNullOrEmpty();
        logEvent.GetProperty("UserName").GetString().Should().NotBeNullOrEmpty();
        logEvent.GetProperty("HasAuthorization").GetBoolean().Should().BeTrue();
        logEvent.GetProperty("ResponseBytes").GetInt64().Should().BeGreaterThan(0);
    }

    // ── AC-OPS-06 — a binary response is logged without its body ─────────────

    [Fact]
    public async Task XlsxExport_IsLoggedWithItsContentTypeAndByteCountButWithoutItsBody()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var before = CountEvents(e => Path(e) == "/api/customers/directory/export");

        var response = await client.GetAsync("/api/customers/directory/export");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);

        var events = await WaitForEventsAsync(e => Path(e) == "/api/customers/directory/export", before + 1);

        var exportEvent = events
            .Where(e => Path(e) == "/api/customers/directory/export")
            .OrderByDescending(e => Timestamp(e))
            .First();

        exportEvent.GetProperty("ResponseContentType").GetString()
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        exportEvent.GetProperty("ResponseBytes").GetInt64().Should().Be(bytes.Length);
        exportEvent.GetProperty("ResponseBody").GetString()
            .Should().Be($"[binary content omitted: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, {bytes.Length} bytes]");
        exportEvent.GetProperty("ResponseBody").GetString().Should().NotContain("PK");

        // AC-OPS-07 — the bytes the client received are the bytes the application produced.
        bytes.Take(2).Should().Equal((byte)'P', (byte)'K');
    }

    [Fact]
    public async Task TheStatementOfAccountPdf_IsLoggedWithItsContentTypeAndByteCountButWithoutItsBody()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var customerId = await client.CreateCustomerAsync();

        var before = CountEvents(e => Path(e) == $"/api/customers/{customerId}/soa");

        var response = await client.GetAsync($"/api/customers/{customerId}/soa");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);

        var events = await WaitForEventsAsync(e => Path(e) == $"/api/customers/{customerId}/soa", before + 1);

        var pdfEvent = events
            .Where(e => Path(e) == $"/api/customers/{customerId}/soa")
            .OrderByDescending(e => Timestamp(e))
            .First();

        pdfEvent.GetProperty("ResponseContentType").GetString().Should().Be("application/pdf");
        pdfEvent.GetProperty("ResponseBytes").GetInt64().Should().Be(bytes.Length);
        pdfEvent.GetProperty("ResponseBody").GetString()
            .Should().Be($"[binary content omitted: application/pdf, {bytes.Length} bytes]");
        pdfEvent.GetProperty("ResponseBody").GetString().Should().NotContain("%PDF");

        // AC-OPS-07 — the PDF the client received is the PDF the application produced.
        bytes.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');
    }

    [Fact]
    public async Task ThePackageSpecSheet_IsLoggedWithItsContentTypeAndByteCountButWithoutItsBody()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var packageId = await CreatePackageAsync(client);

        var before = CountEvents(e => Path(e) == $"/api/packages/{packageId}/spec-sheet");

        var response = await client.GetAsync($"/api/packages/{packageId}/spec-sheet");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);

        var events = await WaitForEventsAsync(e => Path(e) == $"/api/packages/{packageId}/spec-sheet", before + 1);

        var specSheetEvent = events
            .Where(e => Path(e) == $"/api/packages/{packageId}/spec-sheet")
            .OrderByDescending(e => Timestamp(e))
            .First();

        specSheetEvent.GetProperty("ResponseContentType").GetString().Should().Be("application/pdf");
        specSheetEvent.GetProperty("ResponseBytes").GetInt64().Should().Be(bytes.Length);
        specSheetEvent.GetProperty("ResponseBody").GetString()
            .Should().Be($"[binary content omitted: application/pdf, {bytes.Length} bytes]");
        specSheetEvent.GetProperty("ResponseBody").GetString().Should().NotContain("%PDF");
    }

    // ── AC-OPS-07 — the bytes the client receives are the application's bytes ──

    [Fact]
    public async Task AJsonResponse_IsDeliveredByteForByte()
    {
        var client = await _fixture.CreateAdministratorClientAsync();
        var packageId = await CreatePackageAsync(client);

        var before = CountEvents(e => Path(e) == $"/api/packages/{packageId}");

        // The SINGLE-tier detail endpoint is used on purpose: the capture limit is 8 KB, and the
        // catalogue list grows with every tier the suite creates, so the list response is not a
        // reliable subject for the "not truncated" assertion. One tier is always small, and the
        // property under test — the bytes the client receives are the application's bytes — does
        // not depend on the subject's size.
        var response = await client.GetAsync($"/api/packages/{packageId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);

        var events = await WaitForEventsAsync(e => Path(e) == $"/api/packages/{packageId}", before + 1);

        var jsonEvent = events
            .Where(e => Path(e) == $"/api/packages/{packageId}")
            .OrderByDescending(e => Timestamp(e))
            .First();

        // The recorded byte count is the number of bytes the response carried, and the recorded
        // body is the body the client received — the capture neither truncated nor altered it.
        jsonEvent.GetProperty("ResponseBytes").GetInt64().Should().Be(bytes.Length);
        jsonEvent.GetProperty("ResponseBodyTruncated").GetBoolean().Should().BeFalse();
        jsonEvent.GetProperty("ResponseBody").GetString()
            .Should().Be(JsonSerializer.Serialize(
                JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes)).RootElement,
                new JsonSerializerOptions { WriteIndented = false }));
    }

    // ── AC-OPS-12 — a 400 is logged with its { "errors": [...] } envelope ────

    [Fact]
    public async Task ValidationFailure_IsLoggedWithItsStatusAndItsErrorEnvelope()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var before = CountEvents(e => Path(e) == "/api/customers" && Method(e) == "POST");

        var response = await client.PostAsJsonAsync("/api/customers", new { lastName = "", firstName = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var errors = await response.ReadErrorsAsync();
        errors.Should().NotBeEmpty();

        var events = await WaitForEventsAsync(e => Path(e) == "/api/customers" && Method(e) == "POST", before + 1);

        var failureEvent = events
            .Where(e => Path(e) == "/api/customers" && Method(e) == "POST")
            .OrderByDescending(e => Timestamp(e))
            .First();

        failureEvent.GetProperty("EventType").GetString().Should().Be("HttpRequestCompleted");
        failureEvent.GetProperty("StatusCode").GetInt32().Should().Be(400);
        failureEvent.GetProperty("ResponseBody").GetString().Should().Contain("errors");
        failureEvent.GetProperty("ResponseBody").GetString().Should().Contain(errors[0]);

        // A 4xx is a client error: the record is written at the configured client-error level.
        failureEvent.GetProperty("@l").GetString().Should().Be("Warning");
    }

    // ── AC-OPS-02 — an unhandled fault is logged at Error with its stack trace ─

    [Fact]
    public async Task TheTestOnlyFaultEndpoint_IsLoggedAtErrorWithItsStackTrace()
    {
        // The fault is raised by a test-only endpoint (specification §7) — no production route is
        // added for it.
        var client = _fixture.CreateClient();

        var before = CountEvents(e => Path(e) == "/api/test-operational-logging/fault");

        var response = await client.GetAsync("/api/test-operational-logging/fault");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var events = await WaitForEventsAsync(
            e => Path(e) == "/api/test-operational-logging/fault" && HasErrorType(e),
            before + 1);

        var failureEvent = events
            .Where(e => Path(e) == "/api/test-operational-logging/fault" && HasErrorType(e))
            .OrderByDescending(e => Timestamp(e))
            .First();

        failureEvent.GetProperty("EventType").GetString().Should().Be("HttpRequestFailed");
        failureEvent.GetProperty("@l").GetString().Should().Be("Error");
        failureEvent.GetProperty("ErrorType").GetString().Should().Be("InvalidOperationException");
        failureEvent.GetProperty("ErrorMessage").GetString().Should().Contain("A test-only unhandled fault.");
        failureEvent.GetProperty("StatusCode").GetInt32().Should().Be(500);

        // The exception object carries the stack trace.
        failureEvent.GetProperty("@x").GetString().Should().Contain("InvalidOperationException");
        failureEvent.GetProperty("@x").GetString().Should().Contain("at SalsCaterSuite");
    }

    [Fact]
    public async Task UnhandledFault_IsLoggedAtErrorWithItsStackTrace()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        var customerId = await client.CreateCustomerAsync();

        var before = CountEvents(e => Path(e) == "/api/bookings" && Method(e) == "POST");

        // A price-per-head of decimal.MaxValue multiplied by two guests overflows the decimal
        // arithmetic inside the domain factory: a genuine unhandled fault, not a domain rule
        // violation (which the domain exception handler would turn into a 400).
        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            customerId,
            noOfPerson = 2,
            overridePricePerHead = decimal.MaxValue,
            bookType = "Inside",
            typeOfService = "Catering",
            occasion = "Integration Event",
            venue = "Integration Hall",
            startDate = DateTime.UtcNow.Date.AddDays(30).AddHours(10),
            endDate = DateTime.UtcNow.Date.AddDays(30).AddHours(14)
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var events = await WaitForEventsAsync(
            e => Path(e) == "/api/bookings" && Method(e) == "POST" && HasErrorType(e),
            before + 1);

        var failureEvent = events
            .Where(e => Path(e) == "/api/bookings" && Method(e) == "POST" && HasErrorType(e))
            .OrderByDescending(e => Timestamp(e))
            .First();

        failureEvent.GetProperty("EventType").GetString().Should().Be("HttpRequestFailed");
        failureEvent.GetProperty("@l").GetString().Should().Be("Error");
        failureEvent.GetProperty("ErrorType").GetString().Should().Be("OverflowException");
        failureEvent.GetProperty("ErrorMessage").GetString().Should().NotBeNullOrEmpty();
        failureEvent.GetProperty("StatusCode").GetInt32().Should().Be(500);

        // The exception object carries the stack trace.
        failureEvent.GetProperty("@x").GetString().Should().Contain("OverflowException");
        failureEvent.GetProperty("@x").GetString().Should().Contain("at SalsCaterSuite");
    }

    // ── AC-OPS-15 — the log file is one JSON object per line ─────────────────

    [Fact]
    public async Task TheLogFile_IsOneJsonObjectPerLine()
    {
        var client = await _fixture.CreateAdministratorClientAsync();

        await client.GetAsync("/api/dashboard");

        await WaitForEventsAsync(e => Path(e) == "/api/dashboard", 1);

        var lines = ReadLogLines();

        lines.Should().NotBeEmpty();
        lines.Should().OnlyContain(line => line.TrimStart().StartsWith("{") && line.TrimEnd().EndsWith("}"));

        foreach (var line in lines)
        {
            var act = () => JsonDocument.Parse(line);
            act.Should().NotThrow("the Datadog Agent parses every line as a JSON object");
        }
    }

    // ── AC-OPS-11 — every line raised while the request is handled carries the same TraceId ──

    [Fact]
    public async Task EveryLogLineRaisedWhileTheRequestIsHandledCarriesTheSameTraceId()
    {
        // The test-only endpoint raises a SECOND log line inside the request, through the ordinary
        // ILogger<T> an application uses. That line must carry the request event's TraceId, so an
        // operator can join the request event to the error that explains it.
        var client = _fixture.CreateClient();

        var before = CountEvents(e => Path(e) == "/api/test-operational-logging/correlation");

        var response = await client.GetAsync("/api/test-operational-logging/correlation");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.DescribeAsync());

        var events = await WaitForEventsAsync(
            e => Path(e) == "/api/test-operational-logging/correlation" && EventTypeOf(e) == "HttpRequestCompleted",
            before + 1);

        var requestEvent = events
            .Where(e => Path(e) == "/api/test-operational-logging/correlation" && EventTypeOf(e) == "HttpRequestCompleted")
            .OrderByDescending(e => Timestamp(e))
            .First();

        var traceId = requestEvent.GetProperty("TraceId").GetString();
        traceId.Should().NotBeNullOrEmpty();

        // The second line — the one the endpoint raised — carries the same correlation id.
        var raisedLines = events
            .Where(e => e.TryGetProperty("@m", out var message)
                && message.GetString() == "A second line raised while the request was handled.")
            .ToList();

        raisedLines.Should().ContainSingle();
        TraceIdOf(raisedLines.Single()).Should().Be(traceId);
    }

    // ── The exception-handler registration order (specification §4.4) ─────────

    [Fact]
    public void TheDomainExceptionHandlerIsRegisteredBeforeTheRequestLoggingHandler()
    {
        // The chain stops at the first handler that answers, so a domain rule violation is
        // answered by the domain handler — with the approved 400 { "errors": [...] } envelope —
        // and is never recorded as a failure event.
        var handlers = _fixture.Factory.Services.GetServices<IExceptionHandler>().ToList();

        handlers.Should().HaveCountGreaterThanOrEqualTo(2);
        handlers[0].Should().BeOfType<DomainExceptionHandler>();
        handlers[1].Should().BeOfType<RequestLoggingExceptionHandler>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<int> CreatePackageAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/packages", new
        {
            packageType = "Regular",
            description = "Integration Buffet Tier",
            amountPax = 1500m,
            minPax = 50,
            numPaxId = 4
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.DescribeAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("packageId").GetInt32();
    }

    private static string Path(JsonElement logEvent) =>
        logEvent.TryGetProperty("Path", out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string Method(JsonElement logEvent) =>
        logEvent.TryGetProperty("Method", out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static bool HasErrorType(JsonElement logEvent) =>
        logEvent.TryGetProperty("ErrorType", out var value) && value.GetString() is not null;

    private static string? EventTypeOf(JsonElement logEvent) =>
        logEvent.TryGetProperty("EventType", out var value) ? value.GetString() : null;

    private static string? TraceIdOf(JsonElement logEvent) =>
        logEvent.TryGetProperty("TraceId", out var value) ? value.GetString() : null;

    private static DateTimeOffset Timestamp(JsonElement logEvent) =>
        logEvent.TryGetProperty("@t", out var value) && value.GetString() is { } text
            ? DateTimeOffset.Parse(text)
            : DateTimeOffset.MinValue;

    private int CountEvents(Func<JsonElement, bool> predicate) =>
        ReadLogEvents(_fixture.Factory.LogDirectory).Count(predicate);

    private string ReadLogText() =>
        string.Join(Environment.NewLine, ReadLogLines());

    private IReadOnlyList<string> ReadLogLines()
    {
        var directory = _fixture.Factory.LogDirectory;

        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(directory, "*.log")
            .SelectMany(ReadAllLinesShared)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    /// <summary>
    /// Reads the rolling log file while the Serilog file sink still holds its write handle —
    /// the file is opened with a shared read so the sink can keep writing while the test reads.
    /// </summary>
    private static IReadOnlyList<string> ReadAllLinesShared(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd()
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
    }

    private static IReadOnlyList<JsonElement> ReadLogEvents(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<JsonElement>();
        }

        var events = new List<JsonElement>();

        foreach (var line in Directory.GetFiles(directory, "*.log").SelectMany(ReadAllLinesShared))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                events.Add(document.RootElement.Clone());
            }
            catch (JsonException)
            {
                // A partially written line is not a failure of the feature.
            }
        }

        return events;
    }

    private async Task<IReadOnlyList<JsonElement>> WaitForEventsAsync(
        Func<JsonElement, bool> predicate,
        int minimumCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        IReadOnlyList<JsonElement> events = Array.Empty<JsonElement>();

        while (DateTime.UtcNow < deadline)
        {
            events = ReadLogEvents(_fixture.Factory.LogDirectory);
            if (events.Count(predicate) >= minimumCount)
            {
                return events;
            }

            await Task.Delay(100);
        }

        return events;
    }
}
