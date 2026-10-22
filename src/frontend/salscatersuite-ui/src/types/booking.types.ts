/**
 * TypeScript mirror of the Bookings & Events contract.
 *
 * Source: `business/bookings-events-feature-spec.md` v1.1 §3 and §4 and
 * `architecture/approved-api-contracts.md` sections 1 and 14 (REQ-BKG-05 /
 * REQ-BKG-01).
 *
 * Every monetary figure, every percentage and every count below is
 * **server-computed** (DEC-002). The browser formats and displays; it never
 * derives an authoritative amount, a payment state, a countdown or a capacity
 * load of its own.
 *
 * `null` means *unknown*, never zero. An unconfigured capacity, an absent
 * tier, an empty population and an absent preceding period all publish `null`,
 * and the client renders an em dash for them (contract rule 7, "no
 * null-as-zero").
 */

import type { BookingStatus } from './dashboard.types'

/**
 * BR-28 — the **payment-state** vocabulary (DEC-117).
 *
 * These three names answer "how far along is the payment?" and are mutually
 * exclusive and exhaustive. They are **not** lifecycle names: the lifecycle
 * vocabulary (`BookingStatus`) owns `DepositPaid` and `FullyPaid`, and the two
 * vocabularies never share a name.
 */
export type BookingPaymentState = 'AwaitingDeposit' | 'PartiallyPaid' | 'Settled'

/**
 * The `BookingType` enum names — the wire value is the enum **name**
 * (contract §8).
 */
export type BookingTypeName = 'Inside' | 'Outside'

/**
 * The `ServiceType` enum names — the wire value is the enum **name**
 * (contract §8).
 */
export type BookingServiceTypeName =
  | 'Catering'
  | 'BuffetSetup'
  | 'Plated'
  | 'FoodPacks'
  | 'DropOff'
  | 'Pickup'

/**
 * The `PaymentMeans` enum names (contract §8).
 *
 * The client's labels are the operator's wording; the **wire value** is always
 * the enum name.
 */
export type BookingPaymentMeansName = 'Cash' | 'Check' | 'GCash' | 'LDDAP' | 'CreditCard'

/* ------------------------------------------------------------------------- *
 * Contract section 1 — the booking details projection.
 * ------------------------------------------------------------------------- */

/**
 * `GET /api/bookings/{id}` — the full booking details (contract §1).
 *
 * **This is the mirror of the read projection `BookingDto`**, not of the
 * register item: the details endpoint publishes the booking's frozen price
 * snapshot, its dispatch schedule and its money breakdown, and it publishes
 * them for **one** booking rather than for a page of a filtered population.
 *
 * The client does **not** consume this endpoint yet — the booking **detail**
 * and **edit** screens are out of scope for this cycle. The mirror is kept
 * complete and current so that the screen which reads it later cannot be
 * built against a stale shape.
 */
export interface BookingDetails {
  transactionId: number
  /** The contractual `BK-YYYYMMDD-NNNNN` number (BR-09, DEC-016). */
  bookingNumber: string
  /** The booking's transaction timestamp, as an ISO date-time. */
  transactionDate: string
  /** The `BookingType` enum name. */
  bookType: BookingTypeName
  /** The `ServiceType` enum name. */
  typeOfService: BookingServiceTypeName
  customerId: number
  customerName: string
  customerContact: string | null
  occasion: string | null
  venue: string | null
  /** `yyyy-MM-dd`. */
  eventDate: string
  /** `HH:mm:ss`. */
  eventStartTime: string
  /** `HH:mm:ss`. */
  eventEndTime: string
  /** The kitchen call time. `null` when the booking carries none (DEC-106). */
  callTime: string | null
  /** The pack-out time. `null` when the booking carries none (DEC-106). */
  packoutTime: string | null
  numberOfHeads: number
  eventColor: string | null
  packageId: number | null
  packageName: string | null
  reference: string | null
  /** The frozen per-guest rate. */
  pricePerHead: number
  /** The frozen base amount. */
  packageAmount: number
  /**
   * The **frozen** out-of-town surcharge in pesos (BR-26, DEC-116) — the
   * amount the booking was created with, **never** the municipality's current
   * rate. It is `0` when no surcharge was applied.
   */
  extendedAreaAmount: number
  /**
   * The per-guest rate the **frozen** surcharge was derived from — the frozen
   * amount divided by the headcount, which recovers the municipality's rate
   * exactly (BR-17, BR-26).
   *
   * It is published so a reader of the printed contract can see **how** the
   * surcharge was derived (rate × pax). It is **`null` when no surcharge was
   * applied** — "not stated" is null, never zero (BR-20) — and a client that
   * renders it must show an em dash, never `0` and never a peso zero
   * (`utils/currencyFormatter.ts`).
   */
  extendedAreaRatePerGuest: number | null
  /** The frozen subtotal — the base amount plus the surcharge. */
  subtotal: number
  discountType: string
  discountPercentage: number
  /** The **frozen** code of the applied discount. `null` when none applied. */
  discountCode: string | null
  discountAmount: number
  taxAmount: number
  /** The net amount due — the shared calculator's figure (BR-25, DEC-099). */
  amountDue: number
  depositAmount: number
  amountPaid: number
  balance: number
  /** The BR-10 lifecycle status. */
  status: BookingStatus
  reminderSent: boolean
}

/* ------------------------------------------------------------------------- *
 * 14.1 — the register.
 * ------------------------------------------------------------------------- */

/**
 * One dossier card of the Bookings & Events Register (contract §14.1).
 *
 * Every figure is the server's. The client formats it and displays it.
 */
export interface BookingRegisterItem {
  transactionId: number
  /** The contractual `BK-YYYYMMDD-NNNNN` number (BR-09, DEC-016). */
  bookingNumber: string
  /** `yyyy-MM-dd`. */
  eventDate: string
  /** `HH:mm:ss`. */
  eventStartTime: string
  /** `HH:mm:ss`. */
  eventEndTime: string
  /** The kitchen call time. `null` when the booking carries none (DEC-106). */
  callTime: string | null
  /** The pack-out time. `null` when the booking carries none (DEC-106). */
  packoutTime: string | null
  /** The BR-10 lifecycle status. */
  status: BookingStatus
  /** The BR-28 payment state. */
  paymentState: BookingPaymentState
  /** The overdue dimension — a separate axis from the payment state. */
  isOverdue: boolean
  /**
   * The server's countdown. `null` when the event is not in the future.
   * The browser never derives it from its own clock.
   */
  daysUntilEvent: number | null
  customerId: number
  customerName: string
  customerCompany: string | null
  customerContact: string | null
  occasion: string | null
  venue: string | null
  packageId: number | null
  packageName: string | null
  /** The stored, free-text tier type. */
  packageType: string | null
  numberOfHeads: number
  pricePerHead: number
  packageAmount: number
  /** The net amount due — the shared calculator's figure (BR-25, DEC-099). */
  netAmountDue: number
  /** `Σ Payment.AmountPaid`, excluding a soft-deleted payment (DEC-111). */
  amountPaid: number
  /** `max(0, netAmountDue − amountPaid)` (BR-03). */
  outstandingBalance: number
  extendedAreaId: number | null
  extendedAreaName: string | null
  /** The **frozen** out-of-town surcharge (BR-26, DEC-116). */
  extendedAreaAmount: number
  /** The creation-time switch — an audit marker once the booking exists. */
  applyExtendedAmount: boolean
  /** The agreed deposit. `null` when none was agreed. */
  depositAmount: number | null
  /** The frozen discount code (BR-02). */
  discountCode: string | null
  discountAmount: number
  /** The tier's **planning estimate**. `null` when the tier has no costed pool. */
  baseGrossProfitPercentage: number | null
}

/**
 * The totals strip (contract §14.1).
 *
 * Every figure is over the **whole filtered population**, never over the page.
 */
export interface BookingRegisterTotals {
  bookingCount: number
  netContractedValue: number
  collectedAmount: number
  outstandingAmount: number
  totalPax: number
}

/** One facet bucket and its server-computed count. */
export interface BookingFacetCount {
  key: string
  count: number
}

/**
 * The quick-view facet counts (contract §14.1).
 *
 * Each facet is computed with **its own dimension removed**, so every count
 * answers "how many would I get if I chose this?".
 */
export interface BookingRegisterFacets {
  packageTypes: BookingFacetCount[]
  venues: BookingFacetCount[]
  statuses: BookingFacetCount[]
  paymentStates: BookingFacetCount[]
}

/** `GET /api/bookings` — the paged, filtered register (contract §14.1). */
export interface BookingRegisterPage {
  /** The page. */
  items: BookingRegisterItem[]
  /** The filtered population, not the page. */
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  /** The totals strip. */
  summary: BookingRegisterTotals
  /** The facet counts. */
  quickViewCounts: BookingRegisterFacets
  generatedAt: string
}

/* ------------------------------------------------------------------------- *
 * 14.2 — the register aggregate.
 * ------------------------------------------------------------------------- */

/** KPI-33 — the pipeline count and its status split. */
export interface BookingPipelineSummary {
  bookingCount: number
  confirmedCount: number
  tentativeCount: number
  completedCount: number
  otherCount: number
  committedPax: number
}

/** KPI-34 — the leading tier, its share and its period change. */
export interface BookingLeadingTier {
  packageType: string | null
  packageName: string | null
  netContractedValue: number
  /** The share of the population's net contracted value. `null` = unknown. */
  sharePercentage: number | null
  /** The change against the preceding equal-length period. `null` = unknown. */
  changePercentage: number | null
  previousNetContractedValue: number | null
}

/** KPI-35 — the net contracted value, the collected and the outstanding. */
export interface BookingContractValueSummary {
  netContractedValue: number
  collectedAmount: number
  outstandingAmount: number
  collectionRatioPercentage: number | null
}

/** KPI-36 — the committed headcount. */
export interface BookingHeadcountSummary {
  totalPax: number
  averagePaxPerBooking: number | null
  nextEventDate: string | null
  nextEventBookingNumber: string | null
}

/** KPI-37 — the payment-state distribution. */
export interface BookingPaymentStateAggregate {
  key: BookingPaymentState
  count: number
  amount: number
}

/** KPI-38 — the near-term commissary load against the configured capacity. */
export interface BookingCapacitySummary {
  committedPax: number
  /** `null` when no capacity is configured (DEC-102). */
  capacityPax: number | null
  /** `null` when no capacity is configured. */
  utilisationPercentage: number | null
  /** `false` when the configuration key is absent. */
  isConfigured: boolean
}

/** KPI-39 — the register's own receivables and overdue figures. */
export interface BookingReceivablesSummary {
  outstandingAmount: number
  /** The count of bookings carrying an outstanding balance. */
  bookingCount: number
  overdueCount: number
  overdueAmount: number
}

/** `GET /api/bookings/summary` (contract §14.2). */
export interface BookingRegisterSummary {
  pipeline: BookingPipelineSummary
  /** `null` when the population is empty. */
  leadingTier: BookingLeadingTier | null
  contractValue: BookingContractValueSummary
  headcount: BookingHeadcountSummary
  paymentStates: BookingPaymentStateAggregate[]
  capacity: BookingCapacitySummary
  receivables: BookingReceivablesSummary
  generatedAt: string
}

/* ------------------------------------------------------------------------- *
 * 14.3 — the quote.
 * ------------------------------------------------------------------------- */

/** The body of `POST /api/bookings/quote` (contract §14.3). */
export interface BookingQuoteRequest {
  customerId: number
  packageId: number
  noOfPerson: number
  occasion: string
  venue: string
  /** `yyyy-MM-dd`. */
  eventDate: string
  /** `HH:mm:ss`. */
  startTime: string
  /** `HH:mm:ss`. */
  endTime: string
  /** `HH:mm:ss` — `null` when the operator stated none. */
  callTime: string | null
  /** `HH:mm:ss` — `null` when the operator stated none. */
  packoutTime: string | null
  extendedAreaId: number | null
  applyExtendedAmount: boolean
  discountCode: string | null
  depositAmount: number | null
  initialPaymentAmount: number | null
}

/**
 * The municipality coverage verdict (BR-29).
 *
 * `isCovered` is `null` when the tier holds no coverage set — **unknown**,
 * never a breach.
 */
export interface BookingQuoteCoverage {
  isCovered: boolean | null
  coveredAreaCount: number
}

/** One of the five pre-dispatch checks (BR-29, BR-33). */
export interface BookingQuoteCheck {
  key: string
  label: string
  isSatisfied: boolean
  /** `true` only for the deactivated-tier check. */
  isBlocking: boolean
  detail: string | null
}

/** `POST /api/bookings/quote` (contract §14.3). */
export interface BookingQuote {
  packageName: string | null
  packageType: string | null
  /** The headcount floor. `null` when the tier sets none. */
  minPax: number | null
  isTierActive: boolean
  pricePerHead: number | null
  /** The frozen base amount. */
  baseAmount: number | null
  extendedAreaId: number | null
  extendedAreaName: string | null
  /** The municipality's per-pax rate. `null` = not stated. */
  extendedAreaRate: number | null
  extendedAreaAmount: number | null
  discountCode: string | null
  discountType: string | null
  discountPercentage: number | null
  discountAmount: number | null
  grossAmount: number | null
  netAmountDue: number | null
  /** The configured required percentage. `null` when none is configured. */
  requiredDepositPercentage: number | null
  /** `null` when no required percentage is configured (DEC-101). */
  requiredDepositAmount: number | null
  /** The operator's stated deposit. */
  depositAmount: number | null
  initialPaymentAmount: number | null
  outstandingBalance: number | null
  /** The headcount floor note — advisory, never a block (BR-16). */
  headcountFloorNote: string | null
  capacity: BookingCapacitySummary
  /** The tier's standard-run food cost. A planning estimate. */
  standardRunFoodCostPerPax: number | null
  /** The tier's base gross profit percentage. A planning estimate. */
  baseGrossProfitPercentage: number | null
  coverage: BookingQuoteCoverage
  checks: BookingQuoteCheck[]
}

/* ------------------------------------------------------------------------- *
 * 14.4 — the create.
 * ------------------------------------------------------------------------- */

/** The initial payment recorded with a confirmed booking (contract §14.4). */
export interface BookingInitialPayment {
  amountPaid: number
  /** The `PaymentMeans` enum **name**. */
  paymentMeans: BookingPaymentMeansName
  checkNo: string | null
  notes: string | null
  particular: string | null
}

/** The body of `POST /api/bookings` (contract §14.4). */
export interface CreateBookingRequest {
  customerId: number
  /** The `BookingType` enum name. */
  bookType: BookingTypeName
  /** The `ServiceType` enum name. */
  typeOfService: BookingServiceTypeName
  noOfPerson: number
  occasion: string
  venue: string
  /** The event start, as an ISO date-time. */
  startDate: string
  /** The event end, as an ISO date-time. */
  endDate: string
  eventColor: string | null
  packageId: number | null
  reference: string | null
  extendedAreaId: number | null
  applyExtendedAmount: boolean
  specialInstructions: string | null
  overridePricePerHead: number | null
  /** `HH:mm:ss` — `null` when the operator stated none. */
  callTime: string | null
  /** `HH:mm:ss` — `null` when the operator stated none. */
  packoutTime: string | null
  discountCode: string | null
  depositAmount: number | null
  /** `false` creates a **draft** (a `Pending` booking); `true` confirms it. */
  confirm: boolean
  /** The initial payment — omitted entirely when there is none. */
  initialPayment?: BookingInitialPayment
}

/** The `201` body of `POST /api/bookings` (contract §14.4). */
export interface CreateBookingResponse {
  transactionId: number
  /** The contractual `BK-YYYYMMDD-NNNNN` number. */
  bookingNumber: string
  /** The `BookingStatus` name of the created booking. */
  status: BookingStatus
}

/* ------------------------------------------------------------------------- *
 * The client's own filter vocabulary.
 * ------------------------------------------------------------------------- */

/**
 * The orderings the register offers (contract §14.1).
 *
 * The register publishes a **chronological** order, so the timeline view is a
 * server-side sort rather than a second screen or a local re-ordering.
 */
export type BookingSortOption =
  | 'EventDateAsc'
  | 'EventDateDesc'
  | 'ContractValueDesc'
  | 'ContractValueAsc'
  | 'BookingNumberAsc'

/** The register's page sizes — the values the contract accepts. */
export type BookingPageSize = 10 | 25 | 50
