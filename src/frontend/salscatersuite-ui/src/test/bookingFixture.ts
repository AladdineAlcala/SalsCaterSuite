/**
 * TEST FIXTURE — not production data.
 *
 * A fixed, hand-written set of Bookings & Events payloads used by the
 * component tests. Every value is chosen so the assertions can check the exact
 * rendering of the formatters, the server's null semantics, the payment-state
 * vocabulary and the error contract.
 *
 * This file lives under `src/test/` and is never imported by application code.
 */
import type {
  BookingRegisterItem,
  BookingRegisterPage,
  BookingRegisterSummary,
  BookingQuote,
} from '@/types/booking.types'

/**
 * The first dossier card — a **confirmed** booking that is **fully paid**.
 *
 * It carries a countdown, a tier, a venue and a complete ledger, and it is
 * the card the collection ratio is read from.
 */
const confirmedItemFixture: BookingRegisterItem = {
  transactionId: 101,
  bookingNumber: 'BK-20261120-00101',
  eventDate: '2026-11-20',
  eventStartTime: '17:00:00',
  eventEndTime: '22:00:00',
  callTime: '14:00:00',
  packoutTime: '23:30:00',
  status: 'Confirmed',
  paymentState: 'Settled',
  isOverdue: false,
  daysUntilEvent: 29,
  customerId: 14,
  customerName: 'Fernandez, Atilio',
  customerCompany: 'Fernandez Realty Inc.',
  customerContact: '09175552381',
  occasion: 'Fernandez-Santos Nuptials',
  venue: 'The Grand Pavilion',
  packageId: 12,
  packageName: 'Wedding Grand Tier',
  packageType: 'Wedding',
  numberOfHeads: 250,
  pricePerHead: 1140,
  packageAmount: 285000,
  netAmountDue: 285000,
  amountPaid: 285000,
  outstandingBalance: 0,
  extendedAreaId: null,
  extendedAreaName: null,
  extendedAreaAmount: 0,
  applyExtendedAmount: false,
  depositAmount: 0,
  discountCode: null,
  discountAmount: 0,
  baseGrossProfitPercentage: 58.2,
}

/**
 * The second dossier card — a **tentative** booking that is **partly paid**.
 */
const tentativeItemFixture: BookingRegisterItem = {
  transactionId: 102,
  bookingNumber: 'BK-20261121-00102',
  eventDate: '2026-11-21',
  eventStartTime: '18:30:00',
  eventEndTime: '23:00:00',
  callTime: null,
  packoutTime: null,
  status: 'Pending',
  paymentState: 'PartiallyPaid',
  isOverdue: false,
  daysUntilEvent: 30,
  customerId: 21,
  customerName: 'Tan, Liza',
  customerCompany: 'Apex Global Tech PH Inc.',
  customerContact: '09204419090',
  occasion: 'Apex Global Tech Q1 Gala',
  venue: 'Sunset Pavilion',
  packageId: 14,
  packageName: 'Premier Buffet',
  packageType: 'Premier Buffet',
  numberOfHeads: 180,
  pricePerHead: 1100,
  packageAmount: 198000,
  netAmountDue: 198000,
  amountPaid: 50000,
  outstandingBalance: 148000,
  extendedAreaId: null,
  extendedAreaName: null,
  extendedAreaAmount: 0,
  applyExtendedAmount: false,
  depositAmount: null,
  discountCode: null,
  discountAmount: 0,
  baseGrossProfitPercentage: null,
}

/**
 * The third dossier card — a booking whose event date has passed and whose
 * balance is outstanding, so it is **overdue** and **awaiting a deposit**.
 *
 * Its countdown is `null` — the event is not in the future — and its tier
 * states no planning estimate, so both render as unknown.
 */
const overdueItemFixture: BookingRegisterItem = {
  transactionId: 98,
  bookingNumber: 'BK-20261012-00098',
  eventDate: '2026-10-12',
  eventStartTime: '18:00:00',
  eventEndTime: '23:00:00',
  callTime: null,
  packoutTime: null,
  status: 'DepositPaid',
  paymentState: 'AwaitingDeposit',
  isOverdue: true,
  daysUntilEvent: null,
  customerId: 8,
  customerName: 'Morales, Helena',
  customerCompany: null,
  customerContact: '09183127701',
  occasion: 'Dr. Morales 60th Jubilee',
  venue: 'Manila Polo Club Tent',
  packageId: 17,
  packageName: 'VIP Special',
  packageType: 'VIP Special',
  numberOfHeads: 120,
  pricePerHead: 1208.33,
  packageAmount: 145000,
  netAmountDue: 145000,
  amountPaid: 0,
  outstandingBalance: 145000,
  extendedAreaId: 4,
  extendedAreaName: 'Antipolo City',
  extendedAreaAmount: 14400,
  applyExtendedAmount: true,
  depositAmount: 0,
  discountCode: null,
  discountAmount: 0,
  baseGrossProfitPercentage: null,
}

/** The dossier cards of the register fixture. */
export const bookingRegisterItemsFixture: BookingRegisterItem[] = [
  confirmedItemFixture,
  tentativeItemFixture,
  overdueItemFixture,
]

/**
 * The register page.
 *
 * The totals describe the **whole filtered population** — the three cards
 * above — and the facets carry the server's counts.
 */
export const bookingRegisterPageFixture: BookingRegisterPage = {
  items: bookingRegisterItemsFixture,
  totalCount: 3,
  page: 1,
  pageSize: 10,
  totalPages: 1,
  summary: {
    bookingCount: 3,
    netContractedValue: 628000,
    collectedAmount: 335000,
    outstandingAmount: 293000,
    totalPax: 550,
  },
  quickViewCounts: {
    packageTypes: [
      { key: 'Premier Buffet', count: 1 },
      { key: 'VIP Special', count: 1 },
      { key: 'Wedding', count: 1 },
    ],
    venues: [
      { key: 'Manila Polo Club Tent', count: 1 },
      { key: 'Sunset Pavilion', count: 1 },
      { key: 'The Grand Pavilion', count: 1 },
    ],
    statuses: [
      { key: 'Confirmed', count: 1 },
      { key: 'DepositPaid', count: 1 },
      { key: 'Pending', count: 1 },
    ],
    paymentStates: [
      { key: 'AwaitingDeposit', count: 1 },
      { key: 'PartiallyPaid', count: 1 },
      { key: 'Settled', count: 1 },
    ],
  },
  generatedAt: '2026-10-22T03:15:00.1234567Z',
}

/** A register that holds no booking at all. */
export const emptyBookingRegisterPageFixture: BookingRegisterPage = {
  ...bookingRegisterPageFixture,
  items: [],
  totalCount: 0,
  totalPages: 0,
  summary: {
    bookingCount: 0,
    netContractedValue: 0,
    collectedAmount: 0,
    outstandingAmount: 0,
    totalPax: 0,
  },
  quickViewCounts: {
    packageTypes: [],
    venues: [],
    statuses: [],
    paymentStates: [],
  },
}

/**
 * The register aggregate.
 *
 * The capacity is **not configured**, so `capacityPax` and
 * `utilisationPercentage` are `null` — unknown, never zero (DEC-102).
 */
export const bookingRegisterSummaryFixture: BookingRegisterSummary = {
  pipeline: {
    bookingCount: 3,
    confirmedCount: 1,
    tentativeCount: 1,
    completedCount: 0,
    otherCount: 1,
    committedPax: 550,
  },
  leadingTier: {
    packageType: 'Wedding',
    packageName: 'Wedding Grand Tier',
    netContractedValue: 285000,
    sharePercentage: 45.38,
    changePercentage: 8.4,
    previousNetContractedValue: 262900,
  },
  contractValue: {
    netContractedValue: 628000,
    collectedAmount: 335000,
    outstandingAmount: 293000,
    collectionRatioPercentage: 53.34,
  },
  headcount: {
    totalPax: 550,
    averagePaxPerBooking: 183.33,
    nextEventDate: '2026-11-20',
    nextEventBookingNumber: 'BK-20261120-00101',
  },
  paymentStates: [
    { key: 'AwaitingDeposit', count: 1, amount: 145000 },
    { key: 'PartiallyPaid', count: 1, amount: 148000 },
    { key: 'Settled', count: 1, amount: 0 },
  ],
  capacity: {
    committedPax: 550,
    capacityPax: null,
    utilisationPercentage: null,
    isConfigured: false,
  },
  receivables: {
    outstandingAmount: 293000,
    bookingCount: 2,
    overdueCount: 1,
    overdueAmount: 145000,
  },
  generatedAt: '2026-10-22T03:15:00.1234567Z',
}

/**
 * The workbench quote.
 *
 * The required deposit percentage is **not configured**, so
 * `requiredDepositPercentage` and `requiredDepositAmount` are `null` — the
 * rail reads "not stated" and the reference's percentage never appears
 * (DEC-101).
 */
export const bookingQuoteFixture: BookingQuote = {
  packageName: 'Wedding Grand Tier',
  packageType: 'Wedding',
  minPax: 150,
  isTierActive: true,
  pricePerHead: 1140,
  baseAmount: 285000,
  extendedAreaId: 4,
  extendedAreaName: 'Antipolo City',
  extendedAreaRate: 120,
  extendedAreaAmount: 36000,
  discountCode: 'EARLY-BIRD',
  discountType: 'Fixed',
  discountPercentage: 0,
  discountAmount: 16000,
  grossAmount: 321000,
  netAmountDue: 305000,
  requiredDepositPercentage: null,
  requiredDepositAmount: null,
  depositAmount: 0,
  initialPaymentAmount: 50000,
  outstandingBalance: 255000,
  headcountFloorNote: 'The tier states a minimum of 150 heads.',
  capacity: {
    committedPax: 550,
    capacityPax: null,
    utilisationPercentage: null,
    isConfigured: false,
  },
  standardRunFoodCostPerPax: 329.25,
  baseGrossProfitPercentage: 77.29,
  coverage: {
    isCovered: true,
    coveredAreaCount: 4,
  },
  checks: [
    {
      key: 'ClientValidated',
      label: 'Client master validated',
      isSatisfied: true,
      isBlocking: false,
      detail: 'The client exists and is not deleted.',
    },
    {
      key: 'TierActive',
      label: 'Tier is active',
      isSatisfied: true,
      isBlocking: true,
      detail: 'The tier can be booked.',
    },
    {
      key: 'CoursesSpecified',
      label: 'Courses specified',
      isSatisfied: true,
      isBlocking: false,
      detail: 'The tier states at least one course rule.',
    },
    {
      key: 'MunicipalityCovered',
      label: 'Municipality covered',
      isSatisfied: true,
      isBlocking: false,
      detail: 'The chosen municipality is in the tier coverage set.',
    },
    {
      key: 'DepositMatched',
      label: 'Deposit matched',
      isSatisfied: true,
      isBlocking: false,
      detail: 'No required deposit is configured, so nothing is owed.',
    },
  ],
}

/**
 * A quote for a **deactivated** tier.
 *
 * The blocking check fails, so the booking cannot be confirmed (BR-29).
 */
export const inactiveTierBookingQuoteFixture: BookingQuote = {
  ...bookingQuoteFixture,
  isTierActive: false,
  checks: bookingQuoteFixture.checks.map((check) =>
    check.key === 'TierActive'
      ? {
          ...check,
          isSatisfied: false,
          detail: 'The tier is deactivated and cannot be booked.',
        }
      : check,
  ),
}
