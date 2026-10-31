# Customer Master & Accounts Ledger — Feature Specification

| Field | Value |
| :--- | :--- |
| Feature | Customer Master & Accounts Ledger (Customer directory + AR ledger) |
| Version | 1.0 |
| Status | **APPROVED for implementation** (BI Architect) |
| Date | 2026-10-19 |
| Design reference | `d:\Downloads\SalsCaterSuite_Designs\customer_master\code.html` + `screen.png` + `DESIGN.md` |
| Closes | REQ-REC-01 (SOA controller), and the `CustomerDirectoryTable` gap named in `implementation/feature-status.md` |
| New requirement | REQ-CUS-01 — Customer Master (directory, profile, AR ledger) |

---

## 1. Scope

### 1.1 In scope

1. **Customer Master screen** at route `/customers` — a paged, searchable, filterable
   customer directory with a per-customer Accounts Receivable (AR) ledger state.
2. **Customer profile drawer** — read + edit the customer's own attributes.
3. **Statement of Account (SOA)** — per-customer statement endpoint, plus a QuestPDF
   PDF download (REQ-REC-01's missing controller).
4. **Customer directory export** — ClosedXML XLSX of the *current filtered result set*.
5. **Backend corrections** required to make the above truthful (see §7).

### 1.2 Explicitly out of scope (v1)

These appear in the design reference but have **no approved basis** in the project's
business rules, KPI catalogue, domain model or data dictionary. They are **not
implemented** and **not stubbed with placeholder data**. Each is raised in §8 for an
owner decision.

| Design reference element | Why it is out of scope |
| :--- | :--- |
| `CUST-YYYY-NNNN` customer code | No customer-code concept exists. `BR-09`/`DEC-016` cover the **booking** number only. The screen shows the surrogate `customerId`. |
| Tax Identification Number (TIN) / VAT registered | No TIN column, property, rule or decision exists. `DEC-008` (tax policy) is UNRESOLVED. |
| Customer tier (`VIP / Premier / Wedding / Institution`) | "VIP"/"Premier"/"Wedding" exist **only as `Package` types** — a product classification, never a customer one. |
| Metro area filter | `Customer.Address` is free text with no geography, region or city column. |
| Customer email address | No email column on `Customer`. |
| Primary POC / contact role / title | No such attributes exist. |
| Payment terms (Net-30, % deposit) | `DEC-041` establishes the **event date** as the only due date; there is no credit-term concept. `DEC-009` (deposit %) is UNRESOLVED. |
| "Cumulative Client LTV" and "Repeat Rate" KPI cards | Not in the KPI catalogue (19 KPIs, none is a lifetime-value or repeat-rate metric). Replaced per §4.2. |
| "QuestPDF SOA Compliance %" card | **Explicitly rejected** by `DEC-044` and the dashboard spec §3.2 — no document-compliance engine exists. Replaced per §4.2. |
| Per-customer ledger *state* vocabulary ("Paid in Full", "Reconciled Clean", "30% DP Paid", "Final Due in 5d") | No customer-level status enum, glossary term or rule exists. Replaced with the **approved KPI-13 aging-bucket vocabulary** plus a `Clean` key (§4.3). |
| Batch SOA generation (many PDFs at once) | Deferred; the per-customer SOA PDF ships. The button renders **inactive** with an explanatory tooltip. |
| BIR-compliance claim in the footer banner | No compliance engine exists. The banner states only what is true. |
| Customer notes / BEO invoicing instructions | No column exists. |
| `Booking.Occasion` as a customer classification | `Occasion` is a **booking**-level free-text field. |

### 1.3 Design fidelity

The screen reproduces the reference layout and design language exactly: banner +
actions, four KPI cards, search & filter bar with quick-view pills, a 7/5 split
(directory table / detail drawer), pagination footer, and the status footer. It uses
the **existing** `DESIGN.md` token set — the reference `code.html` was authored against
the same tokens, so no new Tailwind tokens are introduced. Where a reference cell has
no data source, the cell is omitted rather than filled with invented values.

---

## 2. Actors and permissions

| Actor | Capability |
| :--- | :--- |
| Any authenticated user | View the directory, view a profile, view the statement, export the directory, download an SOA PDF, create a customer, edit a customer. |

`GET /api/customers/**` keeps its existing `[Authorize]` attribute. **No new role
policy is introduced** — `BR-12` restricts only cancellation/refund and payment
recording, and the Customer Master performs neither.

> **Server-side enforcement.** The frontend hides nothing that the server would allow
> and shows nothing the server would deny. The only client-side guard is
> double-submission prevention while a request is in flight (AGENTS.md: "Prevent
> duplicate submissions during in-flight operations").

---

## 3. Authoritative figures and their sources

Every number on the screen is computed **server-side** (DEC-002). The browser
computes no financial value and **no day count** (AC-F8).

| Figure | Formula | Source | Authority |
| :--- | :--- | :--- | :--- |
| Net Amount Due (per booking) | `BookingAmountDueCalculator.Calculate(booking, Σ Payment.AmountPaid).NetAmountDue` | `Booking` + `Payment` | DEC-046 (shared overload) |
| Amount Paid (per booking) | `Σ Payment.AmountPaid` | `Payment` | DEC-012 / BR-10 |
| Remaining Balance (per booking) | `max(0, NetAmountDue − AmountPaid)` | — | BR-03 |
| **Customer AR** | `Σ max(0, NetAmountDue_b − AmountPaid_b)` over the customer's bookings with `Status ≠ Cancelled` | `Booking` + `Payment` | BR-08 / DEC-025 / KPI-12 |
| **Customer Contracted Value** | `Σ NetAmountDue_b` over the customer's bookings with `Status ≠ Cancelled` | `Booking` | **KPI-21 (new, this spec)** |
| **Customer Total Paid** | `Σ AmountPaid_b` over the customer's bookings with `Status ≠ Cancelled` | `Payment` | DEC-012 |
| **Days overdue** | `today − Booking.EventDate` when `EventDate < today` | `Booking.EventDate` | DEC-041 |
| **Aging bucket** | `NotYetDue` / `Days1To30` / `Days31To60` / `Days61To90` / `Over90Days` | — | KPI-13 / DEC-041 |

**Customer Aging State.** A customer's state is the **worst** (most overdue) bucket
among the bookings that contribute a positive balance to that customer. A customer
with no positive balance is `Clean`. The server returns the bucket **key and its
display label**; the client renders the label verbatim.

**Reconciliation.** The sum of the per-customer `outstandingBalance` over the whole
directory **equals** `summary.activeArAmount`, and `summary.activeArAmount` equals
the `receivables.outstandingAmount` of `GET /api/dashboard` for the same instant
(DEC-025 / AC-B3). Both are computed from the same shared calculator.

**Soft-deleted rows.** `IsDeleted` is stored but never globally filtered
(DEC-038). The Customer Master filters `IsDeleted = false` **inside its own queries
only**, following the established dashboard-scoped pattern (DEC-045). See DEC-050.

---

## 4. Screen specification

### 4.1 Banner

- Pill: `EF Core 10 • SalsCaterSuiteDb.Customer` — static application context, not a
  business figure. (The reference says "EF Core 9"; the project is on EF Core 10.)
- H1: **Customer Master & Accounts Ledger**
- Sub: "Client directory, banquet booking histories, and Statement of Account (SOA)
  receivables pipeline."
- Actions:
  - `Export (ClosedXML)` — **active**; downloads the current filtered directory as XLSX.
  - `Batch SOAs (QuestPDF)` — **inactive** (`aria-disabled`, explanatory tooltip);
    batch generation is out of scope (§1.2).
  - `+ Add New Customer` — **active**; opens the drawer in create mode.

### 4.2 KPI cards (four)

All four are server-computed. Two are existing approved KPIs; two are new KPIs
defined by this specification (§8).

| # | Card | Value | Sub-line | Source |
| :--- | :--- | :--- | :--- | :--- |
| 1 | **Total Registered Clients** | `summary.totalClients` | "Profiles Active" | KPI-20 (new) |
| 2 | **Active AR Exposure** | `summary.activeArAmount` | "{clientsWithBalance} with open balance" | KPI-12 |
| 3 | **Total Contracted Value** | `summary.totalContractedValue` | "Avg {averageValuePerClient} per client" | KPI-21 (new) |
| 4 | **Overdue AR (>30 days)** | `summary.overdueAmount` | "{overdueClientCount} clients overdue" | KPI-13 |

`averageValuePerClient` is **`null`** when `totalClients = 0`; the client renders an
em dash, never `₱0.00` (contract §9.2 rule 6, "no null-as-zero").

### 4.3 Filter bar

- **Search** — free text. Server-side, case-insensitive substring over
  `LastName`, `FirstName`, `Company`, `Contact1` (the existing `GET /api/customers`
  search semantics — unchanged). Placeholder: "Search by client name, company or
  contact number…".
- **TYPE** — `All Accounts` / `Corporate` / `Private`. Derived per §4.4.
- **AR STATE** — `All Ledgers` / `Clean` / `Open Balance` / `Overdue > 30 days` /
  `Overdue > 60 days`. Server-side filter on the customer's aging state.
- **METRO AREA** — **not rendered** (§1.2).
- **Quick views** — `All Clients` / `Corporate` / `Private` / `With Open Balance` /
  `Overdue > 30 days`. Each shows a **live count from the server**, never a literal.
  The counts come from a dedicated `quickViewCounts` object that is computed over the
  **search-filtered population with the type and AR-state filters ignored** — so
  the pills always describe the population the operator is looking at, exactly as
  the reference does ("All Clients (248) · Corporate Accounts (142)"). Clicking a
  pill then applies that scope as a filter.

  This is deliberately **separate** from `summary`, which describes the *fully
  filtered* result set so the KPI cards respond to every active filter. If the
  pills reused `summary`, selecting "Corporate" would make the "All Clients" pill
  report the corporate count.

### 4.4 Customer classification (derived, pending owner)

`Corporate` when `Company` is non-blank; `Private` otherwise. "Blank" is
**whitespace-aware** (`string.IsNullOrWhiteSpace`), so a company of `"   "` is
`Private` — and the same rule applies to the `quickViewCounts` split, so
`corporate + private == all` holds by construction. Recorded as
**DEC-051 (PROPOSED)**. The classification is a *presentation* of the existing
`Company` attribute, is never persisted, and never changes a record.

### 4.5 Directory table

| Column | Content | Source |
| :--- | :--- | --- |
| **Customer ID & State** | `customerId` (mono) + the customer's aging-state chip | `customerId`, `ledgerStateLabel` |
| **Client / Organization** | `fullName` (bold) + `company` (secondary) | `Customer.FullName`, `Customer.Company` |
| **Contact & Address** | `contact1`, `contact2`, `address` | `Customer` |
| **Bookings & Spend** | "{past} past • {upcoming} upcoming" and "Total: {totalContractedValue}" | `bookingCounts`, `totalContractedValue` |
| **AR Ledger** | `outstandingBalance` (right-aligned, mono) + the aging-state chip | `outstandingBalance`, `ledgerStateLabel` |
| **Action** | Edit (opens the drawer) and Download SOA (PDF) icon buttons | — |

**The aging-state chip is always paired with a text label** — status is never
conveyed by colour alone (WCAG 1.4.1, the existing `Badge` component's contract).

> **Scope note — the next upcoming booking is shown in the drawer, not the table.**
> The reference mockup's "Bookings & Spend" cell carries a third line naming the
> client's next event. Publishing it on the directory row would require a
> correlated "next upcoming booking" sub-query per customer inside the paged
> projection, and fetching it per row from the client would be the N+1 the
> engineering standards forbid. The information is therefore presented one click
> away, in the drawer's **Upcoming Booking** ribbon (§4.8), which is also where it
> can be shown in full. This is a deliberate scope decision, not an omission.

### 4.6 Pagination

Server-side. `page` (1-based) and `pageSize` (10 / 25 / 50, default 10). The footer
shows "Showing {first}–{last} of {totalCount} records" and numbered page buttons
with previous/next. The client never slices the list.

### 4.7 Detail drawer

- **Header** — `fullName` (or `company` when present, with `fullName` beneath) + an
  `Update Mode` / `New Customer` pill + the `customerId`.
- **Tabs**
  1. **Profile & Contact** — the editable form (§4.8).
  2. **Ledger History** — the statement (§4.9).
- **Footer actions** — `Print SOA (PDF)` (active), `Cancel`, `Save & Update`
  (label becomes `Create Customer` in create mode).

### 4.8 Profile & Contact form

Only attributes that exist on `Customer`:

| Field | Maps to | Required | Rules |
| :--- | :--- | :--- | :--- |
| Last name | `lastName` | yes | ≤ 100 chars |
| First name | `firstName` | yes | ≤ 100 chars |
| Middle name | `middle` | no | ≤ 50 chars |
| Company | `company` | no | ≤ 150 chars |
| Mobile / primary contact | `contact1` | no | ≤ 15 chars — see DEC-056 |
| Secondary contact | `contact2` | no | ≤ 15 chars — see DEC-056 |
| Address | `address` | no | ≤ 200 chars |

> **Contact-number format (DEC-056, open).** The validator caps a contact number at
> **15 characters** — a pre-existing rule that is stricter than the `nvarchar(50)`
> column. A Philippine mobile number in local format (`09171234567`, 11 characters)
> fits; the same number in international format (`+63 920 441 9090`, 16 characters)
> does **not** and is rejected with `400 "Contact number 1 must not exceed 15
> characters."`. The design reference's own sample records use the international
> format and therefore cannot be entered as drawn. The rule was inherited unchanged
> and has **not** been altered, because changing a validation rule is a business
> decision. The owner is asked to choose: raise the limit to the column width, or
> keep it and document the required local format on the field.

**Account Classification** is rendered as a **read-only derived chip**, not an
editable control, because it is not a stored attribute (§4.4).

Below the form, an **Upcoming Booking** ribbon shows the customer's next
non-cancelled booking with `EventDate ≥ today`: `bookingNumber`, `occasion`,
`eventDate` and `amountDue`. When there is no upcoming booking the ribbon is not
rendered.

**Validation** is enforced **server-side** (BR-11). The client shows the server's
`errors[]` verbatim and preserves the user's input after a rejected save.

### 4.9 Ledger History tab

The statement: `totalContractedValue`, `totalPaid`, `outstandingBalance`, the
customer's aging state, and one entry per non-cancelled booking.

**The lines are a stacked card list, not a table** — the reference mockup's own
anatomy for this panel (it shows a stack of event cards, not a table). The drawer's
right-hand column is 5/12 of the content, so a seven-column table would have had to
hide the **Paid**, **Balance** and **Aging** columns off-screen, and those are the
payload of an Accounts Receivable view. Measured after the change, the panel has no
horizontal overflow at 375/768/1024/1440/1600/1920px and every data point is visible
without scrolling.

Each card carries the **booking number**, **occasion** (falling back to the booking
number when empty), **event date**, the **net amount due**, the **amount paid**, the
**balance**, the **BR-10 lifecycle status** and the **aging bucket** (the chip is
omitted entirely when the line carries no balance). The status chip uses the repo's
shared `getBookingStatusMeta` label; the aging chip renders the **server's**
`agingBucketLabel` verbatim.

A `Print SOA (PDF)` action downloads the QuestPDF statement.

### 4.10 Status footer

Reuses the existing `StatusFooter` component. It shows the API connection state and
the server's `generatedAt` from the directory response. The reference's *"SignalR
Telemetry Latency: 14ms"* and *"248 Total Records Loaded"* are **not** rendered:
there is no SignalR telemetry channel, and the record count is a business figure
that must come from the API.

---

## 5. API contract

Base URL `/api`. Bearer JWT. Enums are string names (§8 of the approved contracts).
Errors use `{ "errors": [ "..." ] }` for `400`/`404`; `401`/`403` use the same shape.

### 5.1 `GET /api/customers/directory`

Query parameters:

| Name | Type | Default | Notes |
| :--- | :--- | :--- | :--- |
| `search` | string? | — | case-insensitive substring over last name, first name, company, contact1 |
| `customerType` | string? | — | `Corporate` \| `Private` (case-insensitive); anything else ⇒ ignored |
| `arState` | string? | — | `Clean` \| `Open` \| `Overdue30` \| `Overdue60` (case-insensitive); anything else ⇒ ignored |
| `page` | int | 1 | values < 1 are clamped to 1 |
| `pageSize` | int | 10 | clamped to 1…100 |

Response `200`:

```json
{
  "summary": {
    "totalClients": 248,
    "activeArAmount": 684200.00,
    "clientsWithBalance": 14,
    "totalContractedValue": 18420000.00,
    "averageValuePerClient": 74274.19,
    "overdueAmount": 142000.00,
    "overdueClientCount": 3
  },
  "quickViewCounts": {
    "all": 248,
    "corporate": 142,
    "private": 106,
    "withOpenBalance": 14,
    "overdue30": 3
  },
  "items": [
    {
      "customerId": 18,
      "lastName": "Tan",
      "firstName": "Liza",
      "middle": null,
      "fullName": "Tan, Liza",
      "company": "Apex Global Technologies Inc.",
      "address": "Level 28, Apex Tower, BGC, Taguig",
      "contact1": "+63 920 441 9090",
      "contact2": "(02) 8876-4321",
      "dateRegistered": "2024-03-01T00:00:00",
      "customerType": "Corporate",
      "bookingCounts": { "total": 7, "past": 6, "upcoming": 1, "completed": 6, "cancelled": 0 },
      "totalContractedValue": 1180000.00,
      "totalPaid": 1180000.00,
      "outstandingBalance": 0.00,
      "ledgerState": "Clean",
      "ledgerStateLabel": "Paid in full"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 248,
  "totalPages": 25,
  "generatedAt": "2026-10-20T09:15:00.1234567Z"
}
```

**`summary` describes the whole filtered result set, not the current page.**
**`quickViewCounts` describes the search-filtered population with the type and
AR-state filters ignored**, so the quick-view pills always show the population
counts. `generatedAt` is the server UTC timestamp of the aggregate, so the status
footer can show a real generation time rather than a client clock (§4.10).

### 5.2 `GET /api/customers/{id}`

`200` with the `CustomerResponse` extended by: `customerType`, `bookingCounts`,
`totalContractedValue`, `totalPaid`, `outstandingBalance`, `ledgerState`,
`ledgerStateLabel`, `upcomingBooking` (`{ transactionId, bookingNumber, occasion,
eventDate, amountDue }` or `null`). `404` when the customer does not exist.

### 5.3 `GET /api/customers/{id}/statement`

Existing endpoint, **corrected** (§7.1). `200` with:

```json
{
  "customerId": 18,
  "customerName": "Tan, Liza",
  "contact": "+63 920 441 9090",
  "address": "Level 28, Apex Tower, BGC, Taguig",
  "totalBilled": 1180000.00,
  "totalPaid": 1180000.00,
  "totalRefunded": 0.00,
  "outstandingBalance": 0.00,
  "ledgerState": "Clean",
  "ledgerStateLabel": "Paid in full",
  "transactions": [
    {
      "transactionId": 601,
      "bookingNumber": "BK-20260315-00601",
      "eventDate": "2026-03-15T00:00:00",
      "occasion": "Q1 Gala Dinner",
      "amount": 198000.00,
      "paid": 198000.00,
      "balance": 0.00,
      "status": "Completed",
      "agingBucket": null,
      "agingBucketLabel": null
    }
  ]
}
```

### 5.4 `GET /api/customers/{id}/soa`

Streams the QuestPDF Statement of Account as `application/pdf`.
`Content-Disposition: attachment; filename="SOA-{customerId}-{yyyyMMdd}.pdf"`.
`404` when the customer does not exist.

### 5.5 `GET /api/customers/directory/export`

Streams the **current filtered result set** as XLSX (ClosedXML).
`Content-Disposition: attachment; filename="CustomerDirectory-{yyyyMMdd}.xlsx"`.
Honours the same `search` / `customerType` / `arState` parameters; **not** paged.

### 5.6 `POST /api/customers`

Existing. `201` with `{ "customerId": n }`. `400 { "errors": [...] }` on validation
failure. **Validation is tightened** (§7.3).

### 5.7 `PUT /api/customers/{id}`

Existing. `204` on success, `404` when the customer does not exist, and now
**`400 { "errors": [...] }` on validation failure** — it previously applied no
validation at all (§7.3).

---

## 6. Acceptance criteria

| ID | Criterion |
| :--- | :--- |
| AC-C1 | The directory loads from `GET /api/customers/directory` and renders the four KPI cards, the filter bar, the table and the pagination footer. |
| AC-C2 | Every figure on the screen comes from the payload. A repo scan of the feature folder finds **no** peso literal, count literal or date literal. |
| AC-C3 | The client computes **no** day count and **no** aging bucket. The bucket key and label are rendered exactly as the server sent them. |
| AC-C4 | `summary.activeArAmount` equals the sum of `items[].outstandingBalance` across the whole directory, and equals the dashboard's `receivables.outstandingAmount` for the same instant. |
| AC-C5 | A fully paid booking contributes `0` to a customer's balance; a cancelled booking contributes nothing at all. |
| AC-C6 | An overpaid booking clamps to a `0.00` balance, never negative. |
| AC-C7 | Search filters by name, company and contact number, case-insensitively. |
| AC-C8 | `customerType` and `arState` filters narrow the result set, and the quick-view counts update with them. |
| AC-C9 | Pagination is server-side: the table shows at most `pageSize` rows and the footer reports the true `totalCount` / `totalPages`. |
| AC-C10 | Clicking a row opens the drawer with that customer's data, loaded from `GET /api/customers/{id}`. |
| AC-C11 | The drawer's Profile & Contact tab edits only the seven real attributes; saving posts to the API and the list refreshes. |
| AC-C12 | A rejected save shows the server's `errors[]` verbatim and **preserves the typed input**. |
| AC-C13 | A successful save closes the drawer, refreshes the directory and the KPIs. |
| AC-C14 | The Ledger History tab renders the statement's lines with their booking number, amount, paid, balance and aging bucket. |
| AC-C15 | `Print SOA (PDF)` downloads a valid PDF for the selected customer. |
| AC-C16 | The export downloads an XLSX containing the current filtered result set. |
| AC-C17 | Every status chip carries a text label; no status is conveyed by colour alone. |
| AC-C18 | The screen is usable at 375 / 768 / 1024 / 1440 px; the table scrolls horizontally on narrow screens. |
| AC-C19 | Loading, empty, error and success states are all implemented; a failed load offers a retry and never renders fabricated content. |
| AC-C20 | The server rejects an over-length name, a blank name and an unknown customer id with the documented error contract. |

---

## 7. Required backend corrections

These are **not optional** — the screen would otherwise report figures that
contradict the approved rules.

### 7.1 F11 — the statement must use the authoritative net amount due

`GetCustomerStatementQuery` currently uses `b.PackageAmount` (the frozen base
package amount), which is a **third** money notion alongside the ledger's
recomputation and the persisted `Booking.AmountDue` (RSK-009). It must use the
shared `BookingAmountDueCalculator.Calculate(booking, totalPaymentsReceived)`
overload (DEC-046), exactly as the ledger and the dashboard do.

`Balance` must be `max(0, NetAmountDue − AmountPaid)` (BR-03) and must never be
negative. `Amount` becomes the Net Amount Due.

### 7.2 The statement's third status vocabulary is retired

`GetCustomerStatementQuery` derives `"Served"` / `"Upcoming"`, which appear in **no**
approved contract and conflict with BR-10. The line's `status` becomes the approved
BR-10 booking lifecycle name (`Pending`, `Confirmed`, `DepositPaid`, `FullyPaid`,
`InProgress`, `Completed`, `Cancelled`, `NoShow`, `Rescheduled`, `Expired`), and
`agingBucket` / `agingBucketLabel` are added on the DEC-041 basis.

### 7.3 Validation gaps closed

- `UpdateCustomerCommand` injects **no validator** today, so `PUT` accepts blank and
  over-length names and can produce a `500` from a SQL truncation. It must run the
  same `CreateCustomerValidator` (BR-11).
- `CreateCustomerValidator` has **no** maximum-length rule for `LastName`
  (DB `nvarchar(100)`), `FirstName` (100) or `Middle` (50). Those rules are added.
- The domain model invariant *"at least one valid contact method must be provided"*
  is **not** enforced today and is **not** introduced here — it would break existing
  records and is raised as an open item in §8.

### 7.4 The `is_deleted` filter

The Customer Master's own queries filter `IsDeleted = false` (DEC-050), matching the
dashboard-scoped pattern of DEC-045. No global query filter is introduced.

---

## 8. New decisions and KPIs raised by this specification

These are **recorded but not ratified**. Nothing in this screen depends on an
unratified rule being *true* — each is either a presentation derivation or a new
metric with a documented formula.

| ID | Type | Status | Subject |
| :--- | :--- | :--- | :--- |
| **DEC-050** | Data | PROPOSED | The Customer Master filters `IsDeleted = false` inside its own queries only (no global filter), following DEC-045's dashboard-scoped precedent. |
| **DEC-051** | Data | PROPOSED | Customer classification is **derived** — `Corporate` when `Company` is non-blank, `Private` otherwise. Never persisted. |
| **DEC-052** | UI | PROPOSED | A customer's AR state reuses the KPI-13 aging-bucket vocabulary plus a `Clean` key. No third status vocabulary is created. |
| **DEC-053** | Policy | OPEN — needs owner | DEC-041 confirmed the event-date aging basis for the **dashboard**; applying it to the **customer statement** was left open. This specification applies it to the statement and the Customer Master, and asks the owner to confirm. |
| **DEC-054** | Policy | OPEN — needs owner | The domain-model invariant *"at least one valid contact method must be provided"* is not enforced. Enforcing it now would reject existing records. |
| **KPI-20** | Metric | PROPOSED | **Total Registered Clients** = count of `Customer` rows with `IsDeleted = false`. Date basis: none (as-of snapshot). |
| **KPI-21** | Metric | PROPOSED | **Total Contracted Value** = `Σ NetAmountDue` over bookings with `Status ≠ Cancelled`. Date basis: none (as-of snapshot). Source: `Booking` + `Payment` via the shared calculator. |

**Aging on statements (DEC-053) is the only item in this specification that changes a
figure the owner has not already ruled on.** The screen's AR exposure and aging
figures are governed by DEC-025 and DEC-041 and are unchanged.

---

## 9. Test requirements

### 9.1 Backend

| # | Test |
| :--- | :--- |
| B1 | The directory projection's AR equals `Σ max(0, NetAmountDue − Σ payments)` over non-cancelled bookings. |
| B2 | A fully paid booking contributes `0`; a cancelled booking contributes nothing. |
| B3 | An overpaid booking clamps to `0.00`, never negative. |
| B4 | The statement's `amount` equals the ledger's net amount due for the same booking (the F11 fix). |
| B5 | The statement's `status` is a BR-10 lifecycle name. |
| B6 | A booking with `EventDate` in the past lands in exactly one aging bucket, and the bucket boundaries match the KPI-13 table. |
| B7 | `PUT` with a blank last name returns `400` with the `errors` array and changes nothing. |
| B8 | `POST` with a 200-character last name returns `400` with the `errors` array and changes nothing. |
| B9 | The directory projection is translatable by SQL Server (the existing `CustomerRepositoryTests` pattern). |
| B10 | `GET /api/customers/{id}/soa` returns a non-empty `application/pdf` for an existing customer and `404` for an unknown one. |
| B11 | The export returns a non-empty XLSX for a filtered query. |
| B12 | Soft-deleted customers are excluded from the directory and from the summary. |
| B13 | The `summary.activeArAmount` equals the sum of the per-customer balances over the whole directory. |

### 9.2 Frontend

| # | Test |
| :--- | :--- |
| F1 | The screen renders the KPI cards, the table and the pagination from a fixture payload. |
| F2 | A `noHardcodedFigures` scan of the feature folder finds no peso/count/date literal (the dashboard's existing pattern). |
| F3 | The client performs no day arithmetic — asserting the rendered bucket label is the server's string. |
| F4 | A rejected save renders the server's `errors[]` and preserves the typed input. |
| F5 | An empty result set renders the empty state, not a fabricated row. |
| F6 | A failed load renders the error state with a retry. |
| F7 | A successful save invalidates the directory query. |
| F8 | The paging controls call the API with the new `page` / `pageSize`. |
| F9 | The export request carries the active filters. |
| F10 | Status chips render their text label (not colour alone). |

---

## 10. Documentation updates required by this specification

| Artefact | Change |
| :--- | :--- |
| `architecture/approved-api-contracts.md` | Add §10 "Customer Master Endpoints" with the contract in §5. |
| `business/kpi-catalog.md` | Add KPI-20 and KPI-21. |
| `business/business-glossary.md` | Add "Statement of Account", "Total Contracted Value", "Customer Aging State", "Customer Classification (derived)". |
| `governance/decision-log.md` | Add DEC-050 … DEC-054. |
| `governance/risk-register.md` | Close/annotate RSK-009's F11 finding; record the new soft-delete scope. |
| `governance/requirements-traceability.md` | Add REQ-CUS-01; update REQ-REC-01 to the shipped endpoint. |
| `implementation/feature-status.md` | Mark Customer Management's frontend and the SOA controller delivered. |
| `implementation/acceptance-test-scenarios.md` | Add the §9 scenarios. |
| `business/data-dictionary.md` | Add `Booking.eventDate` (a documented gap); note the derived classification is not a column. |
| `architecture/domain-model.md` | Refresh the stale `Booking` class (retired `ServeStatus`/`IsCancelled`). |
