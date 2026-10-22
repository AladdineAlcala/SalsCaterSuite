namespace SalsCaterSuite.WebApi.Logging;

using Serilog.Events;

/// <summary>
/// The <b>behaviour</b> of the operational request logger (REQ-OPS-01, specification §5.2).
/// <para>
/// This class is bound from the <c>Logging:RequestLogging</c> configuration section. It owns
/// <b>what</b> the record contains; the <c>Serilog</c> section owns <b>where</b> the record goes
/// and <b>at what level</b> the pipeline logs (specification §5.3). The two sections are
/// deliberately separate so an operator can turn body capture off without touching the sink
/// list, and can move the sink without touching the redaction policy.
/// </para>
/// <para>
/// The sensitive-name sets are <b>configuration, not code</b> (BR-21.3), so they can be changed
/// without a rebuild. The values below are the defaults that ship in <c>appsettings.json</c>.
/// </para>
/// </summary>
public sealed class RequestLoggingOptions
{
    /// <summary>The configuration section this options class is bound from (specification §5.2).</summary>
    public const string SectionName = "Logging:RequestLogging";

    /// <summary>
    /// BR-21.3 — the default sensitive property-name set, **verified against the as-built DTOs**
    /// (DEC-096). The set names properties that exist:
    /// <list type="bullet">
    /// <item><b>Secrets (never negotiable):</b> credentials, tokens and signing material. A
    /// credential in a log is reusable until it is rotated.</item>
    /// <item><b>Account identifiers:</b> <c>username</c>, <c>userName</c> and <c>createdbyuser</c>.
    /// In this deployment the <b>username is the administrator's e-mail address</b>
    /// (<c>Seed:Admin:UserName</c> = <c>admin@salscatersuite.local</c>), so an unredacted username
    /// is a contact identifier on disk. <c>createdbyuser</c> is a user name on every payment and
    /// audit record.</item>
    /// <item><b>Customer contact identifiers (owner-ruled, DEC-093):</b> <c>address</c>,
    /// <c>contact</c>, <c>contact1</c>, <c>contact2</c> and <c>customercontact</c> — the only
    /// contact-identifier property names in the codebase (<c>Customer.Address</c> /
    /// <c>.Contact1</c> / <c>.Contact2</c>, <c>CustomerStatementDto.Contact</c> and
    /// <c>BookingDto.CustomerContact</c>).</item>
    /// <item><b>Forward-looking:</b> <c>email</c>, <c>emailaddress</c> and <c>tin</c>. <b>No
    /// property in the current model carries an e-mail address or a TIN</b> (<c>Customer</c> has
    /// neither). They are kept so the rule is already correct if the model ever grows one.</item>
    /// </list>
    /// <para>
    /// A customer's <b>name</b> and a transaction <b>amount</b> are deliberately <b>not</b> redacted —
    /// the log has to stay operationally useful (BR-21.8). <b>Free text</b> is a known limitation
    /// (BR-21.9): a name-based redactor cannot reach a contact identifier written inside free text.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultSensitiveFieldNames =
    [
        // Secrets — never negotiable.
        "password",
        "passwordconfirmation",
        "newpassword",
        "confirmpassword",
        "token",
        "accesstoken",
        "refreshtoken",
        "idtoken",
        "jwt",
        "secret",
        "secretkey",
        "apikey",
        "api_key",
        "authorization",
        "clientsecret",
        "signature",
        "privatekey",
        "connectionstring",

        // Account identifiers (DEC-096).
        "username",
        "userName",
        "createdbyuser",

        // Customer contact identifiers (DEC-093).
        "address",
        "contact",
        "contact1",
        "contact2",
        "customercontact",

        // Forward-looking — no property in the current model carries an e-mail address or a TIN.
        "email",
        "emailaddress",
        "tin"
    ];

    /// <summary>
    /// BR-21.4 — the sensitive query-parameter names: the body set plus <c>access_token</c> (the
    /// SignalR bearer) and <c>search</c> (a customer search term that matches a name, a company
    /// and a contact number).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultSensitiveQueryParameterNames =
        [.. DefaultSensitiveFieldNames, "access_token", "search"];

    /// <summary>The master switch. <c>false</c> makes the middleware a pass-through (AC-OPS-14).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Capture the request body (specification §5.2).</summary>
    public bool LogRequestBody { get; set; } = true;

    /// <summary>Capture the response body (specification §5.2).</summary>
    public bool LogResponseBody { get; set; } = true;

    /// <summary>BR-22.1 — the hard byte cap on any single captured body.</summary>
    public int MaxBodyLengthBytes { get; set; } = 8192;

    /// <summary>
    /// BR-21.3 — the configured body property names whose value is redacted.
    /// <para>
    /// <c>null</c> means "not configured", in which case <see cref="EffectiveSensitiveFieldNames"/>
    /// applies the BR-21 baseline set. A configured list <b>replaces</b> the baseline, so a name
    /// can genuinely be removed without a rebuild.
    /// </para>
    /// </summary>
    public IReadOnlyList<string>? SensitiveFieldNames { get; set; }

    /// <summary>
    /// BR-21.4 — the configured query parameter names whose value is redacted.
    /// <c>null</c> means "not configured"; see <see cref="SensitiveFieldNames"/>.
    /// </summary>
    public IReadOnlyList<string>? SensitiveQueryParameterNames { get; set; }

    /// <summary>The effective body property names to redact (the configured set or the BR-21 baseline).</summary>
    public IReadOnlyList<string> EffectiveSensitiveFieldNames =>
        SensitiveFieldNames is { Count: > 0 } ? SensitiveFieldNames : DefaultSensitiveFieldNames;

    /// <summary>The effective query parameter names to redact (the configured set or the BR-21 baseline).</summary>
    public IReadOnlyList<string> EffectiveSensitiveQueryParameterNames =>
        SensitiveQueryParameterNames is { Count: > 0 } ? SensitiveQueryParameterNames : DefaultSensitiveQueryParameterNames;

    /// <summary>The level of the request event for a 4xx response (specification §4.1).</summary>
    public LogEventLevel ClientErrorLevel { get; set; } = LogEventLevel.Warning;

    /// <summary>The level of the request event for a 5xx response (specification §4.1).</summary>
    public LogEventLevel ServerErrorLevel { get; set; } = LogEventLevel.Error;

    /// <summary>
    /// Path prefixes to skip. <b>Empty by default</b>, because REQ-OPS-01 requires a record for
    /// <i>every</i> endpoint. An operator may exclude a noisy path (specification §5.2).
    /// </summary>
    public IReadOnlyList<string> ExcludedPathPrefixes { get; set; } = Array.Empty<string>();
}
