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
  AddonMasterItemDto,
  BookingAddonSetDto,
  BookingDetails,
  BookingMenuSelectionDto,
  BookingQuote,
  BookingRegisterItem,
  BookingRegisterPage,
  BookingRegisterSummary,
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
  paymentState: 'FullyPaid',
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
  discountSource: 'None',
  selectionState: 'Locked',
  addonCount: 0,
  addonsTotal: 0,
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
  paymentState: 'DepositPaid',
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
  discountSource: 'HeadcountBand',
  selectionState: 'None',
  addonCount: 1,
  addonsTotal: 1500,
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
  status: 'Confirmed',
  paymentState: 'PendingPayment',
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
  discountSource: 'Code',
  selectionState: 'Selected',
  addonCount: 0,
  addonsTotal: 0,
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
      { key: 'Confirmed', count: 1 },
      { key: 'Pending', count: 1 },
    ],
    paymentStates: [
      { key: 'PendingPayment', count: 1 },
      { key: 'DepositPaid', count: 1 },
      { key: 'FullyPaid', count: 1 },
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
    { key: 'PendingPayment', count: 1, amount: 145000 },
    { key: 'DepositPaid', count: 1, amount: 148000 },
    { key: 'FullyPaid', count: 1, amount: 0 },
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
  menuWindows: {
    openSelectionCount: 2,
    lockedSelectionCount: 1,
  },
  bandDiscount: {
    discountedBookingCount: 1,
    discountedNetAmountDue: 198000,
  },
  vatPercentage: null,
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
  packageCode: 'PKG-WED-07',
  packageType: 'Wedding',
  minPax: 150,
  isTierActive: true,
  pricePerHead: 1140,
  baseAmount: 285000,
  extendedAreaId: 4,
  extendedAreaName: 'Antipolo City',
  extendedAreaRate: 120,
  packageExtendedLocationRate: null,
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
  discountSource: 'Code',
  discountBandLabel: null,
  discountBandRatePerHead: null,
  addonsTotal: 0,
  includedVatAmount: null,
  vatPercentage: null,
  courses: [
    {
      courseId: 4,
      courseName: 'Beef',
      requiredQuota: 2,
      portionGrams: 120,
      batchMassKg: 30,
      ruleDescription: 'Enforce exactly 2 Beef selections',
      qualifiedPool: 5,
    },
    {
      courseId: 5,
      courseName: 'Salad',
      requiredQuota: 1,
      portionGrams: 80,
      batchMassKg: 20,
      ruleDescription: 'One seasonal salad is served',
      qualifiedPool: 3,
    },
  ],
  selection: {
    isLocked: false,
    lockDate: '2026-11-13',
    courses: [],
    selectedFoodCostPerPax: null,
    selectedGrossProfitPercentage: null,
  },
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

/**
 * A quote whose discount came from the **automatic headcount band**.
 *
 * The band's own label and its per-head rate are the server's — the client
 * names the rule without reconstructing either figure (DEC-154, AC-BKG-70).
 */
export const bandDiscountBookingQuoteFixture: BookingQuote = {
  ...bookingQuoteFixture,
  discountCode: null,
  discountAmount: 5000,
  discountSource: 'HeadcountBand',
  discountBandLabel: '100 to 999 heads',
  discountBandRatePerHead: 20,
}

/**
 * The booking's frozen price snapshot (contract §1).
 *
 * It carries a discount code, an out-of-town surcharge with its recovered
 * per-guest rate, an agreed deposit and an outstanding balance.
 */
export const bookingDetailsFixture: BookingDetails = {
  transactionId: 101,
  bookingNumber: 'BK-20261120-00101',
  transactionDate: '2026-10-22T03:15:00Z',
  bookType: 'Inside',
  typeOfService: 'Catering',
  customerId: 14,
  customerName: 'Fernandez, Atilio',
  customerContact: '09175552381',
  occasion: 'Fernandez-Santos Nuptials',
  venue: 'The Grand Pavilion',
  eventDate: '2026-11-20',
  eventStartTime: '17:00:00',
  eventEndTime: '22:00:00',
  callTime: '14:00:00',
  packoutTime: '23:30:00',
  numberOfHeads: 250,
  eventColor: null,
  packageId: 12,
  packageName: 'Wedding Grand Tier',
  reference: null,
  pricePerHead: 1140,
  packageAmount: 285000,
  extendedAreaAmount: 36000,
  extendedAreaRatePerGuest: 144,
  subtotal: 321000,
  discountType: 'Fixed',
  discountPercentage: 0,
  discountCode: 'EARLY-BIRD',
  discountAmount: 16000,
  taxAmount: 0,
  amountDue: 305000,
  depositAmount: 50000,
  amountPaid: 50000,
  balance: 255000,
  status: 'Confirmed',
  reminderSent: false,
}

/**
 * The stored menu selection (contract §15.1).
 *
 * The tier publishes two course rules — two Beef dishes and one Salad — and
 * the booking already carries a compliant selection, so the editor opens on
 * the chosen dishes. The VAT memo is `null`: no rate is configured, so the
 * line is absent (DEC-143).
 */
export const bookingMenuSelectionFixture: BookingMenuSelectionDto = {
  transactionId: 101,
  bookingNumber: 'BK-20261120-00101',
  eventDate: '2026-11-20',
  packageId: 12,
  packageName: 'Wedding Grand Tier',
  menuSelectionLockDate: '2026-11-13',
  menuSelectionLockDays: 7,
  isLocked: false,
  selections: [
    {
      courseId: 4,
      courseName: 'Beef',
      menuId: 'DS-203',
      menuName: 'Beef Caldereta',
      note: 'Tomato, bell pepper, potato',
      foodCost: 240,
      prepInstruction: 'Sauce on the side',
    },
    {
      courseId: 4,
      courseName: 'Beef',
      menuId: 'DS-204',
      menuName: 'Beef Steak',
      note: 'Butter, garlic, thyme',
      foodCost: 180,
      prepInstruction: null,
    },
    {
      courseId: 5,
      courseName: 'Salad',
      menuId: 'DS-301',
      menuName: 'Caesar Salad',
      note: 'Romaine, parmesan, croutons',
      foodCost: 60,
      prepInstruction: null,
    },
  ],
  courses: [
    {
      courseId: 4,
      courseName: 'Beef',
      requiredQuota: 2,
      portionGrams: 120,
      batchMassKg: 30,
      ruleDescription: 'Enforce exactly 2 Beef selections',
      pool: [
        {
          menuId: 'DS-201',
          menuName: 'Beef Royale',
          note: 'Tenderloin, mushroom, cream',
          foodCost: 380,
          price: 1450,
        },
        {
          menuId: 'DS-202',
          menuName: 'Beef Salpicon',
          note: 'Shredded beef, peppers',
          foodCost: 300,
          price: 1450,
        },
        {
          menuId: 'DS-203',
          menuName: 'Beef Caldereta',
          note: 'Tomato, bell pepper, potato',
          foodCost: 240,
          price: 1450,
        },
        {
          menuId: 'DS-204',
          menuName: 'Beef Steak',
          note: 'Butter, garlic, thyme',
          foodCost: 180,
          price: 1450,
        },
        {
          menuId: 'DS-205',
          menuName: 'Beef Sisig',
          note: 'No ingredient note is stated.',
          foodCost: 0,
          price: 1450,
        },
      ],
      chosenMenuIds: ['DS-203', 'DS-204'],
    },
    {
      courseId: 5,
      courseName: 'Salad',
      requiredQuota: 1,
      portionGrams: 80,
      batchMassKg: 20,
      ruleDescription: 'One seasonal salad is served',
      pool: [
        {
          menuId: 'DS-301',
          menuName: 'Caesar Salad',
          note: 'Romaine, parmesan, croutons',
          foodCost: 60,
          price: 1450,
        },
        {
          menuId: 'DS-302',
          menuName: 'Green Salad',
          note: 'Garden greens, vinaigrette',
          foodCost: 48.5,
          price: 1450,
        },
        {
          menuId: 'DS-303',
          menuName: 'Pasta Salad',
          note: 'Penne, olives, herbs',
          foodCost: 0,
          price: 1450,
        },
      ],
      chosenMenuIds: ['DS-301'],
    },
  ],
  isCompliant: true,
  complianceMessage: null,
  selectedFoodCostPerPax: 480,
  standardRunFoodCostPerPax: 329.25,
  selectedGrossProfitPercentage: 57.89,
  standardRunGrossProfitPercentage: 77.29,
  includedVatAmount: null,
  vatPercentage: null,
  selectionState: 'Selected',
  addonCount: 1,
  addonsTotal: 1500,
  // A code replaces the band (DEC-138), so this booking's source is Code and no band applied.
  discountSource: 'Code',
  discountBandLabel: null,
  discountBandRatePerHead: null,
}

/** A selection whose discount came from the automatic headcount band (DEC-154, DEC-160). */
export const bandBookingMenuSelectionFixture: BookingMenuSelectionDto = {
  ...bookingMenuSelectionFixture,
  discountSource: 'HeadcountBand',
  discountBandLabel: '100 to 999 heads',
  discountBandRatePerHead: 20,
}

/** A selection whose menu-change window has closed — the editor is read-only. */
export const lockedBookingMenuSelectionFixture: BookingMenuSelectionDto = {
  ...bookingMenuSelectionFixture,
  menuSelectionLockDate: '2026-10-19',
  menuSelectionLockDays: null,
  isLocked: true,
  selectionState: 'Locked',
}

/** A tier that states **no** course rule, so its standard run is empty too. */
export const emptyBookingMenuSelectionFixture: BookingMenuSelectionDto = {
  ...bookingMenuSelectionFixture,
  selections: [],
  courses: [],
  isCompliant: true,
  selectedFoodCostPerPax: null,
  selectedGrossProfitPercentage: null,
  includedVatAmount: null,
  selectionState: 'None',
  addonCount: 0,
  addonsTotal: 0,
}

/** A selection whose ledger carries the configured VAT-inclusive memo. */
export const vatBookingMenuSelectionFixture: BookingMenuSelectionDto = {
  ...bookingMenuSelectionFixture,
  includedVatAmount: 12889.29,
  vatPercentage: 12,
}

/**
 * The booking's frozen add-on lines (contract §16.3).
 *
 * The line freezes its description, its line total and its pricing basis, so a
 * later catalogue change never moves it (DEC-158, AC-BKG-74).
 */
export const bookingAddonSetFixture: BookingAddonSetDto = {
  transactionId: 101,
  bookingNumber: 'BK-20261120-00101',
  eventDate: '2026-11-20',
  menuSelectionLockDate: '2026-11-13',
  menuSelectionLockDays: 7,
  isLocked: false,
  addonsTotal: 1500,
  addons: [
    {
      addonId: 7,
      description: 'Carving station',
      amount: 1500,
      unit: 'event',
      pricingBasis: 'Flat',
      addonCategoryId: 2,
      addonCategoryName: 'Live stations',
      catalogueAmount: 1500,
      catalogueDescription: 'Carving station',
    },
  ],
}

/** The add-on master, grouped by category by the panel (contract §16.1). */
export const addonMasterFixture: AddonMasterItemDto[] = [
  {
    addonId: 7,
    description: 'Carving station',
    amount: 1500,
    unit: 'event',
    pricingBasis: 'Flat',
    lineAmountForBooking: 1500,
    addonCategoryId: 2,
    addonCategoryName: 'Live stations',
    deptId: 1,
    deptName: 'Kitchen',
    isActive: true,
  },
  {
    addonId: 8,
    description: 'Dessert bar',
    amount: 150,
    unit: 'guest',
    pricingBasis: 'PerGuest',
    lineAmountForBooking: 37500,
    addonCategoryId: 3,
    addonCategoryName: 'Dessert',
    deptId: 2,
    deptName: 'Pastry',
    isActive: true,
  },
  {
    addonId: 9,
    description: 'Cocktail welcome',
    amount: 75,
    unit: null,
    pricingBasis: null,
    lineAmountForBooking: 75,
    addonCategoryId: null,
    addonCategoryName: null,
    deptId: null,
    deptName: null,
    isActive: true,
  },
]

/** The add-on master as it ships in this cycle — empty (M12, RSK-066). */
export const emptyAddonMasterFixture: AddonMasterItemDto[] = []
