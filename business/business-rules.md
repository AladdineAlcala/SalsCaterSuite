# Sal's CaterSuite — Authoritative Business Rules & Financial Formulas

## 1. Booking Pricing & Calculation Engine (Rule BR-01)

### 1.1 Gross Amount Formula
$$\text{Gross Amount} = \text{BasePackage} + \text{ExtendedAreaFee} + \text{AddonsTotal} + \text{OtherCharges}$$

Where:
- $\text{BasePackage} = \text{PackageAmount}$ (if fixed) or $(\text{PricePerPax} \times \text{NoOfPerson})$.
- $\text{ExtendedAreaFee} = \text{ExtendedAreaRate}$ if $\text{ApplyExtendedAmount} = \text{true}$; otherwise $0.00$.
- $\text{AddonsTotal} = \sum (\text{Addon's frozen line total})$ - the sum of the **line totals** the booking's add-on lines carry (DEC-158). A per-guest line's total is $\text{unit price} \times \text{heads}$, so the headcount is expressed **within** the line rather than as a separate stored quantity; a flat line's total is its own amount.
- $\text{OtherCharges} = \text{Any approved operational surcharges}$.

### 1.2 Discount Calculation & Precedence (Rule BR-02)
- If both a Fixed Discount Amount and a Percentage Discount are entered, **Fixed Discount Amount takes precedence**.
- Percentage discount is calculated on the Gross Amount:
  $$\text{DiscountAmount} = \frac{\text{Gross Amount} \times \text{Percentage}}{100}$$
- **Discount Ceiling:** Discount cannot exceed Gross Amount:
  $$\text{DiscountAmount} = \min(\text{Gross Amount}, \text{CalculatedDiscount})$$
- **Resolution by code (added by REQ-BKG-05, 2026-10-22).** A booking names a discount by its **code**. The `Discount` catalogue's `disc_code` carries **no unique index**, so the code is not unique in the model and the resolution is explicit:
  - **no active discount** with that code -> `400` ("The discount code was not found.");
  - **more than one active discount** with that code -> `400` ("The discount code is ambiguous.").
  The server never picks one silently, and a rejected code creates **no** booking and **no** payment row. The catalogue is empty (BR-13 forbids seeding business data), so in practice every code is currently rejected.
- The **applied** discount is **frozen** on the booking: its type, its percentage, its amount and its **code**. A later change to the catalogue never alters a stored booking (BR-04).

### 1.3 Net Amount Due & Balance (Rule BR-03)
$$\text{NetAmountDue} = \text{Gross Amount} - \text{DiscountAmount}$$
$$\text{RemainingBalance} = \max(0.00, \text{NetAmountDue} - \text{TotalPaymentsReceived})$$

- **Fully Paid Indicator (`IsFullyPaid`):**  
  $\text{TotalPaymentsReceived} \ge \text{NetAmountDue}$ and $\text{NetAmountDue} > 0$.

---

## 2. Historical Price Integrity (Rule BR-04)
- **Rule:** Catalog updates to package rates (`amount_pax`), addon prices (`unitPrice`), area surcharges (`additionalRate`), or menu prices (`price`) must **NEVER** retroactively recalculate or alter the pricing breakdown of an already confirmed booking.
- **Enforcement:** At the moment of booking creation or line-item addition, unit prices and calculated base amounts are permanently stored in the booking and line item records.

---

## 3. Payments, Collections & Receipts (Rule BR-05)
- **Official Receipt Numbering:** Sequential receipt formatting with date prefix: `OR-YYYYMMDD-XXXX`.
- **Payment Means:** Supported payment methods are `Cash`, `Check`, `GCash`, `Bank Transfer / LDDAP`, `Credit Card`.
- **Check Payments:** Require bank name, branch, check number, and clearing status.
- **Overpayment Prohibition:** Total payments recorded must not exceed Net Amount Due unless explicitly flagged and acknowledged as an advance credit.

---

## 4. Cancellations & Refunds (Rule BR-06)
- **Served / completed bookings:** a booking in `Completed` status **CANNOT** be cancelled (implemented message: `"A completed booking cannot be cancelled."`). An already-cancelled booking is rejected with `"Booking is already cancelled."`.
- **Maximum Refund Boundary (DEC-024 / F7 — APPROVED):** the refund amount cannot exceed the actual total payments received for that booking (authoritative payment sum, BR-10):
  $$\text{RefundAmount} \le \text{TotalPaymentsReceived}$$
  A request above the boundary is rejected with `400` and `"Refund amount cannot exceed the total payments received."`; a negative amount is rejected with `"Refund amount must not be negative."`. A rejection must leave the booking **uncancelled** and create **no** refund row.
- **No refund:** a refund amount of `0` means the client receives nothing and **no `Refund` row is created**; the booking is still cancelled.
- **Refund Net Calculation:**
  $$\text{NetAmountRefunded} = \text{RefundAmount} - \text{Deduction (Cancellation Penalty)}$$
  (The cancellation-penalty/deduction formula is **not yet approved** — see DEC-007.)
- **State Transition:** upon cancellation `Status` is set to `Cancelled` and the booking's outstanding balance leaves Accounts Receivable; a Cancellation/Refund Voucher is issued.
- **Balance Due impact:** refunds are reported separately as `TotalRefunded` and do **not** reduce `BalanceDue` (DEC-013).

---

## 5. Food Costing & Menu Margins (Rule BR-07)
- **Dishes:** Every dish has a retail selling price ($P$) and a unit food cost ($C$).
- **Contribution Margin:**
  $$\text{Margin} = P - C$$
- **Target Margin Guidelines:** (Default benchmark: Food cost should not exceed 35–40% of selling price unless designated as a premium loss-leader).

---

## 6. Accounts Receivable (AR) & Collections Distinction (Rule BR-08)
- **Collections:** Total actual money received into cash/bank within a date range:
  $$\text{Collections}(t_1, t_2) = \sum_{p.DateOfPayment \in [t_1, t_2]} p.AmountPaid$$
- **Accounts Receivable:** Outstanding debt owed by customers across all active (non-cancelled) bookings, computed from the **authoritative payment sum** (BR-10):
  $$\text{Total AR} = \sum_{b.\text{Status} \ne \text{Cancelled}} \max(0, b.\text{NetAmountDue} - \text{PaymentsReceived}_b)$$
- The AR total must **reconcile** with the sum of each booking's ledger `balanceDue`. Fully paid bookings contribute `0`; cancelled bookings are excluded.
- **Status:** DEC-025 **CONFIRMED** by the owner. Served (`Completed`) bookings with an unpaid balance are still collectible and remain included in AR.
- Under no circumstance may Collections be reported as Receivables.

---

## 7. Booking Identity & Document Numbering (Rule BR-09)
- Every booking must carry a **unique, human-readable** `BookingNumber` that is never blank.
- **Format:** `BK-YYYYMMDD-NNNNN`, where `YYYYMMDD` is the booking creation date and `NNNNN` is the zero-padded surrogate `TransactionId`. Using the surrogate id guarantees uniqueness with no sequence race condition. *Status: DEC-016 **CONFIRMED** by the owner — the format is contractual and may not change without a new decision.*
- `BookingNumber` must be backed by a **unique database index**.
- Payment receipts continue to use the independent receipt series in BR-05 (`OR-YYYYMMDD-XXXX`). A booking number is never re-used or re-issued.

---

## 8. Authoritative Payment Figures (Rule BR-10)
- **Authoritative amount paid** for a booking:
  $$\text{AmountPaid}_{authoritative} = \sum \text{Payment}_b.\text{AmountPaid}$$
- `Booking.AmountPaid` is a **denormalized cache** and must always equal the authoritative sum. It is refreshed whenever a payment is recorded.
- Booking details, ledger, calendar and customer statements must all reconcile to the same authoritative figure.
- `${RemainingBalance} = \max(0.00, \text{NetAmountDue} - \text{AmountPaid}_{authoritative})$` (BR-03 continues to apply).
- **Refunds do not reduce the balance due.** Refunds are reported separately as `TotalRefunded`; whether a refund reduces the outstanding balance depends on the cancellation/refund policy (DEC-007) and is currently **UNRESOLVED**.
- Booking lifecycle `Status` is the single source of truth and is derived from payment progress (`Pending` → `DepositPaid` → `FullyPaid`) and event execution (`InProgress` → `Completed`) with `Cancelled` terminal. The legacy `ServeStatus` / `IsCancelled` boolean pair is retired and must not be reintroduced.

---

## 9. Validation, Error Contract & HTTP Semantics (Rule BR-11)
- Every request payload is validated **server-side before persistence** using the feature's FluentValidation validator. Rejections return `400` with `{ "errors": [ "<message>", ... ] }`.
- Domain rule violations (e.g. invalid event dates, cancellation of an already-served event) must return **`400`** with the same error shape. A domain rule violation must **never** surface as an unhandled `500`.
- Standard status semantics: `200` success · `201` created · `204` no content · `400` invalid request or business rule violation · `401` unauthenticated · `403` authenticated but not permitted · `404` resource not found · `409` duplicate business key (e.g. duplicate menu id) · `500` unexpected server fault.
- Error payloads always use the `errors` array shape for business/validation failures. Authentication/authorization failures return `401`/`403` with no business detail.

---

## 10. Authorization Rules (Rule BR-12)
- **All** API endpoints require an authenticated bearer token. There is no anonymous business endpoint.
- Role-based restrictions apply to financially sensitive operations:

| Capability | Endpoint | Permitted Roles |
| :--- | :--- | :--- |
| Cancel booking / issue refund | `POST /api/bookings/{id}/cancel` | Administrator, Manager |
| Record payment / issue receipt | `POST /api/payments` | Administrator, Manager, Cashier |
| All other business endpoints | — | Any authenticated user |

- Authorization is enforced **server-side** as a named policy. Hiding a button in the UI is never a substitute for a server check.
- *Status: DEC-017 **CONFIRMED** by the owner — role names are contractual; user administration and seeding (BR-13) must provision exactly these names.*

---

## 11. Seeding & Bootstrap Rules (Rule BR-13)
- Only **system-required** data may be seeded automatically: Identity roles (BR-12), one bootstrap administrator account, and standard `CourseCategory` reference values required by the Menu catalog.
- **No fabricated business data** may be seeded: no customers, bookings, payments, refunds, menus, packages, areas, add-ons or discounts.
- Seeding must be **idempotent** — re-running it must not duplicate roles, users or reference rows, and must never overwrite an operator-changed row.
- The bootstrap administrator password is a deployment secret: it must be supplied by configuration/secret storage, must not be committed as a production credential, and must be changed on first login. *Status: DEC-018 CONFIRMED; bootstrap credential mechanism requires owner sign-off before production.*


---

## 12. Package Configuration Rules (added by REQ-PKG-02, 2026-10-22)

### Rule BR-14: The Food-Cost Ceiling Of A Tier

$$\text{CostCeilingPerPax} = \text{AmountPax} \times \frac{\text{TargetFoodCostPercentage}}{100}$$

- `TargetFoodCostPercentage` is the **deployment-configured** food-cost benchmark (`Dashboard:Benchmarks:TargetFoodCostPercentage`, DEC-040) - the same value the menu screen and the operations dashboard read. It is never a constant in code and never a hardcoded band.
- The ceiling is `null` when the tier carries no rate. It is a **warning threshold**, not a block and not a price.
- **A breach warns; it never reprices and it never refuses.** The preview reports the breach, the shortfall in pesos and the percentage over the ceiling, and the selection is marked non-compliant. No automatic premium is added and no price is changed (DEC-080).

### Rule BR-15: Course-Rule Compliance Is Exact

- A course rule is **satisfied** when the number of selected dishes in its course is **exactly** `RequiredQuota`. A rule with a quota of one is satisfied by exactly one dish; two dishes in a quota-of-one course is a **breach**.
- A selection that names a course for which the tier holds **no** rule is **not compliant**, and the package-level verdict is **not compliant**.
- The package-level verdict is `true` only when every rule is satisfied and no selection names an unruled course.
- A rule removed from a submitted ruleset is **soft-deleted**, never destroyed (DEC-085).

### Rule BR-16: The Minimum Contract Value Of A Tier

$$\text{MinimumContractValue} = \text{AmountPax} \times \text{MinPax}$$

- It is a **derived floor** on the contract, not a charge, and it never enters a booking total (DEC-079).
- It is `null` when either `AmountPax` or `MinPax` is unset - **unknown**, never zero (contract §9.2 rule 6).
- The headcount floor itself is `MinPax`. There is no guest ceiling in the model.

### Rule BR-17: The Out-of-Town Surcharge Is One Rate Per Municipality

- `Area.AdditionalRate` is the **single** per-pax out-of-town surcharge for a municipality. It is also the rate BR-01 charges on a booking with an extended area, so it carries **one** meaning in **one** column.
- The effective rate quoted for a municipality is:
  $$\text{EffectiveRatePerPax} = \text{AmountPax} + \text{AdditionalRate}$$
- A coverage row carries **no** money. There is no per-package override, and the rate a tier quotes in a municipality is the rate every tier covering that municipality quotes (DEC-078).
- The contract value of a simulated event is:
  $$\text{TotalContractValue} = (\text{AmountPax} \times \text{Pax}) + (\text{AdditionalRate} \times \text{Pax})$$

### Rule BR-18: The Gross-Profit Split Of A Simulated Event

$$\text{GrossProfit} = \text{TotalContractValue} - \text{FoodCostTotal} - \text{LogisticsCost}$$
$$\text{GrossProfitPercentage} = \frac{\text{GrossProfit}}{\text{TotalContractValue}} \times 100$$

- `FoodCostTotal` is the selected dishes' food cost per pax multiplied by the headcount, or the **standard run** estimate when no selection is supplied (BR-19).
- `LogisticsCost` is the municipality's **per-event** logistics cost. It is a flat cost per event, not per guest. A municipality with **no** logistics cost publishes `null`, the band is **omitted**, and the gross profit is `null` rather than overstated (DEC-081).
- A `null` in any operand makes the gross profit and its percentage `null`. A `0` is published only where zero is a real measurement.
- These are **planning** figures. They are not revenue, not a receivable and not an accounting entry.

### Rule BR-19: The Standard Run Is A Planning Estimate

$$\text{StandardRunFoodCostPerPax} = \sum_{r \in R} \overline{\text{FoodCost}}_r \quad\text{where}\quad \overline{\text{FoodCost}}_r = \frac{\sum_{d \in \text{Pool}(r),\, d.\text{FoodCost} \neq \text{null}} d.\text{FoodCost}}{\,|\{d \in \text{Pool}(r) : d.\text{FoodCost} \neq \text{null}\}|\,}$$

- The **qualified pool** of a course rule is every non-soft-deleted `Menu` whose course matches the rule's course. The mean is over the pool's **costed** dishes only - and a dish is **costed** when its food cost is **greater than zero** (DEC-087), because `Menu.FoodCost` stores zero rather than null when no cost was entered.
- The pool means are computed at full precision and the **sum** is rounded **once**, to two decimals away from the midpoint (DEC-003 / RSK-029). The published per-pool average is separately rounded, but the standard run does **not** sum the rounded means.
- The standard run is a **planning estimate, not a commitment** (DEC-083, owner-ratified 2026-10-22). It is derived on every read, so it is never stale in the response, but it **moves** whenever the kitchen adds or retires a dish.
- The **actual** food cost of a proposal is the **selection preview** (BR-15 / BR-18), computed from the dishes the client actually chose. A selected dish that is **not costed** makes the selected food cost **null** - unknown, never zero - while the quota verdict still counts the dish.
- The dish food costs underneath every one of these figures are **manually entered** decimals (DEC-062), so every margin figure derived from them is **advisory, not auditable** (RSK-034).

### Rule BR-20: Rounding And Null Semantics Of The Workbench

- Every money figure and every percentage is rounded to **two decimals, away from the midpoint** (`MidpointRounding.AwayFromZero`, DEC-003, RSK-029), **once**, in the Application layer. The published value is the rounded one; the browser never re-rounds.
- `null` means **unknown**, never zero. A percentage whose denominator is zero or null is `null`, never `0.00` and never `100.00`.
- A preview is a **what-if**. It persists nothing, it changes no price, and it is not a booking total.

---

## 11. Operational Logging, Redaction & Retention (Rules BR-21 .. BR-24)

> These rules govern the **diagnostic record** the API writes for every request. They do not
> govern any business figure. Specification: `business/observability-logging-feature-spec.md` v1.0.

### Rule BR-21: Redaction Of The Logged Record (owner-ruled, non-negotiable)

- Redaction applies to a **JSON** request or response body by **property name**,
  **case-insensitively**, at **any depth**, including properties inside arrays. A redacted
  property's value becomes the literal marker `***REDACTED***`.
- The **default sensitive-name set** is **configuration** (`Logging:RequestLogging:SensitiveFieldNames`),
  not code, so it can be changed without a rebuild. The set is **verified against the as-built
  DTOs** — it names properties that exist:
  - **Secrets - always redacted, never negotiable:** `password`, `passwordconfirmation`,
    `newpassword`, `confirmpassword`, `token`, `accesstoken`, `refreshtoken`, `idtoken`, `jwt`,
    `secret`, `secretkey`, `apikey`, `api_key`, `authorization`, `clientsecret`, `signature`,
    `privatekey`, `connectionstring`.
  - **Account identifiers:** `username`, `userName`, `createdbyuser`. In this deployment the
    **username is the administrator's email address**, so an unredacted username is a contact
    identifier on disk (DEC-096).
  - **Customer contact identifiers - owner-ruled (DEC-093):** `address`, `contact`, `contact1`,
    `contact2`, `customercontact`. These are the **only** contact-identifier property names in
    the codebase.
  - **Forward-looking:** `email`, `emailaddress`, `tin`. **No property in the current model
    carries an email address or a TIN**; the names are kept so the rule is already correct if
    the model grows one.
- The **query string** is redacted by parameter name against the same set, plus `access_token`
  (the SignalR bearer) and `search` (a customer search term that matches a name, a company and
  a contact number).
- **The sensitive-name set applies to the record's own field names as well as to the body's
  property names.** The record's `UserName` field (the `ClaimTypes.Name` claim) is therefore
  **redacted**, because `username` / `userName` is in the set and in this deployment that claim is
  the operator's **e-mail address** and **login name**. The record's `UserId` (the Identity key, a
  GUID) and `Roles` are **not** in the set and are kept, so "who did this" is still answerable and
  the operator is resolvable from the identity table by `UserId`. Redacting the body's `username`
  while writing the identical value in clear one field later would make the body redaction
  self-defeating (DEC-096).
- **Request headers are never logged.** The only header-derived field is the boolean
  `HasAuthorization`. An `Authorization` or `Cookie` header value can never reach a log.
- A body that is **not valid JSON** is **not logged** - the record carries
  `[non-json body omitted]`. A name-based redactor cannot see a credential hidden in a shape it
  does not recognise, so an unparseable body is treated as sensitive.
- A **binary** response (a content type that is not `application/json`,
  `application/problem+json`, `application/xml` or `text/*`) is recorded by **content type and
  byte count only**; its bytes are never written to the log.
- **Customer names and transaction amounts are NOT redacted by default** (DEC-093). A log that
  cannot identify the booking or show its amount is not operationally useful, and the amounts are
  the business's own transactional data. This is a deliberate data-exposure decision, not an
  oversight, and it is the residual exposure recorded in RSK-040.
- **Free text is a known limitation.** A name-based redactor cannot reach a contact identifier or
  a credential written inside free text - `SpecialInstructions`, `Reference`, `Reason`, `Notes`,
  `Particular` and `CheckNo` are logged verbatim. A future free-text field that is expected to
  carry personal data must be added to the set by name.
- **The rule applies to the log file as a whole, not only to the request record.** The redaction
  engine governs what the request logger writes; **no application log message** may echo a
  credential or a contact identifier either. The one such message in the codebase - the seeder's
  *"Bootstrap administrator already present"* notice, which fires on every startup - now names the
  account by its **id**, not by its login name (DEC-097).

### Rule BR-22: Bounded Capture

- At most **8 192 bytes** of any single body is captured (configurable:
  `Logging:RequestLogging:MaxBodyLengthBytes`). A longer body is **truncated** and the record
  carries `RequestBodyTruncated` / `ResponseBodyTruncated` = `true` with an explicit marker.
- Capture is **read-only**. The bytes the client receives are **byte-for-byte identical** to the
  bytes the application produced, for a JSON response, an XLSX export and a PDF alike.
- The cap is also the **memory** bound: once it is exceeded the remainder **streams through**
  rather than being buffered, so a large document is never held in memory (RSK-041).
- **The request body stream must be rewound.** After the capture the middleware sets
  `Request.Body.Position = 0`, so model binding still sees the **whole** body. The buffering
  wrapper is left in place; the **original** stream is **not** restored, because the original
  stream has already been consumed and restoring it would hand the endpoint a truncated body.
  `Request.EnableBuffering()` imposes **no** size limit - its 30 KB argument is the threshold at
  which the buffer spills to a temporary file, not a cap.
- **The response capture stream must be seekable** (`CanSeek == true`, `SetLength` honoured). The
  error path calls `HttpResponse.Clear()`, which resets a seekable body and **cannot** reset a
  non-seekable one; a non-seekable capture would leave the stale bytes of a partially written
  response in the recorded body.
- The middleware must **not** replace `Response.Body` on a **WebSocket upgrade** request
  (`HttpContext.WebSockets.IsWebSocketRequest`); the WebSocket adapter runs over
  `IHttpResponseBodyFeature`, not over `Response.Body`.

### Rule BR-23: Destination And Retention

- The destination list is **configuration** (`Serilog:WriteTo`). Adding, removing or reordering a
  sink is an `appsettings` edit, not a rebuild.
- The always-available destination is a **rolling file** under `logs/`, one file per day, JSON
  lines, **31** files retained, a **10 MB** per-file cap with a size-based roll.
- A **JSON line** is written per record, not a plain-text template, so the Datadog Agent parses
  the fields (`@t`, `@mt`, `StatusCode`, `ElapsedMs`, ...) as structured attributes (DEC-095).
- The `logs/` directory is **excluded from version control**.

### Rule BR-24: The Record Is Diagnostic, Not Evidential

- A log line is **not** an accounting record, a receipt, a legal document, or proof of a financial
  transaction. The authoritative financial records remain the `Payment`, `Refund` and `Booking`
  rows and the documents rendered from them (DEC-012, DEC-023, BR-04).
- The retention policy may **delete** log lines without any loss of business record.
- A log line carries **no** business figure of its own: no amount, no balance, no count. Those
  live in the authoritative records.
- A `null` field means **absent**, never zero.

---

## 12. Bookings & Events Register & Reservation Workbench (Rules BR-25 .. BR-33)

> These rules govern the **Bookings & Events Register** (`/bookings`) and the **Reservation
> Workbench** (`/bookings/new`). Specification: `business/bookings-events-feature-spec.md` v1.0.
> They complete the money chain that BR-01 described but that no screen could reach until now.

### Rule BR-25: There Is One Net-Due Source, And It Is Rounded Once (DEC-099 — resolves DEC-035; DEC-115)

- The **frozen booking snapshot is the single net-due source**. `BookingAmountDueCalculator.Calculate(Booking, totalPaymentsReceived)` **reproduces** that snapshot; it does not compute a second, parallel total:

  ```
  BasePackageAmount  = booking.PackageAmount                                 (frozen, BR-04)
  ExtendedAreaAmount = booking.ExtendedAreaAmount                            (frozen, BR-26, DEC-116)
  AddonsAmount       = Booking.AddonsTotal                                   (the frozen sum of the add-on line totals - DEC-151)
  OtherChargesAmount = 0
  GrossAmount        = Round2(BasePackageAmount + ExtendedAreaAmount + AddonsAmount + OtherChargesAmount)

  RawDiscount        = booking.DiscountAmount > 0
                          ? booking.DiscountAmount                           (a stored fixed discount wins, BR-02)
                          : booking.DiscountPercentage > 0
                              ? GrossAmount x booking.DiscountPercentage / 100
                              : 0
  DiscountAmount     = Round2( min(RawDiscount, GrossAmount) )                (BR-02 precedence and ceiling)

  NetAmountDue       = Round2( max(0, GrossAmount - DiscountAmount + booking.TaxAmount) )
  RemainingBalance   = Round2( max(0, NetAmountDue - totalPaymentsReceived) ) (BR-03)
  IsFullyPaid        = NetAmountDue > 0 && totalPaymentsReceived >= NetAmountDue
  ```

- **`Round2` is the one rounding of the money chain** (DEC-115): two decimals, **away from the midpoint** (`MidpointRounding.AwayFromZero`) — BR-20's rule and DEC-003's convention, applied **once**, in the Application layer. The published value is the rounded one and the browser never re-rounds.
- **The rounding is what makes the two sources equal, not a cosmetic touch.** `DiscountPercentage` is stored as `decimal(18,4)` while every money column is `decimal(18,2)`. Without a single rounding, a percentage discount of `0.005` on a gross of `100.00` produces a stored `AmountDue` of `99.99` or `100.00` against a calculator figure of `99.995` — a half-cent divergence on one booking and a reconciliation failure across a population.
- The booking create path **stores exactly these values** — it computes the raw discount, rounds it with the same `Round2`, and passes the **rounded** discount to `Booking.RecalculateFinancials` — so `Booking.AmountDue` and the calculator's `NetAmountDue` are **equal for every booking**. The register, the ledger, the booking details, the customer statement, the calendar feed and the dashboard therefore publish **the same figure** for the same booking — the divergence DEC-035 predicted cannot occur.
- The calculator's **six-argument overload** (the from-scratch path) is retained with its current signature and semantics, with the tax term at zero and the same `Round2` applied. The two overloads agree whenever `TaxAmount` is zero.
- A stored **zero** discount must not shadow the percentage rule: a booking that stores `DiscountPercentage > 0` and `DiscountAmount == 0` still has its percentage applied.
- The tax term stays **zero** in practice — the tax policy is **UNRESOLVED** (DEC-008). The term exists so the two sources agree by construction the day a tax is ruled.

### Rule BR-26: The Out-of-Town Surcharge Is Charged On The Booking (DEC-100)

- The surcharge is **the municipality's** rate, applied **per guest**, and it is **frozen** on the booking (BR-04):

  $$\text{ExtendedAreaAmount} = \text{ApplyExtendedAmount} \;?\; \text{Area.AdditionalRate} \times \text{NumberOfHeads} : 0$$

- `Area.AdditionalRate` is the **one** out-of-town rate (BR-17, DEC-078). There is **no second rate column** and no per-tier override.
- A municipality with **no stated rate** contributes **zero** to the money chain (DEC-086) while the published rate stays `null` — the measurement is unknown, the addition is nothing.
- The surcharge is charged **exactly once**. It is **stored** in its own frozen column, `Booking.ExtendedAreaAmount` (`decimal(18,2) NOT NULL DEFAULT 0`), and the shared calculator reads that column directly (DEC-116). It is **not** inferred from `Subtotal - PackageAmount`, because `Subtotal` conflates the surcharge with the add-on total and the other charges and because `ApplyExtendedAmount` is a **creation-time switch**, not a money switch.
- Editing `Area.AdditionalRate` moves the quoted price of every tier that covers the municipality **and** the surcharge of every **new** booking. It never moves a **stored** booking (BR-04).

### Rule BR-27: The Authoritative Figures And One Payment Population

- The **authoritative amount paid** is `Σ Payment.AmountPaid` (**BR-10**) — never the denormalized `Booking.AmountPaid` cache.
- A **soft-deleted payment is excluded** from **every** money surface, and from the **list** of ledger entries as well as from the **sum**. The exclusion is applied by the query that reads the payments; **no global query filter** is introduced (DEC-038, DEC-045, DEC-111). The surfaces are:
  1. `GET /api/dashboard` — already excluded (unchanged);
  2. `GET /api/bookings/{id}`;
  3. `GET /api/bookings/{id}/ledger` and `GET /api/payments/ledger/{id}`;
  4. `GET /api/calendar/events`;
  5. `GET /api/customers/directory` and `GET /api/customers/{id}`;
  6. `GET /api/customers` — the legacy in-SQL receivables roll-up;
  7. the Bookings & Events Register.
- The **overpayment prohibition is enforced** (BR-05, DEC-118). A payment that would take the total above the net amount due is refused with `400`, and **nothing is written** — no payment row, no change to the booking's status, no change to its cached `AmountPaid`. The guard applies to `POST /api/payments` and to the create path's initial payment. It does **not** apply to a **refund** (BR-06).
- The outstanding balance of a booking is `max(0, NetAmountDue - Collected)` (BR-03).
- The register's split always reconciles:

  $$\text{NetContractedValue} = \text{CollectedTotal} + \text{OutstandingTotal}$$

- The register's population is every **non-soft-deleted** booking whose status is **not** `Cancelled`. `Expired` bookings stay in it, because DEC-025 keeps them in AR (RSK-017 remains open).
- A booking with **no tier** (`PackageId` is nullable) is in the population and in every total. It is **excluded from the tier facet** (it has no tier type) and from the **leading tier** (KPI-34), and it is counted in the pipeline.

### Rule BR-28: The Payment-State Vocabulary And The Overdue Dimension

- One classifier, in one class, shared by the register, the summary and the quote. The **payment state** answers one question - *how far along is the payment?* - and it is a **three-state** vocabulary:

| State | Condition |
| :--- | :--- |
| `AwaitingDeposit` | `Collected == 0` **and** `NetAmountDue > 0` |
| `PartiallyPaid` | `Collected > 0` **and** `Collected < NetAmountDue` |
| `Settled` | `Collected >= NetAmountDue` |

- **The names must not collide with the lifecycle vocabulary** (DEC-117). Contract section 5 publishes the ten `BookingStatus` names, and two of them - `DepositPaid` and `FullyPaid` - are *payment* names. The payment states are therefore named **`AwaitingDeposit`**, **`PartiallyPaid`** and **`Settled`**, and the two vocabularies never share a name. `BookingStatus.DepositPaid` remains a **lifecycle** status; `PartiallyPaid` is a **payment** state.
- The evaluation order is `AwaitingDeposit` -> `PartiallyPaid` -> `Settled`, and the three states are **mutually exclusive and exhaustive** by construction: `Collected == 0` and `Collected > 0` cannot both hold, and `Collected < NetAmountDue` and `Collected >= NetAmountDue` cannot both hold. **Every booking has exactly one payment state**, and the three counts sum to the population's booking count.
- `Settled` deliberately covers two edge cases that must never be reported as awaiting anything: a **zero-value** booking (`NetAmountDue == 0`, nothing collected) and an **overpayment** (`Collected > NetAmountDue`).
- **Overdue is a separate dimension, not a payment state.** A booking with a balance is overdue when its **event date has passed** - the event date is the due date (DEC-041):

  $$\text{IsOverdue} = \text{Outstanding} > 0 \;\land\; \text{EventDate} < \text{today}$$

  It is published per item, it drives the register's overdue filter, and it feeds the `overdueCount` / `overdueAmount` aggregates. It is deliberately **not** a payment state: a partly-paid booking is `PartiallyPaid` **and** overdue, and folding the two into one vocabulary would make the states overlap, so a payment-state filter would select rows that no single state describes.
- The register therefore filters on **payment state** (`AwaitingDeposit`, `PartiallyPaid`, `Settled`) and on **overdue** separately. It does **not** filter on a deposit **percentage**: the required deposit percentage is configuration and absent by default (DEC-101, BR-30), so a percentage-based filter would assert a house rule that does not exist.

### Rule BR-29: The Pre-Dispatch Checks Warn, And Only One Blocks

- The workbench publishes five server-derived checks: `ClientValidated`, `TierActive`, `CoursesSpecified`, `MunicipalityCovered`, `DepositMatched`.
- **Four are advisory** — they warn and never block.
- **One blocks**: `TierActive`. A deactivated tier is not offered, so a booking cannot be created on one.
- The headcount floor (`Package.MinPax`) is reported as an **advisory note** beside the quote, not as a check and not as a block — BR-16 makes it a floor on the contract, not a charge.
- A coverage verdict is `null` (unknown) when the tier has **no** coverage set — never a breach.

### Rule BR-30: The Deposit Is Advisory, And The Required Percentage Is Configuration

- The **required deposit percentage** is **configuration** (`Bookings:Deposit:RequiredPercentage`) and is **absent by default**. While it is absent, **no deposit percentage is assumed or implied** (DEC-043), and the minimum deposit question stays **UNRESOLVED** (DEC-009).
- $$\text{RequiredDepositAmount} = \text{RequiredDepositPercentage} \neq null \;?\; \text{Round}\big(\text{NetAmountDue} \times \tfrac{\text{RequiredDepositPercentage}}{100}\big) : null$$
- The **effective deposit** is the required amount when one is configured, otherwise the amount the operator stated, otherwise zero.
- A stored deposit is **advisory**: it records what was agreed. It is **not** a second payment, it never changes the balance, and the balance comes from the payment sum alone (BR-03, BR-10).

### Rule BR-31: The Quote Is A What-If

- `POST /api/bookings/quote` publishes the whole financial picture of a **proposed** booking and **persists nothing**. It changes no price, it writes no row, and it is not a booking total (the BR-20 principle, applied to the booking).
- The quote's figures are computed by the **same** code path the create path uses, so a confirmed booking's frozen snapshot equals the quote that priced it.

### Rule BR-32: The Register's Filters And Facets

- The filters are the booking number / occasion / customer / venue **search**, the **status** list, the **tier type** list, the **venue** list, the **payment state** list and an **inclusive** event-date range.
- An **unknown** status or payment-state name is a `400` — the vocabulary is never silently widened.
- The **facet counts** are computed with the facet's own dimension **removed** — the tier counts ignore the tier filter, the venue counts ignore the venue filter, and so on. Every facet answers "how many would I get if I chose this?".
- The totals are over the **whole filtered population**, never over the page.
- A search term is **redacted** in the operational log (BR-21 — `search` is in the query-parameter set).

### Rule BR-33: A Draft Is A Pending Booking

- **Save draft** creates the booking in the **`Pending`** status; **Confirm booking** creates it in **`Confirmed`**. No draft entity and no new lifecycle status are introduced (BR-10).
- A **confirmed** booking may carry an **initial payment**, which is recorded in the **same commit** as the booking — a confirmed booking with an agreed deposit is never a booking without its payment.
- An initial payment larger than the net amount due is **rejected** (BR-05's overpayment prohibition, enforced by DEC-118), and the rejection creates **no** booking and **no** payment row.
- The initial payment's receipt number comes from the **one** receipt-number generator (BR-05's `OR-YYYYMMDD-XXXX` format), shared with the payment ledger.
### Rule BR-34: The Dispatch Schedule Is An Ordering Within The Event's Day

- A booking may carry a **kitchen call time** (`callTime`) and a **pack-out time** (`packoutTime`). Both are a **time of day**, stored in their own nullable columns (DEC-106).
- The schedule is an **ordering**, and it is validated as one:

  $$\text{CallTime} \le \text{EventStartTime} \qquad \land \qquad \text{PackoutTime} \ge \text{EventEndTime}$$

  A value that breaks the ordering is a `400` (BR-11). The kitchen must be called **before** the programme starts, and the floor must be packed out **after** it ends.
- The rule is **unambiguous** because the model already forbids the case that would make a time of day ambiguous: `Booking.Create` rejects `endDate < startDate`, so an **overnight event cannot exist** and `EventStartTime` / `EventEndTime` are always same-day times of day.
- The rule is **not** a "same day" rule — the columns are a time of day, and the check is an ordering. A call time of `02:00` and a pack-out time of `23:00` around a `17:00`-``22:00` programme are both valid.
- Both times are **optional**. A booking without them is a booking without a dispatch schedule, not an invalid one.
- The rule is **ratified by AGENT 01** (2026-10-22) after the backend delivery, and it is pinned by a test on both the validator and the command path.
---

## 13. Booking Dish Selection & the Menu-Change Window (Rules BR-35 .. BR-37)

> These rules govern the **per-booking dish selection** — REQ-BKG-06. They complete the open half
> of REQ-BKG-02 ("dish selections"). Specification: `business/bookings-dish-selection-feature-spec.md` v1.0.

### Rule BR-35: A Booking Carries Its Own Dish Selection

- A booking may carry a **dish selection**: the dishes chosen for **that event**, drawn from the **tier's** approved qualified pools, one group per course.
- The selection is **recorded on the booking** — it is not a document-only artefact. It is stored in the `BookingMenu` junction (`trn_Id`, `menuId`, `courseId`).
- The selection is validated by the tier's **own** rules — **BR-15**:
  - for **every** course rule of the tier, the number of chosen dishes in that course is **exactly** `RequiredQuota`;
  - **no** chosen dish belongs to a course the tier has **no rule** for;
  - every chosen dish is a **live** member of that course's **qualified pool**.
- A selection that breaks any of these is a **`400`** that names the course and the quota (BR-11). The tier's ruleset is read **live** at the moment of the write.
- The **tier's ruleset is the ceiling**: a client may choose *within* the approved pools and **never outside** them. The menu is personalised; the **standard** is not.
- A selection is **replaced**, never patched: the dishes no longer chosen are **soft-deleted**, never destroyed, and re-choosing one **resurrects** its row rather than inserting a second one (the DEC-085 pattern).
- The selection carries **no money** and **no frozen dish name, price or food cost** - the dish is a **catalogue reference** and the catalogue owns those.
- A selection row may carry a **prep instruction**: free text of at most **500** characters, belonging to **that event** and **that dish** (DEC-146). It is stored **on the selection row**, never on the catalogue, and it is frozen with the selection. Because the selection is a **full replace**, an **omitted** instruction **clears** it (DEC-155) - the client always sends the instruction it is showing, so a chef's note is never wiped by a save that did not carry it.

### Rule BR-36: The Menu-Change Window

- A booking's selection may be changed **after** the booking is confirmed and **after** the deposit is taken, up to a **menu-change window** measured from the event date:

  $$\text{LockDate} = \text{EventDate} - \text{LockDays}$$

- `LockDays` is **deployment configuration** (`Bookings:MenuSelection:LockDaysBeforeEvent`), **absent by default**. Absent, the lock date is the **event date** — the selection is editable until the event begins.
- The **lock date is always published** and is **never `null`**: the operator always sees the deadline. The **days** figure is `null` when the configuration is absent — the "not stated" state belongs to the *number of days*, never to the *date*.
- A write **after** the lock is refused with a **`400`** that **names the lock date**, and **nothing** is written.
- A **cancelled** booking's selection is frozen outright.
- Before the lock, an **explicitly empty** selection **clears** the selection — the event reverts to the tier's standard run. An **absent or `null`** selection member is a **`400`**, never an empty set (the DEC-085 pattern).
- The window is the guard. There is no separate kitchen role: any authenticated user may change a selection **within** the window.

### Rule BR-37: A Selection Never Moves The Money

- Writing a selection **must not touch** any money column: not `PricePerHead`, not `PackageAmount`, not `Subtotal`, not `DiscountAmount`, not `TaxAmount`, not `AmountDue`, not `DepositAmount`, not `AmountPaid`.
- The tier's per-pax rate is frozen at creation (BR-04). The price is the price of the **tier**; the dishes are an **operational override inside the tier's approved pools**. The owner ruled this on 2026-10-22.
- **The consequence, stated plainly.** A premium dish may be swapped for a cheaper one, and the **contract value does not move** while the **food cost does**. The **margin of that event** therefore moves with the selection and the price does not. The business carries that risk (RSK-062).
- A booking with **no** selection is served the tier's **standard run** (BR-19). The selection is an **override, not a requirement**: a booking may be confirmed, deposited and servable before any dish is chosen.
- The register's per-booking planning estimate is the **selection's** when the booking has one and the **tier's standard run** when it does not, and the card's label says which. Both are **planning estimates** — the dish food costs are manually entered decimals (DEC-062) and the recipe engine is unbuilt (REQ-MNU-03) — and **neither** enters a booking total.
---

## 14. The Bookings & Events Operation - Automatic Discounts, Add-Ons and the Extended Location (Rules BR-38 .. BR-41)

> These rules come from the owner's operational note of 2026-10-22 (`Bookings and Events Operation.txt`)
> and complete the Bookings & Events operation. Specification: `business/bookings-events-operations-feature-spec.md` v1.0.

### Rule BR-38: The Headcount-Band Automatic Discounts

- A booking earns an **automatic discount** by its **booked headcount** (`Booking.NumberOfHeads`, never a forecast):

| Heads | The automatic discount |
| :--- | :--- |
| `< 80` | none |
| `80 … 89` | **₱10.00 per head** - a fixed discount of `NumberOfHeads x 10` |
| `90 … 99` | **none** - the negotiated band |
| `100 … 999` | **₱20.00 per head** - a fixed discount of `NumberOfHeads x 20` |
| `>= 1000` | **none** |

- The bands are **half-open**, and the **90-99 gap is real** (DEC-136): a booking in it earns nothing automatically and is handled by an explicit discount code.
- The discount is a **fixed amount** charged against the **gross** and **capped at the gross** (BR-02). It is computed by the **server**, **frozen** on the booking (BR-04), and published on the quote.
- **One discount applies.** The automatic band discount is the booking's discount when the band qualifies and **no** explicit code was given; an **explicit discount code replaces it** (DEC-138). The two never stack.
- The band discount is a **rule**, not a row in the `Discount` catalogue (DEC-137).

### Rule BR-39: The Add-On Master And The Booking's Add-On Lines

- An **add-on master** carries two levels: an **`AddonCategory`** (`CatId`, the category name) and the **add-on** itself (`Id`, `AddonCategoryId`, the description, the amount, the **unit of measure**, and the **pricing basis**).
- The **pricing basis** is an **explicit** field - `Flat` or `PerGuest` - and it is **never inferred from the unit of measure** (DEC-158). The unit stays a **descriptive** unit. A basis that is **not stated** is treated as `Flat`, the safe direction, because it never multiplies a price that was not meant to be multiplied.
- A booking may carry **add-on lines** beyond its tier. Each line **freezes** the **description**, the **line total**, the **unit** and the **pricing basis** (DEC-139); the catalogue's current price never moves a stored line (BR-04).
- The line's **line total** is its commercial truth and is what enters the booking's gross (DEC-135, DEC-140, DEC-158). No quantity is stored separately, so the amount and a quantity can never disagree: for a `PerGuest` add-on the headcount **is** the quantity and it is expressed **within** the line.
- The lines are **replaced**, never patched: the lines no longer carried are **soft-deleted**, and re-adding one **resurrects** its row (the DEC-085 pattern).
- A booking's add-ons are subject to the same **menu-change window** as its dish selection (BR-36): after the lock date they are frozen.

### Rule BR-40: The Extended-Location Amount Is Limited To Two Tier Types

- The out-of-town amount applies **only** to the **`Regular`** and **`Premier`** tiers (DEC-141).
- **The test is pinned** (DEC-156): the tier's `PackageType` is **trimmed** and compared **case-insensitively** for **equality** with `Regular` or `Premier` **verbatim**. Every other stored value - including a type such as "Premier Buffet" - is **not** a surcharge tier. The comparison is on the **type column only**, never on the description: the development catalogue's own tier is typed **`Regular`** while its *description* reads "2026 Premier Package B", so a description match would give the opposite answer.
- The catalogue's type values should be normalised to the `PackageType` enum's vocabulary (DEC-070). Until they are, the behaviour is deterministic and the remedy is data hygiene, not a code change.
- In the workbench, the **"filter by Package Area"** control appears **only** for those two types. For any other type the tiers are filtered by the **type** alone, and the area filter does not appear.
- The rule is enforced **server-side** as well as in the interface: applying a surcharge to a tier of any other type is a **`400`** that **names the two eligible types**, so the operator is not left guessing. A rule that is only hidden is not a rule.
- The area filter has **server support**: `GET /api/packages?areaId=` returns the active tiers whose coverage includes that area (DEC-157). The browser never fans out one request per tier.
- The surcharge is still **the municipality's single rate** (BR-17, DEC-078) and is still **frozen** on the booking (BR-26, DEC-116).

### Rule BR-41: An Add-On Is A Charge; A Dish Is A Substitution

- **A dish selection never moves the money.** Swapping one dish for another inside the tier the client already bought is a **substitution**, and the tier's per-pax rate is frozen (BR-04). The owner's ruling of 2026-10-22 covers exactly this.
- **An add-on is a charge.** It is a new item the client buys, and it **joins the gross** as BR-01's `AddonsTotal` term - the term that has existed in the formula, and in the calculator's argument set, since the beginning, and that has been `0m` only because nothing could create one (RSK-002). This cycle makes it reachable for the first time (DEC-135).
- The **frozen** line amount is what is added, so the booking's total moves only when the operator **adds or removes an add-on** - never when the catalogue's price changes (BR-39, BR-04).
- The band discount and the add-on total are therefore **both** measured against the gross, and the fixed discount is capped at it (BR-02, BR-38).
### Rule BR-42: The Tax Is A VAT-Inclusive Disclosure, And Its Rate Is Configuration

- The **tax policy is UNRESOLVED** (DEC-008) and this cycle does **not** resolve it by inventing a rate. The menu-selection design's ledger prints a **"Standard 12% EVAT Included"** line of **12,889.29** against a total of **120,300.00**, and the arithmetic is conclusive: 120,300 / 1.12 = 107,410.71, and 120,300 - 107,410.71 = **12,889.29** - the figure the design prints.
- The price the client pays is therefore **VAT-inclusive**, and the VAT is a **memo line inside** the total, **never** an addition to it:

  $$\text{IncludedVatAmount} = \text{NetAmountDue} - \frac{\text{NetAmountDue}}{1 + \frac{\text{VatPercentage}}{100}}$$

- The **rate** is **deployment configuration** (`Bookings:Tax:VatPercentage`), **absent by default** - the same discipline as the required deposit percentage (BR-30, DEC-101) and the commissary capacity (DEC-102). With the key absent the figure is **`null`** and the ledger line is **absent**, never "0.00"; with the key set the line reads the **configured** rate and **12 is never hardcoded**.
- The tax does **not** change `NetAmountDue`. The amount due is the gross less the discount, and the VAT is disclosed within it - which keeps BR-25's single net-due path and DEC-099's one-source guarantee intact.
- A **null** VAT figure means the rate is **not stated**, never that the VAT is zero.

### Rule BR-43: An Add-On Write Re-Freezes The Money

- An add-on write **re-prices** the booking. Unlike a dish selection, it **moves** the contract value, and the whole money reaction is specified so that no implementer has to guess (DEC-152).
- The add-on total is **recomputed** and the snapshot is **re-frozen**: `Subtotal`, `DiscountAmount` and `AmountDue` are written again by `RecalculateFinancials`. BR-04 already anticipates this - it freezes "at the moment of booking creation **or line-item addition**".
- The **discount amount is not recomputed**. The headcount band depends on `NumberOfHeads`, which the write did not change, and an explicit code's amount is fixed; the **frozen** discount is only **re-capped** at the new gross (BR-02).
- The **deposit is not changed** by the write. It is advisory (BR-30) and the operator may restate it separately.
- A write that would leave the **collected amount above the new net amount due** is **refused** with a **`400`** and **nothing** is written. BR-05 forbids a payment above the net due, and a re-price that creates such a position must be settled by the operator first - a refund, a credit, or a restated deposit - not silently absorbed.
- A **dish** selection still **never** moves the money (BR-37, DEC-129). The two rules are deliberately different, and the difference is the whole point of BR-41.

### Rule BR-44: The Discount's Source Is Derived, And Published

- A booking's discount has a **source**, and it is **derived**, never stored (DEC-154):
  - a **discount code** was applied -> **`Code`**;
  - otherwise the **headcount band** qualified -> **`HeadcountBand`**;
  - otherwise -> **`None`**.
- The derivation is **exact**, not a heuristic: the headcount band is the **only** source of a fixed discount that carries no code (DEC-137, DEC-138).
- The source is **published** on the register item and in the register summary, so that KPI-42 is **server-computed** rather than inferred in the browser, and so that the operator can see **which** rule produced the number (DEC-160).
- The band that applied publishes its **label** and its **rate per head**; the client composes its wording from them and never hardcodes a bound (DEC-160).

---

## 15. Department, Package Code and Extended Location (added by the 2026-10-24 owner request)

### Rule BR-45: The Package Code Is Generated Once and Frozen

- The human-readable business identifier is `PKG-{TYPE}-{NN}`, e.g. `PKG-REG-01` (DEC-162).
- The type abbreviation is fixed for the six known types — `Regular` → `REG`, `Wedding` → `WED`, `Vip` → `VIP`, `Premier` → `PRE`, `Pm` → `PM`, `Sd` → `SD`. Any other free-text type (DEC-070 permits one) uses its first three alphanumeric characters, upper-cased and padded with `X`.
- `NN` is a **per-type** sequence, padded to at least two digits (`01` … `99`, then `100`). The sequence is derived from every code the catalogue holds, including the soft-deleted rows, so a number is never reused.
- The code is assigned **once, at create**, and is **frozen**: editing the type or the description never renumbers a tier. `PackageId` remains the internal key the three foreign keys reference.
- The code is unique among the **live** tiers (`IX_Package_p_code`, filtered `is_deleted = 0`). The small race two concurrent creates can still lose is the same documented risk the receipt-number generator carries (RSK-013).

### Rule BR-46: The Extended Location Amount Is a Regular/Premier Per-Guest Term

- `Package.ExtendedLocationAmount` is the tier's **own per-guest** extended-location amount (DEC-163). `NULL` means **not stated**, never zero.
- It is available **only** for the `Regular` and `Premier` tier types. The eligibility test is the same pinned test as BR-40: the stored type column, trimmed, compared **case-insensitively** for **equality**; `Premier Buffet` is not eligible.
- A payload that supplies an amount on any other type is a **`400`**, and nothing is written. When an existing tier's type changes away from `Regular`/`Premier`, the stored amount is **cleared**.
- The amount is never negative.

### Rule BR-47: The Extended Location Amount Joins the Booking Surcharge

- When the operator's `ApplyExtendedAmount` switch is **on**, a booking's frozen `ExtendedAreaAmount` is:
  - `(Area.AdditionalRate + Package.ExtendedLocationAmount) × NumberOfHeads` when a municipality is selected;
  - `Package.ExtendedLocationAmount × NumberOfHeads` when no municipality is selected;
  - an absent component contributes **zero** (DEC-086), while its published rate stays `null`.
- When the switch is **off**, both components contribute zero.
- The combined amount is frozen into the booking and reproduced by the shared calculator; a later change to the tier or the municipality never moves a stored booking (BR-04, DEC-099).
- The quote and the create path call the same `BookingPricingService`, so the quoted figure and the frozen figure cannot diverge (BR-31).

### Rule BR-48: Currency Inputs Are Formatted, Not Symbolised

- Every **money** input renders two decimals and thousands separators while unfocused (`1,425.00`) and the raw value while it is being edited. The **peso symbol is not shown inside an editable field**; readouts keep it (`₱1,425.00`).
- The form state stays raw, so a comma never reaches a parser. Counts (headcount, quota, portions, percentages) remain plain numbers.
- The client never derives a monetary figure from a formatted string; the server remains the authority for every total (DEC-002).
