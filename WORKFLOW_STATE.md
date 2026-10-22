# Workflow State - REQ-BKG-05 Bookings & Events Register & Reservation Workbench

| Field | Value |
| :--- | :--- |
| **Cycle** | 2026-10-22 - Bookings & Events Register and Reservation Workbench (REQ-BKG-05, REQ-BKG-01) |
| **Specification** | `business/bookings-events-feature-spec.md` v1.0 |
| **Contract** | `architecture/approved-api-contracts.md` §14 |
| **Agent** | AGENT 01 (BI Architect) |
| **Stage** | 6 - Delegate |
| **Status** | **Approved for delivery as specification v1.1** — the Debater returned `PROPOSE_BETTER_PLAN` with eight blocking findings and seventeen clarifications; **all eight findings were applied** (see the disposition below) and the specification was reissued as v1.1. |

---

## 1. Verified Facts (Stage 1 - Inspect)

- `docs/salscatersuite_project_structure_workplan.md` was read in full and checked against a live file enumeration of the repository. The workplan is **accurate** for the state it describes.
- **The API has no booking list endpoint.** `BookingsController` exposes exactly four actions — `GET {id:int}`, `GET {id:int}/ledger`, `POST` and `POST {id:int}/cancel`. `IBookingRepository` has **no** paged or filtered list method. The dossier grid therefore needs a new endpoint, a new repository projection and a new use case.
- **`Booking.RecalculateFinancials` has never been called in production code.** Only tests call it. So every stored row has `Subtotal == PackageAmount`, `DiscountAmount == 0` and `TaxAmount == 0`.
- **`BookingAmountDueCalculator.Calculate(Booking, decimal)`** is the single shared overload, used by **eight** call sites: the payment ledger, the booking details, the customer statement, the customer repository (twice), the calendar feed, the dashboard and the unit-test fakes. Its six-argument overload is used by the unit tests only.
- **The out-of-town surcharge is not charged on any booking today.** The shared overload passes `extendedAreaRate: 0m`, and DEC-078 states the wiring is "raised as a separate, owner-approved task".
- **Only the dashboard excludes soft-deleted payments** (`DashboardService.cs:296`). The ledger, the customer statement, the customer repository and the calendar do not (RSK-024).
- **`ContractDocument` renders the denormalized `Booking.AmountPaid` and the domain's `Booking.Balance`** — a third money notion (RSK-023) — and no endpoint exposes it today.
- **No BEO entity, no BEO production state, no `Booking`↔`Menu` link, no `Booking`↔`AddonDetail` link.** `AddonDetail` and `Discount` exist as tables with no repository of their own.
- **There is no deposit-percentage rule.** DEC-009 is UNRESOLVED and DEC-043 ruled that *"no deposit percentage is assumed or implied"*.
- **There is no capacity ceiling.** BR-16 says outright that *"there is **no guest ceiling** in the model"*.
- `PaymentMeans` is `Cash | Check | GCash | LDDAP | CreditCard`; the `pay_means` column is mapped with `HasConversion<string>()`, so a legacy value outside those five names throws on read (RSK-016). The **new** write path stores the enum name and therefore cannot produce one, but the register loads payments for every card and so inherits RSK-016's blast radius (RSK-055).
- `src/features/bookings/` does not exist; `src/types/booking.types.ts` does not exist; `src/api/endpoints.ts` has no `bookings` group; `/bookings` resolves to `NotImplementedPage`; the nav item is already labelled **Bookings & Events** with `implemented: false`.
- `src/utils/bookingStatus.ts` already carries the **ten** `BookingStatus` values with their labels, tones and icons — it is reused, not rewritten.
- The backend build is **green**: `dotnet build src/backend/SalsCaterSuite.slnx` → **0 warnings / 0 errors**. (The build must be run with `-p:OutDir=<temp>` while the developer's dev API holds the `bin` DLLs — that is a local lock, not a defect.)
- **The cycle's baseline is measured, not assumed**: build **0 W / 0 E**; unit **700/700**; integration **118/118** against real SQL Server LocalDB; Vitest **281/281**. Those suites are the release gate and must stay green.
- `Discount.disc_code` carries **no unique index**, so a discount code is **not** unique in the model; the resolution is defined explicitly (unknown → `400`, ambiguous → `400`).
- `Payment.pay_means` is mapped with `HasConversion<string>()`, so RSK-016's legacy-value crash is inherited by the register (RSK-055).

## 2. Assumptions (explicitly not treated as facts)

- No assumption is made about a **deposit percentage**, a **capacity ceiling**, a **tax rate**, a **cancellation fee**, a **venue master** or a **chef sign-off workflow**. Each is either configuration or a named capability gap.
- No assumption is made about the **reference's figures** (`30%`, `400 pax`, `₱50/pax`, `58.2%`, `+8.4%`). Every one of them is a mockup number with no rule behind it; each is either a configuration key, a server-computed figure or a named gap.
- No assumption is made that the legacy `AddonDetail` or `Discount` tables hold usable rows. The catalogue is empty and BR-13 forbids seeding business data, so a discount code that does not resolve is a `400`.
- The **legacy data import** is not started (DEC-032) and this cycle does not depend on it.

## 3. Owner Rulings (Stage 4 - Approve, 2026-10-22)

Twenty-one rulings were required to make the references executable. Each is recorded in `governance/decision-log.md` as **DEC-099 .. DEC-119**. Ten of them close an **existing open question** rather than opening a new one, and five more were added by AGENT 01 in response to the Debater's plan review.

| # | Ruling | Decision | Closes |
| :--- | :--- | :--- | :--- |
| 1 | The frozen snapshot is the single net-due source; the calculator reproduces it | DEC-099 | **DEC-035**, RSK-002, RSK-009 |
| 2 | BR-01's out-of-town fee is wired onto the booking | DEC-100 | DEC-078's deferred task, RSK-036 (partly) |
| 3 | The required deposit percentage is configuration, absent by default | DEC-101 | extends **DEC-043**; DEC-009 stays open |
| 4 | The commissary capacity is configuration, absent by default | DEC-102 | — |
| 5 | The margin shown is the tier's planning estimate | DEC-103 | — |
| 6 | The BEO panels and the batch-BEO action are a capability gap | DEC-104 | — |
| 7 | The per-event dish selection is a capability gap | DEC-105 | REQ-BKG-02 (open) |
| 8 | `Booking` gains `CallTime`, `PackoutTime`, `DiscountCode` | DEC-106 | — |
| 9 | A draft is a `Pending` booking | DEC-107 | — |
| 10 | The package tier inclusions are a capability gap | DEC-108 | — |
| 11 | The leading tier and the collected/outstanding split | DEC-109 | — |
| 12 | One authenticated shell; the workbench renders inside `AppLayout` | DEC-110 | — |
| 13 | The soft-deleted-payment exclusion is unified | DEC-111 | **RSK-024** |
| 14 | One receipt-number generator | DEC-112 | narrows RSK-013 |
| 15 | The contract PDF is rendered through the one shared path | DEC-113 | **RSK-023** (narrowed) |

| 16 | The money chain carries **one** rounding - two decimals, away from the midpoint | DEC-115 | added by the Debater's F2 |
| 17 | The surcharge is a **stored** frozen column, not an inference | DEC-116 | added by the Debater's F4 |
| 18 | The payment states are renamed `AwaitingDeposit` / `PartiallyPaid` / `Settled` | DEC-117 | added by the Debater's F5 |
| 19 | The **structural** change is approved and the workplan is realigned in this cycle | DEC-114 | added by the Debater's F6 (AGENTS.md §0.1 rule 3) |
| 20 | BR-05's **overpayment prohibition** is enforced on the payment path | DEC-118 | added by the Debater's F1 (closes RSK-057) |
| 21 | The package tier code is not rendered - `Package` carries no code column | DEC-119 | added by the Debater's clarification |

**None of these required a new owner policy.** DEC-099, DEC-101, DEC-111, DEC-113, DEC-115, DEC-116, DEC-117 and DEC-118 **resolve** open questions in the direction the existing record already points; DEC-101 **extends** DEC-043 without weakening it; DEC-114 is the structural approval AGENTS.md §0.1 rule 3 requires of AGENT 01.

## 4. Deliverable Summary (Stage 3 - Specify)

| # | Deliverable | Layer |
| :--- | :--- | :--- |
| 1 | `Booking.CallTime`, `Booking.PackoutTime`, `Booking.DiscountCode`, `Booking.ExtendedAreaAmount` + one additive migration | Domain / Infrastructure |
| 2 | `BookingAmountDueCalculator` — the shared overload reproduces the frozen snapshot, with one rounding | Application |
| 3 | `BookingPaymentStateClassifier` — the one payment-state vocabulary | Application |
| 4 | `GetBookingDossierQuery` + `GetBookingDossierSummaryQuery` + their DTOs | Application |
| 5 | `PreviewBookingQuoteCommand` + its DTO and validator | Application |
| 6 | `CreateBookingCommand` — the draft/confirmed split, the surcharge, the discount, the deposit, the initial payment | Application |
| 7 | `IDiscountRepository` + its implementation; the paged/filtered `IBookingRepository` projection | Application / Infrastructure |
| 8 | `BookingDossierExport` (ClosedXML); the `ContractDocument` figures corrected | Infrastructure |
| 9 | The six endpoints of contract §14 | WebApi |
| 10 | `src/features/bookings/` — the register, the workbench and their components | Frontend |
| 11 | `src/types/booking.types.ts`, the `bookings` endpoint group, the two routes, the nav flag, the fixture | Frontend |
| 12 | BR-25..BR-33, KPI-33..KPI-39, DEC-099..DEC-119, RSK-048..RSK-057, contract §14, Scenarios 61-72 | Governance |

**No new project. No new root folder. No new top-level package. No new Domain entity and no new enum.** The only schema change is **three nullable columns** on `Booking`.

## 5. Decisions Recorded

DEC-099 (DEC-035 resolved), DEC-100 (the surcharge wired), DEC-101 (the deposit as configuration), DEC-102 (the capacity as configuration), DEC-103 (the planning-estimate margin), DEC-104 (the BEO gap), DEC-105 (the dish-selection gap), DEC-106 (the domain extension), DEC-107 (a draft is `Pending`), DEC-108 (the inclusions gap), DEC-109 (the leading tier and the split), DEC-110 (one shell), DEC-111 (the unified soft-deleted-payment exclusion), DEC-112 (one receipt generator), DEC-113 (the contract PDF path), **DEC-114 (the structural approval)**, **DEC-115 (one rounding)**, **DEC-116 (the stored surcharge)**, **DEC-117 (the payment-state names)**, **DEC-118 (the overpayment guard)**, **DEC-119 (the tier-code gap)**.

## 6. Constraints Carried Into Delegation

- The **Application** project may not reference EF Core or Identity (DEC-037); every repository interface is expressed in Domain entities and Application DTOs.
- The **shared calculator's argument set** (DEC-046) must not change: the surcharge is included by reading the frozen snapshot, not by adding a parameter.
- The **existing 700 unit and 118 integration tests must stay green unchanged**. The calculator change is behaviour-preserving for every existing row, and the existing pinned behaviour (a stored zero discount must not shadow the percentage rule) must still hold.
- A rule that appears in more than one place lives in **one** class: the net-due path, the payment-state vocabulary, the receipt-number generator.
- A `null` means **unknown**, never zero (BR-20).
- The browser never derives an authoritative figure (DEC-002); a progress-track ratio between two published numbers is the only permitted client-side computation.
- The migration is **purely additive** and is **generated and script-reviewed only** — never applied to an existing or production database (DEC-020).
- **No business data is seeded** (BR-13).
- The workplan must be realigned **in this cycle** (§0.4).

## 7. Acceptance Criteria

AC-BKG-01 .. AC-BKG-47 in `business/bookings-events-feature-spec.md` §7.
Acceptance Scenarios 61-72 in `implementation/acceptance-test-scenarios.md`.

## 8. Risks

RSK-048 (add-ons still unlinked), RSK-049 (no per-event dish selection), RSK-050 (`Expired` bookings are in the register's AR), RSK-051 (the snapshot equality is maintained by the create path, not by a constraint), RSK-052 (the deposit and the capacity are configuration), RSK-053 (the register can drift when a filter changes), RSK-054 (the per-tier planning estimate is computed on every read) — in `governance/risk-register.md`.

---

## Debater Review — REQ-BKG-05 Bookings & Events Register & Reservation Workbench (2026-10-22)

**Decision: PROPOSE_BETTER_PLAN**

**Rationale:** The screen design, the money-chain intent and the governance sweep are strong, and most of the artefact set is deliverable as written. But the payment-state classifier in the specification contradicts the very rule it claims to implement (`BR-28`), and Scenario 65 — the acceptance scenario written for it — fails on its first assertion; the net-due chain has no rounding rule, so the central invariant `Booking.AmountDue == calculator.NetAmountDue` (AC-BKG-33) is not guaranteed; and the surcharge is *inferred* from `Subtotal − PackageAmount` rather than stored, which is the one place the money chain can silently diverge. Each of these is cheap to fix now and expensive to fix after the migration lands.

---

### What is sound — must not change

- **One calculator, one net-due path.** `BookingAmountDueCalculator.Calculate(Booking, decimal)` is the right place for it, and the decision to make the calculator *reproduce* the frozen snapshot rather than recompute a parallel total is correct. `BookingAmountDueCalculator.cs:97-111` is the single shared overload; all five pinned tests in `BookingAmountDueCalculatorSharedOverloadTests.cs:24-83` still pass under the specification's §3.5 formula — I traced each one (frozen `PackageAmount`; stored percentage with a zero stored amount; fixed-over-percentage precedence after `RecalculateFinancials`; the balance clamp; and `ApplyExtendedAmount = true` with `Subtotal == PackageAmount` yielding `ExtendedAreaAmount == 0`). The six-argument overload's tests (`BookingAmountDueCalculatorTests.cs:16-193`) are untouched.
- **The behaviour-preservation claim is true.** `RecalculateFinancials` is called only from three test files (`DashboardHarness.cs:129`, `CustomerQueryTests.cs:242`, `BookingAmountDueCalculatorSharedOverloadTests.cs:53`) and from no production code, so every stored row has `Subtotal == PackageAmount`, `DiscountAmount == 0`, `TaxAmount == 0` — the recovery is `0` and the calculator's output is bit-identical for every existing row. The six-argument overload's semantics and the `discountFixedAmount: booking.DiscountAmount > 0 ? … : null` normalisation (`BookingAmountDueCalculator.cs:108`) must be preserved verbatim; the pinned "a stored zero discount must not shadow the percentage rule" behaviour is correct and must survive.
- **The unfiltered register genuinely reconciles with the dashboard's KPI-12.** `BookingRepository.cs:93-105` (`!IsDeleted && Status != Cancelled`) and `DashboardService.cs:126-143` are the same population, the same calculator and the same `!p.IsDeleted` payment exclusion (`DashboardService.cs:295-296`) — once DEC-111 is applied to the register, `summary.outstandingAmount == receivables.outstandingAmount` to the cent. AC-BKG-05 and Scenario 61 are correct as written.
- **The "one money chain" discipline is right and should be protected**: `NetContractedValue = Collected + Outstanding` by construction (BR-27), `null` never rendered as `0` (BR-20), the browser deriving no authoritative figure (DEC-002) with `progressRatio.ts`'s `ratioPercentage` (`progressRatio.ts:32-40`) as the only permitted client-side arithmetic, and `noHardcodedFigures` as a feature test.
- **The capability-gap discipline is exemplary** and must not be diluted: BEO panels, per-event dish selection, tier inclusions, kitchen prep yields and the batch-BEO action are named gaps rendered with the shared `CapabilityGapNotice` (which already carries `requirement` and `footnote` — `CapabilityGapNotice.tsx:5-26`) and the shared `Button.inactive` (`Button.tsx:18`), never fabricated values.
- **One shell, one receipt generator, one classifier class, one soft-delete rule** (DEC-110, DEC-112, DEC-111) are the right calls, and the migration is purely additive with no business data seeded (BR-13).

---

### Blocking findings

**F1 — The payment-state vocabulary contradicts BR-28, Scenario 65 and KPI-37; two of the five states are unreachable, one case matches no state, and the overdue dimension BR-28 mandates is dropped.**
*Claim.* Spec §3.4 (`business/bookings-events-feature-spec.md:141-153`) defines a **five**-state vocabulary in which "`Overdue` and `NotYetDue` **supersede** the payment states … A booking with **no** balance is `AwaitingDeposit`, `DepositPaid` or `FullyPaid`", and asserts "The five states are **mutually exclusive and exhaustive**". Contract §14.1/§14.2 publish that vocabulary.
*Evidence.* `business/business-rules.md:356-373` (BR-28) defines a **three**-state vocabulary and says the opposite in terms: "**Overdue is a separate dimension, not a payment state** … a partly-paid booking is `DepositPaid` **and** overdue, and folding the two into one vocabulary would make the states overlap"; it also mandates "`FullyPaid` deliberately covers two edge cases … a **zero-value** booking (`NetAmountDue == 0`, nothing collected) and an **overpayment** (`Collected > NetAmountDue`)" and "The register therefore filters on **payment state** (`AwaitingDeposit`, `DepositPaid`, `FullyPaid`) **and on overdue separately**". `implementation/acceptance-test-scenarios.md:1062-1078` (Scenario 65) asserts a no-payment booking whose date has passed "is AwaitingDeposit **and it is overdue**" and that "the **three** state counts sum to the population's booking count". `business/kpi-catalog.md:434` (KPI-37) says "each of the **five** states defined by **BR-28**" — BR-28 defines three.
*Consequence.* Under the specification's precedence: (a) `DepositPaid` is **unreachable** — `0 < Collected < NetAmountDue` implies `Balance > 0`, which is always Overdue or NotYetDue; (b) `AwaitingDeposit` is reachable only when `NetAmountDue == 0`, because `Collected == 0` with `Net > 0` also implies `Balance > 0`; (c) `NetAmountDue == 0` with `Collected > 0` matches **no** state at all — `FullyPaid` requires `Net > 0` and the balance is `max(0, 0 − Collected) = 0` — so the vocabulary is **not** exhaustive, and such a row is reachable today because `POST /api/payments` has no overpayment guard (`PaymentLedgerService.cs:34-80` → `Booking.RecordPayment`, `Booking.cs:200-209`, rejects only `amount <= 0` and a cancelled booking). The register's payment-state facet would therefore be empty for the two states the reference's filter offers, the `paymentState` filter would silently drop rows, the facet counts would not sum to the population, and Scenario 65 fails on its first, fifth and sixth assertions. KPI-37's claim that the distribution is "the register-scoped counterpart of the dashboard's KPI-18 'bookings awaiting deposit'" is false under the specification's design (`DashboardService.cs:181-188` counts **every** unpaid booking as awaiting deposit) and true under BR-28's. BR-28's mandated `isOverdue` per-item dimension and its separate **overdue filter** appear nowhere in §3.3, §4.1 or §4.2.
*Required change.* Implement BR-28 as written: a **three**-state payment vocabulary (`AwaitingDeposit` = `Collected == 0 and NetAmountDue > 0`; `DepositPaid` = `Collected > 0 and Collected < NetAmountDue`; `FullyPaid` = `Collected >= NetAmountDue`, which covers both the zero-value and the overpaid cases) **plus a separate `isOverdue` dimension** published on `BookingRegisterItemDto`; add an `overdue` filter to §3.3 and to §14.1's query parameters; keep the overdue count/amount aggregates fed by that dimension; correct KPI-37 to "three states" and correct the spec §3.4 table and contract §14.1/§14.2 to match. If AGENT 01 instead prefers the five-state design, it must **first** amend BR-28 and Scenario 65 and state the precedence that makes all five reachable and exhaustive (including the overpayment case) — the current text does not.

**F2 — The net-due chain has no rounding rule, so AC-BKG-33 and every "to the cent" reconciliation are not guaranteed.**
*Claim.* Spec §3.5: "The create path (§3.7) **stores exactly these values**, so `Booking.AmountDue == calculator.NetAmountDue` for **every booking**."
*Evidence.* `BookingConfiguration.cs:75-84` stores `Subtotal`, `DiscountAmount`, `TaxAmount`, `AmountDue` as `decimal(18,2)` but `DiscountPercentage` as `decimal(18,4)`, so `GrossAmount × DiscountPercentage / 100` carries up to four decimals; `Booking.RecalculateFinancials` (`Booking.cs:187-197`) does raw decimal arithmetic with **no** rounding, and `Math.Min(discountAmount, Subtotal)` caps the *stored* amount, not the argument. The specification's formula (`bookings-events-feature-spec.md:161-179`) has no rounding term either — BR-30 rounds its deposit, the net-due chain does not.
*Consequence.* The two roundings are not associative, so the equality can break by ₱0.01. Concrete case: `Gross = 100.00`, `DiscountPercentage = 0.005` ⇒ `d = 0.005`. The create path calls `RecalculateFinancials(0, 0, 0.005m, 0)` ⇒ in-memory `AmountDue = 99.995`; SQL Server stores `DiscountAmount = 0.01` and `AmountDue = 100.00`. The calculator then reads the **rounded stored** `DiscountAmount = 0.01` and returns `100.00 − 0.01 = 99.99`. Stored `AmountDue` (100.00) ≠ calculator `NetAmountDue` (99.99) — AC-BKG-33 fails, "reconcile to the cent" fails, and the DEC-035 divergence DEC-099 claims to close re-opens. The surcharge term is *not* at risk (`Area.AdditionalRate` is `decimal(18,2)` — `AreaConfiguration.cs:29` — so `rate × heads` is exact).
*Required change.* Mandate one money-rounding rule for the whole chain in BR-25 and §3.5: every monetary component (the percentage discount product, the gross, the tax, the surcharge) is rounded to two decimals with `MidpointRounding.AwayFromZero` — matching SQL Server's `decimal` rounding — **before** it enters the snapshot, and the calculator applies the identical rounding when it recomputes from `DiscountPercentage` (the branch that fires when `DiscountAmount == 0`). Add a snapshot test whose percentage product lands on a half-cent, and assert `Booking.AmountDue == calculator.NetAmountDue` for it.

**F3 — The soft-deleted-payment "unification" (DEC-111 / BR-27 / AC-BKG-35) does not cover every money surface, and one surface is still bound to the persisted `AmountDue` rather than the calculator.**
*Claim.* DEC-111 ("the exclusion is applied by **every query that reads the payments**"), BR-27 ("excluded from **every** money surface"), spec §3.6 and AC-BKG-35.
*Evidence.* The surfaces that read payments and are **not** excluded: `GetBookingDetailsQuery.cs:31` (`booking.Payments.Sum(p => p.AmountPaid)` → `BookingDto.AmountPaid`/`Balance`, the figures contract §1 line 50 requires to reconcile with the ledger); `CustomerRepository.cs:510-511` (`AmountPaid` → the customer directory's and profile's `totalPaid`/`outstandingBalance`); `CustomerRepository.cs:113-121` (`GetListProjectionAsync` computes `TotalReceivables` **in SQL** from the persisted `b.AmountDue` and `b.Payments!.Sum(...)` — it never calls the calculator, so it is a **ninth net-due path**, contradicting "exactly **one** net-due figure per booking" and contract §14.0 rule 1). The cycle's own Verified Facts (`WORKFLOW_STATE.md` §1, item 4) correctly name "the customer repository" as affected — the ruling's enumeration drops it. The three surfaces the specification *does* list (`PaymentLedgerService.cs:107-123`, `GetCustomerStatementQuery.cs:56`, `GetCalendarEventsQuery.cs:36`) are the only ones covered.
*Consequence.* After the cycle, the customer directory/profile AR and the legacy customer list's AR still include soft-deleted payments while the register and the dashboard exclude them, so the same booking's receivables disagree between two delivered screens; and the legacy list's AR remains bound to the stored snapshot, so with F2 open it can differ from the directory's AR on the **same screen**. The "unified" claim and contract §14.0 rule 1 are therefore false of the whole API.
*Required change.* Extend DEC-111, BR-27 and AC-BKG-35 to name **every** surface that reads payments — `GET /api/bookings/{id}`, the customer directory and profile roll-up, the legacy `GET /api/customers` list projection, the ledger, the statement, the calendar and the register — add them to the task brief and to the integration-test list, and convert `GetListProjectionAsync` to the calculator's path (as `RollUp` already does at `CustomerRepository.cs:344-357`) or state explicitly, with a test, that it is a second but *equivalent* path whose equivalence F2 makes exact.

**F4 — The surcharge is inferred (`Subtotal − PackageAmount`), not stored; the flag and the snapshot can drift and the chain breaks silently.**
*Claim.* Spec §3.7: "The stored surcharge is **recoverable from the frozen snapshot** as `Subtotal − PackageAmount` … The surcharge is therefore charged **exactly once**."
*Evidence.* `Booking.RecalculateFinancials` (`Booking.cs:193`) computes `Subtotal = PackageAmount + extendedAreaAmount + addonsAmount` — the surcharge and the add-on total are **not separable** in the stored snapshot, and `Booking` has no `ExtendedAreaAmount` column. `ApplyExtendedAmount` is a **public setter** (`Booking.cs:38`), so the flag and the stored `Subtotal` can drift apart. `Subtotal < PackageAmount` is masked by `max(0, …)` rather than surfaced.
*Consequence.* `ApplyExtendedAmount = false` with `Subtotal > PackageAmount` ⇒ the calculator's `GrossAmount = PackageAmount` while the stored `AmountDue` was computed on the larger `Subtotal` — a silent under-charge and a broken "one net-due source" claim. The day RSK-048's add-on link lands, an add-on total folded into `Subtotal` is either dropped (flag false) or reported as a surcharge (flag true). RSK-051 already concedes the equality is "an invariant maintained by the create path, not a database constraint"; the inference widens that fragility to the surcharge term.
*Required change.* Add a **fourth** nullable column to the same additive migration — `ExtendedAreaAmount decimal(18,2) NULL` — freeze the surcharge there (amend DEC-106 from three columns to four), and have the shared overload read `booking.ExtendedAreaAmount ?? max(0, booking.Subtotal − booking.PackageAmount)` (the fallback is exactly `0` for every pre-migration row, so no existing figure moves and DEC-046's argument set is untouched). Keep the `max(0, …)` guard but **assert** `Subtotal == PackageAmount + ExtendedAreaAmount` in the snapshot test so a drift is loud, not silent.

**F5 — Contract §14 contradicts the live contract: §1's create response and §9.2's "DEC-035 remains open" are not amended.**
*Claim.* Spec §4: "The existing `GET /api/bookings/{id}`, `GET /api/bookings/{id}/ledger` and `POST /api/bookings/{id}/cancel` are **unchanged**."
*Evidence.* `architecture/approved-api-contracts.md:52-82` documents `POST /api/bookings` as returning `CreateBookingResponseDto` = `{ "transactionId": 101 }` and says so explicitly ("The `201` body is the named `CreateBookingResponseDto` (`{ "transactionId": <id> }`)"); the as-built DTO has exactly one property (`CreateBookingResponseDto.cs:7-10`). Contract §14.4 changes that same DTO to `{ transactionId, bookingNumber, status }` — an additive JSON change, but **§1 is left stale** and the cycle does not list `POST /api/bookings` among the changed endpoints. Worse, contract §9.2 rule 1 (lines 411-415) still reads "**DEC-035 remains open** — the dashboard follows the ledger path and must not unilaterally ratify either source", while contract §14.0 rule 1 (line 1200) reads "DEC-035 **is resolved**". Both statements are live in the same approved document.
*Consequence.* Two contradictions inside the single source of truth for the API surface: a delegate reading §1 will implement or reject a different create response than §14.4 requires, and the dashboard's binding rule still forbids what DEC-099 authorises. Contract §5's lifecycle vocabulary (lines 189-200) also already uses `DepositPaid` and `FullyPaid` as **status** names, so §14.1's `status` and `paymentState` filters accept the same two strings with different meanings — a client cannot tell them apart by value.
*Required change.* In the same cycle, amend contract §1 (the create response and its `Location` behaviour) and contract §9.2 (rule 1: DEC-035 resolved, the shared overload now reproduces the snapshot; and rule 3: the soft-deleted-payment exclusion is now shared, not dashboard-scoped). Rename BR-28's states so they do not collide with the §5 lifecycle names (e.g. `PartPaid` / `Paid`) or record the overlap explicitly in §5 and §14.1.

**F6 — The cycle's structural additions carry no AGENT 01 decision entry.**
*Claim.* `WORKFLOW_STATE.md` §6: "The workplan must be realigned **in this cycle**"; AC-BKG-41; TASK-BKG-01-GV.
*Evidence.* `AGENTS.md` §0.1 rule 3: "No agent may add, remove or relocate a project, a top-level folder, a controller, **a repository**, **a frontend feature folder**, a shared component, **a route**, or a layer dependency on their own initiative. Such a change requires an AGENT 01 decision **recorded in `governance/decision-log.md`** **and** an update to the workplan in the same cycle." This cycle adds a **new repository** (`IDiscountRepository` + its implementation — the `Application/Common/Interfaces` folder has no such interface today), a **new frontend feature folder** (`src/features/bookings/`), **two routes** (`/bookings`, `/bookings/new`), a **new client endpoint group**, a **new export generator** (`BookingDossierExport`), a **new migration** and **two configuration keys** (`Bookings:Deposit:RequiredPercentage`, `Bookings:Capacity:MaximumPax`). DEC-099..DEC-113 are business rulings; none of them authorises a structural addition.
*Consequence.* The cycle would land the code and the workplan update without the decision-log entry the hierarchy requires — the precise anti-drift `AGENTS.md` §0.1 exists to prevent, and the alignment gate at stage 8 cannot verify it.
*Required change.* Add one decision (e.g. DEC-114) authorising this cycle's structural set and stating explicitly that **no new project, no new root folder, no new layer dependency, no new shared component and no new Domain entity or enum** are created; then realign the workplan sections §2.3 (the new use cases, the classifier, `IDiscountRepository`), §2.4 (the repository implementation, the dossier export, the EF configuration), §2.5 (the new `BookingsController` actions), §3.2 (the feature folder, `src/types/booking.types.ts`, the fixture, the new payment-state module), §3.3 (the two routes), §3.4 (the `bookings` endpoint group), §4 (the migration), §5 (the new spec file), §7.4 (the two configuration keys), §9 (the delivered-feature state) and the change log.

**F7 — Three of the cycle's "Verified Facts" are false, and two of them are the premise of a ruling and of a risk closure.**
*Claim.* `WORKFLOW_STATE.md` §1, items 22 and 26; DEC-113; RSK-002's closure.
*Evidence.* (a) Item 22: "`ContractDocument` renders the denormalized `Booking.AmountPaid` and the domain's `Booking.Balance` — a third money notion." **False.** `ContractDocument` renders the **DTO's** `AmountPaid` and `Balance` (`Infrastructure/Documents/ContractDocument.cs:82-83`), and `GetBookingDetailsQuery.cs:71-72` sets them from the **payment sum** and the **calculator's `RemainingBalance`** — not the denormalized cache and not the domain's `Booking.Balance`. RSK-023's own evidence text repeats the same error. (b) Item 26: "the column is stored as an **int** (`PaymentConfiguration`), so RSK-016's legacy-string crash does not apply". **False.** `PaymentConfiguration.cs:25-29` maps `PaymentMeans` with `.HasConversion<string>().HasMaxLength(50)` — `nvarchar(50)`, exactly as RSK-016 and this cycle's own **RSK-055** (`risk-register.md:58`) state. (c) RSK-002 is marked **CLOSED (DEC-099)** although its mitigation ("model relationships … query live line items when calculating ledger totals") was deliberately not implemented; the add-on term is still `0m` and no relationship was added.
*Consequence.* DEC-113's **substance** is still required — the document prints `Total Package Amount: PHP {PackageAmount}` (`ContractDocument.cs:81`) and never the net amount due, so a booking with a surcharge would print a contract total that disagrees with the ledger — but its **stated rationale** is wrong, and RSK-023's closure is recorded on a mis-description of the code. The WORKFLOW_STATE's item 26 is contradicted by the risk register written in the same cycle, and a delegate reading it will mis-plan the payment-means handling.
*Required change.* Correct items 22 and 26 in the Verified Facts; re-base DEC-113 on the real defect ("the document prints the frozen `PackageAmount` and the `BookingDto` figures instead of the calculator's `NetAmountDue`/`TotalPayments`/`RemainingBalance`; the domain's `Booking.Balance` remains a live third notion for any future consumer"); rewrite RSK-002's closure rationale to describe the frozen-snapshot approach (the add-on term remains `0m` — RSK-048) rather than the unimplemented mitigation; and correct RSK-023's evidence text to name the DTO figures, not the domain's.

**F8 — The register's compute strategy is unstated, the payment-state dimension cannot be evaluated in SQL, and the population is not bounded.**
*Claim.* Spec §4.1: "Every figure is over the **whole filtered population**, not the page"; BR-32: each facet ignores its own dimension; RSK-054: "the register is paged, so **the population per request is bounded**".
*Evidence.* The facade rule needs one population per dimension — the tier-ignored, venue-ignored, status-ignored and payment-state-ignored sets — plus the fully filtered set for the totals: up to **five** projections over "every non-soft-deleted booking" (spec §3.2). The `paymentState` dimension cannot be pushed into SQL at all: it depends on the payment sum and the calculator's `NetAmountDue`, which is C# code — so it must be evaluated in the Application layer over rows that have already had their payments loaded (the Customer Master's pattern, `CustomerRepository.cs:229-265`, loads its whole customer population into memory and derives everything in one pass, but it has **one** quick-view population, not five). RSK-054's mitigation rationale is simply wrong: the **page** is bounded, the **population** is not.
*Consequence.* The main screen of the product has an unbounded per-request cost that no artefact acknowledges. If AGENT 02 follows the customer precedent literally, the register loads every booking and every payment into memory on each request. The facet rule is implementable — but the plan does not own the decision.
*Required change.* State in the specification where the filtering, the totals and the four facets are computed (SQL `GROUP BY` projections for the scalar dimensions; the Application layer for the payment-state dimension, evaluated only over the rows that survive the SQL filters), bound the population that the payment-state evaluation loads, and correct RSK-054's rationale. Also state where the "All" pill's count comes from (see M9).

---

### Non-blocking clarifications

- **M1 — Wrong decision references in the specification's open-questions table.** §9 rows 1 and 2 attribute the deposit and the capacity to **DEC-099** and **DEC-100**; they are **DEC-101** and **DEC-102** (DEC-099 is the net-due resolution and DEC-100 the surcharge).
- **M2 — Wrong risk reference in the exclusions table.** Spec §1.4 cites **RSK-049** for the add-on exclusion; the add-on risk is **RSK-048** (`risk-register.md:51`), and RSK-049 is the per-event dish selection (contract §14.7 cites RSK-048 correctly).
- **M3 — "The tier code" has no source.** Spec §5.3's tier block names "the **code**"; `Package` carries `PackageId`, `PackageType`, `Description`, `AmountPax`, `MinPax`, `IsActive` and **no code** (`Package.cs:7-12`), while the reference shows a code chip (`Booking_Events_CreateNew/code.html:289`). Say what the code is (`PackageType`? `PackageId`?) or drop it.
- **M4 — "Address" and "municipality" must not be conflated.** `Customer.Address` is free text (`Customer.cs:11`) and there is no customer-municipality link; the reference's "Operating Towns" (`Booking_Events_CreateNew/code.html:199-202`) maps to the **`Area` municipality catalogue**, which the workbench also picks for the surcharge. Name the two sources explicitly in §5.3.
- **M5 — The "countdown line" is a browser-derived figure with no helper.** §5.2 requires "In 3 days • 29 Mar 2026 • 17:00"; `utils/dateFormatter.ts` has no relative-date helper (only `formatEventDate`, `formatEventDateTime`, `formatTimestamp`, `formatMonthYear`). Either publish the countdown/`daysUntilEvent` from the API or state that it is derived from the server's `generatedAt`, never from the browser clock — otherwise the countdown can disagree with the server's own `Overdue` verdict.
- **M6 — The payment-state chip has no vocabulary module.** `utils/bookingStatus.ts:37-48` covers the ten **lifecycle statuses** only; the payment states need their own labels/tones/icons. Declare the module (feature-local, like `packageTypes.ts`, or a new `utils/paymentState.ts`) in the deliverable list and in the workplan §3.2 update.
- **M7 — There is no shared `Select`/`Input`/`Modal`.** Workplan §3.2 line 606 lists `src/components/ui/{Input,Modal,Select,Table}.tsx` as **PLANNED — not yet implemented**; the delivered precedent is native `<input>`/`<select>` styled inline (`PackageFilterBar.tsx:50-101`). State in §5.2/§5.3 that the register's four selects, the search box, the workbench's pickers and its date/time fields use native elements to that precedent and that **no new shared component** is created — otherwise it is a structural change needing a decision (F6). The workbench's event date/start/end/call/packout inputs also need their **input type and wire format** stated (`yyyy-MM-dd`, `HH:mm:ss`), since the API takes those shapes.
- **M8 — `receivables.bookingCount` is undefined.** Contract §14.2 lists it; the dashboard's equivalent counts the bookings **with a balance** (`DashboardService.cs:132-134`), not the population. Say which the register publishes.
- **M9 — The "All" pill and the facet counts come from different populations.** With another filter active, the tier facet's population (tier filter removed) differs from the fully filtered population, so the tier counts will not sum to `summary.bookingCount`. Say which figure the "All" pill shows and whether each facet publishes its own total key; otherwise the design's "All Packages (28) / Wedding 8 / Premier 7 / …" row will visibly contradict the totals strip.
- **M10 — Bespoke bookings have no tier.** `PackageId` is optional (`Booking.cs:33`, and `CreateBookingCommand.cs:50-56` allows a null tier), so `packageType` is null for those rows: they are excluded from the tier facet and from KPI-34's leading tier. Define the facet key (e.g. a distinct "Bespoke" bucket) and state whether a tier-less booking is eligible to be the leading tier.
- **M11 — Tighten AC-BKG-33 and fix the new column's mutability.** The equality holds only for rows written by the create path — RSK-051 says so explicitly, and `Booking.DiscountPercentage`/`DiscountType` remain publicly settable, so a percentage-with-no-amount row (`DiscountPercentage > 0`, `DiscountAmount == 0`, `AmountDue = PackageAmount`) makes the calculator and the stored snapshot differ by the percentage. Scope AC-BKG-33 to create-path-produced bookings, and give the new `DiscountCode` column a **private** setter so the frozen code cannot be edited away from the frozen amount (BR-04).
- **M12 — The grid/timeline toggle contradicts the exclusions.** §1.4 excludes "a second view of the register", §5.2 ships a "grid / timeline" toggle, and the timeline "re-orders the same server page by event date ascending" — which is a no-op at the default sort and shows only the current page in any case. Drop the toggle, or state plainly that it is a presentation switch over one page and not a timeline of the population.
- **M13 — The operational dock's three actions are unspecified.** The reference's "View Kitchen Prep Sheets", "Trigger QuestPDF Engine" and "Review AR Ledger & SOAs" (`Booking_Events_Main/code.html:898-954`) appear in neither §5.2 nor §1.3/§1.4. Declare them inactive (`Button.inactive`, DEC-089) or omit them; a link to `/receivables` currently resolves to `NotImplementedPage`.
- **M14 — Name the catalogues the workbench reads.** AC-BKG-28 says the pickers "read the existing catalogues" but §4 lists no endpoint for them: the client picker reads `GET /api/customers` (the **legacy unpaged** list, RSK-019), the tier picker `GET /api/packages`, the municipality picker `GET /api/areas` and the course specification `GET /api/packages/{id}/course-rules` (contract §13.1). Name them so the client endpoint group and the §3.4 workplan update are complete.
- **M15 — One endpoint, two authorisation levels.** §2 makes `POST /api/bookings` "any authenticated user" but `initialPayment` requires `Payments.Record` (Administrator, Manager, Cashier). An action-level `[Authorize(Policy = …)]` would block the draft path; the policy must be evaluated **inside** the handler **only when `initialPayment` is present**, returning **403** (not 400). State the enforcement point.
- **M16 — The traceability matrix and the risk list are written in the delivered tense.** `governance/requirements-traceability.md:7` states for REQ-BKG-02 that the ledger "**excludes soft-deleted payments (DEC-111)**" and line 9 states REQ-BKG-04 is "**Delivered by this cycle**" — both are untrue of the as-built code at stage 5 (the ledger does not exclude them today: `PaymentLedgerService.cs:107-123`). The same row lists a frontend `BookingDetailPage` that does not exist in the workplan's route table (§3.3). And this cycle adds **RSK-055**, which the cycle's own risk summary (§8) omits. Fix before delegation so the traceability does not claim delivered behaviour.
- **M17 — The test-count claim is unverified.** "The existing 700 unit and 118 integration tests must stay green unchanged" — the as-built suites carry **604** `[Fact]`/`[Theory]` attributes (unit) and **116** (integration); the per-case counts depend on the `InlineData` expansion, so "700 / 118" is plausible but must be copied from an actual run (workplan §0.5). My trace says the suite **will** stay green: no existing ledger, statement, details or directory fixture contains a soft-deleted payment (the only one is the dashboard's, `DashboardServiceTests.cs:113`), so DEC-111's correction changes four endpoints' behaviour without breaking a test — which is also why a **new** fixture with a soft-deleted payment must be added to prove DEC-111.

---

### Coverage of the ten verification questions

| # | Question | Answer |
| :--- | :--- | :--- |
| 1 | Does §3.5 reproduce `RecalculateFinancials` for every input? | **No** — the arithmetic matches for every existing row and all five pinned tests, but the *rounding* is unspecified and the equality can break by ₱0.01 → **F2**. |
| 2 | Is the `Subtotal − PackageAmount` recovery sound? | Today yes (`Subtotal == PackageAmount` for every stored row); as an inference, no → **F4**. |
| 3 | Does the create path's snapshot equal the calculator's output for a percentage discount? | Yes **if** the create path freezes the computed discount amount (BR-02's precedence is preserved because `DiscountAmount` has a private setter and only `RecalculateFinancials` writes it) — but the "stored amount above zero wins" heuristic cannot distinguish "no fixed discount" from a catalogue row with `FixedAmount = 0` (`Discount.cs:11`), so the **catalogue resolution rule** must be stated; and the rounding gap of F2 remains → **F2** + **M11**. |
| 4 | Is the "one population" claim true? | **Yes** for the outstanding total (verified line by line) — but the dashboard's *overdue* is ">30 days" (`DashboardService.cs:138-143`, KPI-13) while KPI-39's is "`EventDate < today`" (`kpi-catalog.md:461`), so KPI-39's "reconciles to the cent" note (line 465) and spec §6's reconciliation sentence are **false** for the overdue count/amount → **F1** + the clarification in the closing paragraph. |
| 5 | Is the payment-state classifier well defined and exhaustive? | **No** — `DepositPaid` is unreachable, `AwaitingDeposit` is reachable only for a zero-value booking, `Net == 0 && Collected > 0` matches **no** state, and `Cancelled`/`Expired` are handled by the population rule rather than the classifier → **F1**. |
| 6 | Is the facet rule implementable and self-consistent? | Implementable, but it needs up to five populations, the payment-state dimension cannot be evaluated in SQL, and the facet counts will not sum to the totals → **F8** + **M9** + **M10**. |
| 7 | Does the design contain anything silently dropped or invented? | Dropped: the per-card "Print QuestPDF Contract" action (`Booking_Events_Main/code.html:288-291`), "Dish Costing & Ingredients", "Manage Event", the "Live Calendar Sync" pill, the three dock actions → **F7** + **M12** + **M13**. Invented without a source: the tier "code" → **M3**; the headcount KPI card (a legitimate replacement for the BEO card, DEC-104). Correctly dropped: `Finalized`/`Invoiced`, "3 Notices Sent", "In Bank", "Official Email", the `30` and the `400` → keep. |
| 8 | Is the frontend implementable against the delivered precedents? | Yes — every primitive the spec assumes exists (`Card`, `Button`+`inactive`, `Badge`, `ProgressTrack`, `CapabilityGapNotice`+`footnote`, `EmptyState`, `Skeleton`, `AlertBanner`, `SpinnerLoader`), and `keepPreviousData` is the packages pattern (`usePackageCatalogue.ts:32`). Missing: a `Select`/`Input` component (use native elements → **M7**), a payment-state vocabulary module (**M6**), a relative-date helper (**M5**), and the "tier code" (**M3**). |
| 9 | Is any structural change undeclared? | The workplan update is planned (AC-BKG-41, TASK-BKG-01-GV) but the **decision-log entry** AGENTS.md §0.1.3 requires is missing → **F6**. |
| 10 | Is contract §14 consistent with §1/§2/§5/§8/§9.2? | §1's create response and §9.2's "DEC-035 remains open" contradict §14.4 and §14.0; §8's string-enum convention is respected (the spec's `PaymentMeans` names are correct); §5's lifecycle names collide with the payment-state names → **F5**. |

---

### Closing verdict

**PROPOSE_BETTER_PLAN.** The plan is close to good enough, but it is not delegable in its present state: the payment-state classifier (F1) contradicts the approved rule it implements and would fail the acceptance scenario written for it; the net-due chain has no rounding rule (F2) and so cannot guarantee the invariant the whole cycle is built on; the soft-deleted-payment unification and the "one net-due path" claim are both incomplete (F3); the surcharge is inferred rather than stored (F4); the live contract still contradicts §14 (F5); the structural changes carry no AGENT 01 decision (F6); three Verified Facts are false and two of them are the premise of a ruling (F7); and the register's compute strategy and cost are unowned (F8).

**Before AGENT 01 delegates, AGENT 01 must:** (1) decide the payment-state design — adopt BR-28's three states plus a separate `isOverdue` dimension and overdue filter, **or** amend BR-28, Scenario 65 and KPI-37 first — and align spec §3.4, contract §14.1/§14.2, KPI-37 and the workbench's filter set to that decision (F1); (2) add the two-decimal `AwayFromZero` rounding rule to BR-25 and §3.5 and the corresponding snapshot test (F2); (3) extend DEC-111/BR-27/AC-BKG-35 to the **complete** list of payment-reading surfaces and convert the legacy customer-list AR off the persisted `AmountDue` (F3); (4) add the frozen `ExtendedAreaAmount` column to the additive migration and amend DEC-106 (F4); (5) amend contract §1 and §9.2 and resolve the §5 name collision (F5); (6) add the structural decision entry and realign the workplan sections named in F6; (7) correct the three false Verified Facts and re-base DEC-113, RSK-002's closure rationale and RSK-023's evidence text (F7); (8) state the register's compute strategy and correct RSK-054 (F8); and (9) correct KPI-39's reconciliation note — the dashboard's overdue is **>30 days** (KPI-13), the register's is **any balance past the event date** — so either align the two definitions or restate the claim to cover the outstanding total only (as AC-BKG-05 and Scenario 61 already do correctly), and answer the M1-M17 clarifications in the delegation briefs.

Nothing in this review asks for a different screen design, a different scope, a new project, a new root folder, a new layer dependency or a new Domain entity. The fifteen rulings stand; the money chain needs a rounding rule, a stored surcharge and a complete exclusion list; and the payment-state vocabulary needs to be reconciled with the rule that already exists.
---

## Debater Review — REQ-BKG-05 Bookings & Events Register & Reservation Workbench (2026-10-22)

**Decision: PROPOSE_BETTER_PLAN** — **8 blocking findings, 17 clarifications.**

The Debater read the as-built code rather than the specification's own claims, and that was the right call: **four of the eight findings are real defects in my plan that only the code could reveal** — the missing rounding rule, the two money paths I had not found, the inferred surcharge, and a status-name collision that would have shipped two meanings under one string. All eight were verified by AGENT 01 against the code before they were applied.

### The blocking findings — disposition

| # | Finding | AGENT 01's disposition |
| :--- | :--- | :--- |
| **F1** | The classifier contradicted the rule it implements; `DepositPaid` was unreachable; a zero-value or overpaid booking matched **no** state; `POST /api/payments` has no overpayment guard. | **Accepted, and the finding was partly against a stale reading of my own file.** The three-state vocabulary and the separate overdue dimension were already in BR-28; the Debater read the earlier five-state draft. But its **substantive** points stand and are now enforced: the states are renamed `AwaitingDeposit` / `PartiallyPaid` / `Settled` (**DEC-117**), the zero-value and overpaid cases are `Settled`, and **BR-05's overpayment prohibition is implemented** on the payment path (**DEC-118**, closing RSK-057). The taxonomy is now exhaustive by construction. |
| **F2** | No rounding rule ⇒ `AmountDue == NetAmountDue` was not guaranteed. A `0.005` discount on a gross of `100.00` left the stored and computed figures half a cent apart. | **Accepted — this was the most valuable finding in the review.** The money chain now carries **one** rounding — two decimals, away from the midpoint — and the create path stores exactly the rounded values (**DEC-115**). It is BR-20's existing convention applied to the money chain, and it is what makes the two sources equal *exactly* rather than approximately. |
| **F3** | The "unified" soft-deleted-payment exclusion missed `GET /api/bookings/{id}`, the customer roll-up and `GetListProjectionAsync` — a **ninth** net-due path computing AR in SQL. | **Accepted and widened.** The exclusion now covers **seven** surfaces, listed explicitly in BR-27 and in the specification §3.6, and it applies to the **listed** ledger entries as well as to the sum. The legacy SQL roll-up is not a divergent money notion once DEC-099 holds — it reads the persisted column, which now **equals** the calculator's figure — but it is brought onto the same payment population (**DEC-111**). |
| **F4** | The surcharge was inferred from `Subtotal − PackageAmount`; `Subtotal` conflates add-ons, and `ApplyExtendedAmount` is publicly settable. | **Accepted.** The surcharge becomes a **fourth frozen column**, `extendedAreaAmount` (`decimal(18,2) NOT NULL DEFAULT 0`), written once and read directly by the calculator; the flag becomes a creation-time switch and an audit marker (**DEC-116**). The migration stays purely additive. |
| **F5** | Contract §14 contradicted the live contract: §1's create response and §9.2's "DEC-035 remains open". The status names collided. | **Accepted.** Contract §1 now carries the extended create request and the extended response, with the two-level authorization spelled out; §9.2's rule 1 now states the resolved basis; and the payment states are renamed so the two vocabularies never share a name (**DEC-117**). |
| **F6** | The structural change needed its own AGENT 01 decision-log entry; DEC-099..113 were business rulings only. | **Accepted.** **DEC-114** records the structural approval — the feature folder, the two routes, the endpoint group, the use cases, the repository contract and its implementation, the export, the migration and the two configuration keys — and binds the workplan realignment to this cycle. |
| **F7** | Three false "Verified Facts": `ContractDocument` renders the **DTO's** calculator figures, not the cache; and `PaymentMeans` is `nvarchar(50)`, not an int. | **Accepted — the finding was correct and one of my facts was wrong.** DEC-113 is **narrowed**: the real requirement is that the contract endpoint must obtain its DTO through `GetBookingDetailsQuery` and must not assemble one from the cache or from the domain's `Booking.Balance`; RSK-023 is narrowed, not closed. The `PaymentMeans` statement in `WORKFLOW_STATE.md` was corrected before the review and the specification already carries the right statement. |
| **F8** | The compute strategy was unstated; the payment state cannot be evaluated in SQL; RSK-054's "the population per request is bounded" was false. | **Accepted.** The strategy is now stated explicitly — the base population is loaded **unpaged**, the derived dimensions are computed in the Application layer, and only then is the page applied — and the cost is recorded honestly as **RSK-056** rather than hidden. RSK-054's claim was corrected. |

### The clarifications

Seventeen were raised. All that carry a decision are applied: the **tier code** is a capability gap (DEC-119); `receivables.bookingCount` is defined as the count of bookings with an outstanding balance; the **"All" tier pill** carries the population count *without* the tier filter, so it agrees with the tier facets; a booking with **no tier** is in the population and in every total, and is excluded only from the tier facet and the leading tier; the **countdown** is the server's (`daysUntilEvent`), never the browser's clock; the **payment dropdown** offers the three states with no percentage option and the **overdue** filter is a separate toggle; the **two-level authorization** on `POST /api/bookings` is spelled out in contract §1 and §14.4; the **traceability matrix** no longer claims a delivered tense for work that is in delivery; and the **test counts** quoted in this cycle (700/700 unit, 118/118 integration) are the **actual** figures from the runs AGENT 01 performed on 2026-10-22, not an estimate.

### Verdict

The plan is **approved for delivery** as **v1.1**. The eight blocking findings have each been applied to the specification, the contract, the business rules, the KPI catalogue, the decision log, the risk register, the traceability matrix, the acceptance scenarios and this file. The specification was reissued as **v1.1** and the cycle's briefs are re-issued against it.
---

## AGENT 01 Review & Independent Validation (Stage 8 and 9, 2026-10-22)

### What AGENT 01 verified independently

| Check | Method | Result |
| :--- | :--- | :--- |
| The build | `dotnet build src/backend/SalsCaterSuite.slnx` | **0 warnings / 0 errors** |
| The unit suite | `dotnet test tests/SalsCaterSuite.UnitTests` | **805/805** (baseline 700/700) |
| The integration suite | `dotnet test tests/SalsCaterSuite.IntegrationTests` | **131/131** (baseline 118/118) |
| The money chain | The formula traced **by hand** through every discount case — fixed, percentage, zero-value, over-capped, zero-gross | `Booking.AmountDue == calculator.NetAmountDue` to the cent holds in **all** of them |
| The migration | The generated T-SQL, read | **Four `AddColumn` calls only** — no drop, rename, re-type or data change |
| The two re-pointed assertions | Both read | **Strengthened, not weakened** — signed off |
| The frontend feature folder | An independent hardcoded-figure scan over 27 files | **Zero** violations (peso sign, thousands separator, decimal literal, four-digit literal, percentage literal) |
| The frontend contract alignment | The TypeScript types and the endpoint group compared with contract §14 | **Field-for-field identical** |
| The two vocabularies | `bookingVocabulary.ts` read | The payment states never collide with the lifecycle names (DEC-117) |

### The live end-to-end validation

AGENT 01 started the **new** build on a **disposable** LocalDB database (`SalsCaterSuiteDb_LiveValidate`, dropped afterwards) on port 5151 — the developer's own development database was never touched — and drove the six endpoints of contract §14 with **independent** assertions.

**47 of 49 live assertions passed.** The run proved: the quote's base, surcharge, gross and net; the **null** required deposit and the **null** capacity (both absent by default); the five pre-dispatch checks; the register's totals reconciling; a confirmed booking created with a surcharge and a deposit; the ledger agreeing with the details to the cent; the receipt number's format; the register row's payment state, overdue flag, surcharge and `daysUntilEvent`; the summary's figures; the **overpayment guard** refusing the payment and writing nothing; and both the contract PDF and the register XLSX being served.

### The two defects the live run found (the suites did not)

| # | Finding | Severity | Disposition |
| :--- | :--- | :--- | :--- |
| **RV-1** | **`overdue=false` returned the whole population.** `MatchesOverdue` was `filter.Overdue != true \|\| row.IsOverdue`, so an explicit `false` was treated as absent. `Overdue` is a `bool?`, so `false` is a value the client can and will send — and the operator would believe they had narrowed the register when they had not. | **High** — a filter that silently returns everything | **Fixed** (round 2). The tri-state is now absent / `true` / `false`, pinned by two unit tests and an integration test, and carried by contract §14.1. |
| **RV-2** | **`GET /api/bookings/{id}` published three fields it never filled.** `BookingDto` declares `CallTime`, `PackoutTime` and `DiscountCode`, but `GetBookingDetailsQuery` never assigns them, so the details endpoint always returned `null` for a booking that had them. `ExtendedAreaAmount` was missing from the DTO entirely. | **High** — a declared-but-never-populated field is a silent wrong answer | **Raised** (round 3): the projection must fill all four, `ExtendedAreaAmount` must be added to the DTO, contract §1 must carry them, and a unit **and** an integration test must assert the projection. The suites were green because **no test asserted it** — the defect was only visible against a real request. |

### The review findings that became follow-ons

| Finding | Disposition |
| :--- | :--- |
| The `overdue` tri-state (RV-1) | Fixed and pinned |
| BR-34 — the dispatch ordering (`callTime <= programme start`, `packoutTime >= programme end`) | **Ratified** by AGENT 01 after verifying it is unambiguous: `Booking.Create` already rejects an end date before the start date, so an overnight event cannot exist and the two stored times are always same-day times of day. Recorded as **BR-34** and pinned by three tests plus an integration test. |
| The redundant payment registration | **Removed.** It broke two unit tests, and the cause was the **hand-written fake** under-modelling EF's graph traversal — the **fake** was fixed, not the assertions, and the real EF path is proven by the integration suite. |
| `BookingCreateCapabilities?` — an optional constructor parameter | **Accepted** for this cycle: a command built without them degrades to a clear business failure, never a crash. Recorded as **RSK-058** with the recommendation to make it required. |
| The workbench's client-side pre-check for BR-34's ordering | Raised to the frontend developer; the server stays authoritative and the client may only mirror the server's own wording. |
---

## Close (Stage 10, 2026-10-22)

### The delivery

**REQ-BKG-05 — the Bookings & Events Register** (`/bookings`) and **REQ-BKG-01 — the Reservation Workbench** (`/bookings/new`) are **delivered**, and the reference designs are implemented as specified: the dossier grid with its four KPI cards, the tier-pill and filter command bar, the totals strip and pagination dock, the three-card operational dock, and the three-column workbench with its client rail, tier and course engine, and financial and seal rail.

| | Result |
| :--- | :--- |
| Backend build | **0 warnings / 0 errors** |
| Unit tests | **823/823** (baseline 700/700) |
| Integration tests | **133/133** against real SQL Server LocalDB (baseline 118/118) |
| Frontend build | **0 TypeScript errors**, a clean Vite build |
| Frontend tests | **327/327** Vitest across 22 files (baseline 281/281) |
| Live end-to-end | **52/52** independent assertions against a disposable database |
| Migration | **purely additive** — four columns on `Booking`, no drop, rename, retype or data change |

### What the cycle changed in the business

- **DEC-035 is resolved.** There is **one** net-due figure per booking: the **frozen snapshot** is the source and `BookingAmountDueCalculator` **reproduces** it, with **one** rounding (DEC-115). The register, the ledger, the booking details, the customer statement, the calendar feed and the dashboard can no longer disagree.
- **The out-of-town surcharge is real** — charged, **stored** and frozen (DEC-100, DEC-116). BR-01's extended-area fee had never been charged on any booking.
- **BR-05's overpayment prohibition is enforced** (DEC-118) on the payment path and on the create path.
- **The soft-deleted-payment exclusion is unified** across all **seven** money surfaces (DEC-111, closing RSK-024).
- **The payment states never collide** with the lifecycle vocabulary (DEC-117), and the **overdue** dimension is separate from them (BR-28).
- **The register's totals, the summary's KPIs and the printed contract** are all the server's; the browser formats and displays (DEC-002).

### What the cycle deliberately did **not** do

Each of these is an explicit, named gap with a capability notice in the UI - never a fabricated figure or state.

| Gap | Why | Tracked as |
| :--- | :--- | :--- |
| Add-on line items on a booking | `AddonDetail` has no link to `Booking`; a junction and a second migration would be needed | RSK-048, REQ-BKG-01 |
| A per-event dish selection | no `Booking`↔`Menu` link | DEC-105, REQ-BKG-02 |
| The BEO production state and the BEO PDF | no BEO entity, and no per-event selection to print | DEC-104, REQ-REP-02 |
| The chef batching yield | the recipe engine is blocked on ten owner decisions | REQ-MNU-03 |
| The package tier inclusions | `Package` carries no inclusions | DEC-108 |
| The booking **edit** screen, the **calendar**, the **receivables** screen | separate requirements | REQ-BKG-01, REQ-CAL-01, REQ-REC-01 |

### The four things the plan review and the live validation caught

The Debater's `PROPOSE_BETTER_PLAN` returned **eight blocking findings**; **all eight were applied** before delivery, and the specification was reissued as **v1.1**. Four of them were defects only the code could reveal: the **missing rounding rule**, the **six money surfaces** that lacked the soft-deleted-payment exclusion, the **inferred** surcharge, and the **status-name collision**.

Then the **live end-to-end validation** found **two more defects that the green suites could not see** - the `overdue=false` filter that returned the whole population, and the details endpoint that **published three fields no projection filled**. Both are fixed and pinned, and the second is now guarded by a **DTO projection guard** that fails the build when a read model gains a member nobody projects. That guard was **proved by execution**, not asserted.

### The owner's open questions

None of these blocks the delivery; each is a **configuration** or a later cycle.

| # | Question | What ships today |
| :--- | :--- | :--- |
| 1 | The required deposit percentage | **Not stated** — `Bookings:Deposit:RequiredPercentage` is absent by default (DEC-101, DEC-009 still open) |
| 2 | The commissary's maximum daily pax | **Not stated** — `Bookings:Capacity:MaximumPax` is absent by default (DEC-102) |
| 3 | Should a booking below the tier's minimum headcount be refused? | **No** — an advisory note (BR-16) |
| 4 | Should a municipality outside the tier's coverage be refused? | **No** — an advisory check (BR-29) |
| 5 | Should add-ons be linked to a booking? | **Deferred** — a junction and a second migration (RSK-048) |
| 6 | The tax policy | **Unresolved** (DEC-008) — the money chain carries a zero tax term by construction |

### The residual risks this cycle leaves behind

RSK-048 (add-ons unlinked) · RSK-049 (no per-event dish selection) · RSK-050 (`Expired` bookings sit in the register's AR, as DEC-025 requires) · RSK-051 (the snapshot equality is maintained by the create path, not by a database constraint) · RSK-052 (the deposit and the capacity are configuration) · RSK-053 (the register can drift when a filter changes) · RSK-054 (the tier estimate is recomputed per request) · RSK-055 (a malformed legacy `pay_means` value fails every read that loads payments - now including the register) · RSK-056 (the register loads the whole filtered population per request) · RSK-057 (closed by DEC-118) · RSK-058 (the create command's optional capabilities).

### The cycle's record

- **Specification:** `business/bookings-events-feature-spec.md` **v1.1**
- **Contract:** `architecture/approved-api-contracts.md` **§14** (six endpoints) and the amended **§1** and **§9.2**
- **Business rules:** **BR-25..BR-34** (ten new rules)
- **KPIs:** **KPI-33..KPI-39** (seven new measures)
- **Decisions:** **DEC-099..DEC-124** (twenty-six new decisions; ten of them close an existing open question)
- **Risks:** **RSK-048..RSK-058** (eleven new; four closed)
- **Acceptance scenarios:** **61..74**
- **Workplan:** sections 2.2-2.6, 3.1-3.4, 4, 7.3, 7.4, 9, 10 and 11 realigned to the as-built state in this cycle.
