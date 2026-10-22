namespace SalsCaterSuite.WebApi.Logging;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
/// The <b>single</b> redaction engine of the API (BR-21). Every value that reaches a log file
/// passes through this class — the request body, the response body and the query string — so the
/// redaction rule exists in exactly one place (the same discipline the repository applies to a
/// duplicated financial calculation).
/// <para>
/// The rule is <b>owner-ruled and non-negotiable</b>: a credential or a customer contact
/// identifier must never reach a log file. The name of the property decides, not its value: a
/// property called <c>password</c> is replaced whether its value is a string, a number, an object
/// or <c>null</c>.
/// </para>
/// </summary>
public sealed class SensitiveDataRedactor
{
    /// <summary>BR-21.2 — the literal marker that replaces a redacted value.</summary>
    public const string RedactionMarker = "***REDACTED***";

    /// <summary>BR-21.6 — the marker written instead of a body that is not valid JSON.</summary>
    public const string NonJsonBodyMarker = "[non-json body omitted]";

    /// <summary>BR-21.5 — the marker written instead of a header value (never a header value itself).</summary>
    public const string HeaderValuesOmittedMarker = "[header values omitted]";

    private readonly HashSet<string> _sensitiveFieldNames;
    private readonly HashSet<string> _sensitiveQueryParameterNames;

    /// <summary>Creates the redactor from the bound request-logging options.</summary>
    public SensitiveDataRedactor(IOptions<RequestLoggingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;

        _sensitiveFieldNames = new HashSet<string>(settings.EffectiveSensitiveFieldNames, StringComparer.OrdinalIgnoreCase);
        _sensitiveQueryParameterNames = new HashSet<string>(settings.EffectiveSensitiveQueryParameterNames, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// BR-21.3 — <b>the sensitive-name set applies to the record's own field names as well as to
    /// the body's property names.</b>
    /// <para>
    /// A record field whose <b>name</b> is in the set has its value replaced by the literal
    /// <see cref="RedactionMarker"/>, whatever the value is; a field whose name is not in the set
    /// is left unchanged. In this deployment the record's <c>UserName</c> field (the
    /// <c>ClaimTypes.Name</c> claim) is the operator's e-mail address and login name, so it is
    /// redacted — writing it in clear one field after redacting the body's identical
    /// <c>username</c> property would make the body redaction self-defeating. The record's
    /// <c>UserId</c> (the Identity key) and <c>Roles</c> are not in the set and are kept, so
    /// "who did this" is still answerable.
    /// </para>
    /// <para>
    /// The rule is deliberately general: an operator who adds a name to the configured set has it
    /// take effect on the record's fields too, with no code change.
    /// </para>
    /// </summary>
    public void RedactRecordFields(IDictionary<string, object?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        // The values change, not the keys, but the key set is snapshotted for clarity.
        foreach (var fieldName in fields.Keys.ToArray())
        {
            if (fields[fieldName] is not null && IsSensitiveFieldName(fieldName))
            {
                fields[fieldName] = RedactionMarker;
            }
        }
    }

    /// <summary>
    /// BR-21.1 / BR-21.2 — redacts a JSON body by property name, case-insensitively, at any
    /// depth, including the properties inside an array.
    /// <list type="bullet">
    /// <item><c>null</c> / empty input → <c>null</c> (there is no body to record).</item>
    /// <item>A body that is not valid JSON → <see cref="NonJsonBodyMarker"/> (BR-21.6): a
    /// malformed body is exactly the shape in which a credential could hide from a name-based
    /// redactor, so it is never written.</item>
    /// <item>A property whose <b>name</b> is sensitive → <see cref="RedactionMarker"/>, whatever
    /// its value (including <c>null</c>).</item>
    /// <item>A property whose name is not sensitive and whose value is <c>null</c> stays
    /// <c>null</c> — it carries no secret.</item>
    /// </list>
    /// </summary>
    public string? RedactJsonBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return NonJsonBodyMarker;
        }

        if (root is null)
        {
            // The body was the JSON literal `null` — valid JSON, but it carries no content.
            return "null";
        }

        RedactNode(root);

        return root.ToJsonString();
    }

    /// <summary>
    /// BR-21.4 — redacts the query string by parameter name against the sensitive query set.
    /// The other parameters are preserved verbatim. <c>null</c> is returned when there is no
    /// query string at all.
    /// </summary>
    public string? RedactQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return null;
        }

        var raw = queryString.StartsWith('?') ? queryString[1..] : queryString;

        if (raw.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder(raw.Length);

        foreach (var segment in raw.Split('&'))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            var separator = segment.IndexOf('=');

            if (separator < 0)
            {
                builder.Append(segment);
                continue;
            }

            var name = segment[..separator];

            builder.Append(name).Append('=');
            builder.Append(IsSensitiveQueryParameter(name) ? RedactionMarker : segment[(separator + 1)..]);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>
    /// BR-21.5 — the <b>only</b> header-derived field of the record: whether an
    /// <c>Authorization</c> header was present. The header's value is never read.
    /// </summary>
    public static bool HasAuthorizationHeader(IHeaderDictionary? headers) =>
        headers is not null && headers.ContainsKey("Authorization");

    /// <summary>
    /// BR-21.5 — no header value is ever written to a log. This always returns the omission
    /// marker, whatever the header was.
    /// </summary>
    public static string RedactHeaderValue(string? headerName, string? headerValue) => HeaderValuesOmittedMarker;

    private bool IsSensitiveFieldName(string name) => _sensitiveFieldNames.Contains(name);

    private bool IsSensitiveQueryParameter(string name)
    {
        if (_sensitiveQueryParameterNames.Contains(name))
        {
            return true;
        }

        try
        {
            return _sensitiveQueryParameterNames.Contains(Uri.UnescapeDataString(name));
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                // The property names are snapshotted because the loop replaces values in place.
                foreach (var propertyName in jsonObject.Select(property => property.Key).ToArray())
                {
                    if (IsSensitiveFieldName(propertyName))
                    {
                        jsonObject[propertyName] = JsonValue.Create(RedactionMarker);
                    }
                    else
                    {
                        RedactNode(jsonObject[propertyName]);
                    }
                }

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    RedactNode(item);
                }

                break;
        }
    }
}
