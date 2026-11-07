import {
  CircleCheckBig,
  CircleDashed,
  Hourglass,
  Lock,
  Wallet,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { BadgeTone } from '@/components/ui/Badge'
import { getBookingStatusMeta } from '@/utils/bookingStatus'
import { EM_DASH, formatPercentage } from '@/utils/currencyFormatter'
import type {
  AddonPricingBasis,
  BookingDiscountSource,
  BookingPaymentMeansName,
  BookingPaymentState,
  BookingQuote,
  BookingSelectionState,
  BookingServiceTypeName,
  BookingSortOption,
  BookingTypeName,
} from '@/types/booking.types'
import type { BookingStatus } from '@/types/dashboard.types'

/**
 * The Bookings & Events vocabulary.
 *
 * Two vocabularies live side by side here and **never** share a name
 * (DEC-117):
 *
 *   • the **lifecycle** status — the ten `BookingStatus` names, resolved
 *     through the shared `utils/bookingStatus.ts`;
 *   • the **payment state** — the three BR-28 names, defined below.
 *
 * Every figure the labels accompany is the server's (DEC-002). The client only
 * chooses the word that sits beside it, and a state is always carried by that
 * word beside its tone — never by colour alone (WCAG 1.4.1).
 */

/**
 * The wording used when a measurement is **unknown**.
 *
 * `null` means unknown, never zero (contract rule 7). An unconfigured
 * capacity and an unstated required deposit both read "not stated" — they are
 * not zeroes.
 */
export const NOT_STATED = 'not stated'

/**
 * The wording that marks the tier's planning estimate (DEC-103).
 *
 * The published gross profit is the **tier's** standard-run estimate. It is
 * not the event's margin and it never enters a booking total.
 */
export const PLANNING_ESTIMATE_NOTE =
  'A planning estimate from the tier — not this event’s margin.'

/**
 * Renders a server-published relative change with its sign.
 *
 * The magnitude is the server's percentage; the client only marks its
 * direction. A `null` change means *unknown* — never zero (contract rule 7).
 */
export function formatSignedPercentage(value: number | null | undefined): string {
  if (typeof value !== 'number' || !Number.isFinite(value)) return EM_DASH
  const magnitude = formatPercentage(Math.abs(value))
  if (value > 0) return `+${magnitude}`
  if (value < 0) return `−${magnitude}`
  return magnitude
}


/**
 * The headcount unit used beside a pax figure.
 *
 * The unit is a caption, not a figure — it carries no number of its own.
 */
export const PAX_UNIT = 'heads'

/* ------------------------------------------------------------------------- *
 * BR-28 — the payment states.
 * ------------------------------------------------------------------------- */

/** The display metadata of one payment state. */
export interface PaymentStateMeta {
  /** The operator-facing label — the reference's wording. */
  label: string
  /** The chip tone that accompanies {@link label}. */
  tone: BadgeTone
  /** The icon that accompanies {@link label}. */
  icon: LucideIcon
}

/**
 * The three payment states (BR-28, DEC-177).
 *
 * Display labels are exactly "Pending Payment", "Deposit Paid", "Fully Paid".
 * The wire values stay the enum names (`PendingPayment`, `DepositPaid`,
 * `FullyPaid`).
 */
export const PAYMENT_STATE_META: Record<BookingPaymentState, PaymentStateMeta> = {
  PendingPayment: { label: 'Pending Payment', tone: 'warning', icon: Hourglass },
  DepositPaid: { label: 'Deposit Paid', tone: 'info', icon: Wallet },
  FullyPaid: { label: 'Fully Paid', tone: 'success', icon: CircleCheckBig },
}

/**
 * Resolves the display metadata of a payment state.
 *
 * An unrecognised value (a state added server-side after this client shipped)
 * degrades to a neutral chip that still shows the raw state text.
 */
export function getPaymentStateMeta(
  state: BookingPaymentState | string | null | undefined,
): PaymentStateMeta {
  if (state && state in PAYMENT_STATE_META) {
    return PAYMENT_STATE_META[state as BookingPaymentState]
  }
  return {
    label: state && state.length > 0 ? state : 'Unknown',
    tone: 'neutral',
    icon: Hourglass,
  }
}

/** The payment states in the order the filter offers them. */
export const PAYMENT_STATE_ORDER: readonly BookingPaymentState[] = [
  'PendingPayment',
  'DepositPaid',
  'FullyPaid',
]

/** One entry of the payment-state filter dropdown. */
export interface PaymentStateFilterOption {
  value: BookingPaymentState
  label: string
}

/** The payment-state filter options (spec §5.2 item 3). */
export const PAYMENT_STATE_FILTER_OPTIONS: readonly PaymentStateFilterOption[] =
  PAYMENT_STATE_ORDER.map((value) => ({ value, label: PAYMENT_STATE_META[value].label }))

/* ------------------------------------------------------------------------- *
 * M2 — the selection state.
 * ------------------------------------------------------------------------- */

/** The display metadata of one selection state. */
export interface SelectionStateMeta {
  label: string
  tone: BadgeTone
  icon: LucideIcon
}

/**
 * The three selection states (BR-28, M2).
 *
 * The wording is the operational note's: *Menu: not chosen*, *Menu: chosen*
 * and *Menu: locked*. Exactly one applies — they are never two flags.
 */
export const SELECTION_STATE_META: Record<BookingSelectionState, SelectionStateMeta> = {
  None: { label: 'Menu: not chosen', tone: 'neutral', icon: CircleDashed },
  Selected: { label: 'Menu: chosen', tone: 'success', icon: CircleCheckBig },
  Locked: { label: 'Menu: locked', tone: 'warning', icon: Lock },
}

/** Resolves the display metadata of a selection state, degrading safely. */
export function getSelectionStateMeta(
  state: BookingSelectionState | string | null | undefined,
): SelectionStateMeta {
  if (state && state in SELECTION_STATE_META) {
    return SELECTION_STATE_META[state as BookingSelectionState]
  }
  return { label: 'Menu: not stated', tone: 'neutral', icon: CircleDashed }
}

/* ------------------------------------------------------------------------- *
 * DEC-154 — the discount source.
 * ------------------------------------------------------------------------- */

/** The operator-facing wording of the discount source. */
export const DISCOUNT_SOURCE_LABELS: Record<BookingDiscountSource, string> = {
  None: 'No discount applied',
  Code: 'Discount code',
  HeadcountBand: 'Automatic headcount band',
}

/** Resolves the wording of a discount source, degrading safely. */
export function discountSourceLabel(
  source: BookingDiscountSource | string | null | undefined,
): string {
  if (source && source in DISCOUNT_SOURCE_LABELS) {
    return DISCOUNT_SOURCE_LABELS[source as BookingDiscountSource]
  }
  return 'Discount source not stated'
}

/* ------------------------------------------------------------------------- *
 * DEC-156 — the out-of-town tier restriction.
 * ------------------------------------------------------------------------- */

/**
 * The two tier types that may carry the out-of-town amount (DEC-141).
 *
 * The comparison is **trimmed** and **case-insensitive** for **equality**
 * against these values, on the type column only (DEC-156). A type spelled
 * "Premier Buffet" is **not** eligible.
 */
export const EXTENDED_AREA_PACKAGE_TYPES: readonly string[] = ['Regular', 'Premier']

/** Whether a stored `PackageType` may carry the out-of-town amount. */
export function supportsExtendedArea(packageType: string | null | undefined): boolean {
  const normalised = (packageType ?? '').trim().toLowerCase()
  return EXTENDED_AREA_PACKAGE_TYPES.some((type) => type.toLowerCase() === normalised)
}

/* ------------------------------------------------------------------------- *
 * AC-BKG-92..97 — the Package type → Area filter → Buffet tier gate.
 * ------------------------------------------------------------------------- */

/**
 * The client gate messages of the tier cascade (AC-BKG-97).
 *
 * They are a **form affordance only** and are never sent to the API: the
 * create payload carries `packageId`, not the gate's fields.
 */
export const TIER_GATE_MESSAGES = {
  /** No package type is selected. */
  typeRequired: 'Select a package type before choosing a buffet tier.',
  /** An eligible type is selected but no event area is. */
  areaRequired: 'Select the event area before choosing a buffet tier.',
  /** A tier is selected while the type/area gate is unsatisfied. */
  tierMismatch: 'The selected buffet tier does not match the package type and area.',
} as const

/**
 * Exact-after-trim package-type equality for the tier list filter (AC-BKG-96).
 *
 * Both sides are trimmed, then compared **case-sensitively** — this is the
 * list filter's own rule, deliberately not the eligibility test, which is
 * trimmed and case-**insensitive** (DEC-156).
 */
export function isSameTierType(tierType: string, selectedType: string): boolean {
  return tierType.trim() === selectedType.trim()
}

/* ------------------------------------------------------------------------- *
 * AC-BKG-89..91, 98 — the Rate-per-head readout of the selected tier.
 * ------------------------------------------------------------------------- */

/** The wording of the quiet refresh note shown on quote/catalogue divergence. */
export const RATE_DIVERGENCE_NOTE =
  'The quoted rate differs from the catalogue rate — the catalogue has been refreshed.'

/**
 * The last quote result the client holds, together with the package ID of the
 * **request that produced it**.
 *
 * The quote DTO carries no `packageId`, so the client correlates the result
 * with its own request instead — no API field is added (AC-BKG-90, AC-BKG-98).
 */
export interface BookingQuoteHeldRate {
  /** The `packageId` of the quote request the rate belongs to. */
  packageId: number
  /** The quote's per-head rate — `null` when the quote stated none. */
  rate: number | null
}

export interface TierRateResolution {
  /** The rate the cell renders — `null` renders as "not stated". */
  statedRate: number | null
  /** `true` when the rate comes from a current quote for the selected tier. */
  isQuoteShown: boolean
  /** `true` when the current quote's rate differs from the catalogue's. */
  isDivergent: boolean
}

/**
 * Resolves the Rate-per-head readout of the selected tier.
 *
 * A current quote whose request named the selected tier is **authoritative
 * even when it states no rate**: a `null` quoted rate renders "not stated" —
 * it never falls back to the catalogue figure. While the quote is being
 * re-priced (no current data), the last resolved rate stands in, but only
 * while the same tier stays selected, and a held `null` rate is a resolved
 * unknown too — again "not stated", never the catalogue (AC-BKG-89,
 * AC-BKG-90). Only when no quote for this tier exists at all does the
 * catalogue `amountPax` back the readout. A catalogue `null` is never
 * collapsed to the quote's zero (AC-BKG-91). The divergence verdict is the
 * **strict** comparison of the current quote's rate against the catalogue
 * rate, including when one side is `null` or zero — the quoted rate wins and
 * the quiet note accompanies it (AC-BKG-98). The browser never derives a
 * base amount from the result.
 */
export function resolveTierQuoteRate(options: {
  selectedPackageId: number | null
  /** The selected tier's catalogue `amountPax`. */
  catalogueRate: number | null
  /** The current quote — may belong to another tier or be absent. */
  quote: BookingQuote | null
  /** The last quote result held with its request's package ID. */
  lastResolvedQuote: BookingQuoteHeldRate | null
}): TierRateResolution {
  const { selectedPackageId, catalogueRate, quote, lastResolvedQuote } = options

  const identityMatches =
    selectedPackageId !== null &&
    lastResolvedQuote !== null &&
    lastResolvedQuote.packageId === selectedPackageId

  if (quote !== null && identityMatches) {
    const quotedRate = quote.pricePerHead
    // A catalogue null is never rendered as the quote's zero, even though the
    // zero is the quote's own figure; an actual zero stays a zero
    // (AC-BKG-91). The silent note still fires: the rates differ strictly.
    const statedRate = quotedRate === 0 && catalogueRate === null ? null : quotedRate
    return {
      statedRate,
      isQuoteShown: statedRate !== null,
      isDivergent: quotedRate !== catalogueRate,
    }
  }

  if (quote === null && identityMatches) {
    // Re-pricing without a current quote: the held rate stands in — including
    // a held null, which stays a resolved "not stated".
    const heldRate = lastResolvedQuote.rate
    return {
      statedRate: heldRate === 0 && catalogueRate === null ? null : heldRate,
      isQuoteShown: false,
      isDivergent: false,
    }
  }

  // No quote belongs to this tier yet — the catalogue backs the readout.
  return { statedRate: catalogueRate, isQuoteShown: false, isDivergent: false }
}

/* ------------------------------------------------------------------------- *
 * DEC-158 — the add-on vocabulary.
 * ------------------------------------------------------------------------- */

/** The operator-facing wording of an add-on's pricing basis. */
export function addonPricingBasisLabel(
  basis: AddonPricingBasis | null | undefined,
): string {
  if (basis === 'PerGuest') return 'Per guest'
  if (basis === 'Flat') return 'Flat rate'
  // A not-stated basis is treated as Flat by the server (DEC-158).
  return 'Flat rate (basis not stated)'
}

/** The category label of an add-on. `null` is **unknown**, never "General". */
export function addonCategoryLabel(
  name: string | null | undefined,
): string {
  return name && name.trim().length > 0 ? name : 'Category not stated'
}

/**
 * The maximum length of a chef's per-event prep instruction (contract §15.2).
 *
 * It is a **validation bound**, not a business figure.
 */
export const PREP_INSTRUCTION_MAX_LENGTH = 500

/* ------------------------------------------------------------------------- *
 * BR-15 — the quota verdict, computed over the rules the server publishes.
 * ------------------------------------------------------------------------- */

/** One course's quota position. */
export interface MenuQuotaStatus {
  courseId: number
  courseName: string
  requiredQuota: number
  selectedCount: number
  isSatisfied: boolean
}

/** A course rule the quota check reads. */
export interface MenuQuotaRule {
  courseId: number
  courseName: string
  requiredQuota: number
}

/** A chosen dish the quota check reads. */
export interface MenuQuotaSelection {
  courseId: number
  menuId: string
}

/**
 * Assesses a draft selection against the tier's rules (BR-15).
 *
 * This is a **form affordance**: it lets the screen say *"2 of 2 selected"*
 * and disable a save that the server would refuse anyway. It never prices
 * anything. The server remains the authority on the verdict.
 */
export function assessMenuQuota(
  rules: readonly MenuQuotaRule[],
  selections: readonly MenuQuotaSelection[],
): MenuQuotaStatus[] {
  return rules.map((rule) => {
    const selectedCount = selections.filter(
      (selection) => selection.courseId === rule.courseId,
    ).length
    return {
      courseId: rule.courseId,
      courseName: rule.courseName,
      requiredQuota: rule.requiredQuota,
      selectedCount,
      isSatisfied: selectedCount === rule.requiredQuota,
    }
  })
}

/**
 * Whether a draft selection is quota-complete (BR-15).
 *
 * An **empty** selection is complete: it clears the selection and reverts the
 * event to the tier's standard run (DEC-131, AC-BKG-59). A non-empty selection
 * is complete only when every rule holds exactly its quota and no chosen dish
 * belongs to a course the tier holds no rule for.
 */
export function isMenuSelectionComplete(
  rules: readonly MenuQuotaRule[],
  selections: readonly MenuQuotaSelection[],
): boolean {
  if (selections.length === 0) return true
  const ruledCourseIds = new Set(rules.map((rule) => rule.courseId))
  if (selections.some((selection) => !ruledCourseIds.has(selection.courseId))) {
    return false
  }
  return assessMenuQuota(rules, selections).every((status) => status.isSatisfied)
}

/* ------------------------------------------------------------------------- *
 * The register's filters.
 * ------------------------------------------------------------------------- */

/**
 * The status filter dropdown.
 *
 * The options are the **ten** `BookingStatus` names — the domain vocabulary
 * wins, so the reference's `Finalized` and `Invoiced` never appear (AC-BKG-07).
 */
export const BOOKING_STATUS_FILTER_OPTIONS: readonly BookingStatus[] = [
  'Pending',
  'Confirmed',
  'InProgress',
  'Completed',
  'Cancelled',
  'NoShow',
  'Rescheduled',
  'Expired',
]

/** The label of a status filter option — the shared lifecycle vocabulary. */
export function bookingStatusFilterLabel(status: BookingStatus | string): string {
  return getBookingStatusMeta(status).label
}

/**
 * The orderings the register offers (contract §14.1).
 *
 * The register publishes a **chronological** order, so the timeline view is a
 * server-side sort rather than a local re-ordering of one page.
 */
export const BOOKING_SORT_OPTIONS: readonly { value: BookingSortOption; label: string }[] = [
  { value: 'EventDateAsc', label: 'Event date (earliest first)' },
  { value: 'EventDateDesc', label: 'Event date (latest first)' },
  { value: 'ContractValueDesc', label: 'Contract value (highest first)' },
  { value: 'ContractValueAsc', label: 'Contract value (lowest first)' },
  { value: 'BookingNumberAsc', label: 'Booking number' },
]

/** The page sizes the contract accepts. */
export const BOOKING_PAGE_SIZES: readonly number[] = [10, 25, 50]

/**
 * The two presentations of the register's page.
 *
 * Both are the **same server page** in the server's own order — the toggle
 * never re-orders a page in the browser, because that would misrepresent the
 * population as a chronology.
 */
export type BookingRegisterView = 'grid' | 'timeline'

/* ------------------------------------------------------------------------- *
 * The workbench's form vocabulary.
 * ------------------------------------------------------------------------- */

/**
 * The four steps of the reservation workflow (spec §5.3).
 *
 * The indicator names the steps the operator is composing; it is a label, not
 * a progress measure, and no figure is attached to it.
 */
export const BOOKING_WORKFLOW_STEPS: readonly string[] = [
  'Client & event',
  'Package & courses',
  'Financials',
  'Deposit',
]

/** The `BookingType` options the workbench offers. */
export const BOOKING_TYPE_OPTIONS: readonly { value: BookingTypeName; label: string }[] = [
  { value: 'Inside', label: 'Inside' },
  { value: 'Outside', label: 'Outside' },
]

/** The `ServiceType` options the workbench offers. */
export const BOOKING_SERVICE_TYPE_OPTIONS: readonly {
  value: BookingServiceTypeName
  label: string
}[] = [
  { value: 'Catering', label: 'Catering' },
  { value: 'BuffetSetup', label: 'Buffet setup' },
  { value: 'Plated', label: 'Plated' },
  { value: 'FoodPacks', label: 'Food packs' },
  { value: 'DropOff', label: 'Drop-off' },
  { value: 'Pickup', label: 'Pick-up' },
]

/**
 * The `PaymentMeans` options the workbench offers.
 *
 * The client's labels are the operator's wording; the **wire value** is always
 * the enum name (contract §8).
 */
export const PAYMENT_MEANS_OPTIONS: readonly {
  value: BookingPaymentMeansName
  label: string
}[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Check', label: 'Check' },
  { value: 'GCash', label: 'GCash' },
  { value: 'LDDAP', label: 'Bank Transfer / LDDAP' },
  { value: 'CreditCard', label: 'Credit Card' },
]

/** The column limits the server enforces (spec §3.12). */
export const OCCASION_MAX_LENGTH = 150
export const VENUE_MAX_LENGTH = 200
export const DISCOUNT_CODE_MAX_LENGTH = 50
export const CHECK_NO_MAX_LENGTH = 50
export const PARTICULARS_MAX_LENGTH = 250

/**
 * The editable form state of the reservation workbench.
 *
 * Every field is a **string**, because a form holds what the operator typed.
 * The numeric fields become numbers only when the request is built.
 */
export interface BookingWorkbenchDraft {
  /** The client the booking is for. */
  customerId: string
  occasion: string
  venue: string
  /** `yyyy-MM-dd`. */
  eventDate: string
  bookType: BookingTypeName
  typeOfService: BookingServiceTypeName
  /** The buffet tier. */
  packageId: string
  /** The contract headcount. */
  noOfPerson: string
  /** `HH:mm` — the kitchen call time. */
  callTime: string
  /** `HH:mm` — the program start. */
  startTime: string
  /** `HH:mm` — the service end. */
  endTime: string
  /** `HH:mm` — the pack-out time. */
  packoutTime: string
  /** The municipality the event is held in. */
  extendedAreaId: string
  /** The creation-time switch for the out-of-town surcharge. */
  applyExtendedAmount: boolean
  discountCode: string
  /** The deposit the operator states. */
  depositAmount: string
  paymentMeans: BookingPaymentMeansName
  checkNo: string
  /** The initial payment amount. */
  initialPaymentAmount: string
  /** The receipt's particulars. */
  particular: string
  /**
   * The package-type filter (M7). An empty string means *no type filter*.
   *
   * It follows the stored `Package.PackageType` values, so it never restricts
   * the free-text column (DEC-070).
   */
  packageType: string
  /**
   * The area filter (DEC-157). It is offered only for `Regular` and
   * `Premier` tiers, and it narrows the tier list by the coverage the server
   * publishes.
   */
  areaFilterId: string
  /**
   * The dishes the operator has chosen for this draft booking.
   *
   * The shape is the packages simulator's `selections` shape, with the chef's
   * per-event instruction added (contract §15.2).
   */
  menuSelections: BookingMenuDraftItem[]
  /** The add-on lines the operator has chosen for this draft booking. */
  addonLines: BookingAddonDraftLine[]
}

/** One draft dish choice (contract §15.2). */
export interface BookingMenuDraftItem {
  courseId: number
  menuId: string
  /** The chef's per-event instruction. */
  prepInstruction: string
}

/** One draft add-on line. `amount` is the line total the operator confirms. */
export interface BookingAddonDraftLine {
  addonId: number
  /** The line total, held as the operator typed it. */
  amount: string
}

/** The per-field client validation messages of the workbench. */
export interface BookingWorkbenchErrors {
  customerId?: string
  occasion?: string
  venue?: string
  eventDate?: string
  packageType?: string
  areaFilterId?: string
  packageId?: string
  noOfPerson?: string
  startTime?: string
  endTime?: string
  callTime?: string
  packoutTime?: string
  extendedAreaId?: string
  applyExtendedAmount?: string
  discountCode?: string
  depositAmount?: string
  initialPaymentAmount?: string
  checkNo?: string
  particular?: string
}

/** A blank workbench draft. */
export function emptyBookingWorkbenchDraft(): BookingWorkbenchDraft {
  return {
    customerId: '',
    occasion: '',
    venue: '',
    eventDate: '',
    bookType: 'Inside',
    typeOfService: 'Catering',
    packageId: '',
    noOfPerson: '',
    callTime: '',
    startTime: '',
    endTime: '',
    packoutTime: '',
    extendedAreaId: '',
    applyExtendedAmount: false,
    discountCode: '',
    depositAmount: '',
    paymentMeans: 'Cash',
    checkNo: '',
    initialPaymentAmount: '',
    particular: '',
    packageType: '',
    areaFilterId: '',
    menuSelections: [],
    addonLines: [],
  }
}

/**
 * Reads a typed number.
 *
 * The browser only parses what the operator typed — it never derives a figure
 * from another figure.
 */
export function parseOptionalNumber(value: string): number | null {
  const trimmed = value.trim()
  if (trimmed.length === 0) return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

/**
 * Reads a typed time.
 *
 * The control holds `HH:mm`; the wire value is `HH:mm:ss`. The conversion
 * only pads what the operator typed — it never derives a schedule.
 */
export function toWireTime(value: string): string | null {
  const trimmed = value.trim()
  if (trimmed.length === 0) return null
  const match = /^(\d{1,2}):(\d{2})$/.exec(trimmed)
  if (!match) return null
  const hours = match[1].padStart(2, '0')
  return `${hours}:${match[2]}:00`
}

/**
 * Renders a server-published `HH:mm:ss` time as `HH:mm`.
 *
 * This is a **display** of the value the API published — no schedule is
 * derived and no clock is read.
 */
export function formatClockTime(value: string | null | undefined): string {
  if (!value) return EM_DASH
  const match = /^(\d{1,2}):(\d{2})/.exec(value.trim())
  if (!match) return EM_DASH
  return `${match[1].padStart(2, '0')}:${match[2]}`
}

/**
 * Compares two `HH:mm` or `HH:mm:ss` times.
 *
 * The comparison is lexicographic on the zero-padded clock, so it never reads
 * a calendar — the event day is the server's business.
 */
export function compareClockTimes(left: string, right: string): number {
  const leftParts = left.split(':')
  const rightParts = right.split(':')
  const leftKey = `${leftParts[0].padStart(2, '0')}:${leftParts[1]}`
  const rightKey = `${rightParts[0].padStart(2, '0')}:${rightParts[1]}`
  if (leftKey === rightKey) return 0
  return leftKey < rightKey ? -1 : 1
}

/**
 * The presence and sign checks the workbench makes before it calls the API.
 *
 * The wording is the **server's own**, so the operator reads one message
 * whether the request is rejected in the browser or by the API (BR-11). The
 * rules are the ones the create and quote validators enforce (spec §3.12,
 * BR-34): the occasion and the venue are required, the headcount is positive,
 * the programme does not run backwards, and the dispatch schedule is ordered
 * against it — the call time precedes the programme and the pack-out follows
 * it.
 */
export function validateBookingWorkbenchDraft(
  draft: BookingWorkbenchDraft,
): BookingWorkbenchErrors {
  const errors: BookingWorkbenchErrors = {}

  if (draft.customerId.trim().length === 0) {
    errors.customerId = 'A valid customer is required.'
  }

  if (
    draft.occasion.trim().length === 0 ||
    draft.occasion.trim().length > OCCASION_MAX_LENGTH
  ) {
    errors.occasion = 'Occasion is required and must not exceed 150 characters.'
  }

  if (draft.venue.trim().length === 0 || draft.venue.trim().length > VENUE_MAX_LENGTH) {
    errors.venue = 'Venue is required and must not exceed 200 characters.'
  }

  if (draft.eventDate.trim().length === 0) {
    errors.eventDate = 'Event start date and time is required.'
  }

  /*
   * The tier cascade gate (AC-BKG-92, AC-BKG-93, AC-BKG-97): a client
   * affordance only — the create request carries `packageId`, never the
   * gate's fields.
   */
  const isTypeChosen = draft.packageType.trim().length > 0
  const isAreaRequired = isTypeChosen && supportsExtendedArea(draft.packageType)
  const isAreaChosen = draft.areaFilterId.trim().length > 0

  if (!isTypeChosen) {
    errors.packageType = TIER_GATE_MESSAGES.typeRequired
  } else if (isAreaRequired && !isAreaChosen) {
    errors.areaFilterId = TIER_GATE_MESSAGES.areaRequired
  }

  if (draft.packageId.trim().length === 0) {
    errors.packageId = 'When a package is specified it must be a valid package ID.'
  } else if (!isTypeChosen || (isAreaRequired && !isAreaChosen)) {
    errors.packageId = TIER_GATE_MESSAGES.tierMismatch
  }

  if (draft.noOfPerson.trim().length === 0) {
    errors.noOfPerson = 'Number of persons must be greater than 0.'
  } else {
    const pax = parseOptionalNumber(draft.noOfPerson)
    if (pax === null || !Number.isInteger(pax) || pax < 1) {
      errors.noOfPerson = 'Number of persons must be greater than 0.'
    }
  }

  if (draft.startTime.trim().length === 0) {
    errors.startTime = 'Event start date and time is required.'
  }
  if (draft.endTime.trim().length === 0) {
    errors.endTime = 'Event end date must be on or after the start date.'
  }
  if (
    draft.startTime.trim().length > 0 &&
    draft.endTime.trim().length > 0 &&
    compareClockTimes(draft.startTime, draft.endTime) > 0
  ) {
    errors.endTime = 'Event end date must be on or after the start date.'
  }

  if (draft.callTime.trim().length > 0) {
    if (toWireTime(draft.callTime) === null) {
      errors.callTime = "The call time must not be later than the programme's start time."
    } else if (
      draft.startTime.trim().length > 0 &&
      compareClockTimes(draft.callTime, draft.startTime) > 0
    ) {
      errors.callTime = "The call time must not be later than the programme's start time."
    }
  }

  if (draft.packoutTime.trim().length > 0) {
    if (toWireTime(draft.packoutTime) === null) {
      errors.packoutTime = "The pack-out time must not be earlier than the programme's end time."
    } else if (
      draft.endTime.trim().length > 0 &&
      compareClockTimes(draft.packoutTime, draft.endTime) < 0
    ) {
      errors.packoutTime = "The pack-out time must not be earlier than the programme's end time."
    }
  }

  if (draft.extendedAreaId.trim().length > 0) {
    const areaId = parseOptionalNumber(draft.extendedAreaId)
    if (areaId === null || !Number.isInteger(areaId) || areaId < 1) {
      errors.extendedAreaId =
        'When an extended area is specified it must be a valid municipality ID.'
    }
  }
  if (draft.applyExtendedAmount && draft.extendedAreaId.trim().length === 0) {
    errors.applyExtendedAmount =
      'A municipality must be selected when the extended area amount is applied.'
  }

  if (draft.discountCode.trim().length > DISCOUNT_CODE_MAX_LENGTH) {
    errors.discountCode = 'The discount code must not exceed 50 characters.'
  }

  if (draft.depositAmount.trim().length > 0) {
    const deposit = parseOptionalNumber(draft.depositAmount)
    if (deposit === null || deposit < 0) {
      errors.depositAmount = 'The deposit amount must be zero or greater.'
    }
  }

  if (draft.initialPaymentAmount.trim().length > 0) {
    const payment = parseOptionalNumber(draft.initialPaymentAmount)
    if (payment === null || payment <= 0) {
      errors.initialPaymentAmount = 'The initial payment amount must be greater than 0.'
    }
  }

  if (draft.checkNo.trim().length > CHECK_NO_MAX_LENGTH) {
    errors.checkNo = 'The cheque number must not exceed 50 characters.'
  }

  if (draft.particular.trim().length > PARTICULARS_MAX_LENGTH) {
    errors.particular = 'The payment description must not exceed 250 characters.'
  }

  return errors
}

/**
 * Validates a chef's per-event prep instruction (contract §15.2).
 *
 * The bound is the server's own (`<= 500` characters); the message is the
 * wording the operator reads whether the refusal happens here or in the API.
 */
export function validatePrepInstruction(value: string): string | undefined {
  return value.length > PREP_INSTRUCTION_MAX_LENGTH
    ? 'The chef prep instruction must not exceed 500 characters.'
    : undefined
}

/**
 * Validates an add-on line total (contract §16.4).
 *
 * The line total is what enters the gross, and the server refuses a negative
 * amount, so the form says so before the request is made.
 */
export function validateAddonLineAmount(value: string): string | undefined {
  const parsed = parseOptionalNumber(value)
  if (value.trim().length === 0) {
    return 'State the line total, or leave the add-on out.'
  }
  if (parsed === null || parsed < 0) {
    return 'The add-on line total must be zero or greater.'
  }
  return undefined
}
