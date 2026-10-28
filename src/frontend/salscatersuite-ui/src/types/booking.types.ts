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
  /** The **derived** source of the discount (DEC-154). */
  discountSource: BookingDiscountSource
  /** Exactly one selection state (BR-28, M2). */
  selectionState: BookingSelectionState
  /** The number of live add-on lines the booking carries. */
  addonCount: number
  /** The **frozen** add-on line total that joins the gross (DEC-151). */
  addonsTotal: number
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
  /** KPI-41 — the open and the locked menu-change windows. */
  menuWindows: BookingMenuWindowSummary
  /** KPI-42 — the reach of the automatic headcount-band discount. */
  bandDiscount: BookingBandDiscountSummary
  /** The configured VAT rate. `null` when the key is absent (DEC-143). */
  vatPercentage: number | null
  generatedAt: string
}

/** KPI-41 — the count of bookings whose menu selection is still editable. */
export interface BookingMenuWindowSummary {
  openSelectionCount: number
  lockedSelectionCount: number
}

/** KPI-42 — the bookings the automatic headcount band priced, and their value. */
export interface BookingBandDiscountSummary {
  discountedBookingCount: number
  discountedNetAmountDue: number
}

/* ------------------------------------------------------------------------- *
 * 15.1 — the dish selection, the qualified pools and the lock state.
 * ------------------------------------------------------------------------- */

/**
 * Exactly one selection state (BR-28, M2).
 *
 * `Locked` when the menu-change window has passed, otherwise `Selected` when
 * the booking carries at least one live selection row, otherwise `None`.
 */
export type BookingSelectionState = 'None' | 'Selected' | 'Locked'

/** The **derived** source of the discount (DEC-154). */
export type BookingDiscountSource = 'None' | 'HeadcountBand' | 'Code'

/**
 * One dish the operator chose (contract §15.2).
 *
 * `prepInstruction` is the chef's **per-event** instruction, stored on the
 * selection row and frozen with it (DEC-146). The write is a full replace, so
 * an omitted instruction clears it (DEC-155).
 */
export interface BookingMenuSelectionRequestItem {
  courseId: number
  menuId: string
  prepInstruction?: string | null
}

/** The body of `PUT /api/bookings/{id}/menus` (contract §15.2). */
export interface BookingMenuSelectionRequest {
  selections: BookingMenuSelectionRequestItem[]
}

/** One chosen dish of a stored selection (contract §15.1). */
export interface BookingMenuSelectedDish {
  courseId: number
  courseName: string
  menuId: string
  menuName: string
  /** The dish's catalogue ingredient note. `null` = not stated. */
  note: string | null
  /** The dish's manually entered food cost. `null` when it is not costed. */
  foodCost: number | null
  /** The chef's per-event instruction. `null` = not stated. */
  prepInstruction: string | null
}

/** One dish of a course's qualified pool (contract §15.1). */
export interface BookingMenuPoolDish {
  menuId: string
  menuName: string
  /** The dish's catalogue ingredient note. `null` = not stated. */
  note: string | null
  /** The dish's manually entered food cost. `null` when it is not costed. */
  foodCost: number | null
  /** The dish's selling price. `null` when it carries none. */
  price: number | null
}

/** One course rule together with its qualified pool (contract §15.1). */
export interface BookingMenuCourse {
  courseId: number
  courseName: string
  /** The number of dishes the client must take from this course. */
  requiredQuota: number
  /** The standard portion per guest in grams. `null` = not specified. */
  portionGrams: number | null
  /** The rule's derived batch mass. `null` when no portion is stated. */
  batchMassKg: number | null
  ruleDescription: string
  /** The live dishes of the course. */
  pool: BookingMenuPoolDish[]
  /** The dishes the booking currently carries for this course. */
  chosenMenuIds: string[]
}

/** `GET /api/bookings/{id}/menus` (contract §15.1). */
export interface BookingMenuSelectionDto {
  transactionId: number
  bookingNumber: string
  /** `yyyy-MM-dd`. */
  eventDate: string
  packageId: number | null
  packageName: string
  /** **Always a concrete date** — never `null` (DEC-128). */
  menuSelectionLockDate: string
  /** `null` when the configuration key is absent. */
  menuSelectionLockDays: number | null
  isLocked: boolean
  selections: BookingMenuSelectedDish[]
  courses: BookingMenuCourse[]
  isCompliant: boolean
  /** `null` when it complies; otherwise it names the course and the quota. */
  complianceMessage: string | null
  /** The sum of the chosen dishes' food costs. `null` when unknown. */
  selectedFoodCostPerPax: number | null
  standardRunFoodCostPerPax: number | null
  selectedGrossProfitPercentage: number | null
  standardRunGrossProfitPercentage: number | null
  /** The VAT-inclusive memo. `null` when no rate is configured. */
  includedVatAmount: number | null
  /** The configured VAT rate. `null` when the key is absent. */
  vatPercentage: number | null
  selectionState: BookingSelectionState
  addonCount: number
  /** The frozen add-on line total that joins the gross. */
  addonsTotal: number
  /** The derived source of the frozen discount (DEC-154). */
  discountSource: BookingDiscountSource
  /** The server's description of the band that applied. `null` when none (DEC-160). */
  discountBandLabel: string | null
  /** The band's rate per head. `null` when no band applied. */
  discountBandRatePerHead: number | null
}

/* ------------------------------------------------------------------------- *
 * 16.1 — the add-on master and the booking's add-on lines.
 * ------------------------------------------------------------------------- */

/**
 * The pricing basis of an add-on (DEC-158).
 *
 * `null` means **not stated**, and the server treats it as `Flat` — the safe
 * direction, because it never multiplies a price that was not meant to be
 * multiplied.
 */
export type AddonPricingBasis = 'Flat' | 'PerGuest'

/** One active add-on of the master (contract §16.1). */
export interface AddonMasterItemDto {
  addonId: number
  description: string
  /** The catalogue unit amount — per guest when the basis is `PerGuest`. */
  amount: number
  /** The descriptive unit of measure. `null` = not stated. */
  unit: string | null
  pricingBasis: AddonPricingBasis | null
  /**
   * The line total this add-on would carry on a booking of the requested
   * headcount. Published so the client never multiplies it itself.
   */
  lineAmountForBooking: number
  /** The owning department (owner request 2026-10-24). `null` = not stated. */
  deptId: number | null
  deptName: string | null
  addonCategoryId: number | null
  addonCategoryName: string | null
  isActive: boolean
}

/** One add-on category of the master (contract §16.2). */
export interface AddonCategoryDto {
  addonCategoryId: number
  addonCategoryName: string
}

/** One frozen add-on line of a booking (contract §16.3). */
export interface BookingAddonLine {
  addonId: number
  /** The **frozen** description. */
  description: string
  /** The **frozen line total** — what joins the gross (DEC-158). */
  amount: number
  /** The frozen descriptive unit. `null` = not stated. */
  unit: string | null
  pricingBasis: AddonPricingBasis
  addonCategoryId: number | null
  addonCategoryName: string | null
  /** The catalogue's current amount. `null` when the row is gone. */
  catalogueAmount: number | null
  /** The catalogue's current description. `null` when the row is gone. */
  catalogueDescription: string | null
}

/** `GET /api/bookings/{id}/addons` (contract §16.3). */
export interface BookingAddonSetDto {
  transactionId: number
  bookingNumber: string
  /** `yyyy-MM-dd`. */
  eventDate: string
  /** **Always a concrete date** — never `null` (DEC-128). */
  menuSelectionLockDate: string
  /** `null` when the configuration key is absent. */
  menuSelectionLockDays: number | null
  isLocked: boolean
  /** The frozen line total that joins the gross. */
  addonsTotal: number
  addons: BookingAddonLine[]
}

/**
 * One add-on line of the replace request (contract §16.4).
 *
 * `amount` is the **line total** the operator confirms. When it is absent the
 * server derives the default from the catalogue amount and the basis.
 */
export interface BookingAddonRequestItem {
  addonId: number
  amount?: number | null
}

/** The body of `PUT /api/bookings/{id}/addons` (contract §16.4). */
export interface BookingAddonSetRequest {
  addons: BookingAddonRequestItem[]
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
  /**
   * The dish selection the quote should price (contract §14.3).
   *
   * It carries exactly the create request's shape, so the quote and the
   * created booking cannot diverge (BR-31, DEC-153).
   */
  selections?: BookingMenuSelectionRequestItem[]
  /** The add-on lines the quote should price (contract §14.3). */
  addons?: BookingAddonRequestItem[]
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
  /** The tier's human-readable code (owner request 2026-10-24). */
  packageCode: string | null
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
  /** The tier's own per-guest extended-location amount. `null` = not stated. */
  packageExtendedLocationRate: number | null
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
  /** The **derived** source of the discount (DEC-154). */
  discountSource: BookingDiscountSource
  /** The server's description of the band that applied. `null` when none did. */
  discountBandLabel: string | null
  /** The band's rate per head. `null` when no band applied. */
  discountBandRatePerHead: number | null
  /** The frozen line total of the add-ons carried by the request. */
  addonsTotal: number
  /** The VAT-inclusive memo. `null` when no rate is configured. */
  includedVatAmount: number | null
  /** The configured VAT rate. `null` when the key is absent. */
  vatPercentage: number | null
  /** The tier's own course rules, in the tier's order. */
  courses: BookingQuoteCourseRule[]
  /** The selection the request asks for, as the read endpoint describes it. */
  selection: BookingQuoteSelection
}

/** One course rule of the quote (contract §14.3). */
export interface BookingQuoteCourseRule {
  courseId: number
  courseName: string
  requiredQuota: number
  /** `null` when the rule states no portion. */
  portionGrams: number | null
  /** The rule's derived batch mass. `null` when no portion is stated. */
  batchMassKg: number | null
  ruleDescription: string
  /** The **count** of live dishes in the rule's qualified pool. */
  qualifiedPool: number
}

/** One chosen dish of the quote's selection (contract §14.3). */
export interface BookingQuoteSelectedDish {
  menuId: string
  menuName: string
  note: string | null
  prepInstruction: string | null
  foodCost: number | null
}

/** One course of the quote's selection (contract §14.3). */
export interface BookingQuoteSelectionCourse {
  courseId: number
  courseName: string
  selectedMenus: BookingQuoteSelectedDish[]
}

/** The selection the quote priced (contract §14.3). */
export interface BookingQuoteSelection {
  isLocked: boolean
  /** **Always a concrete date** — never `null` (DEC-128). */
  lockDate: string
  courses: BookingQuoteSelectionCourse[]
  selectedFoodCostPerPax: number | null
  selectedGrossProfitPercentage: number | null
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
  /**
   * The dishes chosen for the booking (contract §15.3).
   *
   * Stored in the **same commit** as the booking, so a booking and its dishes
   * are never half-written. An **absent** member means no selection; an
   * **explicitly empty** array is a `400` on create.
   */
  selections?: BookingMenuSelectionRequestItem[]
  /**
   * The add-on lines the booking carries (contract §15.3).
   *
   * An **absent** member means no add-ons; an **explicitly empty** array is a
   * `400` on create.
   */
  addons?: BookingAddonRequestItem[]
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
