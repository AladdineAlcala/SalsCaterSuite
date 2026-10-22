# Sal's CaterSuite — Bookings & Events Register & Reservation Workbench — Feature Specification

| Field | Value |
| :--- | :--- |
| **Specification** | `business/bookings-events-feature-spec.md` **v1.1** (reissued after the Debater's plan review) |
| **Requirements delivered** | **REQ-BKG-05** (new — the Bookings & Events Register) and **REQ-BKG-01** (the Booking Creation Wizard's missing screen) |
| **Cycle** | 2026-10-22 — Bookings & Events |
| **Owner** | AGENT 01 — Business Intelligence Architect |
| **Design references** | `D:\Downloads\SalsCaterSuite_Designs\Booking_Events_Main\{code.html,screen.png}` and `D:\Downloads\SalsCaterSuite_Designs\Booking_Events_CreateNew\{code.html,screen.png}` |
| **Contract section** | `architecture/approved-api-contracts.md` §14 |
| **Status** | **APPROVED for delivery** — see §1.2 for the rulings and §9 for the questions that remain open. The Debater's plan review returned `PROPOSE_BETTER_PLAN` with **8 blocking findings and 17 clarifications**; **all eight findings were applied** before delegation (see `WORKFLOW_STATE.md`) |

---

## 1. Objective and Scope

### 1.1 Objective

Deliver the two screens the catering office works from every morning:

1. **The Bookings & Events Register** (`/bookings`) — the *main screen*. A paged, filtered dossier of every booking, with the pipeline telemetry, the four operational KPI cards, the tier/venue/status/payment-state filters, the dossier grid, the live totals strip, the pagination dock and the three-card operational dock.
2. **The Reservation Workbench** (`/bookings/new`) — the *create screen*. A three-column workbench that composes a booking: the client & logistics rail, the package tier & course specification engine, and the live financial ledger & seal rail. It saves a **draft** or **confirms** the booking.

Both screens are **read-only on the money**: every peso, every percentage and every count is published by the API (DEC-002). The browser formats and displays; it never derives an authoritative figure.

### 1.2 The rulings this cycle makes

AGENT 01 is the owner of the business rules. The following rulings were required to make the references executable; each is recorded as a decision in `governance/decision-log.md`.

| # | Question | Ruling | Decision |
| :--- | :--- | :--- | :--- |
| 1 | **DEC-035 is open** — two net-due sources coexist (the frozen `Booking.AmountDue` and the calculator's recomputation). They agree today *only* because no discount, surcharge or add-on can be created. **This cycle creates a surcharge and a discount**, so the divergence becomes real. | **DEC-035 is resolved.** The **frozen booking snapshot is the single net-due source**, and `BookingAmountDueCalculator.Calculate(Booking, …)` **reproduces it** from the frozen columns. The create path stores the snapshot and the calculator recomputes the **same** figure, so the two agree **by construction** for every booking. The register, the ledger, the dashboard, the customer statement and the calendar can no longer disagree. | **DEC-099** (closes RSK-002, RSK-009) |
| 2 | BR-01's extended-area fee is **not charged on any booking today** — DEC-078 deferred the wiring. | **The wiring is ratified and delivered.** `ExtendedAreaAmount = Area.AdditionalRate × NumberOfHeads`, applied only when `ApplyExtendedAmount` is true, **frozen** on the booking (BR-04). The rate is the **municipality's single rate** (BR-17, DEC-078) — there is no second rate column. | **DEC-100** (closes RSK-036's booking half) |
| 3 | The references show a **"30% Downpayment (Required)"**. **DEC-009** (the minimum deposit percentage) is **UNRESOLVED** and DEC-043 ruled that *"no deposit percentage is assumed or implied"* while it stays open. | The **required deposit percentage becomes configuration** (`Bookings:Deposit:RequiredPercentage`), **absent by default**. Absent ⇒ the rail reads *"Required deposit: not stated"* and the operator states the deposit amount. The reference's `30` is **never** hardcoded. This **extends** DEC-043: it still assumes no percentage, but it lets the owner set one **without a release**. | **DEC-101** (extends DEC-043; DEC-009 stays open) |
| 4 | The references show a **"Commissary Capacity 120 / 400 Pax"** ceiling. **No capacity figure is modelled** — BR-16 says outright that *"there is no guest ceiling in the model"*. | The capacity is **configuration** (`Bookings:Capacity:MaximumPax`), **absent by default**. Absent ⇒ the gauge reads *"not stated"* and the load is still published. The reference's `400` is **never** hardcoded. | **DEC-102** |
| 5 | The references show a **per-booking gross margin** ("Margin: 58.2%"). | The register publishes the **tier's base gross profit percentage** — the standard-run estimate (BR-19) — and labels it a **planning estimate**. It is **not** a per-booking margin, it never enters a booking total, and it is `null` when the tier has no costed pool. | **DEC-103** |
| 6 | The references show **Kitchen BEO states** ("BEO Ready", "BEO Dispatched", "6 Dispatched", "2 Pending Executive Chef Sign-off"). | There is **no BEO entity and no BEO production state** (DEC-036, DEC-044). The panels render an explicit **capability-gap notice**; the "Batch QuestPDF BEOs" action is an **inactive** button (DEC-089's `Button.inactive` pattern), never a fabricated queue. | **DEC-104** |
| 7 | The references show a **per-event dish selection** ("Approved 6-Course Menu Specification", six named dishes). | The workbench publishes the tier's **course-coverage ruleset and its qualified pools** (contract §13.1) — the real, approved data. The **per-booking dish selection** has no link in the model and is a **capability gap** in this cycle (it is the open half of REQ-BKG-02). | **DEC-105** |
| 8 | The references show a **Dispatch Schedule Matrix** (Call Time / Program / Packout). | `Booking` gains **two nullable columns**, `CallTime` and `PackoutTime`, in **one purely additive migration**. `Program` is the existing `EventStartTime`; `EventEndTime` is the service end. | **DEC-106** |
| 9 | The references show a **"Save Draft"** and a **"Save Draft Specs"**. | A draft is a booking in the **`Pending`** status — the state `Booking.Create` already produces. **No draft entity and no new status** are introduced. | **DEC-107** |
| 10 | The references show the **package tier inclusions** ("VIP Presidential Head Table Floral Setup", "Full Fine-China …"). | `Package` carries **no inclusions collection**. The panel renders a **capability-gap notice** (the DEC-084 precedent). No inclusion is fabricated. | **DEC-108** |
| 11 | The references show a **"Tier Leader … +8.4% growth vs last cycle"** and an **"In Bank"** split of collections. | The register publishes the **leading tier by net contracted value**, its **share of the population's net contracted value**, and the **change against the immediately preceding equal-length period** (KPI-34). "In bank" is not a business concept in this model, so the card publishes the two modelled quantities — **collected** and **outstanding** (BR-08) — which **sum to the net contracted value**. | **DEC-109** |
| 12 | The references show a workbench with its **own application shell** (a compact top bar instead of the sidebar). | The application keeps **one authenticated shell** (`AppLayout`). The workbench renders **inside** it, so the navigation, the session and the route guard are identical on every screen. The workbench's own header block carries the booking number, the step indicator and the two actions. | **DEC-110** |
| 13 | **RSK-024** — the dashboard excludes soft-deleted payments; the ledger, the customer statement and the calendar do not. The register must reconcile with the dashboard. | The **soft-deleted-payment exclusion is unified**: every money surface — the ledger, the customer statement, the calendar and the register — excludes a soft-deleted payment, exactly as the dashboard does (DEC-045, AC-B9). | **DEC-111** (closes RSK-024) |
| 14 | The create screen records an **initial payment**, and **RSK-013** records the receipt-number race (`OR-{date}-{count+1}`) as an open unhandled-`500` risk. | The receipt number has **one generator** in the Application layer, shared by the ledger and the create path, so no second convention is created. The race itself is **not** fixed by this cycle — RSK-013 stays open — but the create path inherits the existing behaviour rather than a new one. | **DEC-112** (narrows RSK-013) |
| 15 | **RSK-023** — `ContractDocument` renders the denormalized cache and the domain's `Booking.Balance`, a **third money notion**, and its mitigation says the generators must move onto the shared calculator *"before any endpoint exposes them"*. **This cycle exposes it** as `GET /api/bookings/{id}/contract`. | The contract PDF renders the **shared calculator's** figures — `NetAmountDue`, `TotalPayments`, `RemainingBalance` — exactly as the ledger publishes them, so a printed contract can never disagree with the ledger it sits beside (DEC-023, DEC-025). | **DEC-113** (closes RSK-023) |
| 16 | **The money chain has no rounding rule.** `DiscountPercentage` is `decimal(18,4)` while every money column is `decimal(18,2)`, and `RecalculateFinancials` does raw arithmetic. A percentage discount of `0.005` on a gross of `100.00` makes the stored `AmountDue` and the calculator's `NetAmountDue` differ by half a cent. | **One rounding, in one place** — two decimals, away from the midpoint (BR-20, DEC-003) — applied to the **discount** and the **net** in the Application layer, and the create path stores exactly those rounded values. The browser never re-rounds. | **DEC-115** |
| 17 | The surcharge was **inferred** as `Subtotal − PackageAmount`. `Subtotal` conflates the surcharge with the add-on total and the other charges, and `ApplyExtendedAmount` is publicly settable — so the inference changes meaning the day add-ons exist and can be moved after the fact. | The surcharge becomes a **fourth frozen column**, `extendedAreaAmount` (`decimal(18,2) NOT NULL DEFAULT 0`). The flag becomes a creation-time switch and an audit marker; the **stored amount** is what the calculator charges. | **DEC-116** |
| 18 | The payment-state names `DepositPaid` and `FullyPaid` **collide** with the `BookingStatus` lifecycle names of contract §5. A client that received both in one payload could not tell them apart. | The payment states are renamed **`AwaitingDeposit`, `PartiallyPaid`, `Settled`**. The two vocabularies never share a name. | **DEC-117** |
| 19 | The cycle adds a frontend feature folder, two routes, a client endpoint group, Application use cases, a repository interface and its implementation, an export, a migration and two configuration keys. AGENTS.md §0.1 rule 3 requires a **structural** decision recorded in `governance/decision-log.md` and a workplan update **in the same cycle**. | The structural change is **approved** and the workplan is realigned in this cycle. | **DEC-114** |
| 20 | BR-05's **overpayment prohibition** is not implemented on `POST /api/payments` — `Booking.RecordPayment` rejects only a non-positive amount and a payment on a cancelled booking. The register publishes a `Settled` state that **includes** an overpayment, so an unguarded overpayment would let a booking report `Settled` while it owes money no surface can show. | The **overpayment guard is enforced** on the payment path as well as on the create path: a payment that would take the total above the net amount due is refused with `400`, and nothing is written. | **DEC-118** |

### 1.3 What the references show and what the approved model can carry

| Reference panel | Approved source | Verdict |
| :--- | :--- | :--- |
| Booking code `BK-2025-089` | `Booking.BookingNumber` — `BK-{yyyyMMdd}-{TransactionId:D5}` (BR-09, DEC-016) | **Rendered as the domain format.** The reference's `#BKG-2026-1188` is not the contractual format and is not adopted. |
| Status `CONFIRMED` / `TENTATIVE` | `BookingStatus` (BR-10) | **The domain vocabulary wins.** `Pending` is the tentative state; `Finalized` and `Invoiced` do not exist and are not introduced. |
| Tier pills `Wedding Tier` / `Premier Buffet` / `VIP Special` | `Package.PackageType` (free text; the `PackageType` enum members are the suggestions) | **Server-computed facet counts** over the register population. The pill label is the stored type; the client never rewrites it. |
| Package & venue staging box | `Booking.PackageName`, `PricePerHead`, `NumberOfHeads`, `Booking.Venue` | **Rendered.** |
| Contract value / paid / balance block | `BookingAmountDueCalculator` (DEC-042) + `Σ Payment.AmountPaid` (BR-10) | **Rendered, server-computed.** |
| Kitchen telemetry footer (BEO state, chef, margin) | margin → tier standard run (DEC-103); BEO state → **no source** | **Margin rendered as a planning estimate; the BEO chip is a capability gap.** |
| Kitchen prep staging percentages | **no source** — the recipe/yield engine is blocked (REQ-MNU-03, DEC-059) | **Capability gap.** |
| Tier pills, venue filter, status filter, date filter, search | `Booking` + `Customer` + `Package` | **Server-side filters.** |
| The payment filter (*"Fully Paid (100%) / 30% Downpayment Received / Balance Overdue / SOA Sent / Awaiting Initial Deposit"*) | the three BR-28 payment states; the overdue dimension | **Re-mapped.** The dropdown offers the **three** payment states and **no** percentage option (DEC-101); **"Overdue only"** is a **separate** toggle, because a partly-paid booking is both `PartiallyPaid` and overdue and folding the two would make the states overlap (BR-28). |
| Table totals strip | BR-08 + the single calculator | **Rendered, server-computed.** |
| AR & SOA telemetry card | KPI-12 / KPI-13 quantities | **Rendered.** "3 Notices Sent" has no source and is replaced by the **overdue count**. |
| Client profile card | `Customer` (name, company, contact, address) | **Rendered.** The reference's "Official Email" does not exist in the model and is replaced by the **address**. |
| Dispatch schedule matrix | `EventStartTime` / `EventEndTime` + the two new columns | **Rendered** (DEC-106). |
| Venue & spatial logistics | `Area` (municipality, `AdditionalRate`, `DistanceKm`, `LogisticsNote`, `LogisticsCost`) | **Rendered** (BR-17, BR-18). |
| Included site amenities | **no source** | **Capability gap** (DEC-108). |
| Package tier & headcount configuration | `Package.AmountPax`, `Package.MinPax` | **Rendered.** |
| Course selection matrix | `GET /api/packages/{id}/course-rules` (BR-14..BR-19) | **Rendered as the approved course specification**; the per-booking dish selection is a capability gap (DEC-105). |
| Chef batching prep yield | **no source** | **Capability gap.** |
| Commissary capacity gauge | configuration + the server's near-term load | **Rendered** (DEC-102). |
| Contract & ledger rail | the quote endpoint | **Rendered, server-computed.** |
| Pre-dispatch BEO validation | derived server-side checks | **Rendered** (BR-33). |
| "Verify VIP" badge | **no source** | **Replaced by the modelled customer classification** (`Corporate` / `Private`, DEC-051). |
| The header's **"Live Calendar Sync"** badge and the sidebar's **"SIGNALR LIVE"** indicator | The calendar feed exists (`GET /api/calendar/events`) but there is **no hub and no push channel** — SignalR is registered and `NotificationHub` is **not yet built** | **Not rendered.** A "live" badge would assert a capability that does not exist. The calendar's own screen (REQ-CAL-01) is a separate module. |
| The sidebar's **"Kitchen Mode"** toggle | It belongs to the **application shell**, not to this screen | **Not rendered** by the bookings feature. |
| The tier **code** (`PKG-PREM-26B`) in the workbench's tier block | `Package` carries **no code column** — its identity is `PackageId` and its display name is `Description` | **Not rendered.** The tier block shows the id and the description; a code is a capability gap (DEC-119). |

### 1.4 Exclusions (explicit)

The following are **not** delivered in this cycle, each with its reason:

| Excluded | Reason | Tracked as |
| :--- | :--- | :--- |
| Add-on line items on a booking | `AddonDetail` exists as a table but has **no repository and no link to `Booking`**; linking it needs a join entity and a second migration. The reference's ledger rail shows no add-on line. | REQ-BKG-01 (open), RSK-049 |
| A per-booking dish selection | There is **no `Booking`↔`Menu` link**. | REQ-BKG-02 (open), DEC-105 |
| The BEO PDF and the batch-BEO action | There is **no BEO entity** and no per-booking dish selection to print (DEC-036, DEC-104). | REQ-REP-02 (open), DEC-104 |
| Kitchen prep percentages and yields | The recipe/ingredient/yield engine is **blocked** on ten owner decisions (REQ-MNU-03, DEC-059). | REQ-MNU-03 (blocked) |
| A second shell for the workbench | The application has **one authenticated shell** (`AppLayout`). The reference's workbench replaces it; the workbench is rendered **inside** the shell so the navigation, the session and the route guard are identical on every screen. | DEC-110 |
| The "Timeline Board" second view of the register | The reference shows a grid/timeline toggle. The timeline is delivered as the register's **chronological ordering** — a **server-side sort**, not a second screen. | §5.2 |
| A booking **edit** screen | The reference shows "Manage Event" and "Dish Costing" per card. The edit screen is a separate requirement and is not part of this delivery. | REQ-BKG-01 (open) |
| The banquet calendar | `REQ-CAL-01` has its own backlog entry and its own screen. | REQ-CAL-01 |

---

## 2. Actors and Permissions

| Capability | Endpoint | Permitted roles | Basis |
| :--- | :--- | :--- | :--- |
| Read the register, the summary, the quote | `GET /api/bookings`, `GET /api/bookings/summary`, `POST /api/bookings/quote` | any authenticated user | BR-12 |
| Download the contract PDF / the register XLSX | `GET /api/bookings/{id}/contract`, `GET /api/bookings/export` | any authenticated user | BR-12 |
| **Create a booking** (draft or confirmed) | `POST /api/bookings` | any authenticated user | BR-12 |
| Record the **initial payment** on a confirmed booking | `POST /api/bookings` with `initialPayment` | Administrator, Manager, Cashier | BR-12, `AuthorizationPolicies.PaymentsRecord` |
| Cancel a booking / issue a refund | `POST /api/bookings/{id}/cancel` | Administrator, Manager | BR-12, `AuthorizationPolicies.BookingsCancel` |

**The permission to create a booking and the permission to take money are different.** A confirmed booking that carries an initial payment requires the `Payments.Record` policy; a draft never carries a payment. The authorization is enforced **server-side** on the policy, and the client only hides what the server refuses.

---

## 3. Domain and Data

### 3.1 The domain extension (one additive migration)

`Booking` gains **three nullable columns**. The migration is **purely additive** — no column is dropped, renamed, retyped or re-defaulted, and no existing row is modified.

| Column | Type | Null? | Meaning |
| :--- | :--- | :--- | :--- |
| `callTime` | `time(0)` | yes | The kitchen call time (DEC-106). |
| `packoutTime` | `time(0)` | yes | The pack-out time (DEC-106). |
| `discountCode` | `nvarchar(50)` | yes | The **frozen** code of the applied discount (DEC-106, BR-02). The *amount* was already stored; the **code** is what makes the booking auditable. |
| `extendedAreaAmount` | `decimal(18,2)` | **no** (`DEFAULT 0`) | The **frozen** out-of-town surcharge in pesos (DEC-116, BR-26). |

**Why the surcharge is stored and not inferred.** An earlier draft recovered it as `Subtotal − PackageAmount`. That is wrong for three reasons: `Subtotal` **conflates** the surcharge with the add-on total and the other charges, so the inference silently changes meaning the day add-ons exist (RSK-048); `ApplyExtendedAmount` is a **publicly settable** flag, so flipping it after creation would change what the calculator charges while the stored `Subtotal` still carried the surcharge; and a stored amount that is **not** a derived difference is the only thing BR-04 can honestly call *frozen*. The surcharge is therefore its own column, written **once**, and the calculator reads it directly — so the flag becomes a creation-time switch and an audit marker rather than a money switch.

The migration remains **purely additive**: four columns, three nullable and one `NOT NULL DEFAULT 0`. No column is dropped, renamed or retyped and no existing row is modified. A pre-existing row's surcharge is `0` — which is the truth, because no booking has ever been charged one.

**No new entity, no new enum.** `Booking` remains the aggregate root and the only entity whose schema changes.

### 3.2 The register population

The register's population is **every non-soft-deleted booking** that is not `Cancelled`, subject to the active filters. With **no filter applied** this is exactly the dashboard's receivables population (DEC-045), so the unfiltered register totals reconcile with the dashboard's KPI-12/KPI-13 **to the cent**.

Soft-deleted bookings are excluded by the query itself. **No global query filter is introduced** (DEC-038).

### 3.3 The filters (BR-32)

| Filter | Parameter | Semantics |
| :--- | :--- | :--- |
| Search | `search` | Case-insensitive `Contains` over the **booking number**, the **occasion**, the **customer name**, the **customer company** and the **venue**. An empty term matches everything. The term is **redacted** in the operational log (BR-21, `search` is in the query-parameter set). |
| Status | `status` | A comma-separated list of `BookingStatus` **names**. An unknown name is rejected with `400 { errors }` — the vocabulary is never silently widened. |
| Tier type | `packageType` | A comma-separated list of stored `Package.PackageType` values. |
| Venue | `venue` | A comma-separated list of stored `Booking.Venue` values. |
| Payment state | `paymentState` | A comma-separated list of the three states in §3.4. An unknown name is rejected with `400` — the vocabulary is never silently widened. |
| Overdue | `overdue` | A **tri-state**: **absent** ⇒ no overdue filter; `true` ⇒ only the bookings whose balance is outstanding **and** whose event date has passed (DEC-041); `false` ⇒ only the bookings that are **not** overdue. A filter that silently returned everything for `false` would be the null-as-zero defect the workplan forbids. |
| Date range | `fromDate`, `toDate` | An **inclusive** range on `Booking.EventDate`. Both are optional; `fromDate` alone means "from that date on", `toDate` alone means "up to that date". `fromDate > toDate` is a `400`. |
| Page | `page` | 1-based. Default `1`. |
| Size | `pageSize` | Default `10`; the permitted values are `10`, `25`, `50`; anything else is a `400`. |
| Sort | `sort` | `EventDateAsc` (default), `EventDateDesc`, `ContractValueDesc`, `ContractValueAsc`, `BookingNumberAsc`. |

**The facets are computed with the facet's own dimension removed** — the standard faceted-search behaviour: the tier counts ignore the tier filter, the venue counts ignore the venue filter, and so on. Every facet therefore answers "how many would I get if I chose this?".

**One population, one query.** The **search** and the **date range** are the *base* filters; the **status**, **tier type**, **venue**, **payment state** and **overdue** are the *facet* filters. The repository loads the **base population** once — with the per-booking figures the totals and the facets need — and the Application layer derives the main population and each facet from it. The totals are therefore over the **whole filtered population** (never over the page), and each facet is the base population with **one** facet filter removed. This is the same "filter in the browser over the server's list" shape the packages catalogue already uses, applied server-side.

**The compute strategy is stated, because it decides where the work happens.** Two of the dimensions **cannot be expressed in SQL**: the payment state and the overdue dimension are derived from the net amount due, the payment sum and the event date against a "today" the **server** owns, and the payment state in particular has no column. So:

1. The repository loads the **base population** — the bookings matching the search and the date range, with their customer, package, area and payment rows — **with no pagination**. The base population is bounded by the search and the date range, **not** by the page (RSK-056).
2. The Application layer computes each booking's **net amount due**, **collected** and **outstanding** through the one calculator, and derives the **payment state** and the **overdue** flag.
3. The Application layer applies the remaining filters, composes the **totals** and the **facets**, and **only then** applies the pagination and the sort.

The consequence is explicit: **the register's cost is proportional to the filtered population, not to the page size.** For a register of a catering operation's live book that is the right trade — the totals strip and the facet counts must describe the whole population anyway — and it is recorded as RSK-056 rather than hidden.

### 3.4 The payment-state vocabulary (BR-28)

One classifier, in one class, shared by the register, the summary and the quote — the same pattern as `CustomerAgingService` (DEC-052).

The **payment state** answers one question: *how far along is the payment?* It is a **three-state** vocabulary and it is **mutually exclusive and exhaustive**:

| State | Condition |
| :--- | :--- |
| `AwaitingDeposit` | `Collected == 0` **and** `NetAmountDue > 0` — nothing has been collected and something is owed. |
| `PartiallyPaid` | `Collected > 0` **and** `Collected < NetAmountDue` — a deposit or partial payment has been received and a balance remains. |
| `Settled` | `Collected >= NetAmountDue` — nothing is owed. This includes a **zero-value** booking (`NetAmountDue == 0`, nothing collected) and an **overpayment** (`Collected > NetAmountDue`), neither of which may be reported as awaiting anything. |

The evaluation order is **`AwaitingDeposit` → `PartiallyPaid` → `Settled`**, and the three are exclusive by construction: `Collected == 0` and `Collected > 0` cannot both hold, and `Collected < NetAmountDue` and `Collected >= NetAmountDue` cannot both hold. Every booking therefore has **exactly one** payment state, and the three counts sum to the population's booking count.

**The names must not collide with the lifecycle vocabulary.** Contract §5 publishes the ten `BookingStatus` names, and two of them — `DepositPaid` and `FullyPaid` — are *payment* names. The payment states are therefore named **`AwaitingDeposit`, `PartiallyPaid`, `Settled`** (DEC-117), and the two vocabularies never share a name. `BookingStatus.DepositPaid` remains a **lifecycle** status; `PartiallyPaid` is a **payment** state. A client that received both in one payload can tell them apart by name.

**Overdue is a separate dimension, not a payment state.** A booking with a balance is **overdue** when its **event date has passed** — the event date is the due date (DEC-041):

```
isOverdue = Outstanding > 0 && EventDate < today
```

`isOverdue` is published **per item** and drives the register's **overdue filter** and the `receivables.overdueCount` / `overdueAmount` aggregates. It is deliberately **not** a payment state: a partly-paid booking is `PartiallyPaid` **and** overdue, and folding the two into one vocabulary would make the states overlap (the register's filters would then select rows that no single state describes).

**Why not the reference's four payment options.** The reference offers *"Fully Paid (100%) / 30% Downpayment Received / Balance Overdue / SOA Sent / Awaiting Initial Deposit"*. The `30%` cannot be a filter: the deposit percentage is **configuration and absent by default** (DEC-101), so a percentage-based filter would assert a house rule that does not exist. The register therefore filters on the **three** payment states — labelled *"Fully paid"*, *"Partially paid"* and *"Awaiting initial deposit"* — and offers **"Overdue only"** as a separate, clearly-labelled toggle beside the filter row.

### 3.4.1 The filter surface (BR-32)

The **Payment** dropdown offers: *All payment states*, *Fully paid* (`Settled`), *Partially paid* (`PartiallyPaid`) and *Awaiting initial deposit* (`AwaitingDeposit`). The **overdue** filter is a separate toggle chip: *Overdue only*.

Both feed the same `paymentState` / `overdue` query parameters, and the **facet** for the payment states counts the three states of the base population with the payment-state filter removed.

### 3.5 The single net-due source (BR-25, DEC-099, DEC-115)

**DEC-035 is resolved by this cycle.** There is now exactly **one** net-due figure per booking, and it is computed in **one** place — `BookingAmountDueCalculator.Calculate(Booking, totalPaymentsReceived)` — which **reproduces the frozen snapshot** rather than recomputing a second, parallel total.

The shared overload reads the **frozen columns**:

```
BasePackageAmount  = booking.PackageAmount                                 (frozen, BR-04)
ExtendedAreaAmount = booking.ExtendedAreaAmount                            (frozen, BR-26, DEC-116)
AddonsAmount       = 0                                                      (§1.4 — no add-on link)
OtherChargesAmount = 0
GrossAmount        = Round2(BasePackageAmount + ExtendedAreaAmount + AddonsAmount + OtherChargesAmount)

RawDiscount        = booking.DiscountAmount > 0
                        ? booking.DiscountAmount                           (a stored fixed discount wins, BR-02)
                        : booking.DiscountPercentage > 0
                            ? GrossAmount * booking.DiscountPercentage / 100
                            : 0
DiscountAmount     = Round2( min(RawDiscount, GrossAmount) )                (BR-02 precedence and ceiling)

NetAmountDue       = Round2( max(0, GrossAmount - DiscountAmount + booking.TaxAmount) )
RemainingBalance   = Round2( max(0, NetAmountDue - totalPaymentsReceived) ) (BR-03)
IsFullyPaid        = NetAmountDue > 0 && totalPaymentsReceived >= NetAmountDue
```

**The rounding rule (DEC-115).** `Round2` is the **one** rounding of the money chain: **two decimals, away from the midpoint** (`MidpointRounding.AwayFromZero`) — BR-20's rule and DEC-003's convention, applied **once**, in the Application layer. Every money figure the API publishes is a rounded one, and the browser never re-rounds.

**The rounding is not cosmetic — it is what makes the two sources equal.** `DiscountPercentage` is stored as `decimal(18,4)` while every money column is `decimal(18,2)`. Without a single rounding, a percentage discount of `0.005` on a gross of `100.00` yields a raw discount of `0.005`; the stored `AmountDue` would be persisted as `99.99` or `100.00` (the column's scale) while the calculator returned `99.995` — a divergence of half a cent on a single booking, and a reconciliation failure across the whole register. Rounding the **discount** and the **net** once, and storing exactly those rounded values, makes `Booking.AmountDue` and `calculator.NetAmountDue` **identical** for every booking.

**The create path stores exactly these values.** It computes the raw discount, rounds it with the same `Round2`, and passes the **rounded** discount to `Booking.RecalculateFinancials`, which stores `DiscountAmount = min(discount, Subtotal)` and `AmountDue = max(0, Subtotal − DiscountAmount + TaxAmount)`. Every input to that expression is already two decimals, so the stored figure is exact. A unit test asserts the equality for a plain booking, a surcharge booking, a percentage-discount booking, a fixed-discount booking and one with both.

**This is behaviour-preserving for every existing row.** `Booking.RecalculateFinancials` has never been called in production code, so every stored row has `Subtotal == PackageAmount`, `DiscountAmount == 0` and `TaxAmount == 0`. The formula above therefore returns exactly what the previous recomputation returned for those rows, and the **700 unit and 118 integration tests must stay green unchanged** — every pinned expectation is a whole peso amount, so the new rounding is a no-op on all of them.

The calculator's **six-argument overload** — the "from scratch" path used by the unit tests — is **retained with its current signature and semantics**, with the tax term at zero and the same `Round2` applied. The two overloads agree whenever `TaxAmount` is zero, which is every row today.

**Consumers.** The overload is used by the payment ledger, the booking details, the customer statement, the customer repository, the calendar feed and the dashboard. All eight call sites therefore change together, and no surface can hold a different figure from another.

**The stored columns.** The register reads **neither** `Booking.AmountDue` **nor** `Booking.AmountPaid` as a figure; it reads the calculator's output and `Σ Payment.AmountPaid` (BR-10). `Booking.AmountPaid` remains the denormalized cache BR-10 requires it to be.

**The legacy SQL roll-up.** `GET /api/customers` (the legacy unpaged list) computes its receivables **in SQL** off the persisted `Booking.AmountDue`. That is **not** a divergent money notion once DEC-099 holds, because the persisted column now **equals** the calculator's figure for every booking — the SQL read returns the same number. It is nevertheless brought onto the same **payment population** (§3.6), so the soft-deleted-payment exclusion applies there too (DEC-111).

### 3.6 The authoritative figures (BR-27)

Every money figure on both screens comes from the **one** calculator (DEC-042, DEC-099):

```
NetAmountDue      = BookingAmountDueCalculator.Calculate(booking, Σ Payment.AmountPaid).NetAmountDue
Collected         = Σ Payment.AmountPaid, EXCLUDING soft-deleted payments    (BR-10, DEC-111)
Outstanding       = max(0, NetAmountDue − Collected)                          (BR-03)
```

and therefore

```
NetContractedValue = Σ NetAmountDue
CollectedTotal     = Σ Collected
OutstandingTotal   = Σ Outstanding
NetContractedValue = CollectedTotal + OutstandingTotal      ← the register's split always reconciles
```

The **soft-deleted-payment exclusion is unified** (DEC-111): the dashboard already applies it (DEC-045, AC-B9) and six other surfaces did **not**. From this cycle **every** money surface applies the same rule, so "the register's outstanding equals the dashboard's KPI-12" is true by construction and not by coincidence. The complete list of surfaces that must apply it:

| # | Surface | As-built | Action |
| :--- | :--- | :--- | :--- |
| 1 | `GET /api/dashboard` — `DashboardService.AmountPaid` | already filters `!p.IsDeleted` | **unchanged** |
| 2 | `GET /api/bookings/{id}` — `GetBookingDetailsQuery` | `booking.Payments.Sum(p => p.AmountPaid)` | **add the filter** |
| 3 | `GET /api/bookings/{id}/ledger` and `GET /api/payments/ledger/{id}` — `PaymentLedgerService.GetLedgerAsync` | `booking.Payments.Sum(...)` | **add the filter** |
| 4 | `GET /api/calendar/events` — `GetCalendarEventsQuery` | sums the booking's payments | **add the filter** |
| 5 | `GET /api/customers/directory` and `GET /api/customers/{id}` — `CustomerRepository.AmountPaid` | `booking.Payments?.Sum(...)` | **add the filter** |
| 6 | `GET /api/customers` (the legacy unpaged list) — the in-SQL receivables roll-up | `b.Payments!.Sum(p => p.AmountPaid)` | **add the filter to the SQL** |
| 7 | The Bookings & Events Register — the new population query | new | **build it with the filter** |

The **ledger's payment list** is a second, separate question: the ledger *lists* the payment rows, and a soft-deleted payment should not be listed either. The exclusion is therefore applied to **both** the listed entries and the sum — a soft-deleted payment is not money the business holds, and it is not a receipt the customer may see.

**The overpayment guard.** BR-05 prohibits a total payment that exceeds the net amount due *"unless explicitly flagged and acknowledged as an advance credit"*. The guard is **not** implemented on `POST /api/payments` today — `Booking.RecordPayment` only rejects a non-positive amount and a payment on a cancelled booking. This cycle **adds** the guard to the payment path, because the register publishes a `Settled` state that includes an overpayment and a `Balance` that clamps at zero; an unguarded overpayment would let a booking report `Settled` while it owes money that no surface can show. The **create path** carries the same guard (BR-33).

### 3.7 The out-of-town surcharge on a booking (BR-26, DEC-100, DEC-116)

The surcharge is **the municipality's** rate, applied **per guest**, and it is **frozen on the booking** (BR-04):

```
ExtendedAreaAmount = ApplyExtendedAmount ? Round2(Area.AdditionalRate × NumberOfHeads) : 0
```

The amount is **stored** in its own column (`extendedAreaAmount`, §3.1) — it is **not** inferred from `Subtotal − PackageAmount` (DEC-116).

`Area.AdditionalRate` is `null` when the municipality has stated none. The **money chain treats an absent rate as zero** (DEC-086 — an absent addition is nothing) and the **published rate stays `null`** (the measurement is unknown). `ApplyExtendedAmount` is the operator's **creation-time switch**; after the booking exists the switch is an audit marker and the **stored amount** is what the calculator charges, so flipping the flag can never move a stored booking.

There is **one** rate column and it means **one** thing (BR-17, DEC-078): the same `Area.AdditionalRate` that the workbench quotes as the out-of-town surcharge is the rate BR-01 charges on a booking. Editing it therefore moves the quoted price of every tier that covers the municipality **and** the surcharge of every **new** booking; it does **not** move a **stored** booking, because the amount is frozen (BR-04) — the consequence RSK-036 records.

The calculator therefore charges the surcharge **exactly once**, and the ledger, the register, the summary and the dashboard cannot disagree.

### 3.8 The discount (BR-02, unchanged)

A discount is resolved from the `Discount` catalogue by its **code**. The catalogue is currently empty — BR-13 forbids seeding business data — so an unknown code is a `400`, never a silent zero.

**`disc_code` has no unique index.** The code is therefore **not** unique in the model, so the resolution is defined explicitly:

- **No active discount** with that code → `400` ("The discount code was not found.").
- **More than one active discount** with that code → `400` ("The discount code is ambiguous."). The server never picks one silently.

The rules are unchanged:

- A **fixed** amount takes precedence over a percentage.
- A percentage is charged on the **gross amount**.
- The discount is **capped at the gross amount**.

The applied `DiscountType`, `DiscountPercentage`, `DiscountAmount` and the **frozen `DiscountCode`** are stored on the booking, and the stored values are exactly the ones §3.5 reads back. A later change to the catalogue never alters a stored booking (BR-04).

### 3.9 The deposit (BR-30, DEC-101)

```
RequiredDepositAmount = RequiredDepositPercentage is not null
                        ? Round(NetAmountDue × RequiredDepositPercentage / 100)
                        : null
EffectiveDeposit      = RequiredDepositAmount ?? the operator's stated DepositAmount ?? 0
```

The required percentage is **configuration** (`Bookings:Deposit:RequiredPercentage`) and is **absent by default**. When it is absent the rail reads *"Required deposit: not stated"* and the operator states the deposit. The reference's `30` is never hardcoded, and DEC-043's ruling — *"no deposit percentage is assumed or implied"* — is preserved: an absent configuration means **no** percentage is implied.

A stored deposit is **advisory**: it records what was agreed. It is **not** a second payment and never changes the balance — the balance comes from the payment sum alone (BR-03, BR-10). The deposit is written through the existing `Booking.SetDepositAmount` domain method, which rejects a negative amount.

### 3.10 The quote (BR-31)

`POST /api/bookings/quote` publishes the whole financial picture of a **proposed** booking and **persists nothing** (the BR-20 "a preview is a what-if" principle, applied to the booking).

The quote publishes: the frozen base amount, the surcharge and its rate, the discount and its amount, the gross amount, the net amount due, the required deposit percentage and amount, the effective deposit, the stated initial payment, the outstanding balance, the near-term commissary load against the configured capacity, the tier's standard-run food cost and base gross profit percentage (a **planning estimate**, BR-19), the municipality coverage verdict, and the five pre-dispatch checks.

### 3.11 The pre-dispatch checks (BR-33)

Five named, server-derived checks. **Four are advisory** — they warn and never block. **One blocks**: a deactivated tier is not offered, so a booking cannot be created on one.

| Check | Advisory | Condition |
| :--- | :--- | :--- |
| `ClientValidated` | advisory | The customer exists and is not soft-deleted. |
| `TierActive` | **blocking** | The tier is active. |
| `CoursesSpecified` | advisory | The tier has at least one course rule configured. |
| `MunicipalityCovered` | advisory | The chosen municipality is in the tier's coverage matrix (`null` when the tier has no coverage set — unknown, not a breach). |
| `DepositMatched` | advisory | The effective deposit covers the required deposit (`null` when no required deposit is configured). |

The headcount floor (`Package.MinPax`) is reported as an **advisory note** beside the quote, not as a check and not as a block (BR-16 makes it a floor on the contract, not a charge).

### 3.12 Validation (BR-11)

| Field | Rule |
| :--- | :--- |
| `customerId` | `> 0`; the customer must exist. |
| `noOfPerson` | `> 0`. |
| `packageId` | `> 0` when present; the tier must exist **and be active**. |
| `occasion` | required, ≤ 150 characters. |
| `venue` | required, ≤ 200 characters. |
| `eventDate` / `startTime` / `endTime` | required; the end must not be before the start. |
| `callTime` / `packoutTime` | optional; when present they must satisfy the **dispatch ordering** (BR-34): `callTime <= EventStartTime` and `packoutTime >= EventEndTime`. The check is unambiguous because the model forbids an overnight event outright. |
| `extendedAreaId` | `> 0` when present; the municipality must exist and be live. |
| `applyExtendedAmount` | requires `extendedAreaId`. |
| `discountCode` | optional; when present it must resolve to **exactly one active** discount, otherwise `400` (unknown, or ambiguous - `disc_code` has no unique index). |
| `depositAmount` | `>= 0` when present. |
| `initialPayment.amountPaid` | `> 0` when an initial payment is present; the amount must not exceed the net amount due (BR-05's overpayment prohibition). |
| `initialPayment.paymentMeans` | one of the `PaymentMeans` **enum names** — `Cash`, `Check`, `GCash`, `LDDAP`, `CreditCard` — because the enum travels by name (contract §8). The client's labels are *"Cash"*, *"Check"*, *"GCash"*, *"Bank Transfer / LDDAP"* and *"Credit Card"*; the **wire value is the enum name**. The `pay_means` column is mapped with `HasConversion<string>()`, so RSK-016's legacy-value crash is a **pre-existing** defect: the **new** write path stores the enum name and cannot produce one, but the register **loads payments for every card** and therefore inherits RSK-016's blast radius (RSK-055). |

Every rejection is `400 { "errors": [ ... ] }` (BR-11) and the error text is the **server's own**, so the operator reads one message whether the browser or the API refuses.

---

## 4. API Surface (contract §14)

All paths are relative to the API root. Every endpoint requires a bearer token (BR-12).

| # | Method & path | Purpose |
| :--- | :--- | :--- |
| 14.1 | `GET /api/bookings` | The register — the paged, filtered dossier plus the totals and the facets. |
| 14.2 | `GET /api/bookings/summary` | The four KPI cards, the payment-state distribution and the capacity load. |
| 14.3 | `POST /api/bookings/quote` | The server-computed quote for the workbench. Persists nothing. |
| 14.4 | `POST /api/bookings` | The extended create — a draft or a confirmed booking, with an optional initial payment. |
| 14.5 | `GET /api/bookings/{id}/contract` | The QuestPDF catering contract for one booking. |
| 14.6 | `GET /api/bookings/export` | The ClosedXML export of the **current filtered** register. |

The existing `GET /api/bookings/{id}`, `GET /api/bookings/{id}/ledger` and `POST /api/bookings/{id}/cancel` are **unchanged**.

**The shape follows the two delivered precedents**: the register copies the **Customer Master directory**'s envelope (contract §10.1 — `summary`/`quickViewCounts`/`items`/`page`/`pageSize`/`totalCount`/`totalPages`/`generatedAt`), and the KPI aggregate copies the **Packages catalogue summary** (contract §12.2, DEC-068 — the server computes the aggregate and the browser never derives one).

### 4.0 `GET /api/bookings/{id}` — the booking details (extended)

The existing details endpoint is **extended** with the four frozen members this cycle introduced, so it describes a booking the same way the register does:

| Field | Meaning |
| :--- | :--- |
| `callTime` | The dispatch schedule's call time (BR-34, DEC-106). `null` = not stated. |
| `packoutTime` | The dispatch schedule's pack-out time (BR-34, DEC-106). `null` = not stated. |
| `extendedAreaAmount` | The **frozen** out-of-town surcharge (BR-26, DEC-116) — the amount the booking was created with, **never** the municipality's current rate. `0` when no surcharge was applied. |
| `extendedAreaRatePerGuest` | The **frozen** surcharge ÷ the headcount, so the printed contract can show the **rate** without publishing a rate that may have moved. **`null` when the division does not reconstruct the frozen amount exactly at two decimals**, and `null` when no surcharge was applied (DEC-122). |
| `discountCode` | The **frozen** discount code (DEC-106, BR-02). `null` = no discount. |

**The details endpoint and the register describe the same booking the same way** — the net amount due, the collected amount, the outstanding balance, the surcharge, the deposit and the status all agree, because both projections read the **same** frozen columns through the **same** calculator overload and the **same** payment population. A unit test and an integration test pin that parity.

### 4.1 `GET /api/bookings` — the register

**Query** — §3.3.

**Response `BookingRegisterPageDto`**

```
items          : BookingRegisterItemDto[]     ← the page
totalCount     : int                          ← the filtered population
page           : int
pageSize       : int
totalPages     : int
summary        : BookingRegisterTotalsDto     ← the totals strip
quickViewCounts: BookingRegisterFacetsDto     ← the facet counts
generatedAt    : DateTime
```

**`BookingRegisterTotalsDto`** — the totals strip. Every figure is over the **whole filtered population**, not the page.

```
bookingCount, netContractedValue, collectedAmount, outstandingAmount, totalPax
```

**`BookingRegisterFacetsDto`**

```
packageTypes : [{ key, count }]
venues       : [{ key, count }]
statuses     : [{ key, count }]
paymentStates: [{ key, count }]
```

**`BookingRegisterItemDto`** — one dossier card.

```
transactionId, bookingNumber,
eventDate, eventStartTime, eventEndTime, callTime, packoutTime,
status, paymentState, isOverdue, daysUntilEvent,
customerId, customerName, customerCompany, customerContact,
occasion, venue,
packageId, packageName, packageType,
numberOfHeads, pricePerHead, packageAmount,
netAmountDue, amountPaid, outstandingBalance,
extendedAreaId, extendedAreaName, extendedAreaAmount, applyExtendedAmount,
depositAmount, discountCode, discountAmount,
baseGrossProfitPercentage   ← nullable; the tier's planning estimate (DEC-103)
```

### 4.2 `GET /api/bookings/summary` — the KPI cards

Same query as §4.1.

**Response `BookingRegisterSummaryDto`**

```
pipeline : { bookingCount, confirmedCount, tentativeCount, completedCount, otherCount, committedPax }
leadingTier : { packageType, packageName, netContractedValue, sharePercentage,
                changePercentage, previousNetContractedValue }   ← nullable except the counts
contractValue : { netContractedValue, collectedAmount, outstandingAmount, collectionRatioPercentage }
headcount : { totalPax, averagePaxPerBooking, nextEventDate, nextEventBookingNumber }
paymentStates : [{ key, count, amount }]
capacity : { committedPax, capacityPax, utilisationPercentage, isConfigured }
receivables : { outstandingAmount, bookingCount, overdueCount, overdueAmount }
generatedAt : DateTime
```

`leadingTier` is `null` when the population is empty. `changePercentage` is `null` when the preceding period has no bookings (unknown, never zero). `capacityPax` is `null` when no capacity is configured.

**The population.** One population serves the list, the totals and the summary: every **non-soft-deleted** booking whose status is **not** `Cancelled`. `Expired` bookings stay in it, because DEC-025 keeps them in AR — RSK-017 records that as an open question and the register must not silently diverge from the dashboard.

### 4.3 `POST /api/bookings/quote` — the workbench quote

**Request `BookingQuoteRequestDto`**

```
customerId, packageId, noOfPerson, occasion, venue,
eventDate, startTime, endTime, callTime?, packoutTime?,
extendedAreaId?, applyExtendedAmount,
discountCode?, depositAmount?, initialPaymentAmount?
```

**Response `BookingQuoteDto`**

```
packageName, packageType, minPax, isTierActive,
pricePerHead, baseAmount,
extendedAreaId, extendedAreaName, extendedAreaRate, extendedAreaAmount,
discountCode, discountType, discountPercentage, discountAmount,
grossAmount, netAmountDue,
requiredDepositPercentage, requiredDepositAmount, depositAmount,
initialPaymentAmount, outstandingBalance,
headcountFloorNote,
capacity : { committedPax, capacityPax, utilisationPercentage, isConfigured },
standardRunFoodCostPerPax, baseGrossProfitPercentage,   ← nullable planning estimates
coverage : { isCovered, coveredAreaCount },
checks : [{ key, label, isSatisfied, isBlocking, detail }]
```

### 4.4 `POST /api/bookings` — the extended create

**Request `CreateBookingRequestDto`** — the existing fields plus:

```
callTime?, packoutTime?, discountCode?, depositAmount?,
confirm : bool                        ← false = draft (Pending), true = confirmed
initialPayment? : { amountPaid, paymentMeans, checkNo?, notes?, particular? }
```

**Response `CreateBookingResponseDto`** — `{ transactionId, bookingNumber, status }`.

The **confirm** path records the initial payment in the **same commit** as the booking, so a confirmed booking with a deposit is never a booking without its payment.

### 4.5 `GET /api/bookings/{id}/contract` — the contract PDF

`200` with `application/pdf` and a `Content-Disposition` filename. `404` when the booking does not exist.

The document renders the **shared calculator's** figures — the net amount due, the total payments and the remaining balance — and never the denormalized `Booking.AmountPaid` cache or the domain's `Booking.Balance` (DEC-113, closing RSK-023). A printed contract therefore reconciles with the ledger to the cent.

**The document itemises what it charges** (DEC-121, DEC-122):

- The **service-details table** carries the package line and — **only when the booking has one** — an **out-of-town surcharge** line showing the **rate**, the **headcount** and the **total**, so the reader can see how the charge was derived (BR-17, BR-26). A booking with no surcharge has **one** row and no `0.00` line (BR-20's null/zero discipline).
- The **totals block** carries the total charges, the **discount with its frozen code** (only when a discount was applied), the contract amount, the payments received and the remaining balance.
- The **dispatch schedule** (BR-34) is printed **once**, in the event's particulars, and **only when both** times are stated.
- A **per-guest surcharge rate** is published only when the frozen amount ÷ the headcount **reconstructs the frozen amount exactly** at two decimals; otherwise the rate is `null` and the document prints the **amount and the headcount without a rate** (DEC-122). A rate that does not tie out is never printed on a signed document.

**The contract adds; it never rewrites.** The particulars block reads: **Event Date** · **Occasion** · **Venue** · **Serving Time** · **`Guaranteed Pax: N Persons`**, plus the **Dispatch Schedule** when both times are stated. The Event Date, the Occasion and the Venue are the lines the pre-cycle contract printed and they are **unchanged**; the Serving Time and the `Guaranteed Pax: N Persons` lines are the **kitchen BEO's** wording (`KitchenBeoDocument`), adopted because it is the operation's own vocabulary and it now appears on **both** documents. The business term is *"Pax (Headcount)"*; the document's label is *"Guaranteed Pax"* (the glossary records both). The block is **pinned**: a booking with no surcharge, no discount and no dispatch schedule prints the same five lines, in the same order, with the same wording.

The **totals block** reads **Total Charges** (the gross), **Discount** with its frozen code, **Contract Amount** (the net - the shared calculator's figure), **Total Payments Received** and **Remaining Balance** (DEC-123). It does **not** carry the pre-cycle `Total Package Amount` line: that label was correct only while no surcharge could exist, and restoring it would print the **base** amount where a reader expects the **contract value**. The base amount is not lost - it is the service-details table's package row, with its rate and its pax.

### 4.6 `GET /api/bookings/export` — the register XLSX

The **same filters** as §4.1, applied to the **whole** population (not the page). `200` with `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

### 4.7 The error contract

`400 { "errors": [ ... ] }` for every validation and business-rule failure (BR-11); `404 { "errors": [ ... ] }` for a missing resource; `401`/`403` with no business detail. A rule violation is **never** an unhandled `500`.

---

## 5. UI Specification

### 5.1 Routes

| Route | Element | State |
| :--- | :--- | :--- |
| `/bookings` | `BookingsEventsPage` | **Delivered** |
| `/bookings/new` | `BookingWorkbenchPage` | **Delivered** |

The navigation entry **Bookings & Events** becomes `implemented: true`. Every other route is unchanged.

### 5.2 Screen A — the Bookings & Events Register (`/bookings`)

**Order of the page**, following the reference:

1. **The header row** — the title, the subtitle, and the three actions: **Export (XLSX)**, **Batch BEOs (inactive — capability gap)**, **New booking**.
2. **The four KPI cards** — Active Pipeline, Leading Tier, Contract Value, Committed Headcount. Each carries the server's figure and an optional progress track.
3. **The command bar** — the **tier pills** (the facet counts), the **grid / timeline** toggle, the **search**, and the four selects (**status**, **date range**, **venue**, **payment state**), plus the **overdue toggle chip** beside them.
   - The **Payment** dropdown offers *All payment states*, *Fully paid* (`Settled`), *Partially paid* (`PartiallyPaid`) and *Awaiting initial deposit* (`AwaitingDeposit`) — the **three** BR-28 states. It does **not** offer a percentage-based option: the required deposit percentage is configuration and absent by default (DEC-101), so a "30% downpayment" filter would assert a house rule that does not exist.
   - The **overdue toggle chip** (*Overdue only*) is the separate `overdue` filter (§3.4). It is clearly labelled as a dimension of its own, not as a fourth payment state.
   - The **timeline** view is a **server-side sort**: the toggle sets `sort=EventDateAsc` and the page renders as a chronological rail. It is therefore the **same server page**, in the server's own order — never a local re-ordering of one page that would misrepresent the population as a chronology. The paging dock and the totals strip are unchanged.
4. **The dossier grid** — one card per booking, three columns on a wide screen, one on a narrow one.
5. **The totals strip and the pagination dock** — the totals, the "showing *x* to *y* of *n*", the page size and the page buttons.
6. **The three-card operational dock** — **Commissary load**, **BEO & event orders (capability gap)**, **AR & SOA telemetry**.

**The dossier card** carries: the booking number and its status chip; the countdown line ("In 3 days • 29 Mar 2026 • 17:00"); the customer and the contact; the tier block (name, description, pax, rate per head); the venue line; the contract-value block (value, the progress track, the paid and the outstanding figures); the footer strip (the BEO chip — a capability gap — and the planning-estimate margin).

**The countdown is the server's.** The card reads `daysUntilEvent`, an integer the API computes from `IDateTime.UtcNow` and the booking's event date — **never** from the browser's clock. A `null` or a negative value means the event is not in the future, and the card shows the event date alone. The date and the time are then formatted by `utils/dateFormatter.ts`.

**States.** Loading → a skeleton grid. Empty (no bookings at all) → an empty state with the create action. Empty (filtered) → an empty state naming the filters. Error → an alert banner with a retry. A **stale** response keeps the previous page on screen and marks the refresh (the `keepPreviousData` pattern the packages screen already uses).

**No figure is derived in the browser.** The progress ratios are computed from figures the server published (a ratio between two published numbers is a display bound, not a business figure — the `progressRatio.ts` precedent).

### 5.3 Screen B — the Reservation Workbench (`/bookings/new`)

**The header block** (inside the application shell): the title, the live **booking number** placeholder (`assigned on save`), the **step indicator** (4 steps: *Client & event*, *Package & courses*, *Financials*, *Deposit*), the **Save draft** action and the **Confirm booking** action.

**Column 1 — the client & logistics rail.** The client card (name, company, classification chip, contact, address, the municipality picker), the **dispatch schedule matrix** (Call time / Program / Packout), the **venue & spatial logistics** block (the municipality, its distance, its logistics note, the surcharge line, the per-event logistics cost) and the **included site amenities** capability-gap notice.

**Column 2 — the package tier & course engine.** The tier block (the code, the name, the description, the headcount floor, the **headcount**, the **rate per head**, the **base value**), the **approved course specification** (the tier's course rules with their quotas, portions, pool sizes and average food costs), and the **chef batching prep yield** capability-gap notice.

**Column 3 — the financial & seal rail.** The **capacity gauge**, the **contract & ledger rail** (base / surcharge / discount / gross / net / required deposit / balance), the **logged instrument** (the initial payment's means, check number and amount), the **pre-dispatch validation checklist**, and the two actions.

**The live ledger is the quote endpoint's output.** The workbench calls `POST /api/bookings/quote` whenever an input that affects the money changes, and the rail shows the returned figures. While the quote is in flight the rail shows an explicit **recalculating** state — never a figure priced for a different headcount, and never a zero in its place (the FE-2 correction the packages cycle made).

**The 3-column grid** collapses to a single column below the `lg` breakpoint.

**States.** No tier selected → the course specification and the ledger show their empty states. No customer selected → the client rail shows its empty state. A quote error → the rail shows the error and the **Confirm** action is inactive.

### 5.4 The empty, loading and error states

Every panel states its own emptiness. A panel that can legitimately hold no records renders the shared `EmptyState`; a panel whose data the model does not carry renders the shared `CapabilityGapNotice` with the **requirement id** and a **footnote** naming the decision. Nothing is fabricated, and no disabled form stands in for a missing model.

### 5.5 Accessibility and presentation rules

- Every status, tier and payment state is carried by a **text label plus an icon**, never by colour alone (WCAG 1.4.1).
- Every progress track exposes `role="progressbar"` with a label (the shared `ProgressTrack` already does).
- Every form control has an explicit label; the validation message is tied to the field.
- Currency is `₱` with two decimals through `utils/currencyFormatter.ts`; a `null` figure is an **em dash**, never `0` and never `₱0.00`.
- Dates are formatted through `utils/dateFormatter.ts` — never `toLocaleDateString()`.
- The layout is verified at 375 / 768 / 1024 / 1440 px with **no horizontal overflow**.
- A feature folder carries a `noHardcodedFigures` test that fails on a peso sign, a thousands separator, a decimal literal, a four-digit literal or a percentage literal (the packages precedent).

---

## 6. KPI Impact

| KPI | Name | Formula | Source |
| :--- | :--- | :--- | :--- |
| **KPI-33** | Register pipeline count and status split | The count of the filtered population, split into confirmed (`Confirmed`/`DepositPaid`/`FullyPaid`), tentative (`Pending`), completed (`Completed`) and other. | The register query |
| **KPI-34** | Leading tier, its share and its period change | The tier with the highest net contracted value; its share of the population's net contracted value; the change of that tier's net contracted value against the **immediately preceding equal-length period**. | The summary query |
| **KPI-35** | Net contracted value, collected and outstanding | `Σ NetAmountDue`, `Σ Collected`, `Σ Outstanding`. The two sub-figures sum to the headline. | The summary query |
| **KPI-36** | Committed headcount | `Σ NumberOfHeads` over the population, and the mean per booking. | The summary query |
| **KPI-37** | Payment-state distribution | The count and the outstanding amount per payment state (§3.4). | The summary query |
| **KPI-38** | Near-term commissary load | `Σ NumberOfHeads` of the bookings whose event date falls in the **next 14 days**, against the configured capacity. | The summary query + configuration |
| **KPI-39** | Register receivables and overdue | `Σ Outstanding`, the booking count, the overdue count and the overdue amount — the dashboard's KPI-12/KPI-13 over the register's population. | The summary query |

**Reconciliation.** With no filter applied, KPI-35's `outstandingAmount` and KPI-39's figures **equal the dashboard's KPI-12 and KPI-13 to the cent** — the register and the dashboard describe one population.

**Null semantics.** A `null` means **unknown**, never zero (BR-20). An unconfigured capacity, an absent tier, an empty population and an absent preceding period all publish `null`.

---

## 7. Acceptance Criteria

### 7.1 The register

- **AC-BKG-01** — The register lists the bookings of the filtered population, paged, and the totals strip reconciles with the page's own rows.
- **AC-BKG-02** — Every money figure on a dossier card comes from the API; the browser derives none.
- **AC-BKG-03** — The tier pills, the venue list, the status list and the payment-state list carry the **server's** facet counts, and each facet ignores its own dimension.
- **AC-BKG-04** — An invalid filter is rejected with `400 { errors }`; an unknown status name never widens the vocabulary silently.
- **AC-BKG-05** — With no filter, the register's outstanding total equals the dashboard's KPI-12 to the cent.
- **AC-BKG-06** — The register's collected and outstanding figures **sum** to its net contracted value.
- **AC-BKG-07** — A booking's status chip renders the **domain** vocabulary; the reference's `Finalized`/`Invoiced` never appear.
- **AC-BKG-08** — The booking number renders in the contractual `BK-YYYYMMDD-NNNNN` format.
- **AC-BKG-09** — The register has distinct empty states for *no bookings at all* and *no bookings matching the filters*, and the filtered one names the filters.
- **AC-BKG-10** — The register has no horizontal overflow at 375 / 768 / 1024 / 1440 px.
- **AC-BKG-11** — The register's export downloads the **whole filtered population**, not the page.
- **AC-BKG-12** — The "Batch BEOs" action is inactive and explains itself; no BEO queue is fabricated.
- **AC-BKG-13** — Every navigation entry still resolves to a real page; the register is reachable from the sidebar.

### 7.2 The workbench

- **AC-BKG-14** — The workbench prices a booking entirely on the server; the ledger rail's figures are the quote endpoint's.
- **AC-BKG-15** — Changing the headcount re-prices the rail, and while the quote is in flight the rail shows a **recalculating** state — never a stale figure and never a zero.
- **AC-BKG-16** — The out-of-town surcharge is `Area.AdditionalRate × pax`, applied only when the operator switches it on, and the stored amount is frozen (a later rate change does not alter the booking).
- **AC-BKG-17** — The required deposit is `null` and reads *"not stated"* when no percentage is configured; the `30` of the reference never appears.
- **AC-BKG-18** — The capacity gauge reads *"not stated"* when no capacity is configured; the `400` of the reference never appears.
- **AC-BKG-19** — A fixed discount takes precedence over a percentage, the percentage is charged on the gross amount, and the discount is capped at the gross amount.
- **AC-BKG-20** — A disabled tier cannot be booked; the quote reports the blocking check.
- **AC-BKG-21** — The municipality coverage verdict is `null` (unknown) when the tier has no coverage set, and it never blocks.
- **AC-BKG-22** — The course specification is the tier's **approved ruleset**, and the per-booking dish selection renders an explicit capability-gap notice.
- **AC-BKG-23** — The chef batching yield renders an explicit capability-gap notice naming REQ-MNU-03.
- **AC-BKG-24** — **Save draft** creates the booking in `Pending`; **Confirm booking** creates it in `Confirmed`. Neither introduces a draft entity.
- **AC-BKG-25** — A confirmed booking with an initial payment records the payment in the **same commit**; the booking's ledger shows the payment.
- **AC-BKG-26** — An initial payment larger than the net amount due is rejected with `400` (BR-05).
- **AC-BKG-27** — The call time and the pack-out time are stored with the booking and are shown on the schedule matrix.
- **AC-BKG-28** — The workbench's client, tier and municipality pickers read the existing catalogues; no picker fabricates a record.
- **AC-BKG-29** — The workbench has no horizontal overflow at 375 / 768 / 1024 / 1440 px.

### 7.3 The money chain

- **AC-BKG-33** — **The single net-due source (DEC-099).** For every booking, `Booking.AmountDue` (the stored snapshot) **equals** `BookingAmountDueCalculator.Calculate(booking, …).NetAmountDue` **to the cent, after the one rounding** — including a booking that carries **both** a surcharge and a discount, and one whose percentage discount needs the rounding (DEC-115). The pre-existing unit and integration suites stay **green unchanged**.
- **AC-BKG-43** — **One rounding (DEC-115).** Every published money figure is rounded to two decimals, away from the midpoint, **once**, in the Application layer; the browser never re-rounds. A percentage discount that produces a third decimal is rounded **before** it is stored and before it is applied.
- **AC-BKG-44** — **The surcharge is stored, not inferred (DEC-116).** The frozen surcharge is its own column; flipping `ApplyExtendedAmount` after the booking exists changes nothing; and a surcharge of zero is distinguishable from a surcharge that was never applied.
- **AC-BKG-45** — **The payment-state names never collide with the lifecycle names (DEC-117).** A payload that carries both a lifecycle status and a payment state publishes `DepositPaid`/`FullyPaid` only as lifecycle values and `AwaitingDeposit`/`PartiallyPaid`/`Settled` only as payment values.
- **AC-BKG-46** — **The overpayment guard (DEC-118).** A payment that would take the total above the net amount due is refused with `400`, and **nothing** is written — no payment row, no status change, no change to the cached `AmountPaid`. The guard applies to `POST /api/payments` and to the create path's initial payment.
- **AC-BKG-47** — **The soft-deleted-payment exclusion covers every money surface (DEC-111).** The ledger, the booking details, the customer directory and profile, the customer statement, the calendar feed, the legacy customer list and the register all exclude a soft-deleted payment from **both** the sum and the listed entries.
- **AC-BKG-34** — The register, the ledger, the booking details, the customer statement, the calendar feed and the dashboard all publish the **same** net amount due for the same booking.
- **AC-BKG-35** — **The unified soft-deleted-payment exclusion (DEC-111).** A soft-deleted payment is excluded from the ledger, the customer statement, the calendar and the register — exactly as the dashboard already excludes it — so the dashboard and the register cannot diverge.
- **AC-BKG-36** — A surcharge is charged **once**: the register's `netAmountDue`, the ledger's `contractAmount` and the stored `Booking.AmountDue` are the same figure for a booking with an extended area.
- **AC-BKG-37** — A later change to `Area.AdditionalRate`, to the tier's rate or to the discount catalogue does **not** alter a stored booking (BR-04).
- **AC-BKG-38** — The receipt number of the initial payment uses the **one** generator (DEC-112) and follows the `OR-YYYYMMDD-XXXX` format (BR-05).

### 7.4 Governance

- **AC-BKG-39** — The Application project references neither EF Core nor Identity, and no repository interface exposes a `DbSet`, an `IQueryable` or a `DbContext`.
- **AC-BKG-40** — The migration is **purely additive**: three nullable columns, no drop, no rename, no retype, no data change.
- **AC-BKG-41** — The workplan is realigned with the as-built state in the same cycle as the delivery.
- **AC-BKG-42** — **No business data is seeded** (BR-13): no booking, customer, tier, municipality, discount or payment is fabricated to make the screens look populated.
- **AC-BKG-48** — **The DTO projection guard.** Every public member of every read model of this feature — the booking details, the register item and the quote — is assigned by the projection that populates it, and a member added to a read model **without** a projection fails the guard. The defect class is "a declared member that no projection fills", which is a silent wrong answer; the guard is proved by execution, not by assertion.
- **AC-BKG-49** — **The printed contract itemises what it charges.** The service-details table shows the out-of-town surcharge with its rate, its headcount and its total when the booking has one, and has **no** surcharge row when it does not; the totals show the discount with its frozen code; the dispatch schedule is printed **once**, in the particulars, and only when both times are stated; the contract amount equals the shared calculator's net amount due; and **no** line reads `0.00` for a charge that was never applied.
- **AC-BKG-50** — **The contract adds; it never rewrites.** The Event Date, the Occasion and the Venue — the lines the pre-cycle contract printed — are unchanged; the particulars block is **pinned** as Event Date, Occasion, Venue, Serving Time and `Guaranteed Pax: N Persons`, plus the conditional dispatch schedule; and the totals block carries Total Charges, the discount with its frozen code, the Contract Amount, the payments received and the remaining balance (DEC-123).

---

## 8. Required Tests

### 8.1 Backend — unit

| Suite | Asserts |
| :--- | :--- |
| `BookingAmountDueCalculatorSnapshotTests` | The shared overload **reproduces** the frozen snapshot for a plain booking, a booking with a surcharge, a booking with a percentage discount, a booking with a fixed discount and a booking with both; the surcharge is charged **once**; the six-argument overload is unchanged; the **existing** pinned behaviour (a stored zero discount must not shadow the percentage rule) still holds; **the one rounding** is applied to the discount and to the net, and a third-decimal percentage discount produces an identical stored and computed figure. |
| `BookingRoundingTests` | `Round2` rounds away from the midpoint; the discount and the net are each rounded **once**; a percentage of `0.005` on a gross of `100.00` yields the same stored and computed net; the six-argument overload keeps its signature and its semantics. |
| `BookingOverpaymentGuardTests` | A payment above the net amount due is refused and **nothing** is written; the guard does not apply to a refund; a payment that takes the total **exactly** to the net amount due is accepted. |
| `BookingPaymentStateClassifierTests` | The three states are mutually exclusive and exhaustive; the **zero-value** booking and the **overpayment** are `Settled`; the event date is the due date (DEC-041); `isOverdue` is true only for an outstanding balance past the event date. |
| `GetBookingDossierQueryTests` | The filters; the facets ignore their own dimension; the totals are over the population, not the page; the soft-deleted exclusion; the soft-deleted-payment exclusion. |
| `GetBookingDossierSummaryQueryTests` | The KPI formulas; the leading tier; the preceding-period change; the `null` cases; the capacity `null`. |
| `PreviewBookingQuoteCommandTests` | The surcharge; the discount precedence and cap; the deposit; the balance; the checks; the `null` semantics; the quote persists nothing. |
| `CreateBookingCommandTests` | The draft/confirmed split; the frozen snapshot; the **stored** extended-area amount; the discount code (unknown, ambiguous, fixed, percentage); the deposit; the initial payment in the same commit; `Booking.AmountDue == calculator.NetAmountDue`; every validator rejection. |
| `BookingDossierExportTests` | The export's columns and the totals row. |

### 8.2 Backend — integration (against a disposable LocalDB database, DEC-019)

| Scenario | Asserts |
| :--- | :--- |
| The register reconciles with the dashboard | With no filter, `GET /api/bookings` and `GET /api/bookings/summary` and `GET /api/dashboard` agree on the outstanding total **to the cent**. |
| The register is filtered and paged | The filters, the facets, the page and the totals. |
| The quote persists nothing | `POST /api/bookings/quote` leaves the booking count unchanged. |
| A booking is created with a surcharge and a discount | The frozen columns; the ledger; the surcharge; the discount. |
| A draft and a confirmed booking | The two statuses; the initial payment's presence. |
| The contract PDF and the XLSX export | `200`, the content type and the byte length. |
| The migration applies cleanly | The migration chain against an empty database. |

### 8.3 Frontend

| Suite | Asserts |
| :--- | :--- |
| `BookingsEventsPage.test.tsx` | The cards render the server's figures; the filters drive the request; the two empty states; the error state; the totals strip; the pagination; the inactive BEO action. |
| `BookingWorkbenchPage.test.tsx` | The three columns; the quote drives the rail; the recalculating state; the draft/confirm actions; the capability-gap notices. |
| `noHardcodedFigures.test.ts` | No peso sign, thousands separator, decimal literal, four-digit literal or percentage literal in the feature folder. |
| `AppRoutes.test.tsx` | The register replaces the placeholder; every navigation entry still resolves. |

---

## 9. Open Questions for the Owner

None of these blocks the delivery; each is a **configuration** or a **later cycle**.

| # | Question | The default this cycle ships |
| :--- | :--- | :--- |
| 1 | What is the required deposit percentage? | **Not stated** — the operator states the deposit (DEC-099). |
| 2 | What is the commissary's maximum daily pax? | **Not stated** — the gauge reads "not stated" (DEC-100). |
| 3 | Should a booking below the tier's minimum headcount be refused? | **No** — it is an advisory note (BR-16). |
| 4 | Should a municipality outside the tier's coverage be refused? | **No** — it is an advisory check (BR-31). |
| 5 | Should add-ons be linked to a booking? | **Deferred** — it needs a join entity and a second migration (RSK-049). |
| 6 | Should the booking number be shown before the booking is saved? | **No** — the format embeds the surrogate id (BR-09); the workbench shows a placeholder until the save. |

---

## 10. Task Split

| Task | Agent | Scope |
| :--- | :--- | :--- |
| **TASK-BKG-01-BE** | AGENT 02 | The Domain extension and the migration; the Application use cases, DTOs, validators, the payment-state classifier, the repository contract; the Infrastructure repositories, the export and the EF configuration; the WebApi endpoints. |
| **TASK-BKG-01-FE** | AGENT 03 | The `src/features/bookings/` feature folder, the types, the endpoint group, the two routes, the navigation flag, the fixture and the tests. |
| **TASK-BKG-01-GV** | AGENT 01 | The governance record, the contract §14, the KPI and BR catalogues, the risk register, the traceability matrix, the acceptance scenarios, and the workplan realignment. |
