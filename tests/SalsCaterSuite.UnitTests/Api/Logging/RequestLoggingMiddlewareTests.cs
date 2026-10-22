namespace SalsCaterSuite.UnitTests.Api.Logging;

using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using SalsCaterSuite.WebApi.Logging;
using SalsCaterSuite.WebApi.Middleware;
using Xunit;

/// <summary>
/// Verifies the operational request record itself (REQ-OPS-01, AC-OPS-01, AC-OPS-02, AC-OPS-05,
/// AC-OPS-06, AC-OPS-07, AC-OPS-08, AC-OPS-11, AC-OPS-12, AC-OPS-14).
/// </summary>
public class RequestLoggingMiddlewareTests
{
    private const string TraceId = "trace-0001";

    // ── The event shape ─────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTheRequestSucceeds_WritesExactlyOneCompletedEvent()
    {
        var harness = new Harness { ResponseBody = "{\"ok\":true}" };

        await RunAsync(harness);

        harness.Sink.Events.Should().HaveCount(1, "exactly one record is written per request (AC-OPS-01)");

        var logEvent = harness.Sink.Events[0];
        logEvent.Level.Should().Be(LogEventLevel.Information);
        logEvent.Text("EventType").Should().Be(RequestLoggingMiddleware.CompletedEventName);
        logEvent.Text("TraceId").Should().Be(TraceId);
        logEvent.Text("Method").Should().Be("GET");
        logEvent.Text("Path").Should().Be("/api/customers");
        logEvent.Value("StatusCode").Should().Be(200);
        logEvent.Value("ElapsedMs").Should().BeOfType<double>();
        logEvent.Flag("HasAuthorization").Should().BeFalse();
        logEvent.Flag("RequestBodyTruncated").Should().BeFalse();
        logEvent.Flag("ResponseBodyTruncated").Should().BeFalse();
        logEvent.Number("ResponseBytes").Should().Be(11L);
        logEvent.Text("ResponseBody").Should().Be("{\"ok\":true}");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestIsAWrite_RecordsTheRequestBody()
    {
        var harness = new Harness
        {
            Method = "POST",
            Path = "/api/auth/login",
            RequestBody = "{\"username\":\"admin\",\"password\":\"hunter2\"}",
            RequestContentType = "application/json"
        };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Text("Method").Should().Be("POST");
        logEvent.Text("RequestContentType").Should().Be("application/json");
        logEvent.Text("RequestBody").Should().Contain(SensitiveDataRedactor.RedactionMarker);
        logEvent.Text("RequestBody").Should().NotContain("hunter2");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestBodyIsNotJson_RecordsTheNonJsonMarker()
    {
        // BR-21.6 / AC-OPS-08.
        var harness = new Harness
        {
            Method = "POST",
            RequestBody = "username=admin&password=hunter2",
            RequestContentType = "application/x-www-form-urlencoded"
        };

        await RunAsync(harness);

        harness.Sink.Events.Single().Text("RequestBody").Should().Be(SensitiveDataRedactor.NonJsonBodyMarker);
    }

    [Fact]
    public async Task InvokeAsync_WhenThereIsNoRequestBody_RecordsNoRequestBody()
    {
        var harness = new Harness();

        await RunAsync(harness);

        harness.Sink.Events.Single().Text("RequestBody").Should().BeNull();
    }

    // ── The level mapping (specification §4.1) ──────────────────────────────

    [Theory]
    [InlineData(200, LogEventLevel.Information)]
    [InlineData(204, LogEventLevel.Information)]
    [InlineData(302, LogEventLevel.Information)]
    [InlineData(400, LogEventLevel.Warning)]
    [InlineData(404, LogEventLevel.Warning)]
    [InlineData(500, LogEventLevel.Error)]
    [InlineData(503, LogEventLevel.Error)]
    public async Task InvokeAsync_MapsTheStatusCodeToTheConfiguredLevel(int statusCode, LogEventLevel expected)
    {
        var harness = new Harness { ResponseStatusCode = statusCode };

        await RunAsync(harness);

        harness.Sink.Events.Single().Level.Should().Be(expected);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheLevelsAreReconfigured_UsesTheConfiguredLevels()
    {
        var harness = new Harness
        {
            ResponseStatusCode = 400,
            Options = { ClientErrorLevel = LogEventLevel.Error }
        };

        await RunAsync(harness);

        harness.Sink.Events.Single().Level.Should().Be(LogEventLevel.Error);
    }

    // ── The elapsed time ────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_RecordsTheElapsedTimeOfThePipeline()
    {
        var harness = new Harness
        {
            OnPipeline = _ => Thread.Sleep(60)
        };

        await RunAsync(harness);

        harness.Sink.Events.Single().Measure("ElapsedMs").Should().BeGreaterThanOrEqualTo(50d);
    }

    // ── Bounded capture (BR-22) ─────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTheRequestBodyExceedsTheCap_TruncatesAndFlagsIt()
    {
        var harness = new Harness
        {
            Method = "POST",
            RequestBody = "{\"note\":\"" + new string('a', 200) + "-TAIL-SECRET\"}",
            RequestContentType = "application/json",
            Options = { MaxBodyLengthBytes = 64 }
        };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Flag("RequestBodyTruncated").Should().BeTrue("the body was longer than the cap");
        logEvent.Text("RequestBody").Should().Contain(RequestLoggingMiddleware.BodyTruncatedMarker);
        logEvent.Text("RequestBody").Should().NotContain("TAIL-SECRET",
            "the capture never reads past the cap");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheResponseBodyExceedsTheCap_TruncatesAndFlagsIt()
    {
        var harness = new Harness
        {
            ResponseBody = "{\"note\":\"" + new string('a', 200) + "-TAIL-SECRET\"}",
            Options = { MaxBodyLengthBytes = 64 }
        };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Flag("ResponseBodyTruncated").Should().BeTrue();
        logEvent.Text("ResponseBody").Should().Contain(RequestLoggingMiddleware.BodyTruncatedMarker);
        logEvent.Text("ResponseBody").Should().NotContain("TAIL-SECRET");
        logEvent.Number("ResponseBytes").Should().Be((long)harness.ResponseBody!.Length,
            "the whole response is still measured");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheBodyFitsTheCap_DoesNotTruncate()
    {
        var harness = new Harness
        {
            ResponseBody = "{\"ok\":true}",
            Options = { MaxBodyLengthBytes = 8192 }
        };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Flag("RequestBodyTruncated").Should().BeFalse();
        logEvent.Flag("ResponseBodyTruncated").Should().BeFalse();
    }

    // ── Binary responses (BR-21.7 / AC-OPS-06) ──────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTheResponseIsBinary_RecordsTheContentTypeAndByteCountOnly()
    {
        var harness = new Harness
        {
            ResponseContentType = "application/pdf",
            ResponseBody = "%PDF-1.7 binary payload"
        };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Text("ResponseContentType").Should().Be("application/pdf");
        logEvent.Number("ResponseBytes").Should().Be((long)harness.ResponseBody!.Length);
        logEvent.Text("ResponseBody").Should()
            .Be($"[binary content omitted: application/pdf, {harness.ResponseBody!.Length} bytes]");
        logEvent.Text("ResponseBody").Should().NotContain("binary payload");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheResponseIsAnXlsxExport_RecordsNoBodyText()
    {
        var harness = new Harness
        {
            ResponseContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ResponseBody = "PK binary workbook bytes"
        };

        await RunAsync(harness);

        var body = harness.Sink.Events.Single().Text("ResponseBody");
        body.Should().StartWith("[binary content omitted:");
        body.Should().NotContain("binary workbook bytes");
    }

    // ── Byte-for-byte write-back (BR-22.2 / AC-OPS-07) ──────────────────────

    [Fact]
    public async Task InvokeAsync_DeliversTheJsonResponseByteForByte()
    {
        var harness = new Harness { ResponseBody = "{\"customerId\":7,\"name\":\"Maria Santos\"}" };

        var result = await RunAsync(harness);

        Encoding.UTF8.GetString(result.ClientBytes).Should().Be(harness.ResponseBody);
    }

    [Fact]
    public async Task InvokeAsync_DeliversALargeBinaryResponseByteForByte()
    {
        var payload = new string('x', 200_000);
        var harness = new Harness
        {
            ResponseContentType = "application/pdf",
            ResponseBody = payload,
            Options = { MaxBodyLengthBytes = 1024 }
        };

        var result = await RunAsync(harness);

        result.ClientBytes.Length.Should().Be(payload.Length);
        Encoding.UTF8.GetString(result.ClientBytes).Should().Be(payload);
    }

    [Fact]
    public async Task InvokeAsync_RestoresTheOriginalResponseBody()
    {
        var harness = new Harness();

        var result = await RunAsync(harness);

        result.Context.Response.Body.Should().BeSameAs(result.OriginalResponseBody,
            "the host owns the response body and must be able to dispose it");
    }

    [Fact]
    public async Task InvokeAsync_RewindsTheRequestBodySoModelBindingStillWorks()
    {
        var harness = new Harness
        {
            Method = "POST",
            RequestBody = "{\"username\":\"admin\"}",
            RequestContentType = "application/json"
        };

        var result = await RunAsync(harness);

        result.Context.Request.Body.Position.Should().Be(0,
            "a forgotten rewind would break every POST and PUT endpoint");
    }

    // ── The error path (AC-OPS-02) ──────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenThePipelineThrows_WritesTheFailedEventAndRethrows()
    {
        var harness = new Harness { Exception = new InvalidOperationException("the kitchen is on fire") };

        var act = async () => await RunAsync(harness);

        await act.Should().ThrowAsync<InvalidOperationException>("the middleware never swallows the application's exception");

        harness.Sink.Events.Should().HaveCount(1);
        var logEvent = harness.Sink.Events.Single();
        logEvent.Level.Should().Be(LogEventLevel.Error);
        logEvent.Text("EventType").Should().Be(RequestLoggingMiddleware.FailedEventName);
        logEvent.Text("ErrorType").Should().Be("InvalidOperationException");
        logEvent.Text("ErrorMessage").Should().Be("the kitchen is on fire");
        logEvent.Exception.Should().NotBeNull("the exception object carries the stack trace");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestIsAuthenticated_RedactsTheRecordUserNameAndKeepsTheUserIdAndRoles()
    {
        // BR-21.3 — the record's own field names go through the same sensitive-name matching as
        // the body's property names. In this deployment the ClaimTypes.Name claim IS the
        // operator's e-mail address, so the record's "UserName" is redacted; "UserId" and "Roles"
        // are not in the set, so "who did this" is still answerable.
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "4adc482d-d68e-451c-8b9d-53a022bdd30a"),
            new Claim(ClaimTypes.Name, "admin@salscatersuite.local"),
            new Claim(ClaimTypes.Role, "Administrator")
        ], "TestAuth");

        var harness = new Harness { User = new ClaimsPrincipal(identity) };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Text("UserName").Should().Be(SensitiveDataRedactor.RedactionMarker);
        logEvent.Text("UserName").Should().NotContain("admin@salscatersuite.local");

        logEvent.Text("UserId").Should().Be("4adc482d-d68e-451c-8b9d-53a022bdd30a");
        logEvent.Texts("Roles").Should().Equal("Administrator");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestIsAuthenticated_LeavesEveryOtherRecordFieldUntouched()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "4adc482d-d68e-451c-8b9d-53a022bdd30a"),
            new Claim(ClaimTypes.Name, "admin@salscatersuite.local"),
            new Claim(ClaimTypes.Role, "Administrator")
        ], "TestAuth");

        var harness = new Harness { User = new ClaimsPrincipal(identity) };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();

        // No record field other than "UserName" is matched by the sensitive-name set.
        logEvent.Text("TraceId").Should().Be(TraceId);
        logEvent.Text("Method").Should().Be("GET");
        logEvent.Text("Path").Should().Be("/api/customers");
        logEvent.Value("StatusCode").Should().Be(200);
        logEvent.Flag("HasAuthorization").Should().BeFalse();
        logEvent.Text("EventType").Should().Be("HttpRequestCompleted");
    }

    // ── The correlation id (AC-OPS-11) ───────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_PushesTheCorrelationIdIntoTheLoggingScope()
    {
        var harness = new Harness
        {
            OnPipeline = logger => logger.Information("an inner line raised while the request is handled")
        };

        await RunAsync(harness);

        harness.Sink.Events.Should().HaveCount(2, "the pipeline raised its own line");
        harness.Sink.Events.Should().OnlyContain(logEvent => logEvent.Text("TraceId") == TraceId);
    }

    [Fact]
    public async Task InvokeAsync_RecordsTheTraceIdentifierOnTheRequestEvent()
    {
        var harness = new Harness();

        await RunAsync(harness);

        harness.Sink.Events.Single().Text("TraceId").Should().Be(TraceId);
    }

    // ── The capture stream's seekability (BR-22.6) ──────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTheResponseIsCleared_RecordsOnlyTheBodyWrittenAfterTheClear()
    {
        // The error path calls HttpResponse.Clear(), which resets a seekable body and cannot reset
        // a non-seekable one. The recorded body must be the body AS IT STANDS AFTER THE RESET —
        // the stale bytes of the partially written response must not survive into the record.
        var harness = new Harness
        {
            Pipeline = async context =>
            {
                await context.Response.Body.WriteAsync("PARTIAL"u8.ToArray());

                // This is what the exception-handler middleware does before it answers.
                context.Response.Clear();

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.Body.WriteAsync("""{"errors":["ERROR-ENVELOPE"]}"""u8.ToArray());
            }
        };

        var result = await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Text("ResponseBody").Should().Contain("ERROR-ENVELOPE");
        logEvent.Text("ResponseBody").Should().NotContain("PARTIAL",
            "the stale bytes of the partially written response must not survive into the record");
    }

    [Fact]
    public void ResponseCaptureStream_IsSeekableOverItsOwnRetainedBuffer()
    {
        using var inner = new MemoryStream();
        using var capture = new ResponseCaptureStream(inner, 64);

        capture.CanSeek.Should().BeTrue("BR-22.6 — the error path needs a seekable body");
        capture.Length.Should().Be(0);

        capture.Write("PARTIAL"u8);
        capture.Length.Should().Be(7);
        capture.Position.Should().Be(7);

        capture.Position = 0;
        capture.ReadByte().Should().Be((int)'P');

        // SetLength(0) clears the retained buffer and resets the capture state.
        capture.SetLength(0);
        capture.Length.Should().Be(0);
        capture.Position.Should().Be(0);
        capture.Truncated.Should().BeFalse();

        // The bytes the client received are untouched by the reset.
        inner.Length.Should().Be(7);
    }

    [Fact]
    public void ResponseCaptureStream_SetLengthIsNeverDelegatedToTheInnerStream()
    {
        // A non-seekable inner stream (Kestrel's response stream) throws on SetLength — the
        // capture must absorb the call.
        using var inner = new NonSeekableStream();
        using var capture = new ResponseCaptureStream(inner, 64);

        var act = () => capture.SetLength(0);

        act.Should().NotThrow();
        inner.SetLengthCalled.Should().BeFalse();
    }

    [Fact]
    public void ResponseCaptureStream_StopsRetainingPastTheCapButKeepsWritingThrough()
    {
        using var inner = new MemoryStream();
        using var capture = new ResponseCaptureStream(inner, 8);

        capture.Write(new byte[20]);

        capture.Truncated.Should().BeTrue();
        capture.GetCapturedBytes().Length.Should().Be(8);
        inner.Length.Should().Be(20, "every byte still reaches the client");
    }

    // ── The WebSocket-upgrade exemption (BR-22.7) ───────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTheRequestIsAWebSocketUpgrade_DoesNotReplaceTheResponseBody()
    {
        Stream? bodyDuringRequest = null;

        var harness = new Harness
        {
            IsWebSocketRequest = true,
            Pipeline = context =>
            {
                bodyDuringRequest = context.Response.Body;
                return Task.CompletedTask;
            }
        };

        var result = await RunAsync(harness);

        bodyDuringRequest.Should().BeSameAs(result.OriginalResponseBody,
            "the WebSocket adapter runs over IHttpResponseBodyFeature, not over Response.Body");
        result.Context.Response.Body.Should().BeSameAs(result.OriginalResponseBody);

        // The request is still recorded.
        harness.Sink.Events.Should().ContainSingle();
        harness.Sink.Events.Single().Text("Path").Should().Be("/api/customers");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestIsNotAWebSocketUpgrade_ReplacesTheResponseBody()
    {
        Stream? bodyDuringRequest = null;

        var harness = new Harness
        {
            Pipeline = context =>
            {
                bodyDuringRequest = context.Response.Body;
                return Task.CompletedTask;
            }
        };

        var result = await RunAsync(harness);

        bodyDuringRequest.Should().NotBeSameAs(result.OriginalResponseBody);

        // The original is restored afterwards, because the host owns the response body.
        result.Context.Response.Body.Should().BeSameAs(result.OriginalResponseBody);
    }

    // ── The master switch (AC-OPS-14) ───────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenLoggingIsDisabled_WritesNothingAndLeavesTheRequestUntouched()
    {
        var harness = new Harness
        {
            Method = "POST",
            RequestBody = "{\"username\":\"admin\"}",
            RequestContentType = "application/json",
            ResponseBody = "{\"ok\":true}",
            Options = { Enabled = false }
        };

        var result = await RunAsync(harness);

        harness.Sink.Events.Should().BeEmpty("the master switch removes the record entirely");
        Encoding.UTF8.GetString(result.ClientBytes).Should().Be("{\"ok\":true}");
        result.Context.Request.Body.Position.Should().Be(0, "the request body must not be read");
    }

    [Fact]
    public async Task InvokeAsync_WhenLoggingIsDisabled_LeavesTheOriginalStreamInstancesInPlace()
    {
        // AC-OPS-14 — the middleware must not touch Request.Body or Response.Body AT ALL. A
        // middleware that swapped Response.Body but logged nothing would empty every response.
        var harness = new Harness
        {
            Method = "POST",
            RequestBody = "{\"username\":\"admin\"}",
            RequestContentType = "application/json",
            ResponseBody = "{\"ok\":true}",
            Options = { Enabled = false }
        };

        var result = await RunAsync(harness);

        result.Context.Request.Body.Should().BeSameAs(result.OriginalRequestBody);
        result.Context.Response.Body.Should().BeSameAs(result.OriginalResponseBody);
    }

    // ── The request-body rewind (BR-22.4 / AC-OPS-16) ───────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTheRequestBodyIsLargerThanTheBufferingThreshold_RewindsItInFull()
    {
        // Request.EnableBuffering() spills to a temporary file past its 30 KB threshold; that is a
        // threshold, not a cap. The body must still be bound in full.
        var payload = new string('x', 200_000);
        var body = $"{{\"note\":\"{payload}\"}}";
        var harness = new Harness
        {
            Method = "POST",
            RequestBody = body,
            RequestContentType = "application/json"
        };

        var result = await RunAsync(harness);

        result.Context.Request.Body.Position.Should().Be(0, "the body must be rewound");
        result.Context.Request.Body.Length.Should().Be(body.Length);

        // And the endpoint can still read it in full.
        using var reader = new StreamReader(result.Context.Request.Body, leaveOpen: true);
        (await reader.ReadToEndAsync()).Should().Be(body);
    }

    [Fact]
    public async Task InvokeAsync_WhenThePathIsExcluded_WritesNothing()
    {
        var harness = new Harness
        {
            Path = "/health/live",
            Options = { ExcludedPathPrefixes = new[] { "/health" } }
        };

        await RunAsync(harness);

        harness.Sink.Events.Should().BeEmpty();
    }

    // ── The user and the query context ──────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenTheRequestIsAuthenticated_RecordsTheUserAndTheRoles()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "Administrator")
        ], "TestAuth");

        var harness = new Harness { User = new ClaimsPrincipal(identity) };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Text("UserId").Should().Be("42");
        logEvent.Texts("Roles").Should().Equal("Administrator");
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestIsAnonymous_RecordsNoUser()
    {
        var harness = new Harness();

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Has("UserId").Should().BeFalse();
        logEvent.Has("UserName").Should().BeFalse();
        logEvent.Has("Roles").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_RedactsTheSensitiveQueryParameters()
    {
        var harness = new Harness { Query = "?customerType=Corporate&email=maria@example.test" };

        await RunAsync(harness);

        var query = harness.Sink.Events.Single().Text("Query");
        query.Should().Contain("customerType=Corporate");
        query.Should().Contain(SensitiveDataRedactor.RedactionMarker);
        query.Should().NotContain("maria@example.test");
    }

    [Fact]
    public async Task InvokeAsync_RedactsTheCustomerSearchQueryParameter()
    {
        // BR-21.4 — "search" matches a customer name, a company and a contact number.
        var harness = new Harness { Query = "?search=Maria+Santos&page=2" };

        await RunAsync(harness);

        var query = harness.Sink.Events.Single().Text("Query");
        query.Should().Contain("page=2");
        query.Should().Contain(SensitiveDataRedactor.RedactionMarker);
        query.Should().NotContain("Maria");
    }

    [Fact]
    public async Task InvokeAsync_WhenThereIsNoQuery_RecordsNoQuery()
    {
        var harness = new Harness();

        await RunAsync(harness);

        harness.Sink.Events.Single().Text("Query").Should().BeNull();
    }

    // ── The header rule (BR-21.5 / AC-OPS-10) ───────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenAnAuthorizationHeaderIsPresent_RecordsOnlyItsPresence()
    {
        var harness = new Harness
        {
            RequestHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer super-secret-token"
            }
        };

        await RunAsync(harness);

        var logEvent = harness.Sink.Events.Single();
        logEvent.Flag("HasAuthorization").Should().BeTrue();
        logEvent.ToString().Should().NotContain("super-secret-token",
            "an Authorization header value can never reach a log (BR-21.5)");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<RunResult> RunAsync(Harness harness)
    {
        var sink = harness.Sink;

        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var context = new DefaultHttpContext
        {
            TraceIdentifier = TraceId
        };

        context.Request.Method = harness.Method;
        context.Request.Path = harness.Path;

        if (harness.Query is not null)
        {
            context.Request.QueryString = new QueryString(harness.Query);
        }

        var originalRequestBody = new MemoryStream();

        if (harness.RequestBody is not null)
        {
            var requestBytes = Encoding.UTF8.GetBytes(harness.RequestBody);
            originalRequestBody = new MemoryStream(requestBytes);
            context.Request.Body = originalRequestBody;
            context.Request.ContentLength = requestBytes.Length;
        }

        if (harness.RequestContentType is not null)
        {
            context.Request.ContentType = harness.RequestContentType;
        }

        if (harness.RequestHeaders is not null)
        {
            foreach (var header in harness.RequestHeaders)
            {
                context.Request.Headers[header.Key] = header.Value;
            }
        }

        if (harness.User is not null)
        {
            context.User = harness.User;
        }

        if (harness.IsWebSocketRequest)
        {
            context.Features.Set<IHttpWebSocketFeature>(new FakeWebSocketFeature());
        }

        var originalBody = new MemoryStream();
        context.Response.Body = originalBody;

        RequestDelegate pipeline = async httpContext =>
        {
            harness.OnPipeline?.Invoke(logger);

            if (harness.Exception is not null)
            {
                throw harness.Exception;
            }

            if (harness.Pipeline is not null)
            {
                await harness.Pipeline(httpContext);
                return;
            }

            httpContext.Response.StatusCode = harness.ResponseStatusCode;

            if (harness.ResponseContentType is not null)
            {
                httpContext.Response.ContentType = harness.ResponseContentType;
            }

            if (harness.ResponseBody is not null)
            {
                var responseBytes = Encoding.UTF8.GetBytes(harness.ResponseBody);
                await httpContext.Response.Body.WriteAsync(responseBytes);
            }
        };

        var options = harness.Options;

        var middleware = new RequestLoggingMiddleware(
            pipeline,
            logger,
            Options.Create(options),
            new SensitiveDataRedactor(Options.Create(options)));

        await middleware.InvokeAsync(context);

        return new RunResult(
            context,
            sink,
            originalBody.ToArray(),
            originalBody,
            originalRequestBody);
    }

    private sealed class Harness
    {
        public InMemoryLogEventSink Sink { get; } = new();

        public RequestLoggingOptions Options { get; set; } = new();

        public Action<ILogger>? OnPipeline { get; init; }

        /// <summary>A full pipeline override — the response the application produces.</summary>
        public Func<HttpContext, Task>? Pipeline { get; init; }

        public Exception? Exception { get; init; }

        public bool IsWebSocketRequest { get; init; }

        public string Method { get; init; } = "GET";

        public string Path { get; init; } = "/api/customers";

        public string? Query { get; init; }

        public string? RequestBody { get; init; }

        public string? RequestContentType { get; init; }

        public string? ResponseContentType { get; init; } = "application/json";

        public string? ResponseBody { get; init; } = "{}";

        public int ResponseStatusCode { get; init; } = 200;

        public ClaimsPrincipal? User { get; init; }

        public IDictionary<string, string>? RequestHeaders { get; init; }
    }

    private sealed record RunResult(
        DefaultHttpContext Context,
        InMemoryLogEventSink Sink,
        byte[] ClientBytes,
        MemoryStream OriginalResponseBody,
        MemoryStream OriginalRequestBody);

    /// <summary>Marks the request as a WebSocket upgrade (BR-22.7).</summary>
    private sealed class FakeWebSocketFeature : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;

        public Task<System.Net.WebSockets.WebSocket> AcceptAsync(WebSocketAcceptContext context) =>
            throw new NotSupportedException();
    }

    /// <summary>A stream that cannot seek — Kestrel's response stream is like this.</summary>
    private sealed class NonSeekableStream : Stream
    {
        public bool SetLengthCalled { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value)
        {
            SetLengthCalled = true;
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) { }
    }
}
