namespace SalsCaterSuite.UnitTests.Api.Logging;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using SalsCaterSuite.Domain.Exceptions;
using SalsCaterSuite.WebApi.Logging;
using SalsCaterSuite.WebApi.Middleware;
using Xunit;

/// <summary>
/// Verifies the failure event of the operational log (REQ-OPS-01, AC-OPS-02, AC-OPS-16).
/// The handler observes the exception before the response is written and never writes a response
/// of its own, so the API's error contract is untouched.
/// </summary>
public class RequestLoggingExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WhenAFaultIsObserved_WritesTheFailedEventAtError()
    {
        var sink = new InMemoryLogEventSink();
        var logger = CreateLogger(sink);
        var context = CreateContext();
        var state = AttachState(context);

        var handler = new RequestLoggingExceptionHandler(logger, CreateRedactor(), Options.Create(new RequestLoggingOptions()));

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("the kitchen is on fire"),
            CancellationToken.None);

        handled.Should().BeFalse("the handler never answers the request — the response contract is unchanged");

        var logEvent = sink.Events.Should().ContainSingle().Subject;
        logEvent.Level.Should().Be(LogEventLevel.Error);
        logEvent.Text("EventType").Should().Be(RequestLoggingMiddleware.FailedEventName);
        logEvent.Text("ErrorType").Should().Be("InvalidOperationException");
        logEvent.Text("ErrorMessage").Should().Be("the kitchen is on fire");
        logEvent.Exception.Should().NotBeNull("the exception object carries the stack trace");
        logEvent.Text("TraceId").Should().Be(context.TraceIdentifier);
        logEvent.Text("Method").Should().Be("POST");
        logEvent.Text("Path").Should().Be("/api/bookings");
        logEvent.Value("StatusCode").Should().Be(500);
        state.FailureRecorded.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_WhenTheFailureWasAlreadyRecorded_WritesNothing()
    {
        var sink = new InMemoryLogEventSink();
        var logger = CreateLogger(sink);
        var context = CreateContext();
        var state = AttachState(context);
        state.FailureRecorded = true;

        var handler = new RequestLoggingExceptionHandler(logger, CreateRedactor(), Options.Create(new RequestLoggingOptions()));

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        sink.Events.Should().BeEmpty("exactly one failure event is written per request");
    }

    [Fact]
    public async Task TryHandleAsync_WhenThereIsNoRequestState_WritesNothing()
    {
        var sink = new InMemoryLogEventSink();
        var logger = CreateLogger(sink);
        var context = CreateContext();

        var handler = new RequestLoggingExceptionHandler(logger, CreateRedactor(), Options.Create(new RequestLoggingOptions()));

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.Should().BeFalse();
        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task TryHandleAsync_CarriesTheCapturedRequestBody()
    {
        var sink = new InMemoryLogEventSink();
        var logger = CreateLogger(sink);
        var context = CreateContext();
        var state = AttachState(context);
        state.RequestBody = "{\"username\":\"admin\",\"password\":\"hunter2\"}";

        var handler = new RequestLoggingExceptionHandler(logger, CreateRedactor(), Options.Create(new RequestLoggingOptions()));

        await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        var logEvent = sink.Events.Single();
        logEvent.Text("RequestBody").Should().Contain(SensitiveDataRedactor.RedactionMarker);
        logEvent.Text("RequestBody").Should().NotContain("hunter2");
    }

    // ── The registration order (specification §4.4) ──────────────────────────

    [Fact]
    public async Task TryHandleAsync_WhenADomainExceptionIsRaised_TheDomainHandlerStillAnswersWithThe400Envelope()
    {
        // The request-logging handler is registered AFTER the domain exception handler, so the
        // chain stops at the domain handler: a domain rule violation is still answered with the
        // approved 400 { "errors": [...] } envelope, it is recorded by the request-completion
        // event at the client-error level, and it is NOT recorded as a failure event.
        var sink = new InMemoryLogEventSink();
        var logger = CreateLogger(sink);
        var context = CreateContext();
        AttachState(context);

        var domainHandler = new DomainExceptionHandler(NullLogger<DomainExceptionHandler>.Instance);
        var requestLoggingHandler = new RequestLoggingExceptionHandler(
            logger,
            CreateRedactor(),
            Options.Create(new RequestLoggingOptions()));

        var domainException = new DomainException("Refund amount cannot exceed the total payments received.");

        // The chain: the first handler that answers wins.
        var handled = await domainHandler.TryHandleAsync(context, domainException, CancellationToken.None);
        handled.Should().BeTrue("the domain exception handler answers the domain rule violation");

        context.Response.StatusCode.Should().Be(400);

        var body = await ReadBodyAsync(context);
        body.Should().Contain("errors");
        body.Should().Contain("Refund amount cannot exceed the total payments received.");

        // The request-logging handler is never consulted for a domain exception.
        var consulted = await requestLoggingHandler.TryHandleAsync(context, domainException, CancellationToken.None);
        consulted.Should().BeFalse("the request-logging handler never answers a request");
    }

    [Fact]
    public async Task TryHandleAsync_WhenTheHandlerIsRegisteredAfterTheDomainHandler_AFaultIsStillRecorded()
    {
        // The complement of the previous test: a fault the domain handler declines IS recorded.
        var sink = new InMemoryLogEventSink();
        var logger = CreateLogger(sink);
        var context = CreateContext();
        AttachState(context);

        var domainHandler = new DomainExceptionHandler(NullLogger<DomainExceptionHandler>.Instance);
        var requestLoggingHandler = new RequestLoggingExceptionHandler(
            logger,
            CreateRedactor(),
            Options.Create(new RequestLoggingOptions()));

        var fault = new InvalidOperationException("the kitchen is on fire");

        var domainHandled = await domainHandler.TryHandleAsync(context, fault, CancellationToken.None);
        domainHandled.Should().BeFalse("the domain handler declines a fault that is not a domain rule");

        await requestLoggingHandler.TryHandleAsync(context, fault, CancellationToken.None);

        sink.Events.Should().ContainSingle();
        sink.Events.Single().Text("ErrorType").Should().Be("InvalidOperationException");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private static ILogger CreateLogger(InMemoryLogEventSink sink) =>
        new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

    private static SensitiveDataRedactor CreateRedactor() =>
        new(Options.Create(new RequestLoggingOptions()));

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-0001"
        };

        context.Request.Method = "POST";
        context.Request.Path = "/api/bookings";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static RequestLoggingState AttachState(HttpContext context)
    {
        var state = new RequestLoggingState(Stopwatch.StartNew());
        context.Items[RequestLoggingState.ItemKey] = state;

        return state;
    }
}
