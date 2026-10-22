# Sal's CaterSuite — Feature Specification: Operational Logging & Request/Response Traceability (REQ-OPS-01)

| Field | Value |
| :--- | :--- |
| **Feature** | Operational Logging — errors, requests and responses on every endpoint |
| **Requirement** | REQ-OPS-01 |
| **Specification version** | **1.1** (supersedes v1.0 — see §11, the Debater disposition) |
| **Status** | **APPROVED for implementation** |
| **Approved by** | BI Architect (Agent 01) |
| **Owner rulings incorporated** | DEC-092, DEC-093, DEC-094, DEC-095 (2026-10-22) |
| **Applies to** | `src/backend` (.NET 10 Clean Architecture) — **backend only** |
| **Frontend impact** | **None.** No screen, route, component or client contract changes. |

> **Governance statement.** This feature writes a diagnostic record of every HTTP request to a
> configurable destination. It **never** changes a business rule, a financial formula, an API
> contract, a status code or a response body. The one hard constraint it carries is the
> **redaction rule (BR-21)**: a credential or a customer contact identifier must never reach a
> log file. That constraint is owner-ruled and is enforced in code, not by convention.

---

## 1. Business Objective

Sal's CaterSuite is operated by a small catering team. When a coordinator reports *"the booking
screen said it failed"* or *"the customer's payment vanished"*, there is currently **no record**
of what the API actually received, what it answered, how long it took, or what went wrong. The
API answers with a `400 { "errors": [...] }` envelope and the detail is gone.

This feature gives the business an **operational record**: for every endpoint, what was sent,
what came back, how long it took, and — when something failed — the exception with its stack
trace. The record is written in a **machine-readable, structured** form so it can be read by a
person with a text editor, or shipped to a **DataDog** dashboard by the Datadog Agent, without
changing the application.

### 1.1 What it is not

- It is **not** an audit trail. It is a diagnostic record; it does not replace the `BaseEntity`
  audit columns (DEC-038) and it is not evidence of a financial transaction.
- It is **not** a monitoring product. It produces the *telemetry* a monitoring product consumes;
  the dashboards, alerting and retention policies live in the observing platform.
- It is **not** a new business concept. **No** new domain entity, **no** new field, **no**
  migration, **no** KPI, **no** API contract change. The Domain and Application layers are
  **untouched** — logging is a transport concern and lives entirely in the WebApi host.

---

## 2. Scope and Exclusions

### 2.1 In scope

| # | Item |
| :--- | :--- |
| S1 | One structured **request-completion event** per HTTP request, on **every** endpoint, carrying the method, path, query, status, elapsed time, client IP, the authenticated user and the request/response bodies. |
| S2 | One structured **failure event** per raised exception, carrying the exception and its stack trace plus the same request context. |
| S3 | A **redaction engine** that removes credentials and customer contact identifiers from every logged body, query string and header, before the value is written. |
| S4 | A **pluggable destination**: the sink list is configuration, so the same build writes to a text file, to a platform such as DataDog, or to both. |
| S5 | A **rolling file sink** as the always-available destination, with a retention and size policy, writing JSON lines so the Datadog Agent can parse them. |
| S6 | A **correlation id** attached to the request event *and* to every other log line raised while the request is being handled. |
| S7 | **Bounded capture**: a hard byte cap on any body, and no capture at all of binary content. |

### 2.2 Explicitly out of scope

| # | Excluded | Reason |
| :--- | :--- | :--- |
| X1 | Any change to a request DTO, response DTO, status code, route or the `{ "errors": [...] }` contract | The feature observes; it never alters. |
| X2 | Any change to a business rule, a financial formula, a KPI or the dashboard | No figure is derived from a log. |
| X3 | Any database write, entity, migration or seed | The log is a file/sink, not a table. |
| X4 | Any frontend change | No screen consumes the log. |
| X5 | A **direct** DataDog HTTP sink written in this project | `Serilog.Sinks.Datadog.Logs` is version `0.6.1`, pins `Serilog >= 2.9.0` and drags the unmaintained `Serilog.Sinks.PeriodicBatching 3.1.0` into a production API, and it cannot be live-verified without a DataDog API key. The **Agent file-tail** route (DEC-095) is used instead and needs no dependency at all. |
| X6 | Persisting the log to `SalsCaterSuiteDb` | A log table is a growth, retention and privacy problem the business has not asked for. |
| X7 | Sampling, rate-limiting or log-volume analytics | Volume is bounded by the retention and cap policy (BR-22, BR-23). |
| X8 | The `ISalsCaterSuiteDbContext`-style coupling of the log to the data layer | DEC-037's repository-only rule is unchanged; the log never touches EF Core. |
| X9 | Changing the **domain-exception** path | `DomainExceptionHandler` stays exactly as it is. The feature adds a **sibling** handler (§4.4) and changes nothing about how a domain rule violation is answered. |

---

## 3. Business Rules

### 3.1 BR-21 — Redaction (owner-ruled, non-negotiable)

1. Redaction applies to a **JSON** request or response body by **property name**,
   **case-insensitively**, at **any depth**, including properties inside arrays.
2. A redacted property's value is replaced by the literal marker `***REDACTED***`.
3. **The sensitive-name set applies to the record's own field names as well as to the body's
   property names.** The record's fields are named in §4.2, and `UserName` is matched by
   `username` / `userName` in the set — so the record's `UserName` field is redacted. `UserId`
   and `Roles` are **not** in the set and are kept, so "who did this" is still answerable and the
   operator can be resolved from the identity table by `UserId`.
   This matters concretely: in this deployment the **username is the operator's e-mail address**,
   and the login name is also a **credential identifier**. Redacting the body's `username` while
   writing the identical value in clear one field later would make the body redaction
   self-defeating.
3. The **default sensitive-name set** is configuration, not code, so it can be changed without a
   rebuild. The set is **verified against the as-built DTOs** — it names properties that exist:

   | Group | Names | Why |
   | :--- | :--- | :--- |
   | **Secrets** (never negotiable) | `password`, `passwordconfirmation`, `newpassword`, `confirmpassword`, `token`, `accesstoken`, `refreshtoken`, `idtoken`, `jwt`, `secret`, `secretkey`, `apikey`, `api_key`, `authorization`, `clientsecret`, `signature`, `privatekey`, `connectionstring` | A credential in a log is reusable until it is rotated. |
   | **Account identifiers** | `username`, `userName`, `createdbyuser` | In this deployment the **username is the administrator's email address** (`Seed:Admin:UserName` = `admin@salscatersuite.local`), so an unredacted username is a contact identifier on disk. `createdByUser` is a user name on every payment and audit record. |
   | **Customer contact identifiers** (owner-ruled, DEC-093) | `address`, `contact`, `contact1`, `contact2`, `customercontact` | These are the **only** contact-identifier property names in the codebase: `Customer.Address` / `.Contact1` / `.Contact2`, `CustomerStatementDto.Contact` and `BookingDto.CustomerContact`. |
   | **Forward-looking** | `email`, `emailaddress`, `tin` | **No property in the current model carries an email address or a TIN** (`Customer` has neither). They are kept so the rule is already correct if the model ever grows one, and the specification says so rather than implying they exist. |

4. The **query string** is redacted by parameter name against the same set, plus `access_token`
   (the SignalR bearer) and `search` (a customer search term that matches a name, a company and
   a contact number — `CustomersController` documents it as such).
5. **Request headers are never logged.** The single header-derived field is the boolean
   `HasAuthorization`. An `Authorization` or `Cookie` header value can never reach a log.
6. A body that is **not valid JSON** is **not logged**. The field is omitted and
   `[non-json body omitted]` is written instead. This is deliberate: the API accepts JSON only,
   and a malformed body is exactly the shape in which a credential could hide from a name-based
   redactor.
7. A **binary** response (a content type that is not `application/json`, `application/problem+json`,
   `application/xml` or `text/*`) is logged by **content type and byte count only**. Its bytes are
   never written to the log.
8. **Customer names and transaction amounts are NOT redacted by default.** The owner ruled that
   the log must stay operationally useful — a booking that failed must be identifiable and its
   amount must be visible. This is the deliberate consequence of DEC-093 and it is a **data-exposure
   decision**, not an oversight.
9. **Free text is a known limitation.** A name-based redactor cannot reach a contact identifier
   or a credential written inside free text - `CreateBookingRequestDto.SpecialInstructions` and
   `.Reference`, `CancelBookingRequestDto.Reason`, `RecordPaymentRequestDto.Notes`,
   `.Particular` and `.CheckNo` are logged verbatim. This is stated here and in RSK-040 rather
   than left implicit. A future free-text field that is expected to carry personal data must be
   added to the set by name.
10. **The rule applies to the log file as a whole, not only to the request record.** The redaction
    engine governs what the request logger writes; **no application log message** may echo a
    credential or a contact identifier either. The one such message in the codebase - the
    seeder's *"Bootstrap administrator already present"* notice, which fires on **every** startup
    - now names the account by its **id**, not by its login name (DEC-097). The exposure was
    created by the file sink itself: before it, that message reached only the ephemeral console.

### 3.2 BR-22 — Bounded capture

1. At most **8 192 bytes** of any single body is captured (configurable: `MaxBodyLengthBytes`).
   A longer body is **truncated** and the event carries `RequestBodyTruncated` /
   `ResponseBodyTruncated` = `true`.
2. Capture is **read-only**. The bytes the client receives are **byte-for-byte identical** to the
   bytes the application produced. The response buffer is written back unchanged.
3. A body that is not captured contributes **nothing** to the event — no partial JSON, no
   half-written document.
4. **The request body stream must be rewound.** The middleware reads the request body to capture
   it, and must then set `Request.Body.Position = 0` so that model binding still sees the **whole**
   body. This is not optional: without the rewind the endpoint binds an **empty** body and every
   write endpoint silently accepts nothing. The buffering wrapper is left in place; the **original**
   stream is **not** restored, because the original stream has already been consumed and restoring
   it would hand the endpoint a truncated body.
5. `Request.EnableBuffering()` imposes **no** size limit by default — its 30 KB argument is the
   threshold at which the buffer spills to a temporary file, not a cap. A request body of any
   size that the API accepts today is still accepted with the middleware in place.
6. **The response capture stream must be seekable** (`CanSeek == true`, `SetLength` honoured).
   The error path calls `HttpResponse.Clear()`, which resets a seekable body and **cannot** reset a
   non-seekable one — with a non-seekable capture the stale bytes of a partially written response
   survive into the recorded body. A seekable capture is what makes the recorded body the body
   **as it stands after the reset**.
7. The middleware must **not** replace `Response.Body` on a **WebSocket upgrade** request
   (`HttpContext.WebSockets.IsWebSocketRequest`). The WebSocket adapter runs over
   `IHttpResponseBodyFeature`, not over `Response.Body`, so swapping the body there would break a
   future `NotificationHub`. No hub is mapped today, but the rule is pinned now.

### 3.3 BR-23 — Destination and retention

1. The destination list is **configuration** (`Serilog:WriteTo`). Adding, removing or reordering a
   sink is an `appsettings` edit, not a rebuild.
2. The always-available destination is a **rolling file**, one file per day, JSON lines, **31**
   files retained, **10 MB** per-file cap with a size-based roll.
3. A **JSON line** format is used, not a plain-text template, so the Datadog Agent parses the
   fields (`@t`, `@m`, `StatusCode`, `ElapsedMs`, `TraceId`, …) as structured attributes. A JSON
   formatter is also what makes the scoped `TraceId` visible: a text template does not render
   properties it does not name.
4. The file sink's path is relative to the **current working directory**, which ASP.NET Core uses
   as the default content root. Under `npm run dev` that is the **WebApi project directory**, so
   the file lands in `src/backend/SalsCaterSuite.WebApi/logs/`. A Windows service, an IIS app
   pool or a systemd unit must set the working directory or use an **absolute** path.
5. The `logs/` directory is **excluded from version control**.

### 3.4 BR-24 — The record is diagnostic, not evidential

A log line is **not** an accounting record, a receipt, a legal document or proof of a financial
transaction. The authoritative financial records remain the `Payment`, `Refund` and `Booking`
rows and the documents rendered from them (DEC-012, DEC-023, BR-04). A log line may be
**deleted** by the retention policy without any loss of business record.

---

## 4. The Log Record Contract

### 4.1 The event matrix

**One `HttpRequestCompleted` is written for every request that reaches the pipeline and
completes** — success or failure. **One `HttpRequestFailed` is written in addition** when the
request ended in an **unhandled** fault.

| Outcome | `HttpRequestCompleted` | `HttpRequestFailed` | Exception attached | Response body captured |
| :--- | :--- | :--- | :--- | :--- |
| 2xx / 3xx | yes, at `Information` | no | no | yes |
| 4xx produced by a controller (a validator rejection, a `404`, a `409`) | yes, at `ClientErrorLevel` (default `Warning`) | no | no | yes |
| 4xx produced by a **handled** `DomainException` | yes, at `ClientErrorLevel` | **no** — the exception was handled, and the existing handler already logs it at `Warning` | no | yes |
| 5xx — an unhandled fault answered with `ProblemDetails` | yes, at `ServerErrorLevel` (default `Error`) | **yes, at `Error`** | **yes — the exception and its stack trace** | yes — the `application/problem+json` body |
| The exception escaped the whole pipeline (the safety-net case) | **no** — the request never completed inside the pipeline | **yes, at `Error`** | yes | no |
| The client aborted the request | yes, at `ClientErrorLevel` | no | no | no |

**The rule that decides the second event:** `HttpRequestFailed` is written when a captured
exception exists **and** the response status is **5xx**. A handled exception (a 4xx) is already
recorded by the `HttpRequestCompleted` event and, for a domain rule, by the existing
`DomainExceptionHandler`'s own `Warning` — it is not logged twice.

### 4.2 Fields

| Field | Type | Null? | Source |
| :--- | :--- | :--- | :--- |
| `TraceId` | string | no | `HttpContext.TraceIdentifier` |
| `Method` | string | no | `Request.Method` |
| `Path` | string | no | `Request.Path` (no query) |
| `Query` | string | yes | `Request.QueryString`, redacted |
| `StatusCode` | int | no | `Response.StatusCode` |
| `ElapsedMs` | double | no | A stopwatch around the pipeline |
| `ClientIp` | string | yes | `Connection.RemoteIpAddress` |
| `UserId` | string | yes | the `ClaimTypes.NameIdentifier` claim — the Identity key (a GUID). **Not** redacted: it is the audit anchor. |
| `UserName` | string | yes | the `ClaimTypes.Name` claim. **Redacted** — `username` is in the sensitive set (BR-21.3.3), and in this deployment that claim is the operator's e-mail address. |
| `Roles` | string[] | yes | the `ClaimTypes.Role` claims |
| `HasAuthorization` | bool | no | whether an `Authorization` header was present |
| `RequestContentType` | string | yes | `Request.ContentType` |
| `RequestBody` | string | yes | redacted, capped (§3.2) |
| `RequestBodyTruncated` | bool | no | |
| `ResponseContentType` | string | yes | `Response.ContentType` |
| `ResponseBody` | string | yes | redacted, capped (§3.2) |
| `ResponseBodyTruncated` | bool | no | |
| `ResponseBytes` | long | no | the total bytes written to the **original** response stream (not the bytes captured) |
| `ErrorType` | string | yes | the exception type name — `HttpRequestFailed` only |
| `ErrorMessage` | string | yes | the exception message — `HttpRequestFailed` only |

The **message** of the event is the **event name** — `HttpRequestCompleted` or `HttpRequestFailed` —
and the **field names above are the contract**: a sink that renders them as JSON exposes them
under these names, and the names are what a DataDog facet or a saved query is built on.

`null` means **absent**, never zero. The record carries **no** business figure: no amount, no
balance, no count. Those live in the authoritative records (§3.4).

### 4.3 The correlation id

`TraceId` is **also** pushed into the logging scope for the duration of the request, so **every**
other log line raised while the request is handled — a repository warning, a domain-rule
violation, an EF Core message — carries the same `TraceId`. One identifier therefore joins the
request event to any error raised inside it.

Two conditions make this work, and both are required:

1. The Serilog configuration must include **`.Enrich.FromLogContext()`**. Without it the scope is
   silently dropped and no line but the request event carries the `TraceId`.
2. The sink must use a **JSON formatter**. A text template renders only the properties it names,
   so a scoped property is present on the event but invisible in the output.

### 4.4 The pipeline position and the exception capture

**Verified on .NET 10 (SDK 10.0.402).** With the as-built wiring —
`AddExceptionHandler<DomainExceptionHandler>()`, `AddProblemDetails()` and `app.UseExceptionHandler()` —
the exception-handler middleware consumes **every** exception: the `IExceptionHandler` chain runs
first, and the configured ProblemDetails response is used only when every `TryHandleAsync`
returns `false`. An observer middleware registered **before** `UseExceptionHandler()` therefore
sees **no** exception for either a `DomainException` (answered `400 { "errors": [...] }`) or an
`InvalidOperationException` (answered `500 application/problem+json`).

The consequence is that the exception object **cannot** reach the logging middleware by
re-throw. The feature therefore delivers it explicitly:

- A new **`RequestLoggingExceptionHandler : IExceptionHandler`** is registered **after**
  `DomainExceptionHandler`. `IExceptionHandler` implementations are invoked in **registration
  order**, so:
  - a **domain rule violation** is handled first by `DomainExceptionHandler` and still produces
    the approved `400 { "errors": [...] }` envelope, and it is recorded by the request-completion
    event at the client-error level — it is **not** logged twice;
  - an **unhandled fault** is declined by `DomainExceptionHandler` and observed by
    `RequestLoggingExceptionHandler`, which writes the `HttpRequestFailed` event — with the
    exception, its type, its message and its stack trace — and returns **`false`**, so the
    framework's own `ProblemDetails` fallback still answers the request exactly as it does today.
- The handler returns `false` and **never writes a response**. It changes no status code, no
  response body and no content type.
- The middleware and the handler share a small per-request state object on
  `HttpContext.Items` (`SalsCaterSuite.RequestLogging`) that carries the stopwatch, the captured
  request body and a **"the failure event has already been written"** flag — so exactly **one**
  `HttpRequestFailed` event is written per request, whoever observes the exception first.
- The middleware keeps its own error path for the case where an exception escapes the whole
  pipeline; that path is guarded by the same flag.

The exception is therefore logged **before** the response is written, and the response is logged
by the completion event afterwards. The two events describe the two halves of the failure: the
exception, and the response that answered it.

The pipeline becomes:

```
app.UseMiddleware<RequestLoggingMiddleware>();   // 1st — the outermost middleware
app.UseExceptionHandler();                        // existing, unchanged
... Swagger (Development only) ...
app.UseCors("SalsCaterSuiteUi");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

The `400 { "errors": [...] }` body a **controller** writes (a validator rejection, a `404`, a
`409`) is captured, because the controller runs inside the middleware's scope. The `DomainException`
path is registered and behaves exactly as it does today, but **no current endpoint reaches it**:
every `DomainException` the domain raises is caught in the Application layer and converted into a
`Result.Failure` (`CreateBookingCommand`, `CancelBookingCommand`, `PaymentLedgerService`), and
the controller writes the `400` itself. The feature does not change that.

The **500 `ProblemDetails`** body produced by an unhandled fault **is** captured: it is
`application/problem+json`, which BR-21.7 treats as textual.

---

## 5. The Configuration Contract

### 5.1 The `Serilog` section (the destination)

```jsonc
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "WriteTo": [
    { "Name": "Console", "Args": { "formatter": "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact" } },
    { "Name": "File",    "Args": { "path": "logs/salscatersuite-.log", "rollingInterval": "Day",
                                  "retainedFileCountLimit": 31, "fileSizeLimitBytes": 10485760,
                                  "rollOnFileSizeLimit": true,
                                  "formatter": "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact" } }
  ],
  "Properties": { "Application": "SalsCaterSuite.Api" }
}
```

- **`WriteTo[0]` is the Console sink and `WriteTo[1]` is the File sink.** The order is pinned
  because the integration test overrides `WriteTo[1]`'s path to a temporary directory.
- `WriteTo` **is** the "text file or DataDog" switch (DEC-095). A DataDog deployment edits this
  array and the agent configuration; the application code does not change.
- The file sink's `path` is relative to the **current working directory** (the content root by
  default) — see BR-23.4.
- **The wiring.** The logger is configured on the **host builder**:

  ```csharp
  builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
      .ReadFrom.Configuration(context.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Application", "SalsCaterSuite.Api")
      .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));
  ```

  `IHostBuilder.UseSerilog(Action<HostBuilderContext, LoggerConfiguration>, bool preserveStaticLogger, bool writeToProviders)`
  is the extension in `Serilog.Extensions.Hosting 10.0.0`; with `writeToProviders` left at its
  default `false` it **replaces** the default providers, so there is no duplicate console output.
  **There is no `IApplicationBuilder.UseSerilog(...)`** — `Serilog.AspNetCore` exposes only
  `UseSerilogRequestLogging` on the application builder, and this feature does **not** use it.
  Verified: the startup logger (the migration and seed notices) and every in-request line reach
  the file sink through this wiring.
- **The configured `WriteTo` list must not be overridden.** The `AddSerilog` sample form appends
  `.WriteTo.Console(...)` *after* `ReadFrom.Configuration(...)`, which would silently defeat the
  configuration-driven destination switch that is the whole point of DEC-095. The console sink
  comes from the configuration, not from code.
- **The safety net**: if `Serilog:WriteTo` is absent or empty, the host falls back to a
  **Console** sink. A misconfigured deployment must never produce a logger with **no** sink,
  which would silently discard every log line. The decision is taken by inspecting the
  **configuration section** — `configuration.GetSection("Serilog:WriteTo").GetChildren()` counted
  with a **non-empty `Name`** — because a built `LoggerConfiguration` exposes no sink count.
  (A child whose `Name` is absent or blank does not count; `ReadFrom.Configuration` accepts it
  without throwing and produces a sinkless logger.)
- The **Development** file must mirror the base levels it had before Serilog took over —
  `Default: Debug`, `Microsoft.AspNetCore: Information`, `Microsoft.EntityFrameworkCore:
  Information` — so the Development console's verbosity does not change silently. It must **not**
  repeat `WriteTo`: an array in an environment file **replaces** the base array rather than
  merging with it.

### 5.2 The `Logging:RequestLogging` section (the behaviour)

| Key | Default | Meaning |
| :--- | :--- | :--- |
| `Enabled` | `true` | The master switch. `false` removes the middleware entirely — it must not touch `Request.Body` or `Response.Body` at all. |
| `LogRequestBody` | `true` | Capture the request body. |
| `LogResponseBody` | `true` | Capture the response body. |
| `MaxBodyLengthBytes` | `8192` | The capture cap (BR-22). |
| `SensitiveFieldNames` | the BR-21.3 set | The body property names to redact. |
| `SensitiveQueryParameterNames` | the BR-21.3 set + `access_token` + `search` | The query parameter names to redact. |
| `ClientErrorLevel` | `Warning` | The level of a 4xx request event. |
| `ServerErrorLevel` | `Error` | The level of a 5xx request event. |
| `ExcludedPathPrefixes` | `[]` | Path prefixes to skip. **Empty by default**, because the requirement is *every* endpoint. The Development file should list `/swagger` so the Swagger UI page — a `text/html` document that is captured as text — does not fill the console. |

### 5.3 The relationship between the two sections

`Serilog` owns **where** the log goes and **at what level** the pipeline logs.
`Logging:RequestLogging` owns **what the record contains**.
They are separate on purpose: an operator can turn body capture off without touching the sink
list, and can move the sink to DataDog without touching the redaction policy.

The legacy `Logging:LogLevel` subtree is **removed** from `appsettings*.json` — once Serilog owns
the pipeline the stock providers are gone and that subtree is inert. Its levels are mirrored into
`Serilog:MinimumLevel`. The integration-test host's EF command-log override moves with it, to
`Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command`.

---

## 6. Acceptance Criteria

| ID | Criterion |
| :--- | :--- |
| **AC-OPS-01** | Every HTTP request that reaches the pipeline and completes produces exactly **one** `HttpRequestCompleted` event carrying the method, path, status code and elapsed time. |
| **AC-OPS-02** | A request that ends in an unhandled fault produces exactly **one** `HttpRequestFailed` event at `Error`, carrying the exception type, the message and the stack trace, **in addition to** its `HttpRequestCompleted` event, and with the same correlation id. The exception is still answered by the existing error pipeline, which is unchanged. |
| **AC-OPS-03** | A `POST /api/Auth/login` body logged by the middleware contains **no** password value: the `password` property is `***REDACTED***`. The **response** body of the same call contains **no** JWT: the `token` property is `***REDACTED***`. The request's `username` is `***REDACTED***` as well. |
| **AC-OPS-04** | A customer `address`, `contact`, `contact1`, `contact2` and `customercontact` in a logged body are `***REDACTED***`; a `createdByUser` and a `userName` are `***REDACTED***`. A customer **name** and a transaction **amount** are **not** redacted. |
| **AC-OPS-05** | A body longer than `MaxBodyLengthBytes` is truncated, the matching `*BodyTruncated` flag is `true`, and the recorded length never exceeds the cap. |
| **AC-OPS-06** | A response whose content type is not textual — the XLSX export, the SOA PDF, the package spec sheet — records its content type and byte count and contains **no** body text. |
| **AC-OPS-07** | The bytes the client receives are **identical** to the bytes the application produced, for a JSON response, an XLSX export and a PDF. |
| **AC-OPS-08** | A body that is not valid JSON is not logged; the record carries the `[non-json body omitted]` marker. |
| **AC-OPS-09** | A query parameter whose name is in the sensitive set — including `search` and `access_token` — is redacted; the other parameters are preserved. |
| **AC-OPS-10** | An `Authorization` header value never appears in any log line; the record carries `HasAuthorization` = `true` instead. |
| **AC-OPS-11** | Every log line raised while a request is handled carries the same `TraceId` as that request's event — proven by raising a **second** log line inside a request and reading the `TraceId` off it. |
| **AC-OPS-12** | A `400 { "errors": [...] }` produced by a **controller** (a validator rejection) is logged with status `400` and its envelope captured. |
| **AC-OPS-13** | With `Serilog:WriteTo` empty, the host still logs to the console (the safety net). |
| **AC-OPS-14** | With `Logging:RequestLogging:Enabled` = `false`, **no** request event is produced, `Request.Body` and `Response.Body` are the **original** stream instances, and the API behaves exactly as before. |
| **AC-OPS-15** | The rolling file sink writes one **JSON object per line** — so the Datadog Agent can parse it, and the scoped `TraceId` is visible in it — and the file lands under the content root's `logs/` directory. |
| **AC-OPS-16** | The whole pre-existing suite stays green: the feature changes **no** status code, **no** response body and **no** business behaviour. A request body larger than the framework's buffering threshold is still bound in full. |

---

## 7. Required Tests

| Suite | Location | Must cover |
| :--- | :--- | :--- |
| `SensitiveDataRedactorTests` | `tests/SalsCaterSuite.UnitTests/Api/Logging/` | AC-OPS-03, 04, 08, 09, 10 — every name in the BR-21.3 set (the secrets, the account identifiers, the contact identifiers), case-insensitivity, nesting, arrays, a non-JSON body, a null body, an empty body, the query-string redaction, the header rule. |
| `RequestLoggingOptionsTests` | `tests/SalsCaterSuite.UnitTests/Api/Logging/` | the defaults, the configuration binding, the master switch. |
| `RequestLoggingMiddlewareTests` | `tests/SalsCaterSuite.UnitTests/Api/Logging/` | AC-OPS-01, 02, 05, 06, 07, 08, 11, 12, 14, 16 — the event shape, the level mapping, the elapsed time, the truncation, the binary omission, the byte-for-byte write-back, the error path, the correlation id, the disabled switch (the original stream instances are untouched), the request-body rewind (a body larger than the framework's buffering threshold is still bound in full), the capture stream's seekability, and the WebSocket-upgrade exemption. |
| `SerilogLoggingConfigurationTests` | `tests/SalsCaterSuite.UnitTests/Api/Logging/` | AC-OPS-13, 15 — the console fallback (and that it is decided from the **configuration section**), the file sink, the JSON formatter, the retention arguments, and that `FromLogContext` is in the configuration. |
| `RequestLoggingEndpointTests` | `tests/SalsCaterSuite.IntegrationTests/` | AC-OPS-01, 02, 03, 06, 12, 16 — a **real** HTTP call through the test host against a **disposable** database: the login call's password, username and JWT are absent from the log file; the XLSX export, the SOA PDF and the package spec sheet are each logged without a body; a controller-produced `400` is logged with its envelope; a **test-only endpoint** that raises an unhandled fault is logged at `Error` with a stack trace; and the pre-existing endpoint tests stay green. |

The test-only fault endpoint is registered through the integration host's
`ConfigureTestServices` hook and a dedicated `ApplicationPart`, so that **no** production route is
added. The file sink is pointed at a **per-run temporary directory** by overriding
`Serilog:WriteTo:1:Args:path` through `AddInMemoryCollection`; the test then reads the file and
asserts on its content. Nothing is written into the repository's `logs/` folder by a test.

---

## 8. Risks

| # | Risk | Severity | Mitigation |
| :--- | :--- | :--- | :--- |
| R1 | The log file becomes a **plain-text copy of customer data** on the server's disk. | HIGH | The redaction rule (BR-21) removes the credentials, the account identifiers and the contact identifiers; the owner has ruled that names and amounts stay visible (DEC-093). Free text is a stated limitation (BR-21.9). The retention policy bounds the exposure window to 31 days. The `logs/` directory is excluded from version control. |
| R2 | Response buffering adds latency and memory to **every** request. | MEDIUM | The capture is capped at 8 KB and a body that exceeds the cap **streams through** rather than being buffered. The cap is configurable. |
| R3 | A middleware fault would break the API. | HIGH | The middleware **never** swallows an application exception: it logs and rethrows. A failure inside the *logging* code is caught and suppressed so that a logging defect can never become an API outage. |
| R4 | A misconfigured `Serilog` section produces a logger with no sink and **silently discards every line**. | HIGH | The console safety net (AC-OPS-13), decided from the configuration section, asserted by a unit test. |
| R5 | The added package changes the pipeline's logging behaviour for the existing code. | MEDIUM | `Serilog.AspNetCore` is a **provider**, not a replacement: the existing `ILogger<T>` calls keep working unchanged. The pre-existing suite must stay green (AC-OPS-16). |
| R6 | The DataDog route is configured **outside** the application (the Agent), so a deployment can ship the app and forget the agent. | MEDIUM | The ready-made agent configuration and the operating note are delivered with the feature. |
| R7 | The middleware reads the request body and leaves it rewound; a mistake would make every write endpoint accept nothing. | HIGH | The rewind is pinned by a test that posts a body **larger** than the framework's buffering threshold and asserts the endpoint binds it in full (AC-OPS-16). |

---

## 9. Open Questions for the Owner

| # | Question | Status |
| :--- | :--- | :--- |
| Q1 | Should the log be shipped to a **different** destination than the file (a second sink), and should the file sink then be disabled in production? | **OPEN** — the sink list is configuration, so this is an `appsettings` edit with no code change. |
| Q2 | Should the request/response bodies be captured at all on the **production** host, or only on the development host? | **OPEN** — `LogRequestBody` / `LogResponseBody` are already independent switches. |

Neither question blocks the implementation of this specification.

---

## 10. What changed from v1.0

| # | Change | Driver |
| :--- | :--- | :--- |
| 1 | The exception is delivered through a **capture `IExceptionHandler`** registered before `DomainExceptionHandler`; §4.4 is rewritten around the verified .NET 10 behaviour. | F1 |
| 2 | AC-OPS-12 is restated against a **controller-produced** `400`; the domain-exception path is recorded as registered but unreachable from any current endpoint. | F2 |
| 3 | The default sensitive-name set is **corrected against the as-built DTOs** — `username`, `userName`, `createdByUser` and `customercontact` are added; `email`/`emailaddress`/`tin` are documented as forward-looking. | F3 |
| 4 | The capture stream **must be seekable**. | F4 |
| 5 | The request-body **rewind** is stated as a rule; the "restore the original stream" instruction is withdrawn (it would hand the endpoint a truncated body) and the 30 KB "limit" is corrected — it is a spill threshold, not a cap. | F5 |
| 6 | `.Enrich.FromLogContext()` and a **JSON formatter** are both required, and the correlation criterion is proven by a second in-request log line. | F6 |
| 7 | The wiring is named exactly: `builder.Host.UseSerilog(...)`. The "no `IApplicationBuilder.UseSerilog`" fact, the "do not override the configured `WriteTo`" constraint and the startup-logger coverage are stated. | F7 |
| 8 | The **event matrix** is written down. | F8 |
| 9 | The test plan is extended: the test-only fault endpoint, the three binary responses, the byte-for-byte comparison, the correlation test, the strengthened disabled-switch test and the body-rewind test. | F9 |
| 10 | The WebSocket-upgrade exemption, the `search` query parameter, the `ResponseBytes` definition, the message template, the `WriteTo` order, the safety-net detection mechanism, the Development level mirroring, the file-path wording, the Swagger recommendation and the scenario renumbering are all folded in. | M1..M12 |

## 11. Debater disposition (2026-10-22)

The Debater returned **`PROPOSE_BETTER_PLAN`** with ten blocking findings (F1–F10) and twelve
clarifications (M1–M12). **All ten findings and all twelve clarifications were accepted and
applied** to this specification (v1.0 → v1.1), to BR-21..BR-24, to DEC-092 and to the acceptance
scenarios. F1, F4, F5, F6 and F7 were re-verified **independently by AGENT 01** on the installed
SDK (10.0.402) with a throwaway host, because each of them changes an implementation instruction:

| Claim | AGENT 01's independent result |
| :--- | :--- |
| F1 — the exception never reaches an outer middleware | **Confirmed.** `seenException = <none>` for both a `DomainException` (400) and an `InvalidOperationException` (500). The capture-handler fix was verified end to end: `HttpContext.Items` carried both exception types and the 400 envelope was unchanged. |
| F4 — `Response.Clear()` needs a seekable body | **Confirmed.** A seekable capture recorded `ERROR-ENVELOPE`; a non-seekable capture recorded `PARTIALERROR-ENVELOPE`. |
| F5 — `EnableBuffering()` imposes a 30 KB limit | **Rejected.** The 30 KB argument is the *spill threshold*; `bufferLimit` defaults to unlimited. A 200 000-byte body was captured (to the 8 KB cap), rewound, and bound **in full** by the endpoint. The "restore the original stream" instruction was **withdrawn** — it would hand the endpoint an already-consumed body. |
| F6 — `FromLogContext` is required | **Confirmed.** With the JSON formatter the scoped `TraceId` is rendered as a top-level field. |
| F7 — the Serilog wiring | **Corrected.** `IHostBuilder.UseSerilog` **does** exist in `Serilog.Extensions.Hosting 10.0.0` and works: the file sink received the startup, the in-request and the handler events. There is **no** `IApplicationBuilder.UseSerilog`. DEC-092's wording is corrected to name the host-builder form. |
| F7 (M1) — the safety net | **Confirmed and refined.** An absent or empty `WriteTo` yields a sinkless logger without throwing. The detection must count configuration children with a **non-empty `Name`**. |
