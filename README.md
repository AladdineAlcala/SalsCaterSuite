# Sal's CaterSuite

Catering management system — a .NET 10 Web API (Clean Architecture) and a
React 19 + Vite 8 operations dashboard.

```
src/backend/      SalsCaterSuite.Domain · Application · Infrastructure · WebApi
                  SalsCaterSuite.UnitTests · SalsCaterSuite.IntegrationTests
src/frontend/     salscatersuite-ui (React + TypeScript + Tailwind)
scripts/          dev.mjs — the development orchestrator
```

---

## Running the stack

From the **repository root**:

```bash
npm run setup   # first time only — installs the frontend dependencies
npm run dev     # starts the API and the UI together
```

`npm run dev` is the single command you need for day-to-day work. It starts
both processes in one terminal, tagged so you always know which is which:

```
[api]  Now listening on: http://localhost:5149
[api]  Application started. Press Ctrl+C to shut down.
[web]  ➜  Local:   http://localhost:5173/
```

| Service | URL |
| :--- | :--- |
| UI (Vite dev server) | <http://localhost:5173> |
| API (Kestrel, HTTP) | <http://localhost:5149> |
| API (Swagger UI) | <http://localhost:5149/swagger> |

The browser only ever calls `/api/...` on its own origin; the Vite dev proxy
forwards those calls to the API on port 5149, so there is no CORS pre-flight
and the API's `UseHttpsRedirection` middleware is bypassed.

**Press `Ctrl+C` to stop both.** The orchestrator tears down the whole process
tree on the way out, so no orphaned `dotnet watch` or Vite process is left
holding a port.

### First run

The API applies pending EF Core migrations and seeds system-required data on
startup in the `Development` environment only. It expects SQL Server LocalDB:

```powershell
sqllocaldb start MSSQLLocalDB
```

The seeded administrator is `admin@salscatersuite.local` / `Admin@12345`.

---

## Commands

| Command | Purpose |
| :--- | :--- |
| `npm run dev` | Run the API and the UI together (the main entry point). |
| `npm run dev:api` | Run only the API, with hot reload (`dotnet watch`). |
| `npm run dev:web` | Run only the Vite dev server. |
| `npm run build` | Build both: `dotnet build` + `tsc`/`vite build`. |
| `npm run test` | Run both test suites: `dotnet test` + Vitest. |
| `npm run setup` | Install the frontend dependencies. |

The root `package.json` has **no dependencies** — the orchestrator uses only
Node built-ins, so `npm install` at the root is not required.

### Running one side only

`npm run dev:api` and `npm run dev:web` are useful when you are working on a
single area. They behave exactly as the corresponding half of `npm run dev`.

---

## Notes on the orchestrator

`scripts/dev.mjs` replaces the usual `concurrently` setup for a specific
Windows reason:

> Windows does not tear down a process tree when a parent dies, and a
> grandchild keeps the inherited stdout pipe open. `concurrently` therefore
> never observes a child exiting (its `close` event waits for a stdio EOF that
> the surviving grandchild holds), so `--kill-others-on-fail` and `--teardown`
> never fire. The visible symptom is an orphaned API still holding port 5149,
> which makes the *next* start fail with *"Failed to bind to address
> http://127.0.0.1:5149: address already in use"*.

The orchestrator instead watches the `exit` event, and tears the tree down
explicitly (`taskkill /T` on Windows, process groups elsewhere). It also
checks the dev ports before starting and reports the owning PID if something
is already there:

```
[dev] Port 5149 is already in use (PID 14080).
[dev] Another copy of the API is still running. Stop it, then start again:
[dev]   taskkill /PID 14080 /T /F
```

---

## Configuration

| Setting | Where | Notes |
| :--- | :--- | :--- |
| API connection string | `src/backend/SalsCaterSuite.WebApi/appsettings.Development.json` | LocalDB by default. Override with `ConnectionStrings__SalsCaterSuiteDb`. |
| Migrations / seeding on startup | `Database:ApplyMigrationsOnStartup`, `Database:SeedOnStartup` | `true` in Development only. Never point these at a shared or production server. |
| API proxy target | `VITE_API_PROXY_TARGET` | Defaults to `http://localhost:5149`. |
| Required booking deposit | `Bookings:Deposit:RequiredPercentage` | **ABSENT BY DEFAULT.** Absent means *not stated* — the workbench asks the operator for the deposit. Set it to a percentage to have the API quote the required deposit. Never hardcoded (DEC-101). |
| Commissary capacity | `Bookings:Capacity:MaximumPax` | **ABSENT BY DEFAULT.** Absent means *not stated* — the capacity gauge shows no ceiling. Set it to the kitchen's maximum daily pax to have the API publish the utilisation. Never hardcoded (DEC-102). |
| Menu-change window | Bookings:MenuSelection:LockDaysBeforeEvent | **ABSENT BY DEFAULT.** How many days before the event the dish selection locks. Absent, the lock date is the event date. The lock date is always published (DEC-128). |
| VAT disclosure rate | Bookings:Tax:VatPercentage | **ABSENT BY DEFAULT.** The VAT rate for the **inclusive** disclosure line on the contract ledger. Absent, no VAT line is rendered. The tax policy is still UNRESOLVED (DEC-008) and the rate is never hardcoded (DEC-143). |

---

## Logging

The API writes a **structured, machine-readable record of every request**: the method, path,
status, elapsed time, the authenticated user, the request and response bodies, and — when
something fails — the exception with its stack trace. One JSON object per line.

Two destinations ship enabled, and both are **configuration**:

| Destination | Where | Notes |
| :--- | :--- | :--- |
| Console | `Serilog:WriteTo` in `appsettings.json` | JSON lines, so the dev console and the log file read the same. |
| Rolling file | `Serilog:WriteTo` in `appsettings.json` | `logs/salscatersuite-<yyyyMMdd>.log`, one file per day, **31** kept, 10 MB per file. |

The file sink's path is relative to the application's **content root**, which in development is
the WebApi project directory — so the file lands in
`src/backend/SalsCaterSuite.WebApi/logs/`. Point it anywhere with an absolute path:

```jsonc
"Serilog": {
  "WriteTo": [
    { "Name": "File", "Args": { "path": "D:/logs/salscatersuite/salscatersuite-.log", "rollingInterval": "Day", /* … */ } }
  ]
}
```

or per run, without editing the committed file:

```powershell
$env:Serilog__WriteTo__1__Args__path = "D:\logs\salscatersuite\salscatersuite-.log"
```

```bash
# the last few request records
tail -f src/backend/SalsCaterSuite.WebApi/logs/salscatersuite-$(date +%Y%m%d).log
```

**Credentials are never written to the log.** Passwords, tokens, JWTs, secret keys and
connection strings are redacted by name, customer contact identifiers (email, contact numbers,
TIN, address) are redacted, and a body that is not valid JSON is not logged at all. Customer
**names** and transaction **amounts** are visible, so the log file is commercially sensitive and
the host's file permissions apply to it.

| Setting | Default | Notes |
| :--- | :--- | :--- |
| `Logging:RequestLogging:Enabled` | `true` | The master switch. `false` turns the record off. |
| `Logging:RequestLogging:LogRequestBody` | `true` | |
| `Logging:RequestLogging:LogResponseBody` | `true` | |
| `Logging:RequestLogging:MaxBodyLengthBytes` | `8192` | A longer body is truncated and flagged. |
| `Logging:RequestLogging:SensitiveFieldNames` | the secret + PII set | **Configuration**, not code. |
| `Serilog:WriteTo` | Console + File | **The destination switch.** |

To forward the log to **DataDog**, see
[`docs/datadog-log-forwarding.md`](docs/datadog-log-forwarding.md) — the Datadog Agent tails the
JSON log file, so no extra dependency and no API key are needed inside the application.

