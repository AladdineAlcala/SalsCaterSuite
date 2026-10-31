# Background Services Workplan: Sal's CaterSuite

**Canonical path:** `docs/salscatersuite_background_services_workplan.md`
**Document owner:** AGENT 01 — Sal's CaterSuite Business Intelligence Architect (skill `bi-architect`)
**Audience:** AGENT 01 (BI Architect), AGENT 02 (.NET Backend Architect Developer), the Debater plan-review sub-agent
**Authority:** This document is a **future-enhancement workplan** for asynchronous and scheduled backend work. It is subordinate to `docs/salscatersuite_project_structure_workplan.md`, the business rules, the approved API contracts, and `governance/decision-log.md`.
**Status:** **PROPOSED overall.** BG-04 alone is **APPROVED for implementation by DEC-178**, but is not yet implemented; all other items remain `PLANNED — not yet implemented`.

| Field | Value |
| :--- | :--- |
| **Application** | Sal's CaterSuite |
| **Repository root** | `D:\source\repos\SalsCaterSuite` |
| **Backend** | ASP.NET Core Web API — .NET 10 (`net10.0`), Clean Architecture |
| **Database** | SQL Server (`SalsCaterSuiteDb`; LocalDB in development) |
| **Frontend** | React 19 + Vite 8 + TypeScript 6 + Tailwind CSS 4.3 (v4) |
| **Current scheduler** | **None.** No `IHostedService`, `BackgroundService`, Hangfire, or Quartz registration exists in `Program.cs`. |
| **Current real-time** | SignalR is registered (`builder.Services.AddSignalR()`), but no hub is mapped and no `SignalRContext.tsx` exists. |
| **Existing building blocks** | `CustomerAgingService`, `DashboardService`, `MenuCostingService`, `PackageCostingCalculator`, six QuestPDF documents, four ClosedXML exports, `IDateTime` / `DateTimeService`, Serilog |
| **Last updated** | 2026-10-31 (AGENT 01 — BG-04 scope approved by DEC-178; other background work remains proposed) |

> **Read this before acting.** Only BG-04's scheduled booking lifecycle transition scope is approved, under DEC-178. This document does not authorize implementation of any other job, scheduler package, outbox, notification provider, migration unrelated to BG-04, or Worker project. AGENT 02 must follow the scoped decision and obtain AGENT 01 approval for any expansion.
>
> If the code and this document disagree, **the code is the current truth and this document is stale**. Report the discrepancy to AGENT 01; do not silently choose either side.

---

## 0. Purpose and Scope

### 0.1 What this document governs

This document proposes and sequences:

1. The **background job catalog** — what runs asynchronously, why, on what trigger, and what it owns.
2. The **scheduler strategy** — in-process `BackgroundService`, an outbox dispatcher, Hangfire, Quartz, or a separate Worker host.
3. The **layer boundaries** — where job code lives and which existing Application services it calls.
4. The **data additions** — outbox, job-run log, snapshots, document/export jobs, notification log.
5. The **operational contract** — retries, dead letters, idempotency, observability, retention, and security.
6. The **delivery phases** and their exit criteria.

### 0.2 What this document does not govern

- Business rules, financial formulas, KPI definitions, reminder policy, or notification wording. Those belong to `business/business-rules.md`, `business/kpi-catalog.md`, and the relevant feature specifications.
- API payload shapes. Those belong to `architecture/approved-api-contracts.md`.
- The repository layout. That belongs to `docs/salscatersuite_project_structure_workplan.md`.
- Any external provider contract (email, SMS, calendar, accounting, payment gateway).

### 0.3 Anti-drift rules

- Every planned folder, table, endpoint, package, or job carries the marker **`PLANNED — not yet implemented`** until it exists and is verified.
- Do not describe a proposed job as delivered.
- Do not record test counts, timings, or acceptance evidence until they come from a real run.
- Do not duplicate a business calculation in a job. A job orchestrates; the existing Application service remains the single arithmetic authority.
- A new package, project, table, or root folder requires an AGENT 01 decision before implementation.

---

## 1. Current-State Baseline

### 1.1 What already exists

| Area | As-built | Background relevance |
| :--- | :--- | :--- |
| Aging | `SalsCaterSuite.Application/Features/Customers/Services/CustomerAgingService.cs` — one aging vocabulary (`Clean`, `NotYetDue`, `1–30`, `31–60`, `61–90`, `90+`) and `ICustomerAgingService.Today` from `IDateTime` | Feeds the daily aging snapshot and reminders (BG-02, BG-03) |
| Dashboard | `DashboardService` computes KPIs on demand from bookings, payments, and menus | Candidate for nightly snapshots / cache warming (BG-08) |
| Menu costing | `MenuCostingService` and `MenuCostingCalculator` | Candidate for recalculation queues (BG-09) |
| Package costing | `PackageCostingCalculator` — the single package arithmetic | Candidate for recalculation queues (BG-09) |
| Documents | `ContractDocument`, `KitchenBeoDocument`, `PackageSpecSheetDocument`, `PaymentVoucherDocument`, `RefundVoucherDocument`, `StatementOfAccountDocument` (QuestPDF) | Candidate for async generation (BG-06) |
| Exports | `CateringMonthlyReportExport`, `CollectionSummaryExport`, `CustomerDirectoryExport`, `MenuFoodCostingExport` (ClosedXML) | Candidate for async export jobs (BG-07) |
| Payments | `PaymentLedgerService`, `BookingAmountDueCalculator` | Authoritative sources for aging and reconciliation; never reimplement |
| Calendar | `CalendarController`, `GetCalendarEventsQuery` | Candidate for calendar sync (BG-10) |
| Real-time | `AddSignalR()` only; no hub, no `SignalRContext.tsx` | Notification dispatcher can push once a hub exists (BG-01) |
| Logging | Serilog request logging, redaction engine, bounded capture | Reuse for job logs; add job-specific context |
| Time | `IDateTime` / `DateTimeService` | All jobs must use it; schedule in Asia/Manila |
| Tests | `src/backend/SalsCaterSuite.UnitTests`, `src/backend/SalsCaterSuite.IntegrationTests` | Job logic and outbox round-trips belong here |

### 1.2 What does not exist

- No `IHostedService` or `BackgroundService`.
- No Hangfire, Quartz, or other scheduler package.
- No outbox, job-run, notification-log, aging-snapshot, document-job, or export-job table.
- No SignalR hub.
- No email/SMS/calendar/accounting provider abstraction.
- No `/health` job-scheduler check.
- No `SalsCaterSuite.Worker` project.

### 1.3 Constraints

- The backend is Clean Architecture: Domain ← Application ← Infrastructure ← WebApi.
- EF Core types must not leak into Application (DEC-037). Job implementations live in Infrastructure, but they access data through repositories — not `DbContext` directly.
- Financial figures are server-authoritative and must come from the existing calculators/services.
- The current deployment is a single API host; multi-instance behavior is not yet specified.
- Serilog owns the logging pipeline; jobs must not add a second logging stack.

---

## 2. Proposed Architecture

### 2.1 Guiding principles

1. **The HTTP request must not wait for email, PDFs, exports, or external APIs.** Enqueue and return.
2. **Business logic is reused, not copied.** A job calls an Application service; it does not re-derive a financial formula.
3. **At-least-once delivery with idempotent consumers.** Every job must be safe to retry.
4. **Start simple.** Use the framework's `BackgroundService` and a database outbox before adding a scheduler product.
5. **Persist work before acknowledging it.** For user-triggered jobs, write the job row in the same transaction as the business change where possible.
6. **Every job is observable and replayable.** A failed job must be visible, diagnosable, and safe to re-run.
7. **No silent destructive maintenance.** Cleanup jobs support dry-run and retention configuration.

### 2.2 Scheduler options

| Option | Use when | Trade-offs | Recommendation |
| :--- | :--- | :--- | :--- |
| `BackgroundService` / `IHostedService` + `PeriodicTimer` | Single-instance, simple periodic work | No persistence, no built-in retries, no dashboard; duplicate runs if scaled out | **Phase 0–1** |
| `BackgroundService` + `Channel<T>` | In-process queued work | Queue is lost on restart; no cross-instance coordination | **Phase 1** for outbox dispatch |
| **Hangfire** (SQL Server storage) | Persistent, retriable, delayed/recurring jobs; admin dashboard | Adds a package, tables, and an admin surface that must be secured | **Recommended from Phase 2** |
| **Quartz.NET** | Cron-style scheduling without Hangfire storage | More configuration; no built-in dashboard | Alternative if Hangfire is rejected |
| Separate `SalsCaterSuite.Worker` host | Independent scaling/deployment of jobs | New project, deployment unit, configuration, and observability | **Phase 2+**, only if scale requires |
| Azure Functions / Container Apps Jobs / Service Bus | Cloud-native deployment | Platform coupling and cost | Only if the hosting target moves to Azure |

**Proposed decision (for AGENT 01 approval):**

- Phase 0–1: in-process `BackgroundService` + a database outbox. No new package.
- Phase 2+: adopt **Hangfire with SQL Server storage** if the Debater and AGENT 01 accept the package/table/admin-surface cost; otherwise Quartz.NET.
- Keep the job bodies scheduler-agnostic so the choice can change without rewriting business logic.

### 2.3 Layer placement

```text
SalsCaterSuite.Application/
  Common/BackgroundJobs/
    Abstractions/            # IBackgroundJob, IJobQueue, IOutboxStore, IJobRunStore (interfaces only)
    Models/                  # JobEnvelope, JobResult, OutboxMessageDto (no EF Core)
  Features/<Feature>/        # existing business services the jobs call

SalsCaterSuite.Infrastructure/
  BackgroundJobs/
    Outbox/                  # OutboxDispatcher and message handlers (no DbContext)
    Jobs/                    # Job implementations (aging, reminders, documents, exports)
    Scheduling/              # BackgroundService or Hangfire/Quartz registration
    Notifications/           # IEmailSender, ISmsSender, SignalR publisher (adapters)
  Persistence/
    Repositories/            # OutboxRepository, JobRunRepository, ... (DEC-037)
    Configurations/          # OutboxMessage, JobRun, AgingSnapshot, DocumentJob, ExportJob
    Migrations/              # PLANNED

SalsCaterSuite.WebApi/
  Program.cs                 # AddHostedService / AddHangfire / health checks
  (optional) Controllers/    # status/download endpoints for async jobs

src/backend/
  SalsCaterSuite.UnitTests/         # job logic, fake time, idempotency
  SalsCaterSuite.IntegrationTests/  # outbox round-trip, retries, concurrency
```

**Boundary rules:**

- A job may depend on Application interfaces and Infrastructure adapters.
- A job must not contain a second copy of a financial formula. Call `BookingAmountDueCalculator`, `CustomerAgingService`, `DashboardService`, `MenuCostingService`, or `PackageCostingCalculator`.
- A job must not use `HttpContext`. It runs without a request; the acting user is recorded as data.
- A job must not use `DbContext` directly. It calls a repository, per DEC-037; the repository owns the EF Core access.
- A job must not resolve scoped services from the root provider. Use `IServiceScopeFactory`.

### 2.4 Job contract

Every job implements a small, scheduler-agnostic contract:

```csharp
public interface IBackgroundJob
{
    string Name { get; }
    Task<JobResult> ExecuteAsync(JobEnvelope envelope, CancellationToken cancellationToken);
}
```

- `JobEnvelope` carries the job id, type, payload, requested-by, correlation id, attempt, and scheduled time.
- `JobResult` is `Succeeded`, `RetryLater(nextAttemptAt, reason)`, or `Failed(reason)`.
- The scheduler adapter translates `RetryLater` into its delay/retry mechanism.
- Every execution writes a `JobRun` row with status, attempt, duration, and error.

---

## 3. Job Catalog

All items except BG-04 are **`PLANNED — not yet implemented`**. Priorities are proposed and do not imply approval.

| ID | Job | Priority | Trigger | Calls | Output |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **BG-01** | Outbox + notification dispatcher | P0 | Continuous poll / queued | `IEmailSender`, `ISmsSender`, SignalR publisher | Email/SMS/push delivered; outbox marked processed; dead-letter on exhaustion |
| **BG-02** | Daily receivables aging snapshot | P1 | Daily 00:05 Asia/Manila | `ICustomerAgingService`, `IDateTime`, booking/payment repositories | One aging snapshot per customer/booking per date |
| **BG-03** | Receivables reminders | P1 | Daily 08:00, business days | Aging snapshot, customer contact preferences, notification sender | Reminder per customer/booking/bucket/milestone; audit log |
| **BG-04** | Booking lifecycle transitions | P2 | Scheduled polling in the existing API host | Booking lifecycle transition service, booking/status-history repositories, `IDateTime` | `Confirmed` → `InProgress` at event start; `InProgress` → `Completed` at packout + 1 hour, with the approved end-time fallback; durable status history. **APPROVED by DEC-178; implementation pending.** No SignalR/outbox in this scope. |
| **BG-05** | Event reminders | P2 | Daily 07:00 | Upcoming bookings, notification sender | 7-day and 1-day reminders to customer/staff |
| **BG-06** | Async document generation | P2 | Queued on request | QuestPDF documents, file store | PDF path/blob, `DocumentJob` status, notification |
| **BG-07** | Async exports | P2 | Queued on request | ClosedXML exports, file store | XLSX/CSV file, `ExportJob` status, expiring download link |
| **BG-08** | Dashboard KPI snapshots | P3 | Nightly + warm on demand | `DashboardService` | `DashboardSnapshot` per period; "as of" timestamp |
| **BG-09** | Menu/package costing recalculation | P3 | Price/benchmark change event + nightly | `MenuCostingService`, `PackageCostingCalculator` | Recalculated costs and projections; audit |
| **BG-10** | Calendar / accounting / payment-gateway sync | P3 | Queued + retry | Provider adapters, `IHttpClientFactory` | External id mapping, sync status, dead-letter |
| **BG-11** | Scheduled reports | P4 | Monthly, 1st at 06:00 | Existing export classes + notification sender | Report file emailed to the owner; audit |
| **BG-12** | Maintenance and retention | P4 | Daily/weekly | Identity, exports, documents, logs, soft-deleted rows | Purged/archived rows; counts logged; dry-run supported |

### BG-01 — Outbox + notification dispatcher

**Problem.** Booking confirmations, payment receipts, reminders, and SignalR updates must not run inside the request. A naive `Task.Run` loses work on restart and has no retry.

**Proposed flow.**

1. A command writes the business change and an `OutboxMessage` row in the same transaction.
2. A `BackgroundService` (or Hangfire recurring job) polls unprocessed rows in batches.
3. Each message is handled by an idempotent handler keyed by `OutboxMessage.Id`.
4. Success marks `ProcessedAt`; failure increments `Attempts`, sets `NextAttemptAt` with exponential backoff, and records `LastError`.
5. After `MaxAttempts`, the message moves to `DeadLetter` and raises an operational alert.
6. The handler publishes to email/SMS/SignalR through an adapter; no provider SDK leaks into Application.

**Acceptance criteria.**

- A booking/payment request returns without waiting for the provider.
- A transient provider failure is retried; a permanent failure is dead-lettered.
- Re-running the dispatcher does not send the same notification twice (idempotency key).
- The outbox row and the business change commit atomically.
- The dispatcher is safe to run in two instances (row locking or distributed lock).

**Dependencies.** Notification provider decision; SignalR hub (for push); email/SMS templates approved by AGENT 01.

### BG-02 — Daily receivables aging snapshot

**Problem.** Aging is currently computed on demand. A daily snapshot gives consistent reporting, a trend line, and a stable input for reminders.

**Proposed flow.**

1. At 00:05 Asia/Manila, resolve `IDateTime.Today`.
2. Load open bookings/payments and compute the aging bucket through `ICustomerAgingService`.
3. Upsert one snapshot row per customer/booking/date.
4. If the job runs twice for the same date, it replaces the same rows (idempotent).

**Acceptance criteria.**

- For the same `Today`, the snapshot matches the on-demand aging output.
- Re-running the job for the same date produces no duplicates.
- Soft-deleted bookings/payments follow the same exclusion rules as the directory and dashboard.
- The job records the source row count and the `Today` value it used.

**Dependencies.** AGENT 01 decision on snapshot retention and whether the snapshot is authoritative or advisory.

### BG-03 — Receivables reminders

**Problem.** Overdue receivables need consistent, auditable follow-up.

**Proposed flow.**

1. Read the latest aging snapshot (or compute on demand if missing).
2. Select customers/bookings at the approved milestones (proposed: 1, 30, 60, 90 days overdue).
3. Respect contact preferences and opt-out.
4. Render an approved template and enqueue via BG-01.
5. Write a `NotificationLog` row so the same milestone is not sent twice.

**Acceptance criteria.**

- One reminder per customer/booking/bucket/milestone per day.
- Opt-out and missing-contact cases are skipped and logged, not silently dropped.
- The reminder states the authoritative balance from the server.
- The template and cadence are approved by AGENT 01.

**Dependencies.** BG-01, BG-02, notification provider, template approval, business-day/holiday policy.

### BG-04 — Booking lifecycle transitions (**APPROVED by DEC-178; implementation pending**)

**Problem.** Booking statuses can drift from the event date. Manual updates are error-prone.

**Proposed flow.**

1. A scheduled, in-process `BackgroundService` polls due bookings in batches using the shared `IDateTime` abstraction; the implementation proposal must recommend a cadence.
2. Advance only `Confirmed` → `InProgress` at `EventDate + EventStartTime` and `InProgress` → `Completed` at `EventEndDate + PackoutTime + 1 hour`. If packout is absent, use `EventEndDate + EventEndTime + 1 hour`. Interpret scheduled date/time in Asia/Manila and compare as instants.
3. Exclude soft-deleted rows and statuses `Pending`, `Expired`, `Cancelled`, `Rescheduled`, and `NoShow`. A rescheduled booking must be explicitly confirmed again. Manual NoShow/Rescheduled actions are out of scope.
4. Make each transition idempotent and row-version/concurrency safe. If the worker resumes after both thresholds, record both valid transitions in order. Save lifecycle status and `BookingStatusHistory` atomically, with actor/source `system/BG-04` and effective/recorded timestamps.
5. Publish no SignalR event in this cycle: there is no mapped hub and no push channel. Payment status is unchanged by this job.

**Acceptance criteria.**

- Only the two approved transitions occur, at their date/time thresholds.
- `Pending`, `Expired`, `Cancelled`, `Rescheduled`, and `NoShow` never become `Completed` through BG-04.
- Re-running and multi-instance races are safe through idempotence and row-version concurrency.
- Every lifecycle transition is recorded in durable `BookingStatusHistory` in the same transaction.
- No payment status or financial amount is changed by BG-04.

**Dependencies.** DEC-177 / DEC-178; lifecycle-only `BookingStatus`; `EventEndDate`; `BookingStatusHistory`; repository-backed transition service; `IDateTime`. No SignalR hub, outbox, scheduler package, or separate project is in scope.

### BG-05 — Event reminders

**Problem.** Customers and staff need timely reminders before an event.

**Proposed flow.**

1. Daily at 07:00, select bookings at the 7-day and 1-day milestones.
2. Enqueue customer and staff notifications through BG-01.
3. Record the milestone in `NotificationLog`.

**Acceptance criteria.**

- One reminder per booking/milestone/recipient.
- Asia/Manila date boundaries are correct.
- Cancelled bookings are excluded.
- Templates are approved by AGENT 01.

**Dependencies.** BG-01, booking data, notification provider.

### BG-06 — Async document generation

**Problem.** QuestPDF generation can be heavy and should not block the request or hold a database transaction.

**Proposed flow.**

1. A controller validates the request and writes a `DocumentJob` row.
2. A worker picks the job, loads the entity, and calls the existing document class.
3. The PDF is stored in the approved file store (filesystem, database, or blob).
4. The job status and an expiring download link are exposed to the client.
5. A notification is enqueued when the document is ready.

**Acceptance criteria.**

- The request returns a job id immediately.
- A failed generation is retryable and leaves no partial file.
- The download is authorized; the file is not publicly addressable.
- Retention and cleanup are configured.

**Dependencies.** Storage decision; download endpoint contract; authorization policy.

### BG-07 — Async exports

**Problem.** Large ClosedXML exports can exceed a comfortable request duration.

**Proposed flow.**

1. Validate filters and write an `ExportJob` row.
2. A worker generates the file with the existing export class.
3. Store the file and expose status/progress and an expiring download link.

**Acceptance criteria.**

- The request returns a job id immediately.
- The generated file matches the on-demand export for the same filters.
- The file expires and is cleaned up.
- No PII is written to logs.

**Dependencies.** Storage decision; download endpoint contract; retention.

### BG-08 — Dashboard KPI snapshots

**Problem.** The dashboard aggregates several tables on every request. As data grows, the response slows.

**Proposed flow.**

1. Nightly, compute the dashboard for the approved periods and store a snapshot with an "as of" timestamp.
2. The dashboard reads the snapshot for historical periods and falls back to live computation when the snapshot is missing or stale.
3. The response exposes the "as of" timestamp so the UI can show staleness.

**Acceptance criteria.**

- The snapshot matches the live `DashboardService` output for the same period.
- A missing snapshot never breaks the dashboard.
- Staleness is visible to the client.
- The snapshot is not used to hide a failed live calculation.

**Dependencies.** AGENT 01 approval of the snapshot periods and staleness policy.

### BG-09 — Menu/package costing recalculation

**Problem.** A price or benchmark change can invalidate many menu/package cost figures.

**Proposed flow.**

1. A price/benchmark change enqueues recalculation for the affected menu/package ids.
2. The worker calls the existing `MenuCostingService` / `PackageCostingCalculator`.
3. Results are persisted or cached; an audit entry records the trigger and source.

**Acceptance criteria.**

- The recalculation uses the authoritative calculator, not a copy.
- One price change does not recalculate every menu unless the dependency graph requires it.
- Partial failures are retried; no partial write is presented as complete.
- The job is idempotent.

**Dependencies.** AGENT 01 approval of the dependency graph and cache strategy.

### BG-10 — Calendar / accounting / payment-gateway sync

**Problem.** External systems need retries, idempotency, and conflict handling.

**Proposed flow.**

1. A booking/payment change enqueues a sync command.
2. The adapter calls the provider with an idempotency key.
3. The external id is stored in `IntegrationSyncState`.
4. Transient failures retry; permanent failures dead-letter.

**Acceptance criteria.**

- No duplicate external events/payments.
- Conflicts are surfaced, not silently overwritten.
- Credentials are not logged.
- Each provider has a runbook.

**Dependencies.** Provider contracts and credentials; AGENT 01 approval.

### BG-11 — Scheduled reports

**Problem.** The owner wants the monthly catering report and collection summary without manual exports.

**Proposed flow.**

1. On the 1st at 06:00, generate the approved reports for the previous month.
2. Email the files to the owner (or store them with a link).
3. Record the run and the file location.

**Acceptance criteria.**

- The figures match the on-demand export.
- A failed run is retried and alerted.
- The recipient list is configurable.

**Dependencies.** BG-01, BG-07, report approval.

### BG-12 — Maintenance and retention

**Problem.** Tokens, exports, documents, logs, and soft-deleted rows accumulate.

**Proposed flow.**

1. Daily/weekly, apply the approved retention policy.
2. Support a dry-run mode that logs counts without deleting.
3. Never purge financial records without an explicit, approved retention rule.

**Acceptance criteria.**

- Dry-run reports the same set the real run would delete.
- Destructive runs require configuration and are audited.
- Financial and audit records are never purged by a generic cleanup job.

**Dependencies.** AGENT 01 retention policy; legal/compliance review.

---

## 4. Delivery Phases

| Phase | Scope | Exit criteria |
| :--- | :--- | :--- |
| **0 — Foundation** | Job contract, `JobRun` log, configuration, feature flag, `TimeProvider`/`IDateTime` usage, test harness, health check | A no-op job runs, logs, and is covered by a test; no business job yet |
| **1 — Outbox + notifications** | `OutboxMessage`, dispatcher, email/SMS/SignalR adapters, dead-letter, idempotency | BG-01 acceptance criteria met; a booking/payment request never waits for a provider |
| **2 — Receivables** | BG-02, BG-03, aging snapshot, reminder templates, notification log | Snapshot matches on-demand aging; no duplicate reminders; templates approved |
| **3 — Documents and exports** | BG-06, BG-07, job status/download endpoints, storage, retention | Request returns a job id; generated file matches on-demand output |
| **4 — Lifecycle and integrations** | BG-04, BG-05, BG-10, SignalR hub | Valid transitions only; reminders correct; external sync idempotent |
| **5 — Performance and maintenance** | BG-08, BG-09, BG-11, BG-12 | Snapshots match live output; recalculation correct; retention dry-run reviewed |

**Phase gates.** Each phase requires:

1. AGENT 01 approval of the scope and business rules.
2. Debater review of the plan (binary `APPROVE` or `PROPOSE_BETTER_PLAN`).
3. AGENT 02 implementation with tests.
4. AGENT 01 independent validation and a live run.
5. `governance/decision-log.md` updated.
6. `implementation/feature-status.md` updated.
7. This document realigned if the structure changed.

---

## 5. Proposed Data Additions

All tables are **`PLANNED — not yet implemented`**. Names are proposals; AGENT 01 approves the final schema.

### 5.1 `OutboxMessage`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | uniqueidentifier | Primary key; also the idempotency key |
| `Type` | nvarchar(200) | Message type / handler key |
| `Payload` | nvarchar(max) | JSON payload |
| `OccurredAt` | datetime2 | UTC |
| `ProcessedAt` | datetime2 null | Null until handled |
| `Attempts` | int | Starts at 0 |
| `NextAttemptAt` | datetime2 null | Backoff schedule |
| `LastError` | nvarchar(max) null | Redacted error |
| `Status` | nvarchar(20) | `Pending`, `Processing`, `Processed`, `DeadLetter` |

Indexes: `(Status, NextAttemptAt)`, `(OccurredAt)`.

### 5.2 `JobRun`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | uniqueidentifier | Primary key |
| `JobName` | nvarchar(200) | Stable job identifier |
| `StartedAt` / `CompletedAt` | datetime2 | UTC |
| `Status` | nvarchar(20) | `Running`, `Succeeded`, `Failed`, `Cancelled` |
| `Attempt` | int | 1-based |
| `CorrelationId` | nvarchar(100) | Ties to logs |
| `Error` | nvarchar(max) null | Redacted |
| `MetricsJson` | nvarchar(max) null | Counts, duration, batch size |

### 5.3 `CustomerAgingSnapshot`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `SnapshotDate` | date | Asia/Manila business date |
| `CustomerId` | int | FK |
| `BookingId` | int | FK |
| `Bucket` | nvarchar(30) | Existing aging vocabulary |
| `Balance` | decimal(18,2) | Authoritative balance |
| `CreatedAt` | datetime2 | UTC |

Unique index: `(SnapshotDate, CustomerId, BookingId)`.

### 5.4 `DocumentJob` / `ExportJob`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | uniqueidentifier | Primary key; returned to the client |
| `Type` | nvarchar(100) | Document/export type |
| `EntityId` | nvarchar(100) null | Target entity |
| `FiltersJson` | nvarchar(max) null | Export filters |
| `RequestedBy` | nvarchar(100) | User id |
| `Status` | nvarchar(20) | `Queued`, `Running`, `Succeeded`, `Failed`, `Expired` |
| `FilePath` | nvarchar(500) null | Storage location |
| `ExpiresAt` | datetime2 null | Cleanup |
| `Error` | nvarchar(max) null | Redacted |

### 5.5 `NotificationLog`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | uniqueidentifier | Primary key |
| `Channel` | nvarchar(20) | `Email`, `Sms`, `SignalR` |
| `Recipient` | nvarchar(300) | Redacted in logs |
| `Template` | nvarchar(100) | Template key |
| `EntityType` / `EntityId` | nvarchar(100) | Dedup key |
| `Milestone` | nvarchar(50) null | e.g. `7-day`, `30-overdue` |
| `Status` | nvarchar(20) | `Queued`, `Sent`, `Failed`, `Skipped` |
| `SentAt` | datetime2 null | UTC |
| `Error` | nvarchar(max) null | Redacted |

Unique index for dedup: `(Channel, EntityType, EntityId, Template, Milestone)`.

### 5.6 `IntegrationSyncState`

| Column | Type | Notes |
| :--- | :--- | :--- |
| `Id` | uniqueidentifier | Primary key |
| `Provider` | nvarchar(50) | e.g. `GoogleCalendar` |
| `EntityType` / `EntityId` | nvarchar(100) | Local entity |
| `ExternalId` | nvarchar(200) | Provider id |
| `LastSyncedAt` | datetime2 | UTC |
| `Status` | nvarchar(20) | `Synced`, `Pending`, `Failed` |
| `Error` | nvarchar(max) null | Redacted |

---

## 6. Configuration

Proposed keys (all `PLANNED`):

```jsonc
{
  "BackgroundJobs": {
    "Enabled": true,
    "TimeZone": "Asia/Manila",
    "Outbox": {
      "PollIntervalSeconds": 5,
      "BatchSize": 50,
      "MaxAttempts": 8
    },
    "Aging": { "RunAt": "00:05" },
    "Reminders": {
      "RunAt": "08:00",
      "Milestones": [1, 30, 60, 90],
      "BusinessDaysOnly": true
    },
    "EventReminders": { "RunAt": "07:00", "Milestones": [7, 1] },
    "Documents": { "RetentionDays": 30 },
    "Exports": { "RetentionDays": 14 },
    "Maintenance": { "RunAt": "02:00", "DryRun": true }
  }
}
```

- Development defaults may differ; production values require AGENT 01 approval.
- Secrets (SMTP, SMS, provider tokens) go in the existing secret store, never in `appsettings.json`.
- `Enabled=false` must stop all jobs cleanly without unregistering the host.

---

## 7. Observability and Operations

### 7.1 Logging

- Reuse Serilog. Add `JobName`, `JobRunId`, `CorrelationId`, `Attempt`, and `OutboxMessageId` to the log context.
- Never log a full email body, token, password, or customer contact identifier; the existing redaction engine remains authoritative.
- Log job start, completion, duration, item counts, and failure reason.

### 7.2 Metrics and health

Proposed:

- Jobs started/completed/failed by name.
- Job duration (p50/p95) and queue depth.
- Outbox pending count and oldest pending age.
- Dead-letter count.
- Last successful run per recurring job.

Health checks:

- `AddHealthChecks()` with a database check and a "scheduler heartbeat" check.
- `/health` is unauthenticated only for liveness; detailed readiness requires authorization.
- A missed recurring job raises an alert.

### 7.3 Admin and replay

- Hangfire dashboard (if adopted) must require an admin role and must not be publicly exposed.
- A dead-letter list with a manual replay action is required for BG-01.
- Every replay records the operator, reason, and original job id.
- Destructive maintenance jobs support dry-run and require explicit configuration.

### 7.4 Runbooks

Each job needs a short runbook covering:

1. What it does and when it runs.
2. How to check whether it ran.
3. How to replay or pause it.
4. What to do when it fails repeatedly.
5. Who owns the data it touches.

---

## 8. Security, Financial Integrity, and Idempotency

### 8.1 Security

- Jobs run as a system actor. Record the `RequestedBy` user for user-triggered jobs.
- Re-check authorization at execution time for user-initiated actions; do not trust the enqueue-time claim alone.
- Do not bypass the audit trail because "it is only a job".
- Minimize PII in payloads; redact logs; encrypt provider credentials.
- Download links for documents/exports must be authorized and expiring.

### 8.2 Financial integrity

- A job must call the authoritative service (`BookingAmountDueCalculator`, `PaymentLedgerService`, `CustomerAgingService`, `DashboardService`, `MenuCostingService`, `PackageCostingCalculator`).
- Never recompute a financial figure with a second formula.
- Aging snapshots and dashboard snapshots are **derived**; the source tables remain authoritative.
- A failed financial job must not write a partial or zeroed figure.
- Reconciliation tests compare the job output with the on-demand service for the same inputs.

### 8.3 Idempotency

- Every outbox message has an idempotency key (`OutboxMessage.Id`).
- Every notification log row has a dedup key.
- Every snapshot has a unique date/entity key.
- Every external call uses a provider idempotency key.
- Every job must be safe to run twice; the second run either no-ops or replaces the same derived rows.

### 8.4 Concurrency

- If the API runs in more than one instance, use a distributed lock or row-level claim (`Status = Processing`, `LockedUntil`).
- Batch sizes and poll intervals must be configurable to avoid database contention.
- Long-running jobs must not hold a transaction open for the entire batch.

---

## 9. Testing Strategy

| Test type | What it proves |
| :--- | :--- |
| Unit — job logic | Correct selection, mapping, and idempotency with fake repositories and a fake `TimeProvider` |
| Unit — financial parity | Job output equals the existing service output for the same inputs |
| Integration — outbox | Message written in the same transaction; dispatcher processes it; retry and dead-letter work |
| Integration — duplicate run | Running the same job twice produces no duplicate notification/snapshot |
| Integration — failure injection | Transient DB/provider failure retries; permanent failure dead-letters |
| Integration — concurrency | Two workers do not process the same message twice |
| Integration — time zone | Asia/Manila date boundaries are correct |
| Integration — authorization | A user-triggered job re-checks permission at execution |
| Performance | A large export/aging batch stays within the agreed time and memory budget |

**Test rules.**

- No test may depend on the wall clock; inject `TimeProvider` / `IDateTime`.
- No test may send a real email/SMS or call a real provider; use a fake adapter.
- Integration tests use a disposable database, never the development database.
- A job is not "done" without a test for its failure path.

---

## 10. Definition of Done

A job is done when:

1. AGENT 01 approved its scope and business rules.
2. The Debater reviewed the plan.
3. The job calls the existing Application service; no business formula is duplicated.
4. The job is idempotent and safe to retry.
5. The job writes a `JobRun` row and logs with correlation.
6. The failure path, dead-letter, and replay are implemented.
7. Unit and integration tests pass, including a duplicate-run test and a failure test.
8. The configuration keys are documented.
9. A runbook exists.
10. The feature flag can disable the job.
11. `governance/decision-log.md` and `implementation/feature-status.md` are updated.
12. This document is realigned if the structure changed.

---

## 11. Risks and Mitigations

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| Duplicate reminders/emails | Customer annoyance, trust loss | Idempotency key + `NotificationLog` dedup |
| Job runs twice in multi-instance | Duplicate financial snapshots | Distributed lock or row claim |
| Stale dashboard snapshot shown as live | Wrong business decision | "As of" timestamp + fallback to live + staleness indicator |
| Financial divergence between job and API | Reconciliation failure | Reuse authoritative services; parity tests |
| PII leak in logs or email | Compliance/privacy breach | Existing redaction; minimize payloads; template review |
| Scheduler package adds operational complexity | Slower delivery, admin surface risk | Start with `BackgroundService`; adopt Hangfire only when approved |
| Time-zone/DST bug | Wrong day's aging/reminders | `IDateTime` + Asia/Manila + boundary tests |
| Database contention from batch jobs | Slow API | Off-peak schedules, configurable batch size, no long transactions |
| Destructive cleanup deletes financial data | Data loss | Retention policy, dry-run, exclude financial/audit records |
| Provider outage blocks notifications | Missed reminders | Outbox retry + dead-letter + alert; no request blocking |

---

## 12. Open Questions for AGENT 01

1. **Scheduler:** approve in-process `BackgroundService` for Phase 1 and Hangfire for Phase 2, or choose Quartz.NET?
2. **Notification channels:** email, SMS, WhatsApp, or in-app only? Which provider?
3. **Reminder policy:** which milestones, what wording, what opt-out rules, and which business-day calendar?
4. **Aging snapshot:** is it authoritative for reporting, or advisory only? What retention period?
5. **Document/export storage:** filesystem, database, or blob storage? What retention and download-link lifetime?
6. **Worker host:** keep jobs in `WebApi` initially, or create `SalsCaterSuite.Worker` in Phase 2?
7. **Financial mutations:** confirm that background jobs may write only derived snapshots/jobs and never mutate payments, bookings, or ledger entries directly.
8. **Dashboard snapshots:** which periods, and what staleness threshold is acceptable?
9. **Integrations:** which providers are in scope (Google Calendar, Outlook, accounting, payment gateway), and who owns their contracts?
10. **Deployment target:** single VM, containers, or Azure? This determines the scheduler and distributed-lock choice.
11. **Retention/compliance:** how long are notifications, documents, exports, and job logs retained?
12. **Ownership:** who owns notification templates and the runbooks?

---

## 13. Out of Scope

- Implementing any job, table, endpoint, package, or project in this document.
- Changing business rules, financial formulas, KPI definitions, or reminder policy.
- Adding external provider contracts without AGENT 01 approval.
- Exposing the Hangfire dashboard without authentication.
- Replacing Serilog or the existing request-logging pipeline.
- Background processing in the frontend (the browser is not a job host).
- Multi-tenant isolation; the current application is single-tenant.

---

## 14. Change Log

| Date | Agent | Change | Sections touched |
| :--- | :--- | :--- | :--- |
| 2026-10-22 | AGENT 01 (proposed) | Initial background services workplan: baseline, architecture options, job catalog BG-01..BG-12, phased delivery, data additions, configuration, observability, security, testing, definition of done, risks, open questions | All |

---

## Appendix A — Suggested File Layout

```text
src/backend/
  SalsCaterSuite.Application/
    Common/BackgroundJobs/
      Abstractions/
        IBackgroundJob.cs
        IJobQueue.cs
        IOutboxStore.cs
        IJobRunStore.cs
      Models/
        JobEnvelope.cs
        JobResult.cs
        OutboxMessageDto.cs
  SalsCaterSuite.Infrastructure/
    BackgroundJobs/
      Outbox/
        OutboxDispatcher.cs
        OutboxMessageHandler.cs
      Jobs/
        AgingSnapshotJob.cs
        ReceivablesReminderJob.cs
        BookingLifecycleJob.cs
        EventReminderJob.cs
        DocumentGenerationJob.cs
        ExportJob.cs
        DashboardSnapshotJob.cs
        CostingRecalculationJob.cs
        IntegrationSyncJob.cs
        ScheduledReportJob.cs
        MaintenanceJob.cs
      Notifications/
        IEmailSender.cs
        ISmsSender.cs
        SmtpEmailSender.cs
        SignalRNotificationPublisher.cs
      Scheduling/
        BackgroundJobHostedService.cs
        HangfireRegistration.cs        // only if approved
    Persistence/
      Repositories/
        OutboxRepository.cs            // DEC-037: all EF access lives here
        JobRunRepository.cs
        CustomerAgingSnapshotRepository.cs
        DocumentJobRepository.cs
        ExportJobRepository.cs
        NotificationLogRepository.cs
        IntegrationSyncStateRepository.cs
      Configurations/
        OutboxMessageConfiguration.cs
        JobRunConfiguration.cs
        CustomerAgingSnapshotConfiguration.cs
        DocumentJobConfiguration.cs
        ExportJobConfiguration.cs
        NotificationLogConfiguration.cs
        IntegrationSyncStateConfiguration.cs
  SalsCaterSuite.WebApi/
    Program.cs                          // AddHostedService / AddHangfire / health checks
    Controllers/
      JobsController.cs                 // PLANNED: status/download/replay
src/backend/
  SalsCaterSuite.UnitTests/BackgroundJobs/
  SalsCaterSuite.IntegrationTests/BackgroundJobs/
```

## Appendix B — References

| Document | Purpose |
| :--- | :--- |
| `docs/salscatersuite_project_structure_workplan.md` | Repository layout, layer boundaries, build/run/test commands, workflow |
| `business/business-rules.md` | Reminder, aging, and lifecycle rules (approval source) |
| `business/kpi-catalog.md` | KPI definitions used by snapshots and reports |
| `architecture/approved-api-contracts.md` | Job status/download endpoint contracts (to be added) |
| `governance/decision-log.md` | Required decision record for scheduler, tables, and packages |
| `implementation/feature-status.md` | Delivery state and test evidence |
| `.agents/skills/dotnet-backend-architect/SKILL.md` | Backend implementation standards |
| `.agents/skills/debater/SKILL.md` | Plan-review protocol |

---

*End of Background Services Workplan — PROPOSED, not yet approved.*
