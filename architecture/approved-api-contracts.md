# Sal's CaterSuite — Approved API Contracts

Base URL: `/api`  
Security: Bearer JWT (`Authorization: Bearer <token>`)

---

## 1. Bookings Endpoints (`/api/bookings`)

> **Authorization:** any authenticated user, except `POST /{id}/cancel` which requires the `Bookings.Cancel` policy (Administrator, Manager) — see §6.

### `GET /api/bookings/{id}`
- **Description:** Retrieve full booking details, customer header, package, schedule, pricing breakdown and lifecycle status. All financial values are server-calculated and authoritative.
- **Response `200 OK`:**
```json
{
  "transactionId": 101,
  "bookingNumber": "BK-20261018-00101",
  "transactionDate": "2026-10-18T10:00:00Z",
  "bookType": "Inside",
  "typeOfService": "Catering",
  "customerId": 14,
  "customerName": "Santos, Maria",
  "customerContact": "09171234567",
  "occasion": "Silver Wedding Anniversary",
  "venue": "Grand Ballroom, Hotel Sal",
  "eventDate": "2026-11-20",
  "eventEndDate": "2026-11-20",
  "eventStartTime": "17:00:00",
  "eventEndTime": "22:00:00",
  "callTime": "14:00:00",
  "packoutTime": "23:00:00",
  "numberOfHeads": 150,
  "eventColor": "#2563eb",
  "packageId": 3,
  "packageName": "Premier Wedding Package",
  "reference": "Referred by Tita Maria",
  "pricePerHead": 500.00,
  "packageAmount": 75000.00,
  "extendedAreaAmount": 6000.00,
  "extendedAreaRatePerGuest": 50.00,
  "subtotal": 81000.00,
  "discountType": "None",
  "discountPercentage": 0.00,
  "discountCode": null,
  "discountAmount": 0.00,
  "taxAmount": 0.00,
  "amountDue": 81000.00,
  "depositAmount": 0.00,
  "amountPaid": 30000.00,
  "balance": 51000.00,
  "status": "Confirmed",
  "paymentState": "DepositPaid",
  "reminderSent": false
}
```
- **Notes:** `eventDate` and `eventEndDate` are dates (`yyyy-MM-dd`); `eventStartTime`/`eventEndTime` are times (`HH:mm:ss`). `status` is the booking lifecycle enum and `paymentState` is the separate ledger-derived payment state (BR-10/BR-28, DEC-177). `eventEndDate` preserves overnight schedules. `amountPaid` and `balance` must reconcile with the ledger endpoint (§2).
- **The dispatch schedule (DEC-106, BR-34).** `callTime` is the kitchen call time and `packoutTime` the pack-out time. Both are a **time of day** and both are **frozen** on the booking; each is `null` when it was not stated. The two are **ordered against the programme** (BR-34): the call time must not be later than `eventStartTime` and the pack-out time must not be earlier than `eventEndTime`.
- **The frozen surcharge (BR-26, DEC-116).** `extendedAreaAmount` is the out-of-town surcharge the booking was created with — **not** the municipality's current rate. A later rate change moves the quoted price of every new booking and never a stored one (BR-04). It is `0` when no surcharge was applied.
- **The surcharge's per-guest rate (BR-17, BR-26).** `extendedAreaRatePerGuest` is the rate the **frozen** surcharge was derived from — the frozen amount divided by the headcount, which recovers the municipality's rate exactly, because a two-decimal rate times an integer headcount is exact at two decimals. It is published so a reader of the printed contract can see **how** the surcharge was derived (rate × pax). **The division is verified before the rate is published:** if the recovered rate does **not** reconstruct the frozen amount exactly at two decimals, the rate is **`null`** — a rounded rate that does not tie out would put an arithmetic error on a **signed** document, and the document then prints the amount and the headcount **without** a rate. It is also `null` when no surcharge was applied — "not stated" is null, never zero (BR-20).
- **The frozen discount code (DEC-106, BR-02).** `discountCode` is the code of the discount that was applied; it is `null` when no discount was applied.
- **This endpoint and the register (contract §14.1) describe the same booking the same way.** The net amount due, the collected amount, the outstanding balance, the surcharge, the deposit and the status are identical on both.

### `POST /api/bookings`
- **Description:** Create a new catering booking contract. The price snapshot (price per head, package amount) is frozen at creation (BR-04).
- **Authorization:** any authenticated user.
- **Request Body** (extended by REQ-BKG-05; contract section 14.4 carries the full description):
```json
{
  "customerId": 14,
  "bookType": "Inside",
  "typeOfService": "Catering",
  "noOfPerson": 150,
  "occasion": "Silver Wedding Anniversary",
  "venue": "Grand Ballroom, Hotel Sal",
  "startDate": "2026-11-20T17:00:00+08:00",
  "endDate": "2026-11-20T22:00:00+08:00",
  "eventColor": "#2563eb",
  "packageId": 3,
  "reference": "Referred by Tita Maria",
  "extendedAreaId": null,
  "applyExtendedAmount": false,
  "specialInstructions": null,
  "overridePricePerHead": null,
  "callTime": null,
  "packoutTime": null,
  "discountCode": null,
  "depositAmount": null,
  "confirm": true,
  "initialPayment": {
    "amountPaid": 30000.00,
    "paymentMeans": "Check",
    "checkNo": "CK-2026-88412",
    "notes": null,
    "particular": "Downpayment"
  }
}
```
- **Response `201 Created`** (with `Location` header pointing at `GET /api/bookings/{id}`):
```json
{
  "transactionId": 101,
  "bookingNumber": "BK-20261120-00101",
  "status": "Confirmed",
  "paymentState": "PendingPayment"
}
```
- **Response `400 Bad Request`:** `{ "errors": ["Number of persons must be greater than 0."] }` - validation and domain rule violations (BR-11).
- The `201` body is the named `CreateBookingResponseDto`, declared on the action so the OpenAPI document is schema-accurate. It publishes booking number, lifecycle `status`, and separate ledger-derived `paymentState` (DEC-177); the `transactionId` field is unchanged.
- **The authorization is two-level.** Creating a **draft** needs only an authenticated user; creating a **confirmed** booking that carries an `initialPayment` additionally requires the `Payments.Record` policy (Administrator, Manager, Cashier - BR-12). A confirmed booking **without** an initial payment needs only an authenticated user. The server enforces the second level; the client only hides what the server refuses.

### `POST /api/bookings/{id}/cancel`
- **Description:** Cancel an unserved booking and, when a refund amount is supplied, record the refund.
- **Authorization:** `Bookings.Cancel` policy — Administrator or Manager.
- **Request Body:**
```json
{
  "reason": "Client relocated overseas",
  "refundAmount": 15000.00
}
```
- **Response `200 OK`:** `{ "message": "Booking cancelled successfully." }`
- **Response `400 Bad Request`:** `{ "errors": ["Booking is already cancelled."] }`
- **Response `403 Forbidden`:** authenticated user without the `Bookings.Cancel` policy.
- **Notes:** the route `{id}` is the booking `transactionId`. Cancellation sets `status = "Cancelled"`; a refund is only created when `refundAmount > 0`. Refund payouts are reported separately and do not reduce `balanceDue` (BR-10 / DEC-013).

---

## 2. Payments & Ledger Endpoints (`/api/payments`)

### `POST /api/payments`
- **Description:** Record an incoming payment against a booking.
- **Request Body:**
```json
{
  "transactionId": 101,
  "particular": "Initial 50% Downpayment",
  "amountPaid": 30000.00,
  "paymentMeans": "GCash",
  "checkNo": null,
  "notes": "Ref: 90283401923"
}
```
- **Response `200 OK`:**
```json
{
  "paymentNo": "OR-20261018-0001",
  "transactionId": 101,
  "dateOfPayment": "2026-10-18T10:15:00Z",
  "particular": "Initial 50% Downpayment",
  "amountPaid": 30000.00,
  "paymentMeans": "GCash",
  "createdByUser": "cashier_ana"
}
```

### `GET /api/payments/ledger/{transactionId}`
- **Description:** Get authoritative payment history and current balance due.
- **Response `200 OK`:**
```json
{
  "transactionId": 101,
  "customerName": "Juan Dela Cruz",
  "occasion": "Silver Wedding Anniversary",
  "eventDate": "2026-11-20T17:00:00Z",
  "contractAmount": 80000.00,
  "totalPaid": 30000.00,
  "totalRefunded": 0.00,
  "balanceDue": 46000.00,
  "isFullyPaid": false,
  "payments": [
    {
      "paymentNo": "OR-20261018-0001",
      "dateOfPayment": "2026-10-18T10:15:00Z",
      "particular": "Initial 50% Downpayment",
      "amountPaid": 30000.00,
      "paymentMeans": "GCash"
    }
  ]
}
```

---

## 3. Menus Endpoints (`/api/menus`)

### `GET /api/menus`
- **Query Params:** `?courseId=1`
- **Response `200 OK`:** List of menu items with `menuId`, `menuName`, `price`, `foodCost`, `courseName`.

### `GET /api/menus/{id}/costing`
- **Response `200 OK`:**
```json
{
  "menuId": "BEEF-01",
  "menuName": "Roast Beef with Mushroom Gravy",
  "courseName": "Beef Entree",
  "price": 350.00,
  "foodCost": 125.00,
  "contributionMargin": 225.00,
  "foodCostPercentage": 35.71
}
```

---

## 4. Calendar Feed (`/api/calendar/events`)
- **Query Params:** `?start=2026-11-01&end=2026-11-30`
- **Response `200 OK`:** List of calendar items matching FullCalendar event contract. Each item publishes lifecycle `status` and separate `paymentState`; event start/end instants use persisted `EventDate`/`EventEndDate` and their time-of-day values so overnight events span the correct dates.

---

## 5. Booking Lifecycle Status Contract (BR-10, DEC-177)

`BookingDto.status` is the single **event lifecycle** field. Payment progress is separate in `paymentState`; payment recording never overwrites `status`. `DepositPaid` and `FullyPaid` are payment-state values, not `BookingStatus` values. The legacy `serveStatus` / `isCancelled` booleans are retired and must not appear in any contract.

| Value | Meaning |
| :--- | :--- |
| `Pending` | Created, not yet confirmed |
| `Confirmed` | Confirmed contract |
| `InProgress` | Event being served |
| `Completed` | Event fully served |
| `Cancelled` | Cancelled (terminal) |
| `NoShow` | Client/event no-show |
| `Rescheduled` | Moved to a new date |
| `Expired` | Lapsed without confirmation |

### 5.1 Payment state (BR-28, DEC-177)

`paymentState` is a separate projection from the authoritative non-soft-deleted payment sum and the shared net-due calculation. It is not persisted as a second financial truth and is never changed by BG-04.

| Value | Meaning |
| :--- | :--- |
| `PendingPayment` | No payment collected while positive net amount due remains. |
| `DepositPaid` | A first payment has been recorded, with a balance still outstanding; later partial installments remain in this state. |
| `FullyPaid` | Collected payments meet/exceed net amount due. Zero-value bookings retain the existing settled classification. |

The machine values travel as strings. The register filter `paymentState` accepts these three values; old values `AwaitingDeposit`, `PartiallyPaid`, and `Settled` are retired. The deposit status does not assert a deposit percentage or threshold.

---

## 6. Authorization Contract

All endpoints require a bearer JWT. Roles are carried as role claims in the token.

| Policy | Applies to | Permitted Roles | Status |
| :--- | :--- | :--- | :--- |
| `Bookings.Cancel` | `POST /api/bookings/{id}/cancel` | Administrator, Manager | DEC-017 PROPOSED |
| `Payments.Record` | `POST /api/payments` | Administrator, Manager, Cashier | DEC-017 PROPOSED |
| *(authenticated)* | all other business endpoints | any authenticated user | — |

- `401 Unauthorized` — no/invalid/expired token. Body: `{ "errors": ["Authentication is required."] }`
- `403 Forbidden` — valid token, insufficient role. Body: `{ "errors": ["You are not authorized to perform this action."] }`
- Authorization is enforced by the API; the frontend must not rely on hidden controls.

---

## 7. Error Contract

Business and validation failures return a consistent shape:

```json
{ "errors": ["Booking transaction not found."] }
```

| Status | When |
| :--- | :--- |
| `400` | Request validation failure or domain rule violation (e.g. invalid pax, end date before start date, already-cancelled booking) |
| `401` | Missing/invalid token — `{ "errors": ["Authentication is required."] }` |
| `403` | Authenticated but not permitted for the policy — `{ "errors": ["You are not authorized to perform this action."] }` |
| `404` | Resource not found (details/ledger endpoints) |
| `409` | Duplicate business key (e.g. existing menu id) |
| `500` | Unexpected server fault — returned as a **ProblemDetails** body. Must never be used to report a business rule violation |

- Unhandled unexpected faults return a standard `ProblemDetails` payload (via the exception-handler middleware); `DomainException` rule violations are intercepted and returned as `400 { "errors": [...] }` (DEC-014, DEC-026).
- Business/validation failures always use the `errors` array; clients should branch on the status code, not on message text.
- **Known deviation — `POST /api/Auth/login`:** the login endpoint returns `{ "message": "..." }` for its `400` and `401` responses instead of the `{ "errors": [...] }` shape, and its `200` body is an anonymous object (`{ token, userId, userName, fullName, roles[] }`) with no OpenAPI schema. This is pre-existing behaviour, not a contract change. **Clients must normalise both envelopes** — the dashboard frontend does this in its axios response interceptor.

---

## 8. JSON Serialization Convention (IMPLEMENTED)

**Convention:** enum values are exchanged as **string names** (e.g. `"bookType": "Inside"`, `"typeOfService": "Catering"`, `"paymentMeans": "GCash"`, `"status": "Confirmed"`). Lifecycle `status` and ledger-derived `paymentState` are separate fields.

- A global `JsonStringEnumConverter` is registered for the MVC pipeline (single source of truth: `WebApi/Serialization/ApiJsonSerializerOptions.cs`), so enums serialize as names in responses and enum names are accepted in requests.
- Numeric enum input remains accepted for backward compatibility.
- Because enums are string-based on the wire, adding/reordering enum members no longer silently changes client meaning.

---

## 9. Dashboard Endpoint (`/api/dashboard`)

> **Specification:** `business/dashboard-feature-spec.md` (v1.0) — REQ-DSH-01.
> **Status:** APPROVED for implementation, 2026-10-19.

### `GET /api/dashboard`
- **Description:** Single read-only aggregate of the operations dashboard. Every figure is computed
  server-side from authoritative records. The client must never derive a financial figure from this
  payload (DEC-002).
- **Route note:** the controller uses `[Route("api/[controller]")]`, so the literal route segment is
  `Dashboard` (`/api/Dashboard`). ASP.NET routing is case-insensitive, so `/api/dashboard` — the form
  used throughout this document — resolves identically.
- **Authorization:** any authenticated user. Read-only, so no role policy applies.
- **Query Params:** `period` — optional, `YYYY-MM`. Defaults to the current UTC calendar month.
- **Response `200 OK`:**
```json
{
  "period": {
    "year": 2026,
    "month": 10,
    "label": "October 2026",
    "startDate": "2026-10-01",
    "endDate": "2026-10-31"
  },
  "benchmarks": {
    "targetFoodCostPercentage": 35.00,
    "targetMarginPercentage": 65.00,
    "isConfigured": true
  },
  "bookings": {
    "totalCount": 24,
    "committedCount": 18,
    "tentativeCount": 6,
    "otherCount": 0,
    "contractedValue": 1425800.00,
    "totalPax": 4200
  },
  "revenue": {
    "netContractedRevenue": 1425800.00,
    "averageBookingValue": 59408.33,
    "bookingCount": 24
  },
  "collections": {
    "amount": 620000.00,
    "receiptCount": 12,
    "previousPeriodAmount": 541000.00,
    "changePercentage": 14.60,
    "collectionRatioPercentage": 43.49
  },
  "receivables": {
    "outstandingAmount": 342500.00,
    "bookingCount": 8,
    "overdueCount": 3,
    "overdueAmount": 250000.00,
    "agingBuckets": [
      { "key": "NotYetDue",   "label": "Not yet due", "bookingCount": 2, "amount": 92500.00 },
      { "key": "Days1To30",   "label": "1-30 days",   "bookingCount": 3, "amount": 50000.00 },
      { "key": "Days31To60",  "label": "31-60 days",  "bookingCount": 2, "amount": 80000.00 },
      { "key": "Days61To90",  "label": "61-90 days",  "bookingCount": 1, "amount": 30000.00 },
      { "key": "Over90Days",  "label": "90+ days",    "bookingCount": 1, "amount": 70000.00 }
    ]
  },
  "dishCosting": {
    "totalMenuCount": 181,
    "costedMenuCount": 142,
    "uncostedMenuCount": 39,
    "coveragePercentage": 78.45
  },
  "margin": {
    "catalogueRevenueAmount": 1250000.00,
    "catalogueFoodCostAmount": 548750.00,
    "catalogueMarginAmount": 701250.00,
    "catalogueMarginPercentage": 56.10,
    "varianceToBenchmarkPercentage": -8.90
  },
  "foodCostByCategory": [
    { "courseId": 1, "courseName": "Beef Entree", "foodCostAmount": 230625.00, "sharePercentage": 42.03, "menuCount": 12 }
  ],
  "upcomingBookings": [
    {
      "transactionId": 101,
      "bookingNumber": "BK-20261018-00101",
      "customerName": "Santos, Maria",
      "occasion": "Silver Wedding Anniversary",
      "eventDate": "2026-10-28",
      "eventEndDate": "2026-10-28",
      "numberOfHeads": 250,
      "status": "Confirmed",
      "paymentState": "DepositPaid",
      "packageName": "Premier Wedding Package",
      "netAmountDue": 285000.00,
      "amountPaid": 200000.00,
      "balance": 85000.00
    }
  ],
  "ledger": {
    "collectedLast7DaysAmount": 620000.00,
    "collectedLast7DaysReceiptCount": 12,
    "awaitingDepositBookingCount": 5,
    "awaitingDepositAmount": 184000.00,
    "receiptsIssuedCount": 16
  },
  "generatedAt": "2026-10-19T10:00:00Z"
}
```
- **Response `400 Bad Request`:** `{ "errors": ["Period must be in the format YYYY-MM."] }` — malformed `period`.
- **Response `401 Unauthorized`:** `{ "errors": ["Authentication is required."] }`
- **Response `500`:** standard `ProblemDetails` body (contract §7).

### 9.1 Field semantics and formulas

| Field | Formula / rule | KPI |
| :--- | :--- | :--- |
| `period` | The resolved reporting period. The **server** computes the boundaries and returns them; the client never recomputes them. | — |
| `benchmarks.targetFoodCostPercentage` | `Dashboard:Benchmarks:TargetFoodCostPercentage` from configuration; default `35.0` (BR-07). | — |
| `benchmarks.targetMarginPercentage` | `100 - targetFoodCostPercentage`. | — |
| `benchmarks.isConfigured` | `false` when the configuration key is absent and the default was substituted. | — |
| `bookings.totalCount` | Bookings with `EventDate` in the period and `Status ≠ {Cancelled, Expired}`. | KPI-07 |
| `bookings.committedCount` | Of those, `Status == Confirmed`; lifecycle status is no longer payment-derived (DEC-177). | KPI-07 |
| `bookings.tentativeCount` | Of those, `Status == Pending`. | KPI-07 |
| `bookings.otherCount` | Of those, `Status ∈ {InProgress, Completed, NoShow, Rescheduled}`. | KPI-07 |
| `bookings.contractedValue` | `S NetAmountDue` over the same population. | KPI-07 |
| `bookings.totalPax` | `S NumberOfHeads` over the same population. | KPI-07 |
| `revenue.netContractedRevenue` | `S NetAmountDue` over the same population. | KPI-01 / KPI-08 |
| `revenue.bookingCount` | The population size. | KPI-01 |
| `revenue.averageBookingValue` | `netContractedRevenue ÷ bookingCount`; `null` when `bookingCount = 0`. | KPI-04 / KPI-09 |
| `collections.amount` | `S Payment.AmountPaid` where `DateOfPayment` falls in the period. | KPI-02 / KPI-10 |
| `collections.receiptCount` | Count of those payments. | KPI-02 |
| `collections.previousPeriodAmount` | `S Payment.AmountPaid` over the immediately preceding period of equal length. | KPI-02 |
| `collections.changePercentage` | `(amount - previousPeriodAmount) ÷ previousPeriodAmount × 100`; `null` when `previousPeriodAmount = 0`. | — |
| `collections.collectionRatioPercentage` | `amount ÷ netContractedRevenue × 100`; `null` when `netContractedRevenue = 0`. **Indicative only** — the numerator is cash-basis, the denominator accrual-basis. | KPI-11 |
| `receivables.outstandingAmount` | `S max(0, NetAmountDue - S payments)` over all `Status ≠ Cancelled` bookings. | KPI-12 |
| `receivables.bookingCount` | Number of bookings contributing a positive balance. | KPI-12 |
| `receivables.overdueCount` | Bookings in the `Days31To60`, `Days61To90` and `Over90Days` buckets. | KPI-13 |
| `receivables.overdueAmount` | The sum of the amounts in those three buckets. | KPI-13 |
| `receivables.agingBuckets` | Buckets by days past the **event date** (the due date). The five amounts sum exactly to `outstandingAmount`. | KPI-13 |
| `dishCosting.totalMenuCount` | Non-deleted menus. | KPI-14 |
| `dishCosting.costedMenuCount` | Of those, `FoodCost.HasValue && FoodCost > 0`. | KPI-14 |
| `dishCosting.uncostedMenuCount` | `totalMenuCount - costedMenuCount`. | KPI-14 |
| `dishCosting.coveragePercentage` | `costedMenuCount ÷ totalMenuCount × 100`; `0` when `totalMenuCount = 0`. | KPI-14 |
| `margin.catalogueRevenueAmount` | `S Price` over menus with `Price > 0` and `FoodCost > 0`. | KPI-15 |
| `margin.catalogueFoodCostAmount` | `S FoodCost` over the same population. | KPI-15 |
| `margin.catalogueMarginAmount` | `catalogueRevenueAmount - catalogueFoodCostAmount`. | KPI-15 |
| `margin.catalogueMarginPercentage` | `catalogueMarginAmount ÷ catalogueRevenueAmount × 100`; `null` when `catalogueRevenueAmount = 0`. | KPI-15 |
| `margin.varianceToBenchmarkPercentage` | `catalogueMarginPercentage - targetMarginPercentage`; `null` when the margin is `null`. | KPI-15 |
| `foodCostByCategory[]` | One row per course with at least one costed menu: `foodCostAmount = S FoodCost`, `sharePercentage = foodCostAmount ÷ S all course food cost × 100`. Ordered by `foodCostAmount` descending. Menus with a null `CourseId` are grouped under `courseName = "Uncategorised"` with a `null` `courseId`. | KPI-16 |
| `upcomingBookings[]` | Up to 5 bookings with `EventDate = today`, `Status ≠ {Cancelled, Expired}`, ordered by `EventDate` ascending, then `TransactionId` ascending. | — |
| `upcomingBookings[].status` | The lifecycle-only `BookingStatus` name (BR-10, DEC-177). | — |
| `upcomingBookings[].paymentState` | The separate BR-28 payment state (`PendingPayment`, `DepositPaid`, `FullyPaid`). | — |
| `upcomingBookings[].netAmountDue` | The same net-due path the ledger uses. | — |
| `upcomingBookings[].amountPaid` | `S Payment.AmountPaid` (authoritative, DEC-012). | — |
| `upcomingBookings[].balance` | `max(0, netAmountDue - amountPaid)`. | — |
| `ledger.collectedLast7DaysAmount` | `S Payment.AmountPaid` where `DateOfPayment ∈ [today - 6 days, today]`. | KPI-17 |
| `ledger.collectedLast7DaysReceiptCount` | Count of those payments. | KPI-17 |
| `ledger.awaitingDepositBookingCount` | Non-cancelled bookings with `S payments = 0`; remains the established no-payment-recorded KPI, independent of the payment-state classifier (DEC-177). | KPI-18 |
| `ledger.awaitingDepositAmount` | `S NetAmountDue` over that population. | KPI-18 |
| `ledger.receiptsIssuedCount` | Count of `Payment` rows whose `DateOfPayment` falls in the active period. | KPI-19 |
| `generatedAt` | Server UTC timestamp of the aggregate. | — |

### 9.2 Authoritative-basis rules binding this endpoint

1. **One net-due path.** `NetAmountDue` is computed with `BookingAmountDueCalculator` using the
   **shared overload** `Calculate(Booking, decimal)`, which reproduces the **frozen snapshot**
   (BR-25, DEC-099). The dashboard therefore reconciles to the cent with the per-booking ledger
   `balanceDue` (DEC-015, DEC-025). **DEC-035 is resolved** (DEC-099): the frozen snapshot is the
   single net-due source and the calculator reproduces it, so the dashboard, the ledger, the booking
   details, the customer statement, the calendar feed and the Bookings & Events Register publish
   **the same figure** for the same booking. The money chain carries **one** rounding - two decimals,
   away from the midpoint (DEC-115) - and the create path stores exactly the rounded values.
2. **Amount paid is the payment sum.** `S Payment.AmountPaid`, never the denormalized
   `Booking.AmountPaid` cache (DEC-012).
3. **Soft-deleted rows are excluded from every dashboard figure** - `Booking`, `Payment` and `Menu`
   alike. This is a **dashboard-scoped** filter; no global query filter is introduced (DEC-038).
   **Extended by DEC-111:** the **payment** exclusion is now applied by every money surface, not by
   the dashboard alone, so the dashboard's AR and the ledger's AR agree by construction.
4. **Cancelled bookings are excluded** from counts, revenue, contracted value and AR.
   `Completed` bookings **with an outstanding balance remain included in AR** (DEC-025).
5. **AR is never reported as collections** (BR-08), and the two are separate fields.
6. **No null-as-zero.** Undefined ratios are `null`, never `0` and never `100`.
7. **The benchmark travels with the payload.** The frontend renders the returned benchmark and never
   hardcodes it. `MenuCostingService` and `MenuCostingSummaryDto.TargetFoodCostPercentage` must read
   the same configured value so the menu screen and the dashboard cannot disagree.


## 10. Customer Master Endpoints (`/api/customers`)

Added by `business/customer-master-feature-spec.md` v1.0 (REQ-CUS-01). All endpoints
require a bearer token; the Customer Master introduces **no new role policy** (BR-12
restricts only cancellation/refund and payment recording, which this feature performs
neither of).

### 10.1 `GET /api/customers/directory` — the Customer Master directory

| Query parameter | Type | Default | Behaviour |
| :--- | :--- | :--- | :--- |
| `search` | string? | — | Case-insensitive substring over `LastName`, `FirstName`, `Company`, `Contact1` (the existing `GET /api/customers` search semantics). |
| `customerType` | string? | — | `Corporate` \| `Private` (case-insensitive). An unrecognised value is **ignored**, not a `400`. Derived per DEC-051 — never persisted. |
| `arState` | string? | — | `Clean` \| `Open` \| `Overdue30` \| `Overdue60` (case-insensitive). An unrecognised value is **ignored**. |
| `page` | int | 1 | Clamped to `>= 1`. |
| `pageSize` | int | 10 | Clamped to `1…100`. |

`200` response:

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
**`quickViewCounts` describes the search-filtered population with the `customerType`
and `arState` filters ignored**, so the quick-view pills always show the population
counts regardless of which scope is active. `generatedAt` is the server UTC timestamp
of the aggregate, so the client never renders a client clock as the data's age.

| Field | Meaning | Authority |
| :--- | :--- | :--- |
| `summary.totalClients` | Count of non-deleted customers in the filtered set. | KPI-20 |
| `summary.activeArAmount` | `S max(0, NetAmountDue - S payments)` over non-cancelled bookings. | KPI-12 / BR-08 / DEC-025 |
| `summary.clientsWithBalance` | Number of customers whose `outstandingBalance` is positive. | KPI-12 |
| `summary.totalContractedValue` | `S NetAmountDue` over non-cancelled bookings. | KPI-21 |
| `summary.averageValuePerClient` | `totalContractedValue ÷ totalClients`; **`null`** when `totalClients = 0`. | KPI-21 |
| `summary.overdueAmount` | AR in the `Days31To60` / `Days61To90` / `Over90Days` buckets. | KPI-13 / DEC-041 |
| `summary.overdueClientCount` | Customers whose worst bucket is one of those three. | KPI-13 |
| `items[].totalContractedValue` / `totalPaid` / `outstandingBalance` | The same sums restricted to that customer. | KPI-21 / DEC-012 / BR-03 |
| `items[].bookingCounts` | `total` (non-cancelled), `past` (`EventDate < today`), `upcoming` (`EventDate = today`), `completed` (`Status = Completed`), `cancelled`. | `Booking` |
| `items[].customerType` | `Corporate` \| `Private` — **derived** from `Company`. | DEC-051 |
| `items[].ledgerState` | `Clean` \| `NotYetDue` \| `Days1To30` \| `Days31To60` \| `Days61To90` \| `Over90Days` — the **worst** bucket among the customer's positive-balance bookings, or `Clean`. | KPI-13 / DEC-052 |
| `items[].ledgerStateLabel` | The server's display label for `ledgerState`. | DEC-052 |
| `quickViewCounts.all` | Non-deleted customers matching the **search** filter, ignoring the type and AR-state filters. | KPI-20 |
| `quickViewCounts.corporate` | Of that population, those whose `Company` is **non-blank**. "Blank" is whitespace-aware (`string.IsNullOrWhiteSpace`), so a company of `"   "` is `Private` — the same rule as `items[].customerType`. | DEC-051 |
| `quickViewCounts.private` | Of that population, those whose `Company` is blank. | DEC-051 |
| `quickViewCounts.withOpenBalance` | Of that population, those with a positive outstanding balance. | KPI-12 |
| `quickViewCounts.overdue30` | Of that population, those whose worst bucket is `Days31To60` / `Days61To90` / `Over90Days`. | KPI-13 |
| `generatedAt` | Server UTC timestamp of the aggregate. | — |

**Authoritative-basis rules binding this endpoint** (extending §9.2):

1. **One net-due path.** `NetAmountDue` and `RemainingBalance` come from the shared
   `BookingAmountDueCalculator.Calculate(booking, S Payment.AmountPaid)` overload
   (DEC-046) — the same path the ledger and the dashboard use. The persisted
   `Booking.AmountDue` column and the `Booking.AmountPaid` cache are **not** used.
2. **Amount paid is the payment sum** — `S Payment.AmountPaid`, never the denormalized
   cache (DEC-012).
3. **The browser computes no day count and no aging bucket.** The bucket key and its
   label are both server-produced; the client renders the label verbatim (AC-F8).
4. **Cancelled bookings are excluded** from every aggregate. `Completed` bookings with
   an outstanding balance **remain included in AR** (DEC-025). `Expired` bookings are
   included, consistent with DEC-025 / DEC-047.
5. **AR is never reported as collections** (BR-08). `totalContractedValue`,
   `totalPaid` and `outstandingBalance` are three separate figures.
6. **No null-as-zero.** `averageValuePerClient` is `null` when `totalClients = 0`.
7. **Soft-deleted customers are excluded** by the Customer Master's own queries
   (DEC-050). No global query filter is introduced (DEC-038).
8. **The directory AR reconciles to the cent** with the per-booking ledger
   `balanceDue` and with `receivables.outstandingAmount` on `GET /api/dashboard`
   (DEC-025, AC-B3).

### 10.2 `GET /api/customers/{id}`

`200` with the customer profile extended by `customerType`, `bookingCounts`,
`totalContractedValue`, `totalPaid`, `outstandingBalance`, `ledgerState`,
`ledgerStateLabel` and `upcomingBooking`
(`{ transactionId, bookingNumber, occasion, eventDate, amountDue }`, or `null` when
the customer has no non-cancelled booking with `EventDate = today`).
`404` when the customer does not exist.

### 10.3 `GET /api/customers/{id}/statement`

`200` with the customer's Statement of Account:

| Field | Meaning | Authority |
| :--- | :--- | :--- |
| `totalBilled` | `S NetAmountDue` over non-cancelled bookings. | KPI-21 |
| `totalPaid` | `S S Payment.AmountPaid`. | DEC-012 |
| `totalRefunded` | `S Refund.NetAmount`. Reported **separately**; refunds do **not** reduce the balance. | DEC-013 |
| `outstandingBalance` | `S max(0, NetAmountDue - paid)`. | BR-03 |
| `ledgerState` / `ledgerStateLabel` | The customer's aging state. | DEC-052 |
| `transactions[].bookingNumber` | The approved booking number. | BR-09 / DEC-016 |
| `transactions[].amount` | The **Net Amount Due** from the shared calculator — *not* the base package amount. | DEC-046, closes F11 |
| `transactions[].paid` | `S Payment.AmountPaid`. | DEC-012 |
| `transactions[].balance` | `max(0, amount - paid)`. Never negative. | BR-03 |
| `transactions[].status` | The BR-10 lifecycle-only name. | BR-10 / contract §5 |
| `transactions[].paymentState` | Separate BR-28 payment state. | BR-28 / contract §5.1 |
| `transactions[].agingBucket` / `agingBucketLabel` | The booking's DEC-041 aging bucket. `null` when it carries no balance. | KPI-13 / DEC-041 / DEC-053 |

`404` when the customer does not exist.

### 10.4 `GET /api/customers/{id}/soa`

Streams the QuestPDF Statement of Account as `application/pdf` with
`Content-Disposition: attachment; filename="SOA-{customerId}-{yyyyMMdd}.pdf"`.
`404` when the customer does not exist. Closes REQ-REC-01's missing controller.

### 10.5 `GET /api/customers/directory/export`

Streams the **current filtered result set** (the same `search` / `customerType` /
`arState` parameters, **not** paged) as an XLSX workbook with
`Content-Disposition: attachment; filename="CustomerDirectory-{yyyyMMdd}.xlsx"`.

### 10.6 `POST /api/customers` and `PUT /api/customers/{id}`

`POST` accepts `{ lastName, firstName, middle, address, contact1, contact2, company }`
and returns `201 { "customerId": n }`. `PUT` accepts the same body for an existing
customer and returns `204`. Both are validated **server-side before persistence**
(BR-11) and return `400 { "errors": [ "..." ] }` on rejection — `PUT` previously
applied no validation at all, which allowed blank and over-length names to reach a
`NOT NULL` / truncated column and surface as a `500`.

| Field | Rule | Column limit |
| :--- | :--- | :--- |
| `lastName` | required, non-blank | `nvarchar(100)` |
| `firstName` | required, non-blank | `nvarchar(100)` |
| `middle` | optional | `nvarchar(50)` |
| `address` | optional | `nvarchar(200)` (validator) / `250` (column) |
| `contact1` | optional | `nvarchar(15)` (validator) / `50` (column) — see DEC-056 |
| `contact2` | optional | `nvarchar(15)` (validator) / `50` (column) — see DEC-056 |
| `company` | optional | `nvarchar(150)` |

---

## 11. Menu & Food Costing Endpoints (`/api/menus`)

Added by `business/menu-food-costing-feature-spec.md` v1.0 (REQ-MNU-01 / REQ-MNU-02),
including the corrections of §7.4 – §7.8 (DEC-063, RSK-029, RSK-030). All endpoints
require a bearer token; the feature introduces **no new role policy** (BR-12 restricts
only cancellation/refund and payment recording, which this feature performs neither of).

`GET /api/menus` (the legacy unpaged list) keeps its shape and its lack of paging; the
master screen uses the new paged endpoint below. It now excludes soft-deleted dishes
(§11.6).

### 11.1 `GET /api/menus/master` — the Menu & Food Costing Master

| Query parameter | Type | Default | Behaviour |
| :--- | :--- | :--- | :--- |
| `search` | string? | — | Case-insensitive substring over `menuId`, `menuName` and `note`. |
| `courseId` | int? | — | Filters to one course; ignored when absent. |
| `status` | string? | — | `Costed` \| `Uncosted` \| `MarginAlert` (case-insensitive). An unrecognised value is **ignored**, not a `400`. |
| `sort` | string? | `FoodCostAsc` | `FoodCostAsc` \| `FoodCostDesc` \| `PriceDesc` \| `PriceAsc` \| `MarginDesc` \| `NameAsc`. An unrecognised value falls back to the default. |
| `page` | int | 1 | Clamped to `>= 1`. |
| `pageSize` | int | 12 | Clamped to `1…100`. |

`200` response:

```json
{
  "summary": {
    "totalDishes": 181,
    "costedDishes": 142,
    "uncostedDishes": 39,
    "costingCoveragePercentage": 78.45,
    "averageFoodCostPercentage": 28.60,
    "averageFoodCostHeadroomPercentage": 6.40,
    "targetFoodCostPercentage": 35.0,
    "targetMarginPercentage": 65.0,
    "isBenchmarkConfigured": true,
    "averageSellingPrice": 320.00,
    "marginAlertCount": 6,
    "bestMarginCourse": {
      "courseId": 4,
      "courseName": "Main Course",
      "grossMarginPercentage": 74.20,
      "averageSellingPrice": 485.00,
      "dishCount": 42
    }
  },
  "courses": {
    "total": 181,
    "items": [
      { "courseId": 1, "courseName": "Appetizer", "dishCount": 12 }
    ]
  },
  "items": [
    {
      "menuId": "DS-104",
      "menuName": "Prime Roast Angus Ribeye with Truffle Demi-Glace",
      "courseId": 4,
      "courseName": "Main Course",
      "note": null,
      "image": null,
      "sellingPrice": 420.00,
      "foodCost": 138.60,
      "grossProfit": 281.40,
      "foodCostPercentage": 33.00,
      "grossMarginPercentage": 67.00,
      "targetFoodCostPercentage": 35.0,
      "isWithinTarget": true,
      "marginVariancePercentage": -2.00
    }
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 181,
  "totalPages": 16,
  "generatedAt": "2026-10-20T09:15:00.1234567Z"
}
```

**`summary` describes the whole filtered result set, not the current page.** The
**summary scope** is every filter: the search, the course filter and the status filter.

**`courses` describes everything except the course filter** — the search and the status
filters apply. `courses.total` is the exact "All Dishes (n)" count of that scope, and
`courses.items[].dishCount` is the per-course count of the same scope, so the tab counts
narrow with the search and stay stable while a course is selected. Every course category is
listed, including a course with a `dishCount` of `0`. A dish without a course has no tab
(there is no course id `0`) but **is** included in `courses.total`, so
`S items[].dishCount` may be smaller than `courses.total` — the client renders
"All Dishes (`courses.total`)" and never shows the difference.

`generatedAt` is the server UTC timestamp of the aggregate.

| Field | Meaning | Authority |
| :--- | :--- | :--- |
| `summary.totalDishes` | Count of non-deleted dishes in the filtered set. | `Menu` |
| `summary.costedDishes` | Of those, the dishes with `FoodCost > 0`. | KPI-14 |
| `summary.uncostedDishes` | `totalDishes - costedDishes`. | KPI-14 |
| `summary.costingCoveragePercentage` | `costed ÷ total × 100`; **`0`** when the filtered set is empty (KPI-14's own zero-denominator rule). | KPI-14 |
| `summary.averageFoodCostPercentage` | `S FoodCost ÷ S Price × 100` over the **margin population**; **`null`** when that population is empty — never `0`. | KPI-22 |
| `summary.averageFoodCostHeadroomPercentage` | `target - actual`, in percentage points. `null` when the actual is `null`; **negative** when over target. | KPI-22 |
| `summary.targetFoodCostPercentage` / `targetMarginPercentage` | The configured benchmark and its complement. | DEC-040 |
| `summary.isBenchmarkConfigured` | `false` when the benchmark is the BR-07 default rather than a deployment decision. | DEC-040 |
| `summary.averageSellingPrice` | `S Price ÷ |margin population|`; **`0`** when that population is empty. | KPI-22 |
| `summary.marginAlertCount` | Dishes in the filtered set whose **published** (rounded) food cost percentage exceeds the target. The figure and the `MarginAlert` status filter are computed from the same expression. | KPI-24 |
| `summary.bestMarginCourse` | The course with the highest `S(Price - FoodCost) ÷ S Price × 100` over its margin population; **`null`** when the margin population is empty. A dish without a course is grouped under `"Uncategorised"`, exactly as KPI-16 does. | KPI-23 |
| `courses.total` | The number of dishes matching the **search and status** filters, with no course filter — the "All Dishes (n)" count. | `Menu` |
| `courses.items[].dishCount` | The same scope, per `CourseCategory`. A dish without a course is counted in `courses.total` but has no tab. | `Menu` |
| `items[].grossProfit` | `Price - FoodCost`. | `MenuCostingCalculator` |
| `items[].foodCostPercentage` | `round(FoodCost ÷ Price × 100, 2)`, rounded **away from zero**; `0` when the price is not above zero. | `MenuCostingCalculator` |
| `items[].grossMarginPercentage` | `round((Price - FoodCost) ÷ Price × 100, 2)`, rounded **away from zero**. | `MenuCostingCalculator` |
| `items[].isWithinTarget` | `foodCostPercentage = targetFoodCostPercentage`. | KPI-24 |
| `items[].marginVariancePercentage` | `foodCostPercentage - targetFoodCostPercentage`, in percentage points. Positive is over target. | KPI-24 |

**Authoritative-basis rules binding this endpoint** (extending §9.2):

1. **The margin population** is the non-deleted dishes with `Price > 0` **and**
   `FoodCost > 0` — the same population as KPI-15, so `100 - averageFoodCostPercentage`
   equals the dashboard's `margin.catalogueMarginPercentage` to the penny (AC-M4, AC-M13).
2. **The benchmark travels with the payload.** It is read from the single
   `IFoodCostBenchmarkProvider` (`Dashboard:Benchmarks:TargetFoodCostPercentage`) the
   dashboard also reads. The client never hardcodes a target percentage (DEC-040, AC-M7).
3. **The browser computes no margin, percentage or cost figure** (DEC-002). The margin
   preview comes from §11.2.
4. **No null-as-zero.** `averageFoodCostPercentage`, `averageFoodCostHeadroomPercentage`
   and `bestMarginCourse` are `null` — never `0` — when the margin population is empty
   (contract §9.2 rule 6).
5. **Soft-deleted dishes are excluded** by the menu master's own queries (DEC-045 /
   DEC-050), and `DELETE /api/menus/{id}` is a **soft delete** (DEC-063) — the dish is
   flagged, never physically destroyed. No global query filter is introduced (DEC-038).
6. **The dish code is upper-cased by the server** and the uniqueness check runs against
   the stored form, so a code that differs only in case is a conflict (`409`), not a key
   collision (`500`).
7. **One rounding rule.** Every published percentage — the dish card, the preview, the
   costing endpoint and the dashboard — is rounded to two decimals **away from zero**
   (RSK-029). The menu screen and the operations dashboard can therefore never report
   the same dish with two different percentages.
8. **One costing definition.** The dish card, the preview and
   `GET /api/menus/{id}/costing` all call the same shared calculator, so they can never
   disagree (DEC-002).
9. **The margin alert is the published figure.** The `MarginAlert` filter and
   `summary.marginAlertCount` are both computed from the **rounded** food-cost percentage
   the dish card publishes, so a dish inside the 0.005 pp band of the target is never
   listed as an alert while its own card says "within target".

### 11.2 `POST /api/menus/costing/preview` — the server-computed margin preview

Request `{ "sellingPrice": 380.00, "foodCost": 104.20 }`.

`200` response:

```json
{
  "sellingPrice": 380.00,
  "foodCost": 104.20,
  "grossProfit": 275.80,
  "foodCostPercentage": 27.42,
  "grossMarginPercentage": 72.58,
  "targetFoodCostPercentage": 35.0,
  "targetMarginPercentage": 65.0,
  "isWithinTarget": true,
  "marginVariancePercentage": -7.58
}
```

`400 { "errors": [ "..." ] }` when a price is negative or the request is malformed. When
`sellingPrice` is `0`, `foodCostPercentage` and `grossMarginPercentage` are `0` — the same
rule the costing service applies.

The preview is computed by the **same** arithmetic the saved dish and the master grid use,
so the preview and the saved record can never disagree (DEC-002, AC-M15).

### 11.3 `GET /api/menus/master/export`

Streams the **current filtered result set** (the same `search` / `courseId` / `status` /
`sort` parameters, **not** paged) as an XLSX workbook with
`Content-Disposition: attachment; filename="MenuFoodCosting-{yyyyMMdd}.xlsx"`.

Columns: Dish Code, Dish Name, Course, Selling Price, Food Cost, Gross Profit,
Food Cost %, Gross Margin %, Target %, Within Target, Notes.

### 11.4 `POST /api/menus` and `PUT /api/menus/{id}` — tightened

`POST` accepts `{ menuId, courseId, menuName, note, price, foodCost, image }` and returns
`201 { "menuId": "..." }`. `PUT` accepts the same body and returns `204`. Both are
validated **server-side before persistence** (BR-11) and return
`400 { "errors": [ "..." ] }` on rejection — previously neither ran a validator, so a
blank name, a negative price or an over-length name reached the database and surfaced as
a `500`.

| Field | Rule | Column limit |
| :--- | :--- | :--- |
| `menuId` | required, non-blank, unique (`409` on duplicate) | `nvarchar(50)` |
| `courseId` | required, must exist in `CourseCategory` | FK |
| `menuName` | required, non-blank | `nvarchar(150)` |
| `note` | optional | `nvarchar(500)` |
| `price` | `>= 0` | `decimal(18,2)` |
| `foodCost` | `>= 0` | `decimal(18,2)` |
| `image` | optional — the plating photo URL | `nvarchar(250)` |

A duplicate dish code is a `409` and it uses the **standard error envelope**:

```json
{ "errors": [ "Menu item 'DS-104' already exists." ] }
```

The uniqueness check compares the **upper-cased** code, so a code that differs only in
case is a conflict (`409`) and never a key collision (`500`).

`PUT` returns `404 { "errors": [ "..." ] }` when the dish does not exist. The dish code
of the body is validated but **not** applied — the route identifies the record and the
specification does not authorise a key change.

### 11.5 `DELETE /api/menus/{id}` — a soft delete

`204` when the dish is deleted. The delete is a **soft delete** (DEC-063): the dish is
flagged (`is_deleted`) and its audit stamp is refreshed — the row is never physically
destroyed from the kitchen's dish master. A dish that is already soft-deleted is not
found (`404`), and the dish code stays reserved (a new dish with the same code is a
`409`).

### 11.6 The legacy menu reads

`GET /api/menus` (the legacy unpaged list), `GET /api/menus/{id}` and
`GET /api/menus/costing/all` **exclude soft-deleted dishes** (DEC-063), the same scoped
pattern the master query uses — no global query filter is introduced (DEC-038). A
soft-deleted dish is a `404` on `GET /api/menus/{id}`.

`GET /api/menus/{id}` and `GET /api/menus` no longer publish a computed `profitMargin`
field (RSK-030): a get-only computed property cannot be projected by EF Core, so it
always serialised as `0` — a field that always reports zero is worse than no field. The
real margin figures are published by `GET /api/menus/master` (§11.1) and by
`GET /api/menus/{id}/costing`.

### 11.7 `GET /api/menus/{id}/costing` and `GET /api/menus/costing/all` — the gross margin

`MenuCostingSummaryDto` publishes the gross margin percentage alongside the gross profit
and the food-cost percentage:

```json
{
  "menuId": "DS-104",
  "menuName": "Prime Roast Angus Ribeye with Truffle Demi-Glace",
  "sellingPrice": 420.00,
  "foodCost": 138.60,
  "grossProfit": 281.40,
  "foodCostPercentage": 33.00,
  "grossMarginPercentage": 67.00,
  "targetFoodCostPercentage": 35.0,
  "isWithinTarget": true
}
```

| Field | Meaning | Authority |
| :--- | :--- | :--- |
| `grossProfit` | `Price - FoodCost`. | `MenuCostingCalculator` |
| `foodCostPercentage` | `round(FoodCost ÷ Price × 100, 2)`, rounded away from zero; `0` when the price is not above zero. | `MenuCostingCalculator` |
| `grossMarginPercentage` | `round((Price - FoodCost) ÷ Price × 100, 2)`, rounded away from zero; `0` when the price is not above zero. | `MenuCostingCalculator` |
| `isWithinTarget` | `foodCostPercentage = targetFoodCostPercentage`. | KPI-24 |

The gross margin is the **same figure** the master's dish card publishes as
`items[].grossMarginPercentage` (§11.1) — both come from the shared
`MenuCostingCalculator`, so the two surfaces can never disagree. The client is forbidden
from computing a margin (DEC-002), so the server publishes it.

`isWithinTarget` is a get-only computed property, and it works here: this DTO is built in
the service layer, never projected by EF Core. That is exactly why the removed
`MenuDto.ProfitMargin` (an EF projection) had to go and this one did not (RSK-030).

---

## 12. Packages & Buffets Catalogue Endpoints (`/api/packages`)

**Requirements:** REQ-PKG-01 and REQ-PKG-04. **Specification:** `business/packages-catalogue-feature-spec.md` v1.1. **Decisions:** DEC-067..DEC-072, DEC-162..DEC-167, DEC-173.

All endpoints require an authenticated bearer token and carry **no role policy** (BR-12): any authenticated user may read the catalogue, create a tier, edit a tier and activate or deactivate a tier.

### 12.1 `GET /api/packages` - the tier list
- **Query parameters:** `activeOnly` (optional `bool`) - when `true`, only the active tiers are returned; and `areaId` (optional `int`) - when present, only the tiers whose **coverage** includes that area are returned (DEC-157).
- **`areaId`** exists because the owner's operational rule is explicit - "Packages will display by based on the selected area" - and there was **no** server capability to answer it: the coverage repository exposes only a **per-tier** projection, and the browser is not permitted to fan out one request per tier. The filter returns the tiers whose coverage includes the area and whose coverage row is **active**.
- **Response `200 OK`:** an array of tier records.

```json
[
  {
    "packageId": 7,
    "packageCode": "PKG-WED-01",
      "packageType": "Wedding",
      "description": "Wedding Grand Tier",
      "amountPax": 1450.00,
      "minPax": 150,
      "numPaxId": 4,
      "extendedLocationAmount": null,
    "isActive": true
  }
]
```

| Field | Meaning | Authority |
| :--- | :--- | :--- |
| `packageId` | The internal identity key. The business identifier is `packageCode`. | `Package.PackageId` |
| `packageCode` | The human-readable code, `PKG-{TYPE}-{NN}` (`PKG-REG-01`). Generated once at create and frozen (DEC-162). | `Package.PackageCode` |
| `packageType` | The tier's type. Free text; the `PackageType` enum names are the intended vocabulary but the column is not typed to it (DEC-070). | `Package.PackageType` |
| `description` | The tier's display name. | `Package.Description` |
| `amountPax` | The per-guest rate. `null` means **not priced** - never zero (contract rule 6). | `Package.AmountPax` |
| `minPax` | The minimum headcount. `null` means **not set**. | `Package.MinPax` |
| `numPaxId` | The required applicable-pax classification ID. It is informational and does not define booking headcount bounds. | `Package.NumPaxId` → `PackagePaxApplicable.NumPaxId` (DEC-173) |
| `extendedLocationAmount` | The tier's own **per-guest** extended-location amount. Only `Regular`/`Premier` may carry one; `null` = not stated (DEC-163). | `Package.ExtendedLocationAmount` |
| `isActive` | Whether the tier may be selected. An inactive tier stays in the catalogue. | `Package.IsActive` |

The list projection excludes **soft-deleted** tiers (`IsDeleted == false`), so the list and the summary in 12.2 describe the same population (DEC-074). This endpoint is otherwise **unchanged** by this feature: its shape is pinned by existing consumers.

### 12.2 `GET /api/packages/summary` - the catalogue aggregate (DEC-068)

- **Description:** the server-computed catalogue statistics (KPI-25..KPI-28). The browser is forbidden from computing a monetary aggregate (DEC-002), so the per-pax rate statistics are published here.
- **Response `200 OK`:**

```json
{
  "totalPackages": 3,
  "activePackages": 2,
  "inactivePackages": 1,
  "pricedPackages": 2,
  "averageAmountPax": 1200.00,
  "lowestAmountPax": 950.00,
  "highestAmountPax": 1450.00,
  "lowestMinPax": 80,
  "highestMinPax": 150,
  "generatedAt": "2026-10-21T09:15:00.1234567Z"
}
```

| Field | Meaning | Null semantics |
| :--- | :--- | :--- |
| `totalPackages` | Every tier with `IsDeleted == false`. | Never `null`; `0` is a real measurement. |
| `activePackages` | Of those, `IsActive == true`. | Never `null`. |
| `inactivePackages` | `totalPackages - activePackages`. | Never `null`. |
| `pricedPackages` | Of those, `AmountPax != null`. | Never `null`. |
| `averageAmountPax` | The mean of the non-null `AmountPax`, rounded to two decimals away from zero (DEC-003). | `null` when the priced population is empty. |
| `lowestAmountPax` / `highestAmountPax` | The minimum / maximum of the same population. | `null` when the priced population is empty. |
| `lowestMinPax` / `highestMinPax` | The minimum / maximum of the non-null `MinPax`. | `null` when no tier sets a floor. |
| `generatedAt` | The server UTC timestamp of the aggregate. | Never `null`. |

The route is a literal segment and can never be captured by `GET /api/packages/{id:int}`.

### 12.3 `GET /api/packages/{id}` - one tier
- **Response `200 OK`:** one tier record, the same shape as an element of 12.1.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }` - no tier carries that id, **or** the tier is soft-deleted. The detail projection excludes soft-deleted tiers, exactly like the list (DEC-074).

> **Scope note.** `IPackageRepository.GetByIdAsync` - the *entity* fetch used by the write commands and by `CreateBookingCommand` to resolve the booking's price per head (BR-04) - is **deliberately not filtered**. Only the read projections are scoped.

### 12.4 `POST /api/packages` - create a tier
- **Request body:** `{ "packageType": "...", "description": "...", "amountPax": 0.00, "minPax": 0, "numPaxId": 4, "extendedLocationAmount": null }` - `amountPax`, `minPax` and `extendedLocationAmount` may be `null`; `numPaxId` is required and must identify a seeded `PackagePaxApplicable` row (DEC-173). `extendedLocationAmount` is accepted only when `packageType` is `Regular` or `Premier`; any other type with a non-null amount is a `400` (DEC-163).
- **Response `201 Created`:** `{ "packageId": 12 }`. The `PackageCode` is generated by the server and published by the read endpoints.
- **Response `400 Bad Request`:** `{ "errors": [ "Package type is required.", "..." ] }` - the validator's messages, verbatim (BR-11, DEC-069). For `numPaxId`, missing/zero returns `"Applicable pax classification is required."`; an unknown ID returns `"Applicable pax classification was not found."` (DEC-173). Nothing is persisted.

### 12.5 `PUT /api/packages/{id}` - update a tier
- **Request body:** the same body as 12.4. The `PackageCode` is **frozen** and is never accepted from the client; changing the type away from `Regular`/`Premier` clears the stored `extendedLocationAmount`.
- **Response `204 No Content`** on success.
- **Response `400 Bad Request`:** `{ "errors": [ ... ] }` - the payload was rejected. The `numPaxId` error wording is the same as 12.4.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }` - no tier carries that id.

### 12.6 `PATCH /api/packages/{id}/toggle-active` - activate or deactivate a tier
- **Response `200 OK`:** `{ "isActive": false }` - the tier's state **after** the toggle.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }` - no tier carries that id.

This endpoint is the whole of the reference's "Archive Tier" affordance. It is **not** an archive: it flips a flag and the tier stays in the catalogue and in every count (DEC-071).

### 12.7 What this surface deliberately does not publish

Per-tier food cost and gross margin, course allocations, event inclusions and any live-price stream. The approved data model carries none of them (DEC-067, DEC-071), so the client renders an explicit capability-gap notice in their place rather than a fabricated value. The package code and the extended-location amount **are** published now (DEC-162, DEC-163). No package tier is seeded (BR-13); the fixed applicable-pax reference rows are seeded under DEC-173.

### 12.8 `GET /api/packages/pax-applicables` — applicable-pax lookup (DEC-173)

- **Authorization:** authenticated bearer token; no role policy, consistent with the package catalogue.
- **Purpose:** supplies the required New/Edit Tier selector from the persisted reference table. This endpoint is read-only; no lookup CRUD is exposed.
- **Response `200 OK`:** the five fixed choices ordered by `numPaxId`:

```json
[
  { "numPaxId": 1, "packageApplicablePax": "15 to 19 Pax" },
  { "numPaxId": 2, "packageApplicablePax": "20 to 29 Pax" },
  { "numPaxId": 3, "packageApplicablePax": "30 to 49 Pax" },
  { "numPaxId": 4, "packageApplicablePax": "80 to 100 Above" },
  { "numPaxId": 5, "packageApplicablePax": "Not Applicable to PackMeals" }
]
```

The labels are stored and published verbatim. They are not parsed as bounds, and option 5 is not coupled to `PackageType.Pm`. A missing or zero `numPaxId` is rejected by package create/update with `400 { "errors": [ "Applicable pax classification is required." ] }`; an ID that does not reference a live lookup row returns `400 { "errors": [ "Applicable pax classification was not found." ] }`. Neither is persisted. The value never changes booking eligibility, `MinPax`, or pricing.
---

## 13. Package Configuration Workbench Endpoints (`/api/packages`, `/api/areas`)

Issued 2026-10-22 with `business/package-configuration-workbench-feature-spec.md` v1.0 (REQ-PKG-02); amended 2026-10-27 by DEC-172 (v1.1). Decisions DEC-077 .. DEC-085, DEC-172.

**DEC-172 compatibility note:** the course-rule wire shape is intentionally reduced. `inclusionTag`, `upgradeAddonId` and the derived upgrade description/price/unit members are no longer published or accepted as contract fields. The API route paths and all non-course-rule add-on contracts remain unchanged.

**Additive guarantee.** Except for the applicable-pax additions recorded in section 12.8 / DEC-173, sections 1 - 12 remain as previously approved. The existing routes remain; the package create/update body now requires `numPaxId`, and package read projections add that member. Section 12.7 is superseded **only** in this respect: course allocations and venue coverage are now published - by the endpoints below - so the client's capability-gap notices for those two blocks are replaced by real workbench links (DEC-084).

**Common rules.** Every endpoint requires a bearer token (BR-12) and no role policy. Every rejection is `400 { "errors": [ "..." ] }`; a missing tier is `404 { "errors": [ "Package not found." ] }`; a missing municipality is `404 { "errors": [ "Area not found." ] }`. Money is `decimal`, rounded to two decimals away from zero **once**, in the Application layer (DEC-003). `null` means unknown, never zero (§9.2 rule 6).

**The tier population (F6).** Every one of the eleven endpoints resolves the tier through the **catalogue population** (`IsDeleted == false`, DEC-074) and answers `404 { "errors": [ "Package not found." ] }` for a **soft-deleted** tier. The **write** use cases refuse to write onto a soft-deleted tier. This is deliberately stricter than `IPackageRepository.GetByIdAsync`, which stays unfiltered for the booking path (BR-04, DEC-074).

**A dish is costed when its food cost is greater than zero (DEC-087).** `Menu.FoodCost` is nullable but the column carries a database default of `0.00` and the entity's own default is `0.00m`, so a dish created without a cost stores **zero**, not null - and a row inserted outside EF (including by the future legacy import, DEC-032) lands at `0.00` as well. A dish is therefore **costed** iff `FoodCost > 0`, the same "margin population" idea `MenuCostingCalculator` uses. A dish that is not costed is still in a qualified pool, but it contributes to no average, to no standard run, and it makes a selection's food cost **null** (unknown, never zero).

**An unstated out-of-town rate is no surcharge (DEC-086).** `Area.AdditionalRate` is published as `null` when the municipality has stated no rate - that is an unknown *measurement*. The **money chain** nevertheless treats an absent surcharge as **zero**, because a surcharge is an *addition* and an absent addition is zero, so a municipality with no stated rate still yields a complete, non-null contract value. This is a defined default, not a null-as-zero: the surcharge's *amount* is known to be nothing; the rate is simply not on record.

**A null collection member is a rejected payload, never an empty set (F7).** The request records declare their collection members **nullable** and the validator rejects a null explicitly. An absent or explicitly `null` `rules` / `coverages` / `selections` member is a `400`, because an **empty** set has a destructive, documented meaning (§13.2, §13.5).

### 13.1 `GET /api/packages/{id}/course-rules` - the course-coverage ruleset
- **Response `200 OK`:** `PackageCourseRulesetDto`.

| Field | Type | Definition |
| :--- | :--- | :--- |
| `packageId` | `int` | The tier. |
| `packageName` | `string` | `Package.Description`. |
| `packageCode` | `string` | The frozen business code (DEC-162). |
| `packageType` | `string` | `Package.PackageType`, verbatim (DEC-070). |
| `extendedLocationAmount` | `decimal?` | The tier's per-guest extended-location amount; `null` = not stated (DEC-163). |
| `amountPax` | `decimal?` | The per-guest rate. |
| `minPax` | `int?` | The headcount floor. |
| `isActive` | `bool` | The tier's state. |
| `targetFoodCostPercentage` | `decimal` | The configured benchmark (DEC-040). |
| `targetMarginPercentage` | `decimal` | `100 - targetFoodCostPercentage`. |
| `isBenchmarkConfigured` | `bool` | `false` when the configuration provider is absent. |
| `costCeilingPerPax` | `decimal?` | `amountPax * targetFoodCostPercentage / 100`. `null` when `amountPax` is null. |
| `minimumContractValue` | `decimal?` | `amountPax * minPax` (DEC-079). `null` when either is null. |
| `standardRunFoodCostPerPax` | `decimal?` | The mean food cost of each rule's qualified **costed** pool, summed (DEC-083). The pool means are computed at full precision and the sum is rounded **once**. `null` when there is no rule, or when no rule's pool holds a costed dish. |
| `standardRunFoodCostPercentage` | `decimal?` | `standardRunFoodCostPerPax / amountPax * 100`. `null` when the standard run is null, or `amountPax` is null or `<= 0`. |
| `baseGrossProfitPercentage` | `decimal?` | `(amountPax - standardRunFoodCostPerPax) / amountPax * 100`. `null` when the standard run is null, or `amountPax` is null or `<= 0`. |
| `rules` | `PackageCourseRuleDto[]` | The tier's rules, ordered by course name. Soft-deleted rules are excluded. |
| `generatedAt` | `DateTime` | Server UTC timestamp. |

`PackageCourseRuleDto`

| Field | Type | Definition |
| :--- | :--- | :--- |
| `packageCourseRuleId` | `int` | The rule's key. |
| `courseId` | `int` | FK to `CourseCategory`. |
| `courseName` | `string` | The course's name. |
| `requiredQuota` | `int` | The dishes the client must take from this course. |
| `portionGrams` | `int?` | The standard portion per guest. `null` = not specified. |
| `ruleDescription` | `string` | The enforced rule, in plain words. |
| `poolSize` | `int` | The non-soft-deleted dishes of the course. |
| `poolCostedCount` | `int` | The pool's dishes whose food cost is **greater than zero** (DEC-087). |
| `poolAverageFoodCost` | `decimal?` | The mean food cost of the pool's **costed** dishes. `null` when none is costed. |
| `pool` | `PackageCoursePoolDishDto[]` | The pool's dishes. |

`PackageCoursePoolDishDto`: `menuId` (`string`), `menuName` (`string`), `foodCost` (`decimal?`), `price` (`decimal?`).

- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }`.

### 13.2 `PUT /api/packages/{id}/course-rules` - replace the ruleset
- **Request body:** `{ "rules": [ { "courseId": 0, "requiredQuota": 0, "portionGrams": 0, "ruleDescription": "..." } ] }`. `portionGrams` may be `null`. The `rules` member itself is **nullable**, and a `null` is a `400`, never an empty set. Retired `inclusionTag` and `upgradeAddonId` members are not part of the contract (DEC-172).
- **Compatibility:** unknown JSON members sent by an older client are ignored by the default serializer; they are not a validation error. Clients should use the current request shape.
- **Response `200 OK`:** the new `PackageCourseRulesetDto`, exactly as 13.1 publishes it.
- **Response `400 Bad Request`:** the validator's messages, verbatim - a `null` `rules` member, a duplicate `courseId` inside the set, an unknown or soft-deleted course, a quota below 1, a negative portion, or a missing or over-long description. Nothing is persisted.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }` - including for a **soft-deleted** tier, which is also refused by the write.

**The set is replaced (DEC-085).** The submitted set **replaces** the tier's ruleset. An entry that is not submitted is **soft-deleted** (`is_deleted = 1`), never destroyed. An **empty** set (`{"rules": []}`) therefore clears the tier's ruleset by soft-deleting every live rule - a documented, deliberate meaning.

**A re-added key resurrects its row (DEC-088).** The upsert matches the natural key over **all** rows, including the soft-deleted ones, and **resurrects** a soft-deleted match (`is_deleted = 0`) rather than inserting a second row. The migration creates a **filtered** unique index on `(`p_id`, `courseId`)` with the predicate `is_deleted = 0`, so the database refuses a second live row for the same natural key.

The submitted set **replaces** the tier's ruleset; an entry that is not submitted is soft-deleted (DEC-085).

### 13.3 `POST /api/packages/{id}/course-rules/preview` - the selection simulator
- **Request body:** `{ "selections": [ { "courseId": 0, "menuId": "DS-101" } ], "stressPercentage": 0.00, "pax": 100 }`. `stressPercentage` and `pax` may be `null` or omitted. The `selections` member is **nullable**, and a `null` is a `400`, never an empty selection.
- **Response `200 OK`:** `PackageCourseRulePreviewDto`.

| Field | Type | Definition |
| :--- | :--- | :--- |
| `packageId` | `int` | The tier. |
| `amountPax` | `decimal?` | The per-guest rate. |
| `minPax` | `int?` | The tier's headcount floor. |
| `pax` | `int?` | The **resolved** headcount: the requested one, otherwise the tier's `minPax`, otherwise `null`. |
| `targetFoodCostPercentage` | `decimal` | The configured benchmark. |
| `costCeilingPerPax` | `decimal?` | `amountPax * targetFoodCostPercentage / 100`. `null` when `amountPax` is null. |
| `selectedFoodCostPerPax` | `decimal?` | The sum of the selected dishes' food costs. `null` when nothing is selected, **or** when any selected dish is not costed (DEC-087). |
| `foodCostPerPax` | `decimal?` | The selection's `selectedFoodCostPerPax`, otherwise the **standard run**. `null` when neither is available. |
| `effectiveFoodCostPercentage` | `decimal?` | `selectedFoodCostPerPax / amountPax * 100`. `null` when the selected cost is null, or `amountPax` is null or `<= 0`. |
| `isCostKnown` | `bool` | `false` when the selected food cost is null (an uncosted dish, or no selection). |
| `isCeilingBreached` | `bool` | `selectedFoodCostPerPax > costCeilingPerPax`. `false` when the selected cost is null. **A breach warns; it never reprices and never blocks** (DEC-080). |
| `ceilingShortfallPerPax` | `decimal?` | `max(0, selectedFoodCostPerPax - costCeilingPerPax)`. `null` when the selected cost is null or the ceiling is null. |
| `ceilingOveragePercentage` | `decimal?` | `max(0, selectedFoodCostPerPax - costCeilingPerPax) / costCeilingPerPax * 100` - **the percentage over the ceiling** (DEC-080). `null` when the selected cost is null, or the ceiling is null or `<= 0`. |
| `stressPercentage` | `decimal?` | The echoed stress factor. A `null` means **no stress** (a factor of 1); `0` is the same. |
| `stressedFoodCostPerPax` | `decimal?` | `selectedFoodCostPerPax * (1 + stressPercentage / 100)`. `null` when the selected cost is null. |
| `stressedFoodCostPercentage` | `decimal?` | `stressedFoodCostPerPax / amountPax * 100`. `null` when the stressed cost is null, or `amountPax` is null or `<= 0`. |
| `grossRevenue` | `decimal?` | `amountPax * pax`. `null` when `amountPax` is null or no pax is resolved. |
| `foodCostTotal` | `decimal?` | `foodCostPerPax * pax`. `null` when `foodCostPerPax` is null or no pax is resolved. |
| `grossProfit` | `decimal?` | `grossRevenue - foodCostTotal`. `null` when either operand is null. |
| `grossProfitPercentage` | `decimal?` | `grossProfit / grossRevenue * 100`. `null` when `grossRevenue` is null or `<= 0`. |
| `isQuotaCompliant` | `bool` | `true` when every rule is satisfied **and** no selection names an unruled course. |
| `isCompliant` | `bool` | The **overall** verdict: `isQuotaCompliant` **and** the selection does not breach the cost ceiling. A breach **marks the selection non-compliant** (DEC-080). |
| `compliance` | `PackageCourseComplianceDto[]` | One entry per rule, plus one per unruled course named by the selection. |
| `selections` | `PackageCourseSelectionResultDto[]` | The selected dishes and their food costs. |
| `generatedAt` | `DateTime` | Server UTC timestamp. |

`PackageCourseComplianceDto`: `courseId` (`int`), `courseName` (`string`), `requiredQuota` (`int`), `selectedCount` (`int`), `isCompliant` (`bool`), `hasRule` (`bool` - `false` when the selection names a course the tier has no rule for).

`PackageCourseSelectionResultDto`: `courseId` (`int`), `courseName` (`string`), `menuId` (`string`), `menuName` (`string`), `foodCost` (`decimal?`).

- **Response `400 Bad Request`:** `{ "errors": [ ... ] }`. The exact messages are:

| Condition | Message |
| :--- | :--- |
| A `null` `selections` member | "Selections must be supplied." |
| The same `menuId` twice in one set | "Dish {menuId} is selected more than once." |
| An unknown or soft-deleted dish | "Dish {menuId} does not exist." |
| The dish's own course is not the named course | "Dish {menuId} does not belong to course {courseId}." |
| A negative stress percentage | "Stress percentage must be zero or greater." |
| A negative `pax` | "Pax must be at least 1." |

Nothing is persisted (a preview persists nothing).
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }`.

### 13.4 `GET /api/packages/{id}/areas` - the coverage matrix
- **Response `200 OK`:** `PackageAreaCoverageMatrixDto`.

| Field | Type | Definition |
| :--- | :--- | :--- |
| `packageId` | `int` | The tier. |
| `packageName` | `string` | `Package.Description`. |
| `packageCode` | `string` | The frozen business code (DEC-162). |
| `extendedLocationAmount` | `decimal?` | The tier's per-guest extended-location amount; `null` = not stated (DEC-163). |
| `amountPax` | `decimal?` | The per-guest rate. |
| `minPax` | `int?` | The headcount floor. |
| `minimumContractValue` | `decimal?` | `amountPax * minPax` (DEC-079). |
| `standardRunFoodCostPerPax` | `decimal?` | The tier's standard run, read from the course ruleset. `null` when there is no rule or no costed dish. |
| `baseGrossProfitPercentage` | `decimal?` | `(amountPax - standardRunFoodCostPerPax) / amountPax * 100`. `null` when the standard run is null, or `amountPax` is null or `<= 0`. |
| `coverageCount` | `int` | The tier's covered municipalities. |
| `activeCoverageCount` | `int` | The covered municipalities whose coverage is active. |
| `lowestAdditionalRate` | `decimal?` | The smallest surcharge among the **active** coverage. `null` when none is active or none carries a rate. |
| `highestAdditionalRate` | `decimal?` | The largest surcharge among the **active** coverage. |
| `lowestGrossProfitPercentage` | `decimal?` | The smallest gross-profit percentage across the **active** coverage, at the tier's floor headcount. **Derivation:** the food cost per pax is the **standard run**, the headcount is the tier's `minPax` (the floor), and the gross profit is `totalContractValue - foodCostTotal - logisticsCost` for that municipality. A coverage row that yields no computable figure (no standard run, or no `logisticsCost`) is **excluded from the range** rather than counted as zero. The range is `null` when `minPax` is null, when there is no active coverage, or when no active coverage yields a computable figure. |
| `highestGrossProfitPercentage` | `decimal?` | The largest gross-profit percentage across the same population and the same derivation. |
| `grossProfitCoverageCount` | `int` | How many active coverage rows yielded a computable gross-profit figure. It is `0` when the range is `null`, and it is a real measurement, never `null`. |
| `areas` | `PackageAreaCoverageRowDto[]` | The coverage rows. |
| `generatedAt` | `DateTime` | Server UTC timestamp. |

`PackageAreaCoverageRowDto`: `packageAreaCoverageId` (`int`), `areaId` (`int`), `areaName` (`string` - `Area.AreaDetails`), `additionalRate` (`decimal?` - `Area.AdditionalRate`, the **municipality's** rate, DEC-078), `distanceKm` (`int?`), `logisticsNote` (`string?`), `logisticsCost` (`decimal?` - the per-event cost), `isActive` (`bool`).

A coverage row whose **municipality** is soft-deleted is **excluded** from `areas` and from every count: the municipality catalogue is the population (M5). Course rules have no add-on relationship (DEC-172).

- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }`.

### 13.5 `PUT /api/packages/{id}/areas` - replace the coverage set
- **Request body:** `{ "coverages": [ { "areaId": 0, "isActive": true } ] }`. The `coverages` member is **nullable**, and a `null` is a `400`, never an empty set.
- **Response `200 OK`:** the new `PackageAreaCoverageMatrixDto`, exactly as 13.4 publishes it.
- **Response `400 Bad Request`:** the validator's messages - a `null` `coverages` member, a duplicate `areaId` inside the set, an unknown or soft-deleted area. Nothing is persisted.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }` - including for a **soft-deleted** tier, which is also refused by the write.

The submitted set **replaces** the tier's coverage; an entry that is not submitted is soft-deleted (DEC-085). An **empty** set (`{"coverages": []}`) clears the tier's coverage by soft-deleting every live row. A re-added `areaId` **resurrects** its soft-deleted row rather than inserting a second one, and the migration creates a **filtered** unique index on `(`p_id`, `areaId`)` with the predicate `is_deleted = 0` (DEC-088).

### 13.6 `POST /api/packages/{id}/areas` - attach a municipality
- **Request body:** `{ "areaId": 0, "isActive": true }`.
- **Response `201 Created`:** `{ "packageAreaCoverageId": 0 }`.
- **Response `400 Bad Request`:** `{ "errors": [ "Area 0 does not exist." ] }` or `"Area 0 is already covered by this package."`.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }` - including for a soft-deleted tier.

### 13.7 `POST /api/packages/{id}/areas/preview` - the out-of-town pricing simulator
- **Request body:** `{ "pax": 0, "areaId": 0, "selections": [ { "courseId": 0, "menuId": "DS-101" } ] }`. `areaId` and `selections` may be `null` or omitted. A `null` `selections` member is a `400`; an **empty** array is a valid "no selection" request.
- **Response `200 OK`:** `PackageLocationPreviewDto`.

| Field | Type | Definition |
| :--- | :--- | :--- |
| `packageId` | `int` | The tier. |
| `pax` | `int` | The requested headcount (>= 1). |
| `amountPax` | `decimal?` | The per-guest rate. |
| `areaId` | `int?` | The selected municipality. |
| `areaName` | `string?` | The municipality's name. |
| `additionalRate` | `decimal?` | The municipality's per-pax surcharge, `null` when the municipality has stated no rate. |
| `packageExtendedLocationRate` | `decimal?` | The tier's own per-guest extended-location amount; `null` = not stated (DEC-163). |
| `baseTotal` | `decimal?` | `amountPax * pax`. `null` when `amountPax` is null. |
| `extendedTotal` | `decimal?` | `(effectiveAdditionalRate + packageExtendedLocationRate) * pax`, where `effectiveAdditionalRate` is the stated rate **or zero** when the municipality has stated none (DEC-086). `null` when neither an area nor a tier amount applies. |
| `effectiveRatePerPax` | `decimal?` | `amountPax + effectiveAdditionalRate + packageExtendedLocationRate`. `null` when `amountPax` is null. |
| `totalContractValue` | `decimal?` | `baseTotal + extendedTotal`. `null` when `amountPax` is null. |
| `foodCostPerPax` | `decimal?` | The selection's food cost when a selection is supplied, otherwise the standard-run estimate. `null` when neither is available, and `null` when a selected dish is not costed (DEC-087). |
| `foodCostTotal` | `decimal?` | `foodCostPerPax * pax`. |
| `logisticsCost` | `decimal?` | The municipality's per-event logistics cost. `null` when it has none - the band is **omitted**, never zero (DEC-081). |
| `grossProfit` | `decimal?` | `totalContractValue - foodCostTotal - logisticsCost`. `null` when an operand is unknown. |
| `grossProfitPercentage` | `decimal?` | `grossProfit / totalContractValue * 100`. `null` when `totalContractValue` is null or `<= 0`. |
| `generatedAt` | `DateTime` | Server UTC timestamp. |

- **Response `400 Bad Request`:** `{ "errors": [ "Pax must be at least 1." ] }`, or the selection rules of 13.3 (an unknown, soft-deleted, mis-coursed or repeated dish, or a `null` `selections` member). Nothing is persisted.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }`.

**No maximum pax is ruled (M10).** The reference's slider runs to 500 pax, but no maximum is approved, so none is enforced; `amountPax * pax` is arithmetically safe for any `int`.

### 13.8 `GET /api/packages/{id}/spec-sheet` - the printable specification sheet
- **Response `200 OK`:** `application/pdf` - the tier's specification sheet (QuestPDF): its identity and commercial terms, its course rules with their qualified pools, its covered municipalities and its planning figures.
- **Response `404 Not Found`:** `{ "errors": [ "Package not found." ] }`.

### 13.9 `GET /api/areas` - the municipality catalogue
- **Response `200 OK`:** `AreaDto[]` - `areaId` (`int`), `areaDetails` (`string`), `additionalRate` (`decimal?`), `distanceKm` (`int?`), `logisticsNote` (`string?`), `logisticsCost` (`decimal?`). Soft-deleted municipalities are excluded.

### 13.10 `POST /api/areas` - add a municipality
- **Request body:** `{ "areaDetails": "...", "additionalRate": 0.00, "distanceKm": 0, "logisticsNote": "...", "logisticsCost": 0.00 }`. Every member except `areaDetails` may be `null`.
- **Response `201 Created`:** `{ "areaId": 0 }`.
- **Response `400 Bad Request`:** the validator's messages - a missing or over-long name, a negative rate, distance or logistics cost, an over-long note.

### 13.11 `PUT /api/areas/{id}` - update a municipality
- **Request body:** the same body as 13.10.
- **Response `204 No Content`** on success.
- **Response `400 Bad Request`:** the validator's messages.
- **Response `404 Not Found`:** `{ "errors": [ "Area not found." ] }`.

> **Scope note.** A municipality's rate is **one rate per municipality**, not one per package (DEC-078). Editing it affects every tier that covers it, and a coverage row cannot override it.

### 13.12 What this surface deliberately does not publish

A stored per-tier food cost or margin, a per-package rate override, a guest ceiling, a flat out-of-town percentage surcharge, a duplicate/clone operation, a publish action, a bulk edit, a banquet-amenity or staples record, a mobile-logistics checklist, a package export and a live-price stream. None of them has an approved basis, so the client renders an explicit capability-gap notice in their place rather than a fabricated value (DEC-084). No rule, coverage row or municipality is seeded (BR-13).
---

## 14. Bookings & Events Register & Reservation Workbench Endpoints (`/api/bookings`)

> Added by `business/bookings-events-feature-spec.md` v1.0 (REQ-BKG-05, REQ-BKG-01).
> Every figure in this section is **server-computed** (DEC-002). The client never derives a
> money figure, a percentage or a count from these payloads - it formats and displays.

### 14.0 The authoritative-basis rules binding this surface

These eight rules bind every endpoint in section 14. They mirror the Customer Master's rule set (10.1) and are **not** optional.

1. **One net-due path.** `NetAmountDue` comes from `BookingAmountDueCalculator.Calculate(booking, Σ Payment.AmountPaid)` - the overload that **reproduces the frozen snapshot** (BR-25, DEC-099). The stored `Booking.AmountDue` and the calculator's figure are **equal for every booking**, so there is no second money notion (DEC-035 is resolved).
2. **Amount paid is `Σ Payment.AmountPaid`**, never the denormalized `Booking.AmountPaid` cache (BR-10, DEC-012).
3. **Soft-deleted payments are excluded** from every figure on this surface - and from the ledger, the customer statement and the calendar feed, which now apply the same rule (DEC-111, DEC-045).
4. **Soft-deleted bookings are excluded.** The exclusion is applied by the query; no global query filter is introduced (DEC-038, DEC-045).
5. **`Cancelled` bookings are excluded** from the register population. `Expired` bookings stay in it, because DEC-025 keeps them in AR (RSK-017 remains open).
6. **The population is one.** The list, the totals and the summary describe the **same** filtered population. With no filter applied, the register's outstanding total equals the dashboard's KPI-12 **to the cent** (AC-BKG-05).
7. **No null-as-zero.** A `null` means **unknown**, never zero (BR-20). An unconfigured capacity, an absent tier, an empty population and an absent preceding period all publish `null`.
8. **The frozen snapshot is authoritative.** A later change to a tier's rate, a municipality's rate or the discount catalogue never alters a stored booking (BR-04). The surcharge is a **stored** column, not an inference (DEC-116), and the deposit percentage and the capacity are **configuration**, absent by default (DEC-101, DEC-102).
9. **One rounding.** Every published money figure is rounded to two decimals, away from the midpoint, **once**, in the Application layer (DEC-115, BR-20). The browser never re-rounds.
10. **The overpayment prohibition is enforced.** A payment that would take the total above the net amount due is refused with `400` and nothing is written (BR-05, DEC-118).

### 14.1 `GET /api/bookings` - the Bookings & Events Register

- **Query parameters:**

| Parameter | Type | Meaning |
| :--- | :--- | :--- |
| `search` | string | Case-insensitive match over the booking number, the occasion, the customer name, the customer company and the venue. Empty matches everything. Redacted in the operational log (BR-21). |
| `status` | csv | Lifecycle-only `BookingStatus` names (§5). An unknown name is a `400`. |
| `packageType` | csv | Stored `Package.PackageType` values. |
| `venue` | csv | Stored `Booking.Venue` values. |
| `paymentState` | csv | `PendingPayment`, `DepositPaid`, `FullyPaid`. An unknown name is a `400`. |
| `overdue` | bool | The **tri-state** overdue filter. **`true`** selects only the bookings whose balance is outstanding **and** whose event date has passed (DEC-041). **`false`** selects only the bookings that are **not** overdue. **Absent** applies no overdue filter at all. An explicit `false` **narrows** the population — it is never silently treated as absent, because an operator who believes they have filtered must not be shown the whole book. |
| `fromDate` | `yyyy-MM-dd` | Inclusive lower bound on `Booking.EventDate`. |
| `toDate` | `yyyy-MM-dd` | Inclusive upper bound on `Booking.EventDate`. |
| `page` | int | 1-based. Default `1`. |
| `pageSize` | int | `10`, `25` or `50`. Default `10`. Anything else is a `400`. |
| `sort` | enum | `EventDateAsc` (default), `EventDateDesc`, `ContractValueDesc`, `ContractValueAsc`, `BookingNumberAsc`. |

- **Response `200 OK`** - `BookingRegisterPageDto`:

```
items           : BookingRegisterItemDto[]
totalCount      : int          <- the filtered population, not the page
page            : int
pageSize        : int
totalPages      : int
summary         : BookingRegisterTotalsDto
quickViewCounts : BookingRegisterFacetsDto
generatedAt     : DateTime
```

- **`BookingRegisterTotalsDto`** - the totals strip. Every figure is over the **whole filtered population**:

```
bookingCount, netContractedValue, collectedAmount, outstandingAmount, totalPax
```

- **`BookingRegisterFacetsDto`** - each facet is computed with **its own dimension removed** (BR-32):

```
packageTypes : [{ key, count }]
venues       : [{ key, count }]
statuses     : [{ key, count }]
paymentStates: [{ key, count }]
```

- **`BookingRegisterItemDto`** - one dossier card:

```
transactionId, bookingNumber,
eventDate, eventEndDate, eventStartTime, eventEndTime, callTime, packoutTime,
status, paymentState, isOverdue, daysUntilEvent,
customerId, customerName, customerCompany, customerContact,
occasion, venue,
packageId, packageName, packageType,
numberOfHeads, pricePerHead, packageAmount,
netAmountDue, amountPaid, outstandingBalance,
extendedAreaId, extendedAreaName, extendedAreaAmount, applyExtendedAmount,
depositAmount, discountCode, discountAmount,
baseGrossProfitPercentage,
discountSource, selectionState, addonCount, addonsTotal
```

- **Field notes:**
  - `status` is the lifecycle-only `BookingStatus` **name** (§5). `paymentState` is one of the three BR-28 states.
  - `isOverdue` is the separate overdue dimension of BR-28: `Outstanding > 0` and the event date has passed (DEC-041).
  - `daysUntilEvent` is an integer the API computes from `IDateTime.UtcNow`; it is `null` when the event is not in the future. The browser never derives it from its own clock.
  - `extendedAreaAmount` is the **frozen** surcharge in pesos (BR-26, DEC-116). It is the amount the booking was created with, **not** the municipality's current rate - a later rate change never moves a stored booking (BR-04).
  - `baseGrossProfitPercentage` is the **planning estimate** for the event (DEC-132): the **selection's** when the booking carries a selection, and the **tier's standard run** otherwise. It is `null` when neither is available. It never enters a booking total.
  - `discountSource` is the **derived** source of the discount (DEC-154): `Code` when a discount code was applied, `HeadcountBand` when the automatic headcount band applied it, `None` otherwise. It is derived, never stored, and it is **published** so that KPI-42 is server-computed rather than guessed.
  - `selectionState` is **exactly one** of `None`, `Selected` or `Locked` (BR-28, M2): `Locked` when the menu-change window has passed, otherwise `Selected` when the booking carries at least one live selection row, otherwise `None`. It is never two flags.
  - `addonCount` is the number of live add-on lines the booking carries, and `addonsTotal` their **frozen line total** (BR-39, DEC-158) - the amount that joins the gross (DEC-135).
  - `netAmountDue + outstandingBalance` and `amountPaid` satisfy `netAmountDue = amountPaid + outstandingBalance` for every card.
- **Responses:** `200`, `400` (an invalid filter), `401`, `403`, `500`.

### 14.2 `GET /api/bookings/summary` - the register aggregate

- **Query parameters:** exactly those of 14.1. The summary describes the **same filtered population** as the list.
- **Response `200 OK`** - `BookingRegisterSummaryDto`:

```
pipeline     : { bookingCount, confirmedCount, tentativeCount, completedCount, otherCount, committedPax }
leadingTier  : { packageType, packageName, netContractedValue, sharePercentage,
                 changePercentage, previousNetContractedValue }
contractValue: { netContractedValue, collectedAmount, outstandingAmount, collectionRatioPercentage }
headcount    : { totalPax, averagePaxPerBooking, nextEventDate, nextEventBookingNumber }
paymentStates: [{ key, count, amount }]
capacity     : { committedPax, capacityPax, utilisationPercentage, isConfigured }
receivables  : { outstandingAmount, bookingCount, overdueCount, overdueAmount }
menuWindows  : { openSelectionCount, lockedSelectionCount }
bandDiscount : { discountedBookingCount, discountedNetAmountDue }
vatPercentage: decimal?
generatedAt  : DateTime
```

- **Field notes:**
  - `pipeline.confirmedCount` counts lifecycle `Confirmed`; `tentativeCount` counts `Pending`; `completedCount` counts `Completed`; `otherCount` is the remainder (KPI-33). Payment state does not alter lifecycle buckets (DEC-177).
  - `leadingTier` is the tier with the highest net contracted value. `sharePercentage` is its share of the population's net contracted value. `changePercentage` compares it with the **immediately preceding equal-length window**; it is `null` when the preceding window has no bookings in that tier (KPI-34, DEC-109).
  - `contractValue.collectedAmount + contractValue.outstandingAmount = contractValue.netContractedValue` (BR-27).
  - `capacity.capacityPax` is read from `Bookings:Capacity:MaximumPax` and is **`null`** when the key is absent; `utilisationPercentage` is then `null` (DEC-102, KPI-38).
  - `receivables` is the register's own AR: the outstanding total, the **count of bookings with an outstanding balance**, the overdue count and the overdue amount, measured from the **event date** as the due date (DEC-041, KPI-39).
  - `menuWindows` counts the bookings whose menu-change window is still **open** and those whose window has **closed** (KPI-41). The window is read from `Bookings:MenuSelection:LockDaysBeforeEvent` (DEC-128).
  - `bandDiscount` counts the bookings priced by the **automatic headcount band** and the net amount due of those bookings (KPI-42, DEC-154). The count is never `null`; it is `0` for an empty population.
  - `vatPercentage` is the **configured** VAT rate, or `null` when the key is absent (DEC-143, BR-42). The client composes the ledger's wording from it and **never** hardcodes a rate.
- **Responses:** `200`, `400`, `401`, `403`, `500`.

### 14.3 `POST /api/bookings/quote` - the server-computed quote

- **Request body** - `BookingQuoteRequestDto`:

```
customerId, packageId, noOfPerson, occasion, venue,
eventDate, startTime, endTime, callTime?, packoutTime?,
extendedAreaId?, applyExtendedAmount,
discountCode?, depositAmount?, initialPaymentAmount?,
selections? : [ { courseId, menuId, prepInstruction? } ],
addons?     : [ { addonId, amount? } ]
```

`selections` and `addons` are **optional** and carry **exactly the same shapes** as the create request (contract 15.2 and 16.4). The quote prices them through the **same code path** the create uses, so the quote's figures and the created booking's figures agree (BR-31, DEC-153).

- **Response `200 OK`** - `BookingQuoteDto`:

```
packageName, packageCode, packageType, minPax, isTierActive,
pricePerHead, baseAmount,
extendedAreaId, extendedAreaName, extendedAreaRate, packageExtendedLocationRate,
extendedAreaAmount,
discountCode, discountType, discountPercentage, discountAmount,
grossAmount, netAmountDue,
requiredDepositPercentage, requiredDepositAmount, depositAmount,
initialPaymentAmount, outstandingBalance,
headcountFloorNote,
capacity         : { committedPax, capacityPax, utilisationPercentage, isConfigured },
standardRunFoodCostPerPax, baseGrossProfitPercentage,
coverage         : { isCovered, coveredAreaCount },
checks           : [{ key, label, isSatisfied, isBlocking, detail }]
discountSource   : None | HeadcountBand | Code
discountBandLabel: string?
discountBandRatePerHead : decimal?
addonsTotal      : decimal
includedVatAmount: decimal?
vatPercentage    : decimal?
courses          : [{ courseId, courseName, requiredQuota, portionGrams, batchMassKg,
                      ruleDescription, qualifiedPool }]
selection        : { isLocked, lockDate, courses: [{ courseId, courseName,
                      selectedMenus: [{ menuId, menuName, note, prepInstruction, foodCost }] }],
                      selectedFoodCostPerPax, selectedGrossProfitPercentage }
```

- **Field notes:**
  - The quote **persists nothing** and changes no price (BR-31).
  - `requiredDepositPercentage` is read from `Bookings:Deposit:RequiredPercentage` and is **`null`** when the key is absent; `requiredDepositAmount` is then `null` and `depositAmount` is the operator's stated amount (DEC-101, BR-30).
  - `coverage.isCovered` is **`null`** when the tier has no coverage set - unknown, never a breach (BR-29).
  - `checks` always contains the five named checks of BR-29. Only `TierActive` has `isBlocking = true`.
  - `standardRunFoodCostPerPax` and `baseGrossProfitPercentage` are **planning estimates** and are `null` when the tier has no costed pool.
  - `discountSource` is the **derived** source of the discount (DEC-154): `Code` when a discount code was applied, `HeadcountBand` when the automatic headcount band applied it, `None` otherwise.
  - `discountBandLabel` is the **server's** human-readable description of the band that applied (for example `"100 to 999 heads"`), and `discountBandRatePerHead` is that band's rate per head. Both are `null` when no band applied. The client composes its wording from these two and **never** hardcodes a bound (DEC-160).
  - `addonsTotal` is the **frozen line total** of the add-ons carried by the request (BR-39, DEC-158) - the amount that joins the gross (DEC-135). It is `0` when the request carries no add-ons.
  - `packageCode` is the tier's frozen business code (DEC-162). `packageExtendedLocationRate` is the tier's own per-guest extended-location amount, `null` when not stated (DEC-163). The combined `extendedAreaAmount` is `(extendedAreaRate + packageExtendedLocationRate) x noOfPerson` when `applyExtendedAmount` is true; when no municipality is selected, only the tier amount applies. DEC-086's null discipline is unchanged: an absent component contributes zero while its published rate stays `null`.
  - `includedVatAmount` is the **VAT-inclusive disclosure** (DEC-143, BR-42): `NetAmountDue - NetAmountDue / (1 + VatPercentage / 100)`. It is **`null`** when `Bookings:Tax:VatPercentage` is absent or the net amount due is zero. It is a **memo** figure disclosed **within** the net amount due and **never** added to it.
  - `vatPercentage` is the **configured** rate, or `null` when the key is absent. The client never hardcodes a rate.
  - `courses` describes the tier's **own rules** - one entry per rule, in the tier's order - and `batchMassKg` is the rule's `PortionGrams x NumberOfHeads / 1000` (DEC-145), `null` when the rule states no portion (or a portion of zero). `qualifiedPool` is the **count** of live dishes in the rule's qualified pool.
  - `selection` describes the selection the request asks for, exactly as `GET /api/bookings/{id}/menus` describes a stored one (contract 15.1).
- **Responses:** `200`, `400` (the validator's messages), `401`, `403`, `500`.

### 14.4 `POST /api/bookings` - create a draft or a confirmed booking

- **Request body** - `CreateBookingRequestDto`. The **existing** fields (`customerId`, `bookType`, `typeOfService`, `noOfPerson`, `occasion`, `venue`, `startDate`, `endDate`, `eventColor`, `packageId`, `reference`, `extendedAreaId`, `applyExtendedAmount`, `specialInstructions`, `overridePricePerHead`) **plus**:

```
callTime?, packoutTime?, discountCode?, depositAmount?,
confirm          : bool
initialPayment?  : { amountPaid, paymentMeans, checkNo?, notes?, particular? }
selections?      : [ { courseId, menuId, prepInstruction? } ]
addons?          : [ { addonId, amount? } ]
```

- **Field notes:**
  - `startDate` and `endDate` are ISO-8601 schedule instants with an explicit `+08:00` offset; they represent Asia/Manila local event times, not UTC wall time. `endDate` may be on a later local calendar date to support overnight events and may not precede `startDate`.
  - `callTime` is the kitchen call time and `packoutTime` the pack-out time (DEC-106). Both are a **time of day** and both are **frozen** on the booking.
  - The **dispatch schedule is ordered against the full event interval** (BR-34): `callTime` is combined with `startDate` and must not be later than the programme start; `packoutTime` is combined with `endDate` and must not be earlier than programme end. A violation is a **`400`**.
  - Overnight events are supported. `startDate` and `endDate` can fall on different local dates; both dates and times are persisted. BG-04 interprets these local schedule instants in Asia/Manila.

- **Response `201 Created`** - `CreateBookingResponseDto`:

```
transactionId, bookingNumber, status, paymentState
```

  `bookingNumber` is the contractual `BK-YYYYMMDD-NNNNN` (BR-09, DEC-016); `status` is the lifecycle-only `BookingStatus` name, and `paymentState` is the separate ledger-derived status.
- **Responses:** `201`, `400` (the validator's messages and the rule failures), `401`, `403`, `500`.
- **Behaviour:**
  - `confirm = false` creates a **draft** in the `Pending` status; `confirm = true` creates a `Confirmed` booking (BR-33, DEC-107).
  - The **frozen snapshot** is stored: `PackageName`, `PricePerHead`, `PackageAmount`, `Subtotal` (base + surcharge), `DiscountType`, `DiscountPercentage`, `DiscountAmount`, `TaxAmount`, `AmountDue`, plus `DiscountCode`, `CallTime`, `PackoutTime` and `DepositAmount`. `Booking.AmountDue` therefore **equals** the calculator's `NetAmountDue` (BR-25, DEC-099).
  - The out-of-town surcharge is `(Area.AdditionalRate + Package.ExtendedLocationAmount) x NumberOfHeads`, applied only when `applyExtendedAmount` is true, and it is **stored** in the frozen `extendedAreaAmount` column (BR-26, DEC-100, DEC-116, DEC-164). When no municipality is selected, only the tier amount applies. After the booking exists the flag is an audit marker; the stored amount is what the calculator charges.
  - The **money chain rounds once** - two decimals, away from the midpoint - and the stored snapshot holds exactly the rounded values, so `Booking.AmountDue` and the calculator's `NetAmountDue` are equal to the cent (DEC-115).
  - The **authorization is two-level**: a draft needs only an authenticated user; a confirmed booking that carries an `initialPayment` additionally requires the `Payments.Record` policy (BR-12).
  - The **discount** is resolved from the `Discount` catalogue by `discountCode`; an unknown code, or a code that resolves to **more than one active** discount (`disc_code` carries no unique index), is a `400` (BR-02).
  - `initialPayment` is recorded in the **same commit** as the booking. An amount larger than the net amount due is a `400` and **no** booking and **no** payment row is created (BR-05, BR-33).
  - The **dispatch schedule** is ordered against the programme (BR-34): `callTime` must not be later than the programme's start and `packoutTime` must not be earlier than its end. A violation is a `400` and nothing is written.
  - The receipt number comes from the **one** generator and follows `OR-YYYYMMDD-XXXX` (BR-05, DEC-112).
  - `selections` and `addons` are **optional** and carry the **same shapes** the quote accepts (contract 14.3), so the quote and the create cannot diverge (BR-31, DEC-153). A selection that breaks a tier's quota, or an add-on that is not active, is a `400` and **nothing** is written.
  - The **frozen snapshot** gains `AddonsTotal` (DEC-151): the sum of the add-on **line totals** joins the gross, and `Booking.AmountDue` still **equals** the calculator's `NetAmountDue` to the cent (BR-25, DEC-099). The add-on total is **stored**, and every read path recomputes it from that stored column rather than from a zero placeholder.

### 14.5 `GET /api/bookings/{id}/contract` - the printable contract

- **Response `200 OK`:** `application/pdf` with a `Content-Disposition` filename.
- **Response `404 Not Found`:** `{ "errors": [ "Booking not found." ] }`.
- The document renders the **shared calculator's** figures - the net amount due, the total payments and the remaining balance - and never the denormalized cache (DEC-113).
- The document **itemises** what it charges: the out-of-town surcharge as its own line (with its per-guest rate and the headcount, so the derivation is visible) whenever the booking carries one, the discount as its own line with the frozen code whenever a discount was applied, and the **dispatch schedule** (BR-34) in the event's particulars - printed **once**, next to the programme times it is ordered against, and omitted entirely when either time is unstated. A zero charge is never printed as a "0.00" row (BR-20).

### 14.6 `GET /api/bookings/export` - the register export

- **Query parameters:** exactly those of 14.1. The export covers the **whole filtered population**, never the page.
- **Response `200 OK`:** `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` with a `Content-Disposition` filename.

### 14.7 What this surface deliberately does not publish

- A **BEO production state** ("ready", "dispatched", "pending sign-off") - there is no BEO entity (DEC-036, DEC-104).
- A **per-event gross margin** that is anything other than a **planning estimate** - the dish food costs are manually entered decimals (DEC-062) and the recipe engine is unbuilt (REQ-MNU-03). The register publishes the **selection's** estimate when the booking carries a selection, and the tier's standard-run estimate otherwise (DEC-132).
- A **tax amount** added to the total - the tax policy is UNRESOLVED (DEC-008) and the price is **VAT-inclusive** (DEC-143, BR-42), so `TaxAmount` stays zero and the VAT is a **disclosure** of the configured rate, not a charge.
- A **deposit percentage** - it is configuration and absent by default (DEC-101, DEC-009).
- A **guest ceiling** - the capacity is configuration and absent by default (DEC-102).
- A **venue master** - the venue is free text on the booking; the register publishes the distinct values it finds.
- A **booking edit** endpoint - the edit screen is a separate requirement.
- Any **allergen**, **quality**, **approval** or **tasting** figure - none of them exists in the model (DEC-150).
---

## 15. Booking Dish Selection & the Menu-Change Window Endpoints (`/api/bookings/{id}/menus`)

> Added by `business/bookings-dish-selection-feature-spec.md` v1.0 (REQ-BKG-06, DEC-127..DEC-134).
> Every figure is **server-computed** (DEC-002). The selection **never** carries money and **never** moves a booking's totals (BR-37).

### 15.0 The authoritative-basis rules binding this surface

1. **The selection is validated by the tier's own course rules (BR-15, BR-35).** For every course rule of the tier, the number of chosen dishes is **exactly** `RequiredQuota`; no dish is chosen from a course the tier has no rule for; every chosen dish is a **live** member of that course's qualified pool. The ruleset is read **live** at the moment of the write.
2. **The selection never moves the money (BR-37, DEC-129).** No money column is written. The tier's per-pax rate stays frozen (BR-04).
3. **The menu-change window is configuration (BR-36, DEC-128).** `LockDate = EventDate - LockDays`, with `LockDays` read from `Bookings:MenuSelection:LockDaysBeforeEvent`. **Absent**, the lock date is the **event date**.
4. **The lock date is always published and is never `null`** (DEC-128). The **days** figure is `null` when the configuration is absent - the "not stated" state belongs to the *days*, never to the *date*.
5. **A write after the lock is refused** with a `400` that names the lock date, and nothing is written (BR-36).
6. **A selection is replaced, never patched (DEC-134).** The rows no longer chosen are soft-deleted; a re-chosen dish resurrects its row. The filtered unique index on (`trn_Id`, `menuId`) is the database-level backstop.
7. **A booking with no selection is served the tier's standard run** (BR-35, DEC-131). A selection is an override, not a requirement, and it is **not** required to confirm a booking.
8. **An explicitly empty `selections` clears** the selection (subject to the lock); an **absent or `null`** member is a `400` (the DEC-085 pattern).
9. **The tax is a VAT-INCLUSIVE disclosure and the rate is configuration** (DEC-143). `Bookings:Tax:VatPercentage` is **absent by default**; the disclosed amount is `NetAmountDue - NetAmountDue / (1 + VatPercentage / 100)`, it is `null` when the key is absent or the net amount due is zero, and it **never** changes `NetAmountDue`.
10. **The batch mass is derived, not invented** (DEC-145): `PortionGrams x NumberOfHeads / 1000`, published per course rule, `null` when the rule states no portion. It is a **planning figure**.

### 15.1 `GET /api/bookings/{id}/menus` - the selection, the pools and the lock state

- **Response `200 OK`** - `BookingMenuSelectionDto`:

| Field | Type | Definition |
| :--- | :--- | :--- |
| `transactionId` | `int` | The booking. |
| `bookingNumber` | `string` | The contractual booking number (BR-09). |
| `eventDate` | `DateOnly` | The event date - the anchor of the lock window. |
| `packageId` | `int?` | The tier. |
| `packageName` | `string` | The tier's description. |
| `menuSelectionLockDate` | `DateOnly` | **Always a concrete date** - never `null` (DEC-128). |
| `menuSelectionLockDays` | `int?` | `null` when the configuration key is absent. |
| `isLocked` | `bool` | Whether the lock date has passed. |
| `selections` | `[{ courseId, courseName, menuId, menuName, note, foodCost, prepInstruction }]` | The **chosen** dishes. Empty when the booking has no selection. `prepInstruction` is the chef's **per-event** instruction for that dish (DEC-146), `null` when not stated. |
| `courses` | `[{ courseId, courseName, requiredQuota, portionGrams, batchMassKg, ruleDescription, pool: [{ menuId, menuName, note, foodCost, price }], chosenMenuIds: [] }]` | The tier's ruleset **with its qualified pools** - the same shape the packages workbench publishes (section 13.1). `batchMassKg` is the rule's `PortionGrams x NumberOfHeads / 1000` (DEC-145), `null` when the rule states no portion or a portion of zero (M3). A **planning figure**. |
| `isCompliant` | `bool` | Whether the selection satisfies BR-15. |
| `complianceMessage` | `string?` | `null` when it complies; otherwise it names the course and the quota. |
| `selectedFoodCostPerPax` | `decimal?` | The sum of the chosen dishes' food costs. `null` when nothing is selected **or** when any chosen dish is **not costed** (DEC-087). |
| `standardRunFoodCostPerPax` | `decimal?` | The tier's standard run (BR-19, DEC-083). |
| `selectedGrossProfitPercentage` | `decimal?` | `(AmountPax - selectedFoodCostPerPax) / AmountPax * 100`. `null` when the selected cost is unknown or the rate is null. |
| `standardRunGrossProfitPercentage` | `decimal?` | The tier's base gross profit percentage. |
| `includedVatAmount` | `decimal?` | The **VAT-inclusive disclosure** for the booking (DEC-143). `null` when no VAT percentage is configured or the net amount due is zero. It is a **memo** figure - disclosed **within** the net amount due, never added to it. |
| `vatPercentage` | `decimal?` | The **configured** VAT rate, or `null` when the key is absent (DEC-160). The client never hardcodes a rate. |
| `selectionState` | `None` \| `Selected` \| `Locked` | **Exactly one** of the three (M2): `Locked` when the lock date has passed, otherwise `Selected` when the booking carries at least one live selection row, otherwise `None`. It is never two flags. |
| `addonCount` | `int` | The number of live add-on lines the booking carries. |
| `addonsTotal` | `decimal` | The **frozen line total** of the add-ons - the amount that joins the gross (DEC-135, DEC-151). |
| `discountSource` | `None` \| `HeadcountBand` \| `Code` | The **derived** source of the frozen discount (DEC-154), exactly as the register publishes it. The menu ledger names the rule that produced the number instead of inferring it. |
| `discountBandLabel` | `string?` | The **server's** human-readable description of the band that applied (DEC-160). `null` when no band applied. The client never composes a bound. |
| `discountBandRatePerHead` | `decimal?` | The band's rate per head. `null` when no band applied. |

- **Responses:** `200`, `404` (the booking does not exist), `401`, `403`, `500`.
- **Notes:** the `courses` array carries the **live** pools, so a client never has to fetch them separately. `foodCost` is the dish's **manually entered** catalogue cost (DEC-062) - a planning input, not an auditable one (RSK-034).

### 15.2 `PUT /api/bookings/{id}/menus` - replace the selection

- **Request body** - the **same shape** as the packages' selection simulator (section 13.3), so one editor pattern serves both screens, with **one** addition - the chef's per-event instruction:

```json
{ "selections": [ { "courseId": 0, "menuId": "DS-101", "prepInstruction": "Walnuts on the side for the allergy table" } ] }
```

- `prepInstruction` is **optional** free text of at most **500** characters. It is stored **on the selection row** and **frozen** with it (DEC-146) - it is never taken from the catalogue and never touches another booking's row.
- The write is a **full replace** (DEC-134), so an **omitted** `prepInstruction` **clears** the instruction (DEC-155). The client always sends the instruction it is showing, so a chef's note is never wiped by a save that did not carry it.
- **Response `200 OK`:** the **saved** selection, in the `BookingMenuSelectionDto` shape of 15.1 - not the request.
- **Response `400 Bad Request`:** the BR-15 breach (naming the course and the quota), a dish that is not a live member of the pool, a duplicate dish, a `null` or absent `selections`, a **cancelled** booking, or a write **after** the lock date (naming the date).
- **Response `404 Not Found`:** the booking does not exist.
- **Concurrency:** the replace is **last write wins** (M9). `Booking.RowVersion` guards the booking, not the selection rows, and a stale booking version is a `400`.
- **The selection never moves the money** (BR-37, DEC-129). Replacing the dishes leaves `AmountDue`, the discount and the deposit exactly as they were.

### 15.3 `POST /api/bookings` - the extended create

`CreateBookingRequestDto` gains **two** optional members:

```
selections? : [{ courseId, menuId, prepInstruction? }]
addons?     : [{ addonId, amount? }]
```

- `selections` is validated by the **same** BR-15 rule and stored **in the same commit** as the booking, so a booking and its dishes are never half-written (DEC-134).
- `addons` is validated by the **same** rules as `PUT /api/bookings/{id}/addons` (contract 16.4) and stored in the same commit; its **line total** joins the gross and is **frozen** onto the booking (DEC-151, DEC-158).
- An **absent** member means **no selection** and **no add-ons** - the standard run applies (DEC-131).
- An **explicitly empty** array is a `400` on create: there is nothing to clear on a booking that does not exist yet.

### 15.4 What this surface deliberately does not publish

- Any **money** derived from the selection - the selection never reprices a booking (BR-37).
- An **authoritative** food cost or margin - the dish costs are manually entered decimals (DEC-062) and the recipe engine is unbuilt (REQ-MNU-03). Both figures are planning estimates.
- A **dish approval** workflow - no approval state exists in the model.
- Any **allergen**, **quality**, **approval** or **tasting** figure - none of them exists in the model (DEC-150).
- A **separate lock action** - the cutoff is the only lock (DEC-149).

### 15.5 `GET /api/bookings/{id}/beo` - the Kitchen Banquet Event Order

- **Response `200 OK`:** `application/pdf` with a `Content-Disposition` filename.
- The document is the QuestPDF `KitchenBeoDocument`, rendered from the booking's **selection** - the "menu items" REQ-REP-02 has always asked for and could never have (DEC-148, DEC-159).
- The document is **extended** so the kitchen's own words reach the sheet: it carries the **per-event prep instruction** (the catalogue's ingredient note alone would print the wrong text - it is not what the chef typed for this event) and the booking's **add-on lines**, which the kitchen may have to staff for.
- A booking with **no** selection prints the tier's **course rules** and states plainly that the event runs the tier's standard run and that **no dish selection has been recorded**. It prints **no dish list**: BR-19 defines the standard run as a **cost figure**, the model holds no canonical standard dish list, and printing the cheapest dish in each pool would be an invented rule (DEC-159).
- **Responses:** `200`, `404` (the booking does not exist), `401`, `403`, `500`.
---

## 16. Booking Add-Ons & the Add-On Master Endpoints (`/api/addons`, `/api/bookings/{id}/addons`)

> Added by `business/bookings-events-operations-feature-spec.md` v1.0 (REQ-BKG-07, DEC-135, DEC-139, DEC-140, BR-39, BR-41).

### 16.0 The authoritative-basis rules binding this surface

1. **The add-on total joins the gross** (DEC-135, BR-41). The gross is `BasePackage + ExtendedAreaFee + AddonsTotal + OtherCharges` (BR-01) - BR-01's third term, which has been `0m` until now only because nothing could create one.
2. **The line amount is frozen** (DEC-139, BR-04). The **frozen** line amount is what enters the gross; a later catalogue price change never moves a stored booking.
3. **The line amount is the commercial truth** (DEC-140, DEC-158). The frozen line stores the **line total**; no separate quantity column is stored, so an amount and a quantity can never disagree. For a `PerGuest` add-on the headcount is the quantity, and it is expressed **within** the line - the line total is `unit price x heads`, and the unit price is derived from the line total for display.
4. **The lines are replaced, never patched** (DEC-134). The lines no longer carried are soft-deleted; a re-added add-on resurrects its row. The filtered unique index on (`trn_Id`, `addonId`) is the database-level backstop.
5. **The menu-change window governs the add-ons too** (BR-36, BR-39). A write after the lock date is refused with a `400` that names the date, and nothing is written.
6. **The booking picker's read is active-only; the master is now administered from Settings** (DEC-168, 2026-10-24). `GET /api/addons` remains the booking picker's active-only read. The administration endpoints under `/api/addons/manage` and the `POST`/`PUT`/`DELETE` routes expose the CRUD, and `DELETE` is a **soft delete**. The picker still never writes an add-on; the administration surface does.
7. **The pricing basis is an explicit column, not a string test** (DEC-158). `AddonDetail.PricingBasis` is `Flat` or `PerGuest`, and **`null` means not stated and is treated as `Flat`** - the safe direction, because it never multiplies a price that was not meant to be multiplied. The `unit` stays a **descriptive** unit of measure.
8. **The amount is the LINE TOTAL** (DEC-158). The request's optional `amount` is the **line total** the operator confirms. When it is absent the server derives the default: `PerGuest` gives `catalogueAmount x NumberOfHeads`, `Flat` gives `catalogueAmount`. The frozen line records the **line total**, and the per-guest unit price is derived from it (`line total / heads`) for display - never the other way round.
9. **The add-on write RE-FREEZES the money** (DEC-152). The add-on total is recomputed, `RecalculateFinancials` re-freezes `Subtotal`, `DiscountAmount` and `AmountDue` (BR-04 already anticipates "line-item addition"), the **frozen** discount amount is **re-capped** at the new gross but **not** recomputed - the band depends on the headcount, which did not change - the **deposit is not** touched, and a write that would leave the **collected amount above the new net amount due** is a `400`, because BR-05 forbids a payment above the net due and the operator must settle the credit first.
10. **The add-on total is STORED on the booking** (DEC-151). `Booking.AddonsTotal` is the **frozen** sum of the line totals, and the authoritative calculator reads it - so `Booking.AmountDue` and the calculator's `NetAmountDue` remain equal to the cent (DEC-099).

### 16.1 `GET /api/addons` - the add-on master

- **Query parameters:**

| Parameter | Type | Meaning |
| :--- | :--- | :--- |
| `categoryId` | `int?` | Optional - the category to filter by. Absent applies no category filter. |
| `headcount` | `int` | **Required.** The headcount `lineAmountForBooking` is computed for (DEC-158) - the booking's `NumberOfHeads` from the picker's context. Absent or `< 1` is a `400`: the endpoint prices a line for a booking, and a default of one guest would publish a per-guest unit price indistinguishable from a genuine one-guest booking (BR-20). |

- **Response `200 OK`** - `List<AddonMasterItemDto>`:

| Field | Type | Definition |
| :--- | :--- | :--- |
| `addonId` | `int` | The add-on. |
| `description` | `string` | The catalogue description. |
| `amount` | `decimal` | The catalogue amount - the **unit** amount, per guest when the basis is `PerGuest`. |
| `unit` | `string?` | The unit of measure. `null` = not stated. **Descriptive only** - it never decides the pricing basis. |
| `pricingBasis` | `Flat` \| `PerGuest` \| `null` | The pricing basis. `null` = **not stated**, and the server treats it as `Flat` (DEC-158). |
| `lineAmountForBooking` | `decimal` | The line total this add-on would carry on a booking of the given headcount - `amount x heads` for `PerGuest`, `amount` for `Flat`. Published so the client never performs the multiplication itself. |
| `addonCategoryId` | `int?` | The category. **`null` = not stated** - an add-on may pre-date the category master. |
| `addonCategoryName` | `string?` | The category's name. `null` when the add-on has no category. |
| `deptId` | `int?` | The owning department (DEC-165). `null` = not stated. |
| `deptName` | `string?` | The department's name. `null` when the add-on has no department. |
| `isActive` | `bool` | Only the **active** rows are returned. |

### 16.2 `GET /api/addons/categories` - the category master

- **Response `200 OK`** - `List<AddonCategoryDto>`: `addonCategoryId`, `addonCategoryName`.

### 16.2a The add-on administration surface (Settings → Add-ons, 2026-10-24)

The administration surface is separate from the booking picker: it publishes **active and inactive** rows and excludes the soft-deleted ones, so an administrator can see and reactivate a deactivated add-on.

#### `GET /api/addons/manage` - the administration list

- **Response `200 OK`** - `List<AddonAdminItemDto>`:

| Field | Type | Definition |
| :--- | :--- | :--- |
| `addonId` | `int` | The add-on. |
| `addonCategoryId` | `int?` | The category. `null` = not stated. |
| `addonCategoryName` | `string?` | The category's name. `null` when no category. |
| `deptId` | `int?` | The owning department. `null` = not stated. |
| `deptName` | `string?` | The department's name. `null` when no department. |
| `pricingBasis` | `Flat` \| `PerGuest` \| `null` | `null` = not stated, treated as `Flat` (DEC-158). |
| `description` | `string` | The catalogue description. |
| `unitPrice` | `decimal` | The **unit** amount - per guest for `PerGuest`, the whole line for `Flat`. |
| `unit` | `string?` | The descriptive unit of measure. |
| `isActive` | `bool` | The row's state; inactive rows are listed and can be reactivated. |

#### `GET /api/addons/manage/{id}` - one add-on

- **Response `200 OK`:** one `AddonAdminItemDto`.
- **Response `404 Not Found`:** `{ "errors": [ "Add-on not found." ] }` - no live add-on carries that id.

#### `POST /api/addons` - create an add-on

- **Request body:** `AddonUpsertRequest`:

| Field | Type | Rule |
| :--- | :--- | :--- |
| `description` | `string` | Required, maximum **150** characters (the column). |
| `addonCategoryId` | `int?` | Optional; when present it must be a **live** category. |
| `deptId` | `int?` | Optional; when present it must be a **live** department. |
| `pricingBasis` | `Flat` \| `PerGuest` \| `null` | `null` = not stated (treated as `Flat`). |
| `unitPrice` | `decimal` | `>= 0`. |
| `unit` | `string?` | Optional, maximum **50** characters. |
| `isActive` | `bool` | Defaults to `true`. |

- **Response `201 Created`:** `{ "addonId": 12 }`.
- **Response `400 Bad Request`:** the validator's messages, or `The add-on category does not exist.` / `The department does not exist.` Nothing is persisted.

#### `PUT /api/addons/{id}` - update an add-on

- **Request body:** the same `AddonUpsertRequest`.
- **Response `204 No Content`** on success.
- **Response `400 Bad Request`:** the payload was rejected.
- **Response `404 Not Found`:** no live add-on carries that id.

#### `DELETE /api/addons/{id}` - soft-delete an add-on

- **Response `204 No Content`** on success. The row is flagged (`IsDeleted = true`), never physically destroyed, so a booking's frozen line still refers to a real catalogue row.
- **Response `404 Not Found`:** no live add-on carries that id (a second delete is a 404).

### 16.2b `GET /api/departments` - the department lookup

- **Response `200 OK`** - `List<DepartmentDto>`: `deptId`, `deptName`. It is read-only; the master ships empty (BR-13), so an empty list is a valid answer and the add-on form leaves the selector empty.

### 16.3 `GET /api/bookings/{id}/addons` - the booking's add-on lines

- **Response `200 OK`** - `BookingAddonSetDto`:

```
transactionId, bookingNumber, eventDate,
menuSelectionLockDate : DateOnly   <- always a concrete date
menuSelectionLockDays : int?       <- null when the configuration is absent
isLocked              : bool
addonsTotal          : decimal     <- the frozen line total that joins the gross
addons : [{ addonId, description, amount, unit, pricingBasis,
            addonCategoryId, addonCategoryName,
            catalogueAmount, catalogueDescription }]
```

- **Field notes:** `description`, `amount`, `unit` and `pricingBasis` are the **frozen** line values. `amount` is the **line total** - what joins the gross (DEC-158). `catalogueAmount` and `catalogueDescription` are the catalogue's **current** values, published alongside so the operator can see when a catalogue price has moved since the line was frozen. A `null` catalogue field means the catalogue row is gone or soft-deleted - **not** that the line is zero.

### 16.4 `PUT /api/bookings/{id}/addons` - replace the add-on lines

- **Request body:** `{ "addons": [ { "addonId": 3, "amount": 18000.00 } ] }`
  - `addonId` is **required** and must be an **active** catalogue row.
  - `amount` is **optional** and is the **line total** the operator confirms (DEC-158). When it is **absent**, the server derives the default from the catalogue's amount and the basis - `PerGuest` gives `amount x NumberOfHeads`, `Flat` gives `amount`. When it is **present**, it is the line total, and the server does **not** multiply it again.
  - The request carries **no** quantity and **no** basis. Both are the catalogue's business; the operator states the commercial truth of the line.
- **Response `200 OK`:** the **saved** lines, in the `BookingAddonSetDto` shape of 16.3 - not the request.
- **Response `400 Bad Request`:** a `null` or absent `addons` member; an add-on that is not **active** in the catalogue; a negative line total; a **duplicate** add-on on one booking; a **cancelled** booking; a write **after** the lock date (naming the date); or a write that would leave the **collected amount above the new net amount due** (DEC-152).
- **Response `404 Not Found`:** the booking does not exist.
- **The write re-freezes the money** (DEC-152): the add-on total is recomputed, `Subtotal`, `DiscountAmount` and `AmountDue` are re-frozen, the frozen discount is **re-capped** at the new gross but **not** recomputed, the **deposit is not** changed, and the write is **refused** when it would overpay the booking.

### 16.5 What this surface deliberately does not publish

- An **authoritative** add-on cost - the amounts are catalogue and operator values, frozen on the line, and no recipe or yield engine underlies them (REQ-MNU-03).
- Any **create or edit** of the **category** or **department** masters - they are read-only lookups (DEC-169). The **add-on** master itself is administrable from Settings → Add-ons (DEC-168, §16.2a); if the master is empty, the booking picker is empty until an add-on is created (RSK-066, RSK-071, M12).
- Any **allergen**, **quality** or **approval** figure attached to an add-on - none of them exists in the model (DEC-150).
