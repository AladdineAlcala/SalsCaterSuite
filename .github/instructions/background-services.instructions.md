---
applyTo: "**/SalsCaterSuite.Worker.BackgroundServices/**/*.cs, **/*BackgroundService*.cs, **/*HostedService*.cs, **/*Worker*.cs"
---
# Background Services Rules

## Placement (mandatory)

Every new background service MUST be implemented inside the project:

    SalsCaterSuite.Worker.BackgroundServices

- Do not add background services to `SalsCaterSuite.WebApi`, `SalsCaterSuite.Application`,
  `SalsCaterSuite.Infrastructure`, or `SalsCaterSuite.Domain`.
- Do not create additional worker projects without an AGENT 01 decision recorded in
  `governance/decision-log.md` and a workplan update in the same cycle.

## Project structure

Organize each background service by concern under the Worker project:

    SalsCaterSuite.Worker.BackgroundServices/
    ├── {TypeOfBackgroundService}/
    │   ├── {Name}BackgroundService.cs      # the IHostedService / BackgroundService
    │   ├── {Name}Options.cs                # configuration options (if any)
    │   └── {Name}Schedule.cs               # trigger / schedule definition (if any)
    ├── Common/
    │   ├── Interfaces/                     # worker-local abstractions
    │   └── Extensions/                     # DI registration helpers
    ├── appsettings.json
    ├── appsettings.Development.json
    └── Program.cs                          # Host.CreateApplicationBuilder(...)

`{TypeOfBackgroundService}` is the category of the service, for example:

- `Scheduling` — cron / timer-driven jobs (e.g., nightly cleanup, reminder dispatch)
- `Messaging` — queue / bus consumers (e.g., SignalR fan-out, email queue)
- `Integration` — external system polling or sync (e.g., payment gateway reconciliation)
- `Reporting` — periodic report / export generation (e.g., daily sales summary)
- `Maintenance` — housekeeping (e.g., expired token purge, temp-file cleanup)

If a new category is needed, raise it to AGENT 01 rather than inventing one inline.

## Dependency direction

- The Worker project may reference `SalsCaterSuite.Application` and
  `SalsCaterSuite.Infrastructure` for DI composition — the same allowance
  `SalsCaterSuite.WebApi` already has.
- The Worker project MUST NOT be referenced by `Domain`, `Application`,
  `Infrastructure`, or `WebApi`.
- The Worker project MUST NOT contain business logic. Orchestration only —
  call Application use cases / services, never reimplement domain rules.
- EF Core access inside the Worker follows DEC-037: use repository interfaces
  from `Application/Common/Interfaces`; do not reference `SalsCaterSuiteDbContext`
  directly. Create a request scope per unit of work (`IServiceScopeFactory`).

## Implementation standards

- Each service derives from `BackgroundService` (or implements `IHostedService`
  for startup-only work) and lives in its own file.
- One service per file. Do not combine multiple `BackgroundService` classes in a
  single file (mirrors the DTO one-type-per-file rule).
- Override `ExecuteAsync(CancellationToken stoppingToken)` and honor the token on
  every awaited call. Do not swallow `OperationCanceledException` on shutdown.
- Wrap the loop body in try/catch so a single failure does not kill the host;
  log the failure with structured logging and continue.
- Make the interval / cron expression configurable via `IOptions<T>` bound from
  `appsettings.json`. Do not hardcode schedules.
- Register services and options in `Program.cs` (or a
  `Common/Extensions/ServiceCollectionExtensions.cs`) — one explicit registration
  per service.
- Never log credentials, tokens, connection strings, or sensitive personal data.
- Respect idempotency: a background job that may overlap a previous run must
  guard against double-processing (e.g., advisory lock, row version, or status
  check) before mutating financial or booking state.
- Financial mutations from a background service follow
  `.github/instructions/financial-integrity.instructions.md` — use `decimal`,
  explicit precision, and a transaction.

## Testing

- Unit-test the service's inner logic by extracting it into a callable method or
  an injected handler; do not test `ExecuteAsync` timing directly.
- Put worker tests under `SalsCaterSuite.UnitTests/Worker/` (or a dedicated
  `SalsCaterSuite.Worker.BackgroundServices.Tests` project only if AGENT 01
  approves a new project).
- Integration tests that need the worker host use `WebApplicationFactory`-style
  harness only if the worker exposes HTTP; otherwise use a plain
  `HostBuilder` harness in the unit test project.

## Registration and deployment

- The Worker project is a console host (`Microsoft.NET.Sdk.Worker`) with its own
  `Program.cs` and `appsettings*.json`.
- Do not merge worker hosting into `SalsCaterSuite.WebApi`. If a short-lived
  in-process job is unavoidable, raise it to AGENT 01 first.
- CI must build and test the Worker project alongside the rest of the solution.