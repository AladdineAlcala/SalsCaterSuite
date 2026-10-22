namespace SalsCaterSuite.WebApi.Middleware;

using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using SalsCaterSuite.WebApi.Logging;

/// <summary>
/// The per-request state the request logger shares with <see cref="RequestLoggingExceptionHandler"/>.
/// It travels on <see cref="HttpContext.Items"/> because the exception handler is a singleton
/// while the middleware is created per request.
/// </summary>
public sealed class RequestLoggingState
{
    /// <summary>The <c>HttpContext.Items</c> key the state travels under.</summary>
    public const string ItemKey = "SalsCaterSuite.RequestLogging";

    public RequestLoggingState(Stopwatch stopwatch)
    {
        Stopwatch = stopwatch;
    }

    public Stopwatch Stopwatch { get; }

    /// <summary>The captured request body (already read by the middleware).</summary>
    public string? RequestBody { get; set; }

    /// <summary>Whether the captured request body was cut at the capture cap.</summary>
    public bool RequestBodyTruncated { get; set; }

    /// <summary>Whether the failure event has already been written for this request.</summary>
    public bool FailureRecorded { get; set; }
}

/// <summary>
/// Writes the <c>HttpRequestFailed</c> event for a request whose pipeline threw
/// (specification §4.1 / AC-OPS-02).
/// <para>
/// The handler is registered <b>after</b> the domain exception handler, so a domain rule
/// violation is still answered with the approved <c>400 { "errors": [...] }</c> envelope and is
/// recorded by the request-completion event at the client-error level. A genuine fault reaches
/// this handler, which observes the exception object <b>before</b> the response is written — so
/// the record carries the exception type, the message and the stack trace (specification §4.4).
/// </para>
/// <para>
/// The handler returns <c>false</c>: it never writes a response. The response contract belongs to
/// the domain exception handler (DEC-014) and to the framework's ProblemDetails fallback
/// (DEC-026), so this feature changes no status code and no response body (AC-OPS-16).
/// </para>
/// </summary>
public sealed class RequestLoggingExceptionHandler : IExceptionHandler
{
    private readonly ILogger _logger;
    private readonly SensitiveDataRedactor _redactor;
    private readonly IOptions<RequestLoggingOptions> _options;

    /// <summary>Creates the handler.</summary>
    public RequestLoggingExceptionHandler(
        ILogger logger,
        SensitiveDataRedactor redactor,
        IOptions<RequestLoggingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            var state = httpContext.Items[RequestLoggingState.ItemKey] as RequestLoggingState;

            // The record is written once per request, whoever observes the exception first.
            if (state is null || state.FailureRecorded)
            {
                return ValueTask.FromResult(false);
            }

            state.FailureRecorded = true;

            // The response has not been written yet; the fault is answered with a 500.
            var statusCode = Math.Max(httpContext.Response.StatusCode, StatusCodes.Status500InternalServerError);

            var options = _options.Value;

            var properties = RequestLogRecord.Build(
                httpContext,
                _redactor,
                RequestLogRecord.FailedEventName,
                statusCode,
                state.Stopwatch.Elapsed.TotalMilliseconds,
                RequestLogRecord.BuildRequestBody(options, _redactor, state.RequestBody, state.RequestBodyTruncated),
                state.RequestBodyTruncated,
                responseBody: null,
                responseBodyTruncated: false,
                responseBytes: 0,
                exception);

            RequestLogRecord.Write(
                _logger,
                httpContext,
                properties,
                LogEventLevel.Error,
                exception);
        }
        catch (Exception)
        {
            // Risk R3 — a logging defect must never become an API outage.
        }

        // The response is never written here: the framework's ProblemDetails fallback owns it.
        return ValueTask.FromResult(false);
    }
}
