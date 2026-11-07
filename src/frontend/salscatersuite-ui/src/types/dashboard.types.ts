/**
 * TypeScript mirror of the FROZEN dashboard contract.
 *
 * Source: `architecture/approved-api-contracts.md` §9 (Dashboard Endpoint).
 * Every field below is a server-calculated value. The client never derives a
 * financial figure from this payload (DEC-002) — it formats and displays only.
 */

/**
 * BR-10 booking lifecycle status. Serialised by the API as its string name
 * (contract §8 — string enum convention).
 */
export type BookingStatus =
  | 'Pending'
  | 'Confirmed'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow'
  | 'Rescheduled'
  | 'Expired'

export interface DashboardPeriod {
  year: number
  month: number
  label: string
  startDate: string
  endDate: string
}

export interface DashboardBenchmarks {
  /** `Dashboard:Benchmarks:TargetFoodCostPercentage` (default 35.0). */
  targetFoodCostPercentage: number | null
  /** `100 − targetFoodCostPercentage`. */
  targetMarginPercentage: number | null
  /** `false` when the configuration key was absent and the default was substituted. */
  isConfigured: boolean
}

export interface DashboardBookings {
  totalCount: number
  committedCount: number
  tentativeCount: number
  otherCount: number
  contractedValue: number
  totalPax: number
}

export interface DashboardRevenue {
  netContractedRevenue: number
  averageBookingValue: number | null
  bookingCount: number
}

export interface DashboardCollections {
  amount: number
  receiptCount: number
  previousPeriodAmount: number
  changePercentage: number | null
  collectionRatioPercentage: number | null
}

export type AgingBucketKey =
  | 'NotYetDue'
  | 'Days1To30'
  | 'Days31To60'
  | 'Days61To90'
  | 'Over90Days'

export interface DashboardAgingBucket {
  key: AgingBucketKey | string
  label: string
  bookingCount: number
  amount: number
}

export interface DashboardReceivables {
  outstandingAmount: number
  bookingCount: number
  /** Bookings in the `Days31To60`, `Days61To90` and `Over90Days` buckets. */
  overdueCount: number
  /** Sum of the amounts in those three buckets. */
  overdueAmount: number
  agingBuckets: DashboardAgingBucket[]
}

export interface DashboardDishCosting {
  totalMenuCount: number
  costedMenuCount: number
  uncostedMenuCount: number
  coveragePercentage: number | null
}

export interface DashboardMargin {
  catalogueRevenueAmount: number
  catalogueFoodCostAmount: number
  catalogueMarginAmount: number
  catalogueMarginPercentage: number | null
  /** Percentage points against the configured benchmark. */
  varianceToBenchmarkPercentage: number | null
}

export interface DashboardFoodCostCategory {
  courseId: number | null
  courseName: string
  foodCostAmount: number
  sharePercentage: number
  menuCount: number
}

export interface DashboardUpcomingBooking {
  transactionId: number
  bookingNumber: string
  customerName: string
  occasion: string | null
  eventDate: string
  numberOfHeads: number
  status: BookingStatus
  packageName: string | null
  netAmountDue: number
  amountPaid: number
  balance: number
}

export interface DashboardLedger {
  collectedLast7DaysAmount: number
  collectedLast7DaysReceiptCount: number
  awaitingDepositBookingCount: number
  awaitingDepositAmount: number
  receiptsIssuedCount: number
}

export interface DashboardDto {
  period: DashboardPeriod
  benchmarks: DashboardBenchmarks
  bookings: DashboardBookings
  revenue: DashboardRevenue
  collections: DashboardCollections
  receivables: DashboardReceivables
  dishCosting: DashboardDishCosting
  margin: DashboardMargin
  foodCostByCategory: DashboardFoodCostCategory[]
  upcomingBookings: DashboardUpcomingBooking[]
  ledger: DashboardLedger
  generatedAt: string
}
