# Forwarding the Sal's CaterSuite API Log to DataDog

**Applies to:** REQ-OPS-01 (Operational Logging) · DEC-095 · BR-23
**Owner decision:** DataDog is reached by the **Datadog Agent tailing the JSON log file** — not by a direct in-app HTTP sink.

---

## 1. Why this route

Datadog's own C# documentation lists three ways to get .NET logs into the platform:

| Route | Needs a package? | Needs an API key in the app? | Verdict |
| :--- | :--- | :--- | :--- |
| **Tail the log file with the Datadog Agent** | No | No | **Used here.** |
| Ship agentless through the APM Agent | No | No | An alternative, but it couples the log to the APM agent's own configuration. |
| Install the Serilog sink (`Serilog.Sinks.Datadog.Logs`) | Yes | Yes | **Rejected.** Its latest stable is `0.6.1`; it pins `Serilog >= 2.9.0` and pulls the unmaintained `Serilog.Sinks.PeriodicBatching 3.1.0` into a production API, and it cannot be verified without a DataDog API key. |

The file-tail route needs **nothing** from the application beyond the rolling JSON file it already
writes, and it is the route the owner approved (DEC-095).

---

## 2. What the application writes

The API writes one **JSON object per line** (JSON Lines) under `logs/`:

```
logs/salscatersuite-20261022.log
logs/salscatersuite-20261023.log
...
```

- One file per day (`rollingInterval: Day`).
- **31** files retained; a **10 MB** per-file cap with a size-based roll.
- Every line is a JSON object, so the Agent parses the fields as structured attributes rather
  than as a log message.

The sink list is configuration (`Serilog:WriteTo` in `appsettings.json`), so **no code change** is
needed to move the destination.

> **Where the file lands.** The sink's `path` is relative to the application's **current working
> directory**, which ASP.NET Core uses as the default content root. Under `npm run dev` that is
> the **WebApi project directory**, so the file lands in
> `src/backend/SalsCaterSuite.WebApi/logs/`. A **Windows service, an IIS app pool or a systemd
> unit** does not necessarily set a working directory — use an **absolute** path there.

---

## 3. The Datadog Agent configuration

Create `conf.d/csharp.d/conf.yaml` on the host (paths are relative to the Agent's
`/etc/datadog-agent` directory unless an absolute path is given):

```yaml
init_config:

instances:

  - name: salscatersuite-api
    logs:
      - type: file
        path: "/var/log/salscatersuite/salscatersuite-*.log"
        service: "salscatersuite-api"
        source: csharp
        sourcecategory: sourcecode
        tags: "env:production,app:salscatersuite"
```

Restart the Agent:

```bash
sudo systemctl restart datadog-agent
sudo agent status
```

`sudo agent status` lists the files the Agent is tailing. The log file must be **readable** by
the Agent's user (`datadog` on most installs) — check with:

```bash
sudo -u datadog ls -l /var/log/salscatersuite/
```

---

## 4. Pointing the application at a directory the Agent can read

The file sink's `path` is relative to the application's **content root** by default. To write
where the Agent expects, set the path explicitly:

```json
"Serilog": {
  "WriteTo": [
    { "Name": "Console", "Args": { "formatter": "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact" } },
    { "Name": "File", "Args": {
        "path": "/var/log/salscatersuite/salscatersuite-.log",
        "rollingInterval": "Day",
        "retainedFileCountLimit": 31,
        "fileSizeLimitBytes": 10485760,
        "rollOnFileSizeLimit": true,
        "formatter": "Serilog.Formatting.Compact.RenderedCompactJsonFormatter, Serilog.Formatting.Compact" } }
  ]
}
```

Override it per environment without editing the committed file:

```powershell
$env:Serilog__WriteTo__1__Args__path = "D:\logs\salscatersuite\salscatersuite-.log"
```

---

## 5. Using the log in DataDog

Because every line is a JSON object, the properties are queryable as attributes. The most useful
fields are:

| Attribute | Meaning |
| :--- | :--- |
| `@t` | the timestamp |
| `@m` | the message — the event name: `HttpRequestCompleted` or `HttpRequestFailed` |
| `StatusCode` | the HTTP status code |
| `ElapsedMs` | how long the endpoint took |
| `TraceId` | the correlation id — the same id appears on every other log line raised while the request was handled |
| `Method`, `Path`, `Query` | the request line (the query is redacted) |
| `UserId`, `UserName`, `Roles` | the authenticated user |
| `RequestBody`, `ResponseBody` | the redacted bodies (capped) |
| `ResponseBytes` | the bytes written to the client |
| `ErrorType`, `ErrorMessage` | present on a failure event |

The **field names above are the contract** — a sink that renders them as JSON exposes them under
these names, and the names are what a DataDog facet or a saved query is built on.

Useful searches:

- Every 5xx in the last day: `StatusCode:>=500`
- The slowest endpoints: sort by `ElapsedMs` descending
- One request's whole story: filter by its `TraceId`
- Every failure with its stack trace: filter on `ErrorType`

---

## 6. Operating notes

- **The file sink and the console sink are both on by default.** The record exists on the host
  whether or not an Agent is installed, so a deployment that forgets the Agent still has the log
  (RSK-044).
- **A misconfigured sink list is survivable.** If `Serilog:WriteTo` is empty or absent the host
  falls back to the console, so the logger is never sinkless (RSK-043).
- **The log is diagnostic, not evidential.** A log line is not an accounting record and the
  retention policy may delete it without any loss of business record (BR-24). The authoritative
  financial records remain the `Payment`, `Refund` and `Booking` rows and the documents rendered
  from them.
- **Credentials are never in the log.** Passwords, tokens, JWTs, secret keys and connection
  strings are redacted by name, and a body that is not valid JSON is not logged at all (BR-21).
  Customer **contact identifiers** are redacted; customer **names** and transaction **amounts**
  are visible by owner ruling (DEC-093) — so the log file is a copy of commercially sensitive
  data and the host's filesystem access control applies to it (RSK-040).
- **Turning the record off** is a configuration change, not a deployment:
  `Logging:RequestLogging:Enabled` = `false`.
