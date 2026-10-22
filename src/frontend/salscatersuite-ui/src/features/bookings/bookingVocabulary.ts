import {
  CircleCheckBig,
  Hourglass,
  Wallet,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { BadgeTone } from '@/components/ui/Badge'
import { getBookingStatusMeta } from '@/utils/bookingStatus'
import { EM_DASH, formatPercentage } from '@/utils/currencyFormatter'
import type {
  BookingPaymentMeansName,
  BookingPaymentState,
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
 * The three payment states (BR-28, DEC-117).
 *
 * The labels are the reference's wording: *Fully paid*, *Partially paid* and
 * *Awaiting initial deposit*. The wire values stay the enum names.
 */
export const PAYMENT_STATE_META: Record<BookingPaymentState, PaymentStateMeta> = {
  AwaitingDeposit: { label: 'Awaiting initial deposit', tone: 'warning', icon: Hourglass },
  PartiallyPaid: { label: 'Partially paid', tone: 'info', icon: Wallet },
  Settled: { label: 'Fully paid', tone: 'success', icon: CircleCheckBig },
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
  'AwaitingDeposit',
  'PartiallyPaid',
  'Settled',
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
  'DepositPaid',
  'FullyPaid',
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
}

/** The per-field client validation messages of the workbench. */
export interface BookingWorkbenchErrors {
  customerId?: string
  occasion?: string
  venue?: string
  eventDate?: string
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

  if (draft.packageId.trim().length === 0) {
    errors.packageId = 'When a package is specified it must be a valid package ID.'
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
