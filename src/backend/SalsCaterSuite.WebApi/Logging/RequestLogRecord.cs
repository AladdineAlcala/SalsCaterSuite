namespace SalsCaterSuite.WebApi.Logging;

using System.Security.Claims;
using Serilog;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// The <b>single</b> place where the operational record's fields are defined (specification §4.2).
/// <para>
/// The request-completion event and the failure event share it, so the two events always carry
/// the same field names and the same shape — the same discipline the repository applies to a
/// duplicated financial calculation.
/// </para>
/// </summary>
internal static class RequestLogRecord
{
    /// <summary>The event name of a request that returned normally (specification §4.1).</summary>
    internal const string CompletedEventName = "HttpRequestCompleted";

    /// <summary>The event name of a request that threw (specification §4.1).</summary>
    internal const string FailedEventName = "HttpRequestFailed";

    /// <summary>The marker appended to a body that was cut at the capture cap (BR-22.1).</summary>
    internal const string BodyTruncatedMarker = "[truncated]";

    /// <summary>
    /// BR-21 / BR-22 — the <b>one</b> transformation a captured request body goes through before
    /// it reaches a record: it is redacted, and the truncation marker is appended when the capture
    /// was cut at the cap. Both the request-completion event and the failure event use it, so a
    /// raw payload can never be written by either of them.
    /// </summary>
    internal static string? BuildRequestBody(
        RequestLoggingOptions options,
        SensitiveDataRedactor redactor,
        string? rawBody,
        bool truncated)
    {
        if (!options.LogRequestBody || string.IsNullOrEmpty(rawBody))
        {
            return null;
        }

        var body = redactor.RedactJsonBody(rawBody);

        return truncated ? AppendTruncationMarker(body) : body;
    }

    /// <summary>Appends the explicit truncation marker to a captured body.</summary>
    internal static string? AppendTruncationMarker(string? body) =>
        body is null ? BodyTruncatedMarker : $"{body} {BodyTruncatedMarker}";

    /// <summary>Writes the record as a structured Serilog event.</summary>
    internal static void Write(
        ILogger logger,
        HttpContext context,
        IReadOnlyDictionary<string, object?> properties,
        LogEventLevel level,
        Exception? exception)
    {
        logger
            .ForContext(new RequestLogPropertiesEnricher(properties))
            .Write(level, exception, properties["EventType"] as string ?? CompletedEventName);
    }

    /// <summary>Builds the record's fields (specification §4.2).</summary>
    internal static Dictionary<string, object?> Build(
        HttpContext context,
        SensitiveDataRedactor redactor,
        string eventName,
        int statusCode,
        double elapsedMs,
        string? requestBody,
        bool requestBodyTruncated,
        string? responseBody,
        bool responseBodyTruncated,
        long responseBytes,
        Exception? exception)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["EventType"] = eventName,
            ["TraceId"] = context.TraceIdentifier,
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path.Value ?? string.Empty,
            ["Query"] = redactor.RedactQueryString(context.Request.QueryString.Value),
            ["StatusCode"] = statusCode,
            ["ElapsedMs"] = Math.Round(elapsedMs, 3),
            ["ClientIp"] = context.Connection.RemoteIpAddress?.ToString(),
            ["HasAuthorization"] = SensitiveDataRedactor.HasAuthorizationHeader(context.Request.Headers),
            ["RequestContentType"] = context.Request.ContentType,
            ["RequestBody"] = requestBody,
            ["RequestBodyTruncated"] = requestBodyTruncated,
            ["ResponseContentType"] = context.Response.ContentType,
            ["ResponseBody"] = responseBody,
            ["ResponseBodyTruncated"] = responseBodyTruncated,
            ["ResponseBytes"] = responseBytes
        };

        var user = context.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            properties["UserId"] = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            properties["UserName"] = user.FindFirst(ClaimTypes.Name)?.Value;
            properties["Roles"] = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        }

        if (exception is not null)
        {
            properties["ErrorType"] = exception.GetType().Name;
            properties["ErrorMessage"] = exception.Message;
        }

        // BR-21.3 — the sensitive-name set applies to the record's OWN field names as well as to
        // the body's property names, through the one redaction engine. A record field whose name
        // is in the set has its value replaced by the marker — the record's "UserName" is the
        // operator's e-mail address in this deployment — while a field whose name is not in the
        // set ("UserId", "Roles") is left untouched.
        redactor.RedactRecordFields(properties);

        return properties;
    }

    /// <summary>
    /// Writes the record's fields onto the log event as named properties, so the Datadog Agent
    /// reads them as structured attributes (BR-23.3) instead of parsing a message template.
    /// </summary>
    private sealed class RequestLogPropertiesEnricher : ILogEventEnricher
    {
        private readonly IReadOnlyDictionary<string, object?> _properties;

        public RequestLogPropertiesEnricher(IReadOnlyDictionary<string, object?> properties)
        {
            _properties = properties;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var property in _properties)
            {
                var value = property.Value is null
                    ? new ScalarValue(null)
                    : propertyFactory.CreateProperty(property.Key, property.Value).Value;

                logEvent.AddPropertyIfAbsent(new LogEventProperty(property.Key, value));
            }
        }
    }
}
