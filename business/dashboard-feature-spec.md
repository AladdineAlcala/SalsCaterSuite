# Sal's CaterSuite — Feature Specification: Operations Dashboard (REQ-DSH-01)

| Field | Value |
| :--- | :--- |
| **Feature** | Operations Dashboard (KPI + Operations Matrix) |
| **Requirement** | REQ-DSH-01 |
| **Specification version** | 1.1 (DEC-177 lifecycle/payment status amendment) |
| **Status** | **APPROVED for implementation** |
| **Approved by** | BI Architect (Agent 01) |
| **Owner rulings incorporated** | DEC-040, DEC-041, DEC-042, DEC-043, DEC-044 (2026-10-19); DEC-177 (2026-10-31) |
| **Design reference** | `stitch_catersuite_dashboard_design` (Stitch export: `code.html`, `screen.png`, `DESIGN.md`) — visual/structural reference only |
| **Applies to** | `src/backend` (.NET 10 Clean Architecture) and `src/frontend/salscatersuite-ui` (React 19 + Vite + Tailwind) |

> **Governance statement.** The Stitch design is a *visual* reference. It is **not** a data source.
> Every figure rendered on the dashboard is calculated **server-side** from authoritative records
> (DEC-002, DEC-012, DEC-025). Where a design element has no backing data, this specification
> states the replacement or the explicit "not available" treatment. No figure is ever hardcoded,
> estimated, or carried over from the mockup.

---

## 1. Business Objective

Give the catering operations team a single screen that answers, at a glance:

1. **How much business is contracted** in the active period, and how much of it is committed vs tentative.
2. **How healthy is menu costing** — how many dishes are actually costed, and what margin the catalogue yields.
3. **How much money is still owed** — total AR, how many ledgers carry it, and how much is overdue.
4. **What happens next** — the upcoming banquet schedule, the food-cost distribution by course, and the payment ledger position.

The dashboard is an **operations telemetry surface**, not a financial statement. It must reconcile
exactly with the per-booking ledger (`PaymentLedgerService`) and the customer receivables aggregate
(`CustomerRepository`), per DEC-015 and DEC-025.

---

## 2. Actors & Permissions

| Actor | Access |
| :--- | :--- |
| Any authenticated user (Administrator, Manager, Cashier, Accountant) | View the dashboard. `GET /api/dashboard` requires authentication only — it is a read-only aggregate and exposes no personal data beyond the customer name already visible on booking screens. |
| Administrator, Manager | Same, plus the in-app "Book New Banquet Event" action, which routes to the booking wizard (future feature). |

The dashboard endpoint is **read-only** and therefore carries no role policy. Authorization is enforced
server-side by `[Authorize]`; the frontend hides nothing that the API would still allow, and the API
authorizes nothing that the frontend merely hides (BR-12).

---

## 3. Scope & Explicit Exclusions

### 3.1 In scope

| ID | Deliverable | Owner |
| :--- | :--- | :--- |
| B1 | `GET /api/dashboard` aggregate endpoint + DTOs + Application service | Agent 02 (backend) |
| B2 | Configurable margin/food-cost benchmark (replacing the hardcoded `35.0m` in `MenuCostingService`) | Agent 02 |
| B3 | Repository queries required by the aggregate (payment date range, menu-by-course aggregate, AR aging) | Agent 02 |
| B4 | Frontend app shell: Vite/TS/Tailwind repair, design tokens, sidebar + top bar, router, API client | Agent 03 (frontend) |
| B5 | `AuthContext` + minimal login page + protected route (prerequisite: every endpoint needs a JWT) | Agent 03 |
| B6 | Dashboard screen: 4 KPI cards, telemetry pill, operational hub, 3-column operations matrix, status footer | Agent 03 |
| B7 | Typed API contracts, currency/date formatters, loading / empty / error / success states | Agent 03 |
| B8 | Vitest + React Testing Library unit/component tests for the frontend dashboard logic | Agent 03 |
| B9 | Backend unit + integration tests for the aggregate and every KPI formula | Agent 02 |

### 3.2 Explicitly OUT of scope (recorded as gaps, not silently dropped)

| Gap | Why it is out of scope | Consequence for the screen |
| :--- | :--- | :--- |
| **BEO production states** (`BEO Ready` / `Prep Pending` / `Finalized`) | There is no BEO entity, no BEO status field, and no BEO workflow in the domain model. | The schedule shows the **booking lifecycle status** and the separate **payment status** (DEC-177); it never presents a BEO readiness badge, which is deferred to the BEO feature (REQ-REP-02 follow-on). |
| **Banquet-level (booking) margin** | A booking's food cost cannot be computed: there is no `BookingMenu` junction and no recipe / ingredient / yield model. `Menu.FoodCost` is a manually entered decimal. | The margin card shows the **catalogue gross margin** (point-in-time, across the costed menu catalogue), explicitly labelled *Catalogue Margin*. True banquet margin requires the `BookingMenu` junction + recipe costing — a new backlog requirement. |
| **`+X% vs last month` on the margin card** | The menu catalogue has no costing history, so no prior-period margin exists. | The delta shown is **vs the configured benchmark** (percentage points), which is derivable. |
| **`QuestPDF Receipts Issued — Compliant`** | No document-compliance engine exists. | The tile shows **Official Receipts Issued** (count of `Payment` rows in the period) with the receipt-series label `OR-…`. The word "Compliant" is removed. |
| **A hardcoded product version** in the footer (`v2.5.0`, `.NET 9`) | The mockup's version strings are fabricated and the real stack is .NET 10 (DEC-011). | The footer states the real stack and a **live** API-connection indicator derived from the dashboard fetch result. No version number is invented. |
| **A "30% downpayment" figure** | The minimum deposit percentage is UNRESOLVED (DEC-009). | The tile shows **Bookings Awaiting Deposit** — count and full outstanding balance of non-cancelled bookings with no payment recorded. No deposit percentage is implied. |
| **Playwright end-to-end suite** | Requires a live API + seeded business data; DEC-018 forbids fabricating business data. | Deferred. Frontend coverage this cycle is Vitest + RTL. E2E is a follow-up once a data-seeding policy for non-production data exists. |
| **Real-time SignalR updates** | `AddSignalR()` is registered but there is **no hub** in the solution. | The dashboard polls on focus/revalidate only. The "reconciliation active via SignalR" line in the mockup is replaced by a truthful refresh indicator. |
| **Month-over-month booking/revenue comparison** | Not requested by the design and not an approved KPI. | Not implemented. |

---

## 4. Preconditions

- The API is running and reachable (`http://localhost:5149` / `https://localhost:7087`).
- A JWT is held by the client (obtained via `POST /api/Auth/login`).
- The `SalsCaterSuiteDb` schema is at the latest migration.

---

## 5. Reporting Period Model

The dashboard is **period-aware**, and the active period is **always visible on screen** (governance
requirement: every metric must state its date basis and range).

| Scope class | Metrics | Date basis |
| :--- | :--- | :--- |
| **Period-scoped** | Bookings count, Contracted pipeline value, Net contracted revenue, Average booking value, Collections, Collection ratio, Receipts issued | `Booking.EventDate` for bookings; `Payment.DateOfPayment` for collections |
| **As-of (point-in-time)** | Total AR, AR aging & overdue, Dish costing coverage, Catalogue margin, Food cost by category, Bookings awaiting deposit, Collections (trailing 7 days) | Current state of the book at request time |

Rationale: receivables and the menu catalogue are **snapshots**, not flows. Scoping a snapshot to a
past month would be meaningless and misleading. The period applies to flows only, and the UI labels
each card accordingly.

- **Active period** = calendar month, defaulting to the current month (UTC), selectable by the user.
- The period is passed to the API as `?period=YYYY-MM`. The server is authoritative for the period
  boundaries it returns; the client never computes them.

---

## 6. Business Rules & KPI Definitions (authoritative)

All formulas are computed **server-side**. The frontend formats and displays; it never derives a
financial figure (DEC-002).

### 6.0 Shared financial basis — one calculation path

- **Net Amount Due** for a booking = `BookingAmountDueCalculator.Calculate(...)` output `NetAmountDue`,
  computed with the **same arguments** the authoritative ledger uses
  (`extendedAreaRate: 0m, addonsTotal: 0m, otherChargesTotal: 0m` until the add-on / extended-area /
  discount junctions exist). This guarantees the dashboard reconciles 1:1 with
  `PaymentLedgerService.GetLedgerAsync` (DEC-015) and with `CustomerRepository`'s receivables (DEC-025).
- **Amount Paid** for a booking = `Σ Payment.AmountPaid` — the **authoritative** sum, never the
  denormalized `Booking.AmountPaid` cache (DEC-012).
- **Balance** = `max(0, NetAmountDue − Σ payments)` (BR-03).

> **Open item DEC-035.** Two net-due sources exist in the codebase (the stored `Booking.AmountDue`
> column vs the ledger recomputation). They agree today only because discounts, extended area and
> add-ons cannot be created. This specification mandates the **recomputed** path for the dashboard so
> that the dashboard and the ledger can never disagree. DEC-035 remains open for the owner; if the
> owner instead ratifies the stored column, this specification must be revised and the dashboard
> re-verified. The backend agent must **not** resolve DEC-035 unilaterally.

### 6.1 KPI-07 — Active Bookings, Committed vs Tentative, Contracted Pipeline Value *(new)*

- **Population:** bookings with `EventDate` in the active period, `Status ∉ {Cancelled, Expired}`.
- **Total count** = |population|.
- **Committed** = `Status == Confirmed` (a signed/committed contract). Legacy DepositPaid/FullyPaid lifecycle values are mapped to Confirmed; payment progress is reported separately.
- **Tentative** = `Status ∈ {Pending}` (created, not yet confirmed). *New glossary term — see §11.*
- **Other** = `Status ∈ {InProgress, Completed, NoShow, Rescheduled}` (executed or exceptional events
  inside the period). Shown only when the count is greater than zero.
- **Contracted pipeline value** = `Σ NetAmountDue` over the population.
- **Total pax** = `Σ NumberOfHeads` over the population.

> The card must never label `Pending` bookings as "Confirmed" or hide the Other bucket. Lifecycle statuses and payment status remain separate; payment progress does not move a booking between these lifecycle groups.

### 6.2 KPI-01 / KPI-08 — Net Contracted Catering Revenue *(extended to the dashboard)*

- `Revenue(period) = Σ NetAmountDue` over the same population as KPI-07.
- Date basis `Booking.EventDate`; excludes `Cancelled` and `Expired`; excludes soft-deleted rows
  **for dashboard queries only** (see §6.11).

### 6.3 KPI-04 / KPI-09 — Average Booking Value

- `ABV(period) = Revenue(period) ÷ |population|`. When the population is empty, ABV is `null`
  (the UI renders `—`, never `0`).

### 6.4 KPI-02 / KPI-10 — Total Collections (Cash Inflow)

- `Collections(t1, t2) = Σ Payment.AmountPaid` where `Payment.DateOfPayment ∈ [t1, t2]`.
- Independent of event dates: includes prepayments for future events and settlements for past events.

### 6.5 KPI-11 — Collection Ratio *(new)*

- `CollectionRatio(period) = Collections(period) ÷ Revenue(period) × 100`.
- When `Revenue(period) = 0`, the ratio is `null` and the UI renders `—`.
- **Interpretation caveat (must be disclosed in the UI tooltip):** the numerator is a cash-basis
  flow and the denominator is an accrual-basis flow, so the ratio is an indicative collection rate,
  not a settlement rate. It can exceed 100% when prior-period receivables are collected in-period.

### 6.6 KPI-12 — Total Outstanding Accounts Receivable (as-of)

- `TotalAR = Σ max(0, NetAmountDue − Σ payments)` over **all** bookings with `Status ≠ Cancelled`.
- Reconciles exactly to the sum of each booking's ledger `balanceDue` (DEC-025).
- A `Completed` booking with an outstanding balance **remains included** (DEC-025, owner-confirmed).
- **Never** reported as collections (BR-08).

### 6.7 KPI-13 — AR Aging & Overdue (as-of) *(new — closes REQ-REC-02's date-basis gap)*

- **Due date = `Booking.EventDate`** (owner ruling, DEC-041). A booking's balance is collectible
  once its event date has passed.
- **Days overdue** = `today − EventDate` when `EventDate < today`; otherwise the booking is *not yet due*.
- **Buckets** (REQ-REC-02):

  | Bucket key | Label | Condition |
  | :--- | :--- | :--- |
  | `NotYetDue` | Not yet due | `EventDate ≥ today` |
  | `Days1To30` | 1–30 days | `1 ≤ daysOverdue ≤ 30` |
  | `Days31To60` | 31–60 days | `31 ≤ daysOverdue ≤ 60` |
  | `Days61To90` | 61–90 days | `61 ≤ daysOverdue ≤ 90` |
  | `Over90Days` | 90+ days | `daysOverdue > 90` |

- **Overdue (>30 days)** = bookings in `Days31To60 ∪ Days61To90 ∪ Over90Days`; the card shows the
  **count** and the **total amount** overdue.
- Each bucket reports `bookingCount` and `amount`; the bucket amounts sum exactly to `TotalAR`.

### 6.8 KPI-14 — Dish Costing Coverage (as-of) *(new)*

- **Population:** menus that are not soft-deleted.
- **Costed** = `FoodCost.HasValue && FoodCost > 0`.
- **Uncosted / needs audit** = `FoodCost` is null or `≤ 0`.
- `CoveragePercentage = Costed ÷ Total × 100` (0 when Total = 0).
- This is a **data-completeness** metric, not a financial one. It is explicitly *not* a profitability KPI.

### 6.9 KPI-15 — Catalogue Gross Margin (as-of) *(new)*

- **Population:** non-deleted menus with `FoodCost > 0` **and** `Price > 0`.
- `CatalogueRevenueAmount = Σ Price`
- `CatalogueFoodCostAmount = Σ FoodCost`
- `CatalogueMarginAmount = Σ (Price − FoodCost)`
- `CatalogueMarginPercentage = CatalogueMarginAmount ÷ CatalogueRevenueAmount × 100`
- `VarianceToBenchmarkPercentage = CatalogueMarginPercentage − TargetMarginPercentage`
- **This is a catalogue measure, not a banquet measure** (see §3.2). The UI must label it
  "Catalogue Margin" and must not present it as the profitability of booked events.
- **Benchmark** — see §6.12.

### 6.10 KPI-16 — Food Cost Share by Course Category (as-of) *(new)*

- For each `CourseCategory` with at least one costed menu: `foodCostAmount = Σ FoodCost`.
- `sharePercentage = categoryFoodCost ÷ Σ allCategoryFoodCost × 100`.
- Ordered by `foodCostAmount` descending. Shares sum to 100% (subject to rounding).
- Menus with a null `CourseId` are grouped under the label `Uncategorised` and reported as such.

### 6.11 KPI-17 / KPI-18 / KPI-19 — Ledger tiles *(new)*

- **KPI-17 — Collections, trailing 7 days:** `Σ Payment.AmountPaid` where
  `DateOfPayment ∈ [today − 6 days, today]` (inclusive, 7 calendar days). Also returns the receipt count.
- **KPI-18 — Bookings Awaiting Deposit:** bookings with `Status ≠ Cancelled` and `Σ payments = 0`;
  reports `bookingCount` and `awaitingDepositAmount = Σ NetAmountDue` for that population.
  **No deposit percentage is assumed** (DEC-009 remains open; DEC-043). This remains a zero-payment-recorded measure; it is not replaced by the `PendingPayment` classifier, whose zero-due case is `FullyPaid` (DEC-177).
- **KPI-19 — Official Receipts Issued (period):** count of `Payment` rows whose `DateOfPayment`
  falls in the active period. Every row carries an `OR-YYYYMMDD-XXXX` receipt number (BR-05).

### 6.12 Benchmark configuration *(new — owner ruling DEC-040)*

The margin benchmark is **deployment configuration**, not a hardcoded constant.

- Configuration key: `Dashboard:Benchmarks:TargetFoodCostPercentage` (decimal, default `35.0`).
- `TargetMarginPercentage = 100 − TargetFoodCostPercentage`.
- The default `35.0` is the conservative end of the BR-07 band (food cost should not exceed 35–40% of
  selling price) and **preserves the current behaviour** of `MenuCostingService` (which hardcodes
  `35.0m` in two places and in `MenuCostingSummaryResponse`).
- The benchmark is **returned by the API** in the payload. The frontend renders the returned value and
  never hardcodes it. If configuration is absent, the API returns the default and `isConfigured: false`.
- `MenuCostingService` and `MenuCostingSummaryResponse.TargetFoodCostPercentage` must read the same
  configured value so that the menu screen and the dashboard can never disagree.

### 6.13 Soft-deleted rows

- `IsDeleted` is stored but **no global query filter is applied** (DEC-038). Applying one globally
  would be an unapproved behaviour change.
- **Dashboard queries exclude soft-deleted rows explicitly** (`Booking`, `Payment`, `Menu`), because
  KPI-01 mandates the exclusion. This is a **dashboard-scoped** rule, stated here so it is auditable.
- If a booking is soft-deleted it is excluded from the KPI population, from AR, and from the aging
  buckets, in the same query, so the totals stay internally consistent.

---

## 7. Data Requirements

| Data | Source | Notes |
| :--- | :--- | :--- |
| Booking period population | `Booking` + `Customer` + `Payment` | `EventDate` in period; `Status` filter; `IsDeleted = false` |
| Booking net due | `BookingAmountDueCalculator` | Same argument set as `PaymentLedgerService` |
| Booking amount paid | `Payment` (`Σ AmountPaid`) | Authoritative (DEC-012) |
| Collections | `Payment` by `DateOfPayment` range | **New repository query required** — `IPaymentRepository` currently has no date-range method |
| AR + aging | `Booking` + `Payment` | Excludes `Cancelled` and soft-deleted |
| Menu costing coverage & margin | `Menu` | **New aggregate query required** (menu count by course, costed/uncosted counts, Σ price/Σ food cost) |
| Food cost by course | `Menu` + `CourseCategory` | **New aggregate query required** |
| Upcoming bookings | `Booking` + `Customer` | `EventDate ≥ today`, `Status ∉ {Cancelled, Expired}`, ordered by `EventDate` ascending, limit 5 |
| Customer name | `Customer.FullName` | Display name on the schedule |

**Required new repository capabilities** (interfaces in `Application\Common\Interfaces`, implementations
in `Infrastructure\Persistence\Repositories`, per DEC-037/DEC-039):

| # | Capability | Notes |
| :--- | :--- | :--- |
| R1 | Payments summed and counted over a `DateTime` range | Must be SQL-translatable. |
| R2 | Bookings with payments for the period population | Existing `GetByDateRangeAsync` is a partial fit but does not exclude `Cancelled`/`Expired`/soft-deleted or eager-load payments. |
| R3 | Bookings with payments for the AR population (all non-cancelled) | Excludes soft-deleted. |
| R4 | Menu aggregates: total count, costed count, Σ price, Σ food cost, and a per-course breakdown | Single grouped query where possible. |
| R5 | Upcoming bookings (event date ≥ today) with customer and payment totals | Limit 5 at the application layer, not in SQL, so the limit is not silently applied before filtering. |

**Anti-pattern prohibited:** the Application project must not reference EF Core, and repository
interfaces must not expose `DbSet`, `IQueryable`, `DbContext` or any EF type (DEC-037/DEC-039).
`ISalsCaterSuiteDbContext` stays deleted.

---

## 8. API Requirements

Full contract: `architecture/approved-api-contracts.md` §9. Summary:

- `GET /api/dashboard?period=YYYY-MM` → `200` `DashboardResponse`.
- `[Authorize]`. Read-only. No role policy.
- `period` is optional; defaults to the current UTC calendar month. An unparseable value → `400`
  `{ "errors": ["Period must be in the format YYYY-MM."] }`.
- Response is `application/json`; enums are string names (contract §8).
- All monetary values are `decimal(18,2)` on the server and are serialised as JSON numbers with
  2 decimal places of precision (the frontend formats them as ₱ with `Intl.NumberFormat`).
- `null` is used for undefined ratios (never `0`, never `100`).

---

## 9. UI Requirements

### 9.1 Visual system

Implements the design system's tokens (`DESIGN.md`) in `tailwind.config.js`:

| Token group | Values |
| :--- | :--- |
| Canvas / surfaces | `artisan-canvas #FAF8F5`, `artisan-surface #FFFFFF`, `artisan-subtle #F4EFEB`, `artisan-border #E8E1D9`, `artisan-border-contrast #DDD5CC` |
| Ink | `espresso-900 #1A120B`, `espresso-850 #231810`, `espresso-700 #433022`, `espresso-600 #634835`, `espresso-400 #8C7362`, `espresso-200 #C7B9AC` |
| Accent — culinary | `culinary-50 #F0FDF4`, `culinary-100 #DCFCE7`, `culinary-200 #BBF7D0`, `culinary-500 #22C55E`, `culinary-600 #16A34A`, `culinary-700 #15803D`, `culinary-800 #166534` |
| Accent — caramel | `caramel-50 #FFFBEB`, `caramel-100 #FEF3C7`, `caramel-200 #FDE68A`, `caramel-500 #F59E0B`, `caramel-600 #D97706`, `caramel-700 #B45309` |
| Surfaces (Material) | `surface-container #efeeeb`, `surface-container-low #f5f3f0`, `surface-container-high #eae8e5` |
| Type | `Plus Jakarta Sans` (headlines/body), `JetBrains Mono` (all monetary and numeric readouts) |
| Radii | `4px` micro · `8px` controls · `12px` cards · `16px` hero · `9999px` pills |
| Elevation | Level 1 `0 1px 3px rgba(35,24,16,.04), 0 1px 2px rgba(35,24,16,.02)`; Level 2 `0 4px 16px -2px rgba(35,24,16,.05), 0 2px 6px -1px rgba(35,24,16,.03)`; Level 3 `0 8px 24px -4px rgba(22,163,74,.16)` |
| Layout | Left nav fixed `16rem` on `md:`+, off-canvas drawer below; content `max-w-7xl` (1280px), `px-4` mobile / `px-8` desktop |

Rules:
- **Every monetary and numeric readout uses `JetBrains Mono`** and `font-variant-numeric: tabular-nums`.
- **Currency is Philippine Peso (₱)**, formatted `₱1,425,800.00` (PHP, `en-PH` locale) with the
  peso sign. Compact forms (`₱1.43M`) are **not** used in KPI readouts — full precision only.
- **Status is never conveyed by colour alone** (WCAG 1.4.1): every status chip carries a text label
  and an icon/shape distinction.
- No dark mode in this release; the design ships a light-only system.

### 9.2 Screen composition (in the design's order)

1. **App shell** — persistent left navigation (Dashboard, Bookings & Events, Menu & Food Costing,
   Packages & Buffets, Payments & Ledger, Accounts Receivable & SOA, Banquet Calendar, Reports & BEO,
   Settings) and a top bar (global search placeholder, "New Payment / OR" and "+ New Booking"
   affordances, notification bell, user chip with role). Nav items other than Dashboard route to an
   explicit **"not yet implemented"** placeholder — never a dead link and never fabricated data.
2. **Dashboard header** — eyebrow "Catering Telemetry / Commissary Operations", H1, subtitle, and the
   **telemetry pill** showing: `ACTIVE PERIOD: <label>` (a month selector), `TARGET FC: <server value>%`,
   `COLLECTION: <server value>%`.
3. **Four KPI cards** — Active Bookings · Dish Costing Coverage · Catalogue Margin · Outstanding Receivables.
   Each card has an icon badge, an optional action chip, the headline figure (mono), a supporting
   line, a foot line, and a progress track. The progress track shows a **bounded, meaningful
   ratio**, never a decorative guess:
   - Bookings → committed share of the period population (0–100%).
   - Coverage → costed share (0–100%).
   - Margin → margin ÷ benchmark, clamped to 0–100%.
   - Receivables → overdue share of total AR (0–100%).
4. **Operational hub** — the 3-step process (Intake → Kitchen → Finance) with the CTA row. The CTAs
   route to the future modules; where the target module is not built, the button is rendered
   **disabled with an explanatory tooltip** rather than a dead link.
5. **Operations matrix** — three columns:
   - **Upcoming Banquet Schedule** — next 5 events: customer + occasion, event date + pax, separate lifecycle
     status and payment-state chips, package name, net amount due, and the balance. The customer name **wraps** rather
     than truncating, so the booking stays identifiable in the narrow matrix columns.
   - **Food Cost by Category** — horizontal bars, one per course, share % and amount. The panel chip
     states **"Catalogue Cost Mix"** — the mockup's "Price Volatility" is not rendered, because the
     payload carries no price history, trend or volatility data (DEC-044).
   - **Payment Ledger & Receivables** — Collected (trailing 7 days) with the receipt count, Bookings
     Awaiting Deposit (count + amount), Official Receipts Issued (count), **and the server's AR
     aging breakdown** (the five buckets with their counts and amounts, overdue buckets in the
     action tone). The aging block replaces the mockup's dead "View Aging →" affordance: it renders
     the already-approved KPI-13 buckets and lets the browser compute nothing (the day counts are
     the server's).

   Bars and the column order adapt to the available width; the matrix is `lg:grid-cols-3`.
6. **Status footer** — real stack string (`.NET 10 Clean Architecture & EF Core`), the database name,
   a **live** API-connection indicator derived from the last successful fetch, and the timestamp of the
   data (`generatedAt`). No fabricated version number.

### 9.3 States

| State | Behaviour |
| :--- | :--- |
| Loading | Skeleton placeholders matching the card layout; no spinner-over-stale-data flash. |
| Error | Inline `AlertBanner` with the API's `errors` array rendered verbatim; a Retry button re-runs the query. A `401`/`403` redirects to the login page. A `500` shows the ProblemDetails `detail` when present. |
| Empty | Every panel renders an explicit empty state ("No bookings in <period>", "No menus costed yet", "No receivables outstanding"). A `null` ratio renders `—`, never `0%`. |
| Success | Values render with a mono numeric format and `₱` currency. |
| Stale | TanStack Query keeps the previous data visible during a refetch and marks the panel as "Refreshing…". |

### 9.4 Accessibility

- Semantic landmarks (`nav`, `main`, `header`), one `h1` per screen.
- Every interactive element is keyboard reachable with a visible focus ring
  (`0 0 0 3px rgba(22,163,74,.12)` emerald halo).
- Icons that carry meaning have an accessible name; decorative icons are `aria-hidden`.
- Progress tracks expose `role="progressbar"` with `aria-valuenow` / `aria-valuemin` / `aria-valuemax`
  and an accessible label.
- Colour contrast: body text ≥ 4.5:1, large text ≥ 3:1 against its actual background.
- The mobile navigation drawer traps focus and closes on `Escape`.

### 9.5 Responsive

- < `md`: single-column KPI grid, off-canvas navigation drawer, stacked operations matrix.
- `md`–`lg`: 2-column KPI grid.
- `lg`: 3-column operations matrix.
- `xl`+: 4-column KPI grid.

> **Approved deviation (DEC-048).** This specification originally required the 4-column KPI grid from
> `lg` (1024px). That is not achievable without degrading the card: with the 16rem rail, a
> 1024px viewport leaves ≈161px per card (121px of content), while the reference design's own
> 4-column render needs ≈1290px of viewport to produce its ≈210px cards. A full-precision peso
> headline (`₱701,000.00`, 11 characters in JetBrains Mono) does not fit a 121px content box.
> The grid therefore moves to `xl` (1280px) — the closest standard breakpoint to the design's own
> requirement — and the KPI card is a **size container** whose headline scales with the space it
> actually has (`clamp(0.875rem, 12cqi, 1.75rem)`, capped at the `data-lg` 28px token). The
> measurable outcome is preserved: **no horizontal overflow at any of 375 / 768 / 1024 / 1440px**
> (AC-F15) and the full-precision peso is never truncated. The 2-column layout between 768px and
> 1280px is wider and more readable, not degraded.

---

## 10. Acceptance Criteria

### 10.1 Backend

| # | Criterion |
| :--- | :--- |
| AC-B1 | `GET /api/dashboard` returns `200` with the full `DashboardResponse` for a valid `period`, and `400` `{errors:[...]}` for a malformed `period`. |
| AC-B2 | The endpoint requires authentication: no token → `401 {errors:["Authentication is required."]}`; a valid token → `200`. |
| AC-B3 | Dashboard AR equals the sum of the per-booking ledger `balanceDue` for the same bookings (DEC-025 reconciliation). Proven by a test. |
| AC-B4 | Collections equal `Σ Payment.AmountPaid` for the period, verified against the same payments the payments-ledger service reads. |
| AC-B5 | The aging bucket amounts sum exactly to Total AR, and every booking with a balance lands in exactly one bucket. |
| AC-B6 | The `Bookings` KPI counts and the `Revenue` KPI sum are computed from the same population, so `Revenue ÷ count = ABV` holds exactly. |
| AC-B7 | The margin benchmark is read from configuration; the API's returned benchmark equals the configured value; with configuration removed the default `35.0` is returned and `isConfigured` is `false`. |
| AC-B8 | `MenuCostingService` and the dashboard return the **same** target food-cost percentage. |
| AC-B9 | Soft-deleted bookings, payments and menus are excluded from every dashboard figure (dashboard-scoped; no global query filter is introduced). |
| AC-B10 | `Upcoming Bookings` excludes cancelled/expired bookings and orders by `EventDate` ascending. |
| AC-B11 | No Application-layer file references EF Core; repository interfaces expose no EF type. The existing compliance check stays green. |
| AC-B12 | The existing unit suite (171) and integration suite (19) remain green, with no change to existing routes, status codes, DTO shapes or messages. |
| AC-B13 | A new integration test exercises the dashboard endpoint against a real SQL Server database with known seed data and asserts the independently-calculated expected values. |

### 10.2 Frontend

| # | Criterion |
| :--- | :--- |
| AC-F1 | `npm run build` (tsc + vite build) succeeds with **zero TypeScript errors**. |
| AC-F2 | `npm run test` passes; the suite covers the currency/date formatters, the status→label mapping, the progress-bar ratio clamping, and the dashboard's loading / error / empty rendering. |
| AC-F3 | The screen reproduces the design's structure: app shell, header + telemetry pill, 4 KPI cards, operational hub, 3-column matrix, footer. |
| AC-F4 | **No figure in the UI is hardcoded.** Every number, percentage, currency value, date and label comes from the API payload. A repo-wide check must find no literal peso amount or metric in the dashboard feature folder. |
| AC-F5 | All monetary readouts use `JetBrains Mono` and the ₱ format with full precision. |
| AC-F6 | The schedule shows the BR-10 lifecycle status and separate BR-28 payment state; no BEO production state is rendered. |
| AC-F7 | The margin card is labelled "Catalogue Margin" and shows the server-supplied benchmark; a `null` benchmark renders "Not configured". |
| AC-F8 | The overdue figure is derived from the server's aging buckets; the frontend does not compute days-overdue. |
| AC-F9 | The deposit tile is labelled "Bookings Awaiting Deposit" and shows no percentage. |
| AC-F10 | The footer contains no version number and no fabricated connection claim; the connection indicator reflects the real fetch result. |
| AC-F11 | Unauthenticated access to the dashboard route redirects to the login page; after a successful login the user lands on the dashboard; logout clears the token. |
| AC-F12 | A `401`/`403` from the API returns the user to the login page; a `500` renders the ProblemDetails detail; a `400` renders the `errors` array. |
| AC-F13 | Every nav item other than Dashboard resolves to a labelled "not yet implemented" page — no dead links. |
| AC-F14 | Keyboard navigation reaches every interactive element; focus is visible; the mobile drawer traps focus and closes on `Escape`. |
| AC-F15 | The layout holds at 375px, 768px, 1024px and 1440px widths with no horizontal overflow. |

### 10.3 Reconciliation (the release gate)

The dashboard's AR total, the aging bucket total, and the sum of the per-booking ledger balances for
the same population must be **equal to the cent**. Any divergence is a release blocker.

---

## 11. Glossary Additions (approved with this specification)

| Term | Definition |
| :--- | :--- |
| **Committed booking** | A booking in `Confirmed` lifecycle status — a contract that is in force. Payment progress is a separate status. |
| **Tentative booking** | A booking in `Pending` status — created but not yet confirmed. *(Distinct from `Reservation`, which is a pre-contract expression of interest.)* |
| **Other / executed booking** | A booking in `InProgress`, `Completed`, `NoShow` or `Rescheduled` status. |
| **Contracted pipeline value** | `Σ NetAmountDue` over a defined booking population. |
| **Not yet due** | An AR balance on a booking whose event date has not yet passed. |
| **Overdue** | An AR balance on a booking whose event date passed more than 30 days ago. The due date is the event date (DEC-041). |
| **Catalogue margin** | The gross margin across the costed menu catalogue. **Not** the margin of booked events. |
| **Bookings awaiting deposit** | Non-cancelled bookings with no payment recorded. Carries no assumed deposit percentage while DEC-009 is open. |

---

## 12. Required Tests

### 12.1 Backend (Agent 02)

| Suite | Coverage |
| :--- | :--- |
| Unit — KPI formulas | Each KPI computed on a hand-built fixture with independently-calculated expected values: bookings split, contracted value, revenue, ABV, collections, collection ratio (incl. zero-revenue → null), AR, aging buckets (boundary days 0/1/30/31/60/61/90/91), coverage, catalogue margin, category shares (incl. uncategorised), trailing-7-day collections, awaiting-deposit. |
| Unit — benchmark | Configured value is used; default `35.0` when absent; `isConfigured` flag correct. |
| Unit — cancellation/exclusion | Cancelled bookings excluded from AR, revenue and counts; `Completed` with a balance **included** in AR (DEC-025). |
| Integration | `GET /api/dashboard` against a real SQL Server database seeded with known data; assert every KPI against independently computed expected values; assert `401` without a token; assert `400` on a malformed period; assert AR == sum of ledger balances. |
| Regression | The full existing unit (171) and integration (19) suites stay green. |

### 12.2 Frontend (Agent 03)

| Suite | Coverage |
| :--- | :--- |
| Unit | `currencyFormatter` (₱, full precision, thousands separators, null handling); `dateFormatter` (event date, period label); progress-ratio clamping to 0–100; `null` ratio → `—`. |
| Component | `KpiCard` renders a value, a unit, a support line and a bounded progress track with correct `aria-valuenow`; the status chip renders the BR-10 label; the dashboard renders loading skeletons, the error banner with the API `errors` array, the empty state, and the populated state from a fixed fixture. |
| Type / build | `tsc` clean; `vite build` clean. |

---

## 13. Dependencies

| # | Dependency | Owner | Risk if unmet |
| :--- | :--- | :--- | :--- |
| D1 | `GET /api/dashboard` contract frozen in `architecture/approved-api-contracts.md` §9 | Agent 01 (done) | Frontend and backend diverge. |
| D2 | New repository capabilities R1–R5 | Agent 02 | Dashboard cannot be computed. |
| D3 | Benchmark configuration binding | Agent 02 | Hardcoded benchmark (the defect this replaces). |
| D4 | Frontend Vite/TypeScript/Tailwind repair (`vite.config.ts` is **absent**; `tsconfig.json` has no `jsx` setting; `tailwind.config.js` has an empty `content` array) | Agent 03 | The scaffold cannot compile today. |
| D5 | `POST /api/Auth/login` returns `{token,userId,userName,fullName,roles[]}`; errors use `{message}` (not `{errors}`) | Agent 02 (existing) | The client must handle two error envelopes. |
| D6 | CORS permits `http://localhost:5173` | Agent 02 (existing) | Direct cross-origin calls fail; the Vite dev proxy avoids this entirely. |

---

## 14. Risks

| ID | Risk | Severity | Mitigation |
| :--- | :--- | :--- | :--- |
| R-1 | DEC-035 (two net-due sources) is still open. | High | The dashboard uses the ledger's recomputed path so it can never disagree with the ledger. If the owner ratifies the stored column, the specification is revised and the dashboard re-verified. |
| R-2 | `Booking.RefreshPaymentStatus()` is never invoked, so `Status` can drift from payment progress. The bookings KPI splits on `Status`. | High | The dashboard reads the persisted `Status` (BR-10 single source of truth) and does not re-derive it. **The drift is a pre-existing backend defect, reported as finding F8, not fixed inside this feature** — fixing it changes BR-10 behaviour. The backend agent must report it with evidence. |
| R-3 | `IPaymentRepository` has no date-range query; R1–R5 are new. | Medium | Backed by a new repository method with EF-translatable projections and an integration test. |
| R-4 | `MenuCostingService` hardcodes the target in three places. | Medium | A single configuration source read by both the menu service and the dashboard (AC-B8). |
| R-5 | `PaymentNo` is a PK generated by a read-then-increment count — a concurrency race that surfaces as a `500`. | Medium (pre-existing) | Out of scope; reported as finding F9 for a follow-up decision. |
| R-6 | `CateringMonthlyReportExport` writes `AmountDue` under the "Total Paid" column. | Medium (pre-existing) | Out of scope for the dashboard; reported as finding F10. It will corrupt the monthly sales export. |
| R-7 | Three different "amount due" notions exist (stored column, ledger recomputation, statement's `PackageAmount`). | High | The dashboard uses one path (§6.0). The statement/export divergence is reported as finding F11. |
| R-8 | The frontend has no test infrastructure today. | Medium | Vitest + RTL added in this cycle; Playwright deferred (see §3.2). |
| R-9 | No business data is seeded (DEC-018), so the dashboard renders empty states in a fresh environment. | Low | Expected and correct. The empty states are designed for exactly this. |

---

## 15. Open Questions for the Owner

| # | Question | Status |
| :--- | :--- | :--- |
| Q1 | Ratify DEC-035 — the persisted `Booking.AmountDue` column, or the ledger recomputation, as the single net-due source? | **OPEN** — the dashboard currently uses the ledger path. |
| Q2 | Confirm the AR due date is the event date (DEC-041) for statements, not only the dashboard. | **OPEN** — applied to the dashboard. |
| Q3 | DEC-009 — what is the minimum deposit percentage, and should `Booking.DepositAmount` become a first-class field? | **OPEN** — the dashboard avoids assuming it. |
| Q4 | Should the benchmark be per-package or global? | **OPEN** — currently global. |

None of these blocks the implementation of this specification.
