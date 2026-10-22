namespace SalsCaterSuite.WebApi.Middleware;

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using SalsCaterSuite.WebApi.Logging;

/// <summary>
/// The operational request logger (REQ-OPS-01). It writes exactly one structured event per
/// HTTP request — <c>HttpRequestCompleted</c> when the pipeline returned, <c>HttpRequestFailed</c>
/// when it threw — and it is the <b>outermost</b> middleware so it observes the response the
/// exception handler produced, including the <c>{ "errors": [...] }</c> envelope of a 400
/// (specification §4.4).
/// <para>
/// The record is <b>diagnostic, not evidential</b> (BR-24): it never replaces the
/// <c>Payment</c>, <c>Refund</c> and <c>Booking</c> rows and the documents rendered from them.
/// </para>
/// <para>
/// Two rules are absolute:
/// <list type="bullet">
/// <item><b>BR-21</b> — a credential or a customer contact identifier never reaches the log. The
/// redaction is owned by <see cref="SensitiveDataRedactor"/>; this middleware never decides what
/// is sensitive.</item>
/// <item><b>Risk R3</b> — a defect in the logging code can never become an API outage. Every
/// logging step is wrapped, and the application's own exception is never swallowed.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RequestLoggingMiddleware
{
    /// <summary>The event name written when the pipeline returned normally (specification §4.1).</summary>
    public const string CompletedEventName = RequestLogRecord.CompletedEventName;

    /// <summary>The event name written when the pipeline threw (specification §4.1).</summary>
    public const string FailedEventName = RequestLogRecord.FailedEventName;

    /// <summary>The marker appended to a body that was cut at the capture cap (BR-22.1).</summary>
    public const string BodyTruncatedMarker = RequestLogRecord.BodyTruncatedMarker;

    private static readonly string[] TextualMediaTypes =
    [
        "application/json",
        "application/problem+json",
        "application/xml"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private readonly IOptions<RequestLoggingOptions> _options;
    private readonly SensitiveDataRedactor _redactor;

    /// <summary>Creates the middleware.</summary>
    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger logger,
        IOptions<RequestLoggingOptions> options,
        SensitiveDataRedactor redactor)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
    }

    /// <summary>Runs the pipeline and writes the single request record.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = _options.Value;

        // AC-OPS-14 — the master switch. The middleware must not touch the request or the
        // response at all, so the API behaves exactly as it did before the feature.
        if (!options.Enabled || IsExcludedPath(context.Request.Path, options))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        // AC-OPS-11 — the correlation id is pushed into the logging scope for the whole request,
        // so every other line raised while the request is handled carries the same id.
        using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
        {
            var state = new RequestLoggingState(stopwatch);
            context.Items[RequestLoggingState.ItemKey] = state;

            var requestCapture = await CaptureRequestBodyAsync(context, options).ConfigureAwait(false);

            // The state carries the RAW capture; both record writers run it through the single
            // redaction-and-truncation transformation (BR-21), so a raw payload can never be
            // written by either of them.
            state.RequestBody = requestCapture.Text;
            state.RequestBodyTruncated = requestCapture.Truncated;

            var originalBody = context.Response.Body;
            ResponseCaptureStream? responseCapture = null;

            try
            {
                // BR-22.7 — the WebSocket adapter runs over IHttpResponseBodyFeature, not over
                // Response.Body, so swapping the body on an upgrade request would break a future
                // NotificationHub. The body is left untouched; the request is still recorded.
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    // The capture stream is installed for every request so that "ResponseBytes" is
                    // always the real number of bytes the response carried. Whether the body *text*
                    // reaches the record is controlled by "LogResponseBody".
                    responseCapture = new ResponseCaptureStream(originalBody, options.MaxBodyLengthBytes);
                    context.Response.Body = responseCapture;
                }

                try
                {
                    await _next(context).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    stopwatch.Stop();

                    // The exception is logged with the exception object itself, so Serilog writes
                    // the stack trace, and then it is rethrown — never swallowed. When the
                    // exception handler already recorded the failure, the record is not repeated.
                    if (!state.FailureRecorded)
                    {
                        state.FailureRecorded = true;

                        WriteEvent(
                            LogEventLevel.Error,
                            RequestLogRecord.FailedEventName,
                            context,
                            options,
                            state.RequestBody,
                            state.RequestBodyTruncated,
                            responseCapture,
                            stopwatch.Elapsed.TotalMilliseconds,
                            exception);
                    }

                    throw;
                }

                stopwatch.Stop();

                WriteEvent(
                    ResolveLevel(context.Response.StatusCode, options),
                    RequestLogRecord.CompletedEventName,
                    context,
                    options,
                    state.RequestBody,
                    state.RequestBodyTruncated,
                    responseCapture,
                    stopwatch.Elapsed.TotalMilliseconds,
                    exception: null);
            }
            finally
            {
                // The original body is restored so the host disposes the stream it owns.
                context.Response.Body = originalBody;
                responseCapture?.Dispose();
            }
        }
    }

    internal static LogEventLevel ResolveLevel(int statusCode, RequestLoggingOptions options)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return options.ServerErrorLevel;
        }

        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            return options.ClientErrorLevel;
        }

        return LogEventLevel.Information;
    }

    private static bool IsExcludedPath(PathString path, RequestLoggingOptions options)
    {
        if (options.ExcludedPathPrefixes.Count == 0)
        {
            return false;
        }

        return options.ExcludedPathPrefixes.Any(prefix =>
            !string.IsNullOrWhiteSpace(prefix)
            && path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// BR-22.2 — the capture is read-only. The body is buffered, read at most once up to the cap
    /// and rewound, so model binding still sees the complete payload.
    /// </summary>
    private async Task<BodyCapture> CaptureRequestBodyAsync(HttpContext context, RequestLoggingOptions options)
    {
        if (!options.LogRequestBody || context.Request.ContentLength is 0)
        {
            return BodyCapture.Empty;
        }

        try
        {
            context.Request.EnableBuffering();

            var buffer = new byte[options.MaxBodyLengthBytes + 1];
            var read = 0;

            while (read < buffer.Length)
            {
                var count = await context.Request.Body
                    .ReadAsync(buffer.AsMemory(read, buffer.Length - read))
                    .ConfigureAwait(false);

                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            var truncated = read > options.MaxBodyLengthBytes;
            var length = Math.Min(read, options.MaxBodyLengthBytes);

            return new BodyCapture(Encoding.UTF8.GetString(buffer, 0, length), truncated);
        }
        catch (Exception)
        {
            // A capture failure must never break the API (risk R3).
            return BodyCapture.Empty;
        }
        finally
        {
            // Critical: the body must be rewound or every POST/PUT endpoint would break.
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }
    }

    private void WriteEvent(
        LogEventLevel level,
        string eventName,
        HttpContext context,
        RequestLoggingOptions options,
        string? rawRequestBody,
        bool requestBodyTruncated,
        ResponseCaptureStream? responseCapture,
        double elapsedMs,
        Exception? exception)
    {
        try
        {
            var responseBody = BuildResponseBody(options, context, responseCapture);

            var properties = RequestLogRecord.Build(
                context,
                _redactor,
                eventName,
                context.Response.StatusCode,
                elapsedMs,
                RequestLogRecord.BuildRequestBody(options, _redactor, rawRequestBody, requestBodyTruncated),
                requestBodyTruncated,
                responseBody,
                responseCapture?.Truncated ?? false,
                responseCapture?.TotalBytesWritten ?? 0,
                exception);

            RequestLogRecord.Write(_logger, context, properties, level, exception);
        }
        catch (Exception)
        {
            // Risk R3 — a logging defect must never become an API outage.
        }
    }

    private string? BuildResponseBody(
        RequestLoggingOptions options,
        HttpContext context,
        ResponseCaptureStream? responseCapture)
    {
        if (!options.LogResponseBody || responseCapture is null)
        {
            return null;
        }

        var contentType = context.Response.ContentType;

        // BR-21.7 — a binary response is recorded by content type and byte count only.
        if (!IsTextualContentType(contentType))
        {
            return $"[binary content omitted: {contentType ?? "unknown"}, {responseCapture.TotalBytesWritten} bytes]";
        }

        var text = Encoding.UTF8.GetString(responseCapture.GetCapturedBytes());
        var body = _redactor.RedactJsonBody(text);

        return responseCapture.Truncated ? RequestLogRecord.AppendTruncationMarker(body) : body;
    }

    internal static bool IsTextualContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            // No declared content type: the body is still attempted as JSON.
            return true;
        }

        var mediaType = contentType.Split(';')[0].Trim().ToLowerInvariant();

        return mediaType.StartsWith("text/", StringComparison.Ordinal)
            || TextualMediaTypes.Contains(mediaType, StringComparer.Ordinal);
    }

    internal static string? AppendTruncationMarker(string? body) =>
        RequestLogRecord.AppendTruncationMarker(body);

    /// <summary>The captured prefix of a request body and whether it was cut at the cap.</summary>
    internal readonly record struct BodyCapture(string Text, bool Truncated)
    {
        public static BodyCapture Empty { get; } = new(string.Empty, false);

        public bool IsEmpty => Text.Length == 0;
    }
}
