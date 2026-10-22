/**
 * TEST FIXTURE — not production data.
 *
 * A fixed, hand-written `DashboardDto` used by the component tests. Every
 * value is chosen so the assertions can check the exact rendering of the
 * formatters, the progress-track bounds and the panel mappings.
 *
 * This file lives under `src/test/` and is never imported by application code.
 */
import type { DashboardDto } from '@/types/dashboard.types'

export const dashboardFixture: DashboardDto = {
  period: {
    year: 2026,
    month: 10,
    label: 'October 2026',
    startDate: '2026-10-01',
    endDate: '2026-10-31',
  },
  benchmarks: {
    targetFoodCostPercentage: 35,
    targetMarginPercentage: 65,
    isConfigured: true,
  },
  bookings: {
    totalCount: 4,
    committedCount: 3,
    tentativeCount: 1,
    otherCount: 0,
    contractedValue: 425000,
    totalPax: 320,
  },
  revenue: {
    netContractedRevenue: 425000,
    averageBookingValue: 106250,
    bookingCount: 4,
  },
  collections: {
    amount: 300000,
    receiptCount: 6,
    previousPeriodAmount: 250000,
    changePercentage: 20,
    collectionRatioPercentage: 70.59,
  },
  receivables: {
    outstandingAmount: 125000,
    bookingCount: 3,
    overdueCount: 1,
    overdueAmount: 40000,
    agingBuckets: [
      { key: 'NotYetDue', label: 'Not yet due', bookingCount: 2, amount: 65000 },
      { key: 'Days1To30', label: '1-30 days', bookingCount: 0, amount: 0 },
      { key: 'Days31To60', label: '31-60 days', bookingCount: 1, amount: 40000 },
      { key: 'Days61To90', label: '61-90 days', bookingCount: 0, amount: 0 },
      { key: 'Over90Days', label: '90+ days', bookingCount: 0, amount: 0 },
    ],
  },
  dishCosting: {
    totalMenuCount: 20,
    costedMenuCount: 15,
    uncostedMenuCount: 5,
    coveragePercentage: 75,
  },
  margin: {
    catalogueRevenueAmount: 500000,
    catalogueFoodCostAmount: 220000,
    catalogueMarginAmount: 280000,
    catalogueMarginPercentage: 56,
    varianceToBenchmarkPercentage: -9,
  },
  foodCostByCategory: [
    {
      courseId: 1,
      courseName: 'Beef Entree',
      foodCostAmount: 132000,
      sharePercentage: 60,
      menuCount: 5,
    },
    {
      courseId: null,
      courseName: 'Uncategorised',
      foodCostAmount: 88000,
      sharePercentage: 40,
      menuCount: 3,
    },
  ],
  upcomingBookings: [
    {
      transactionId: 101,
      bookingNumber: 'BK-20261018-00101',
      customerName: 'Santos, Maria',
      occasion: 'Silver Wedding Anniversary',
      eventDate: '2026-10-28',
      numberOfHeads: 250,
      status: 'Confirmed',
      packageName: 'Premier Wedding Package',
      netAmountDue: 285000,
      amountPaid: 200000,
      balance: 85000,
    },
    {
      transactionId: 102,
      bookingNumber: 'BK-20261019-00102',
      customerName: 'Dela Cruz, Juan',
      occasion: 'Corporate Gala',
      eventDate: '2026-11-04',
      numberOfHeads: 180,
      status: 'DepositPaid',
      packageName: 'Premier Buffet Package',
      netAmountDue: 198000,
      amountPaid: 99000,
      balance: 99000,
    },
  ],
  ledger: {
    collectedLast7DaysAmount: 180000,
    collectedLast7DaysReceiptCount: 5,
    awaitingDepositBookingCount: 2,
    awaitingDepositAmount: 75000,
    receiptsIssuedCount: 6,
  },
  generatedAt: '2026-10-19T10:00:00Z',
}

/**
 * TEST FIXTURE — the same shape with nothing recorded yet. Used to prove the
 * empty states render instead of zeros.
 */
export const emptyDashboardFixture: DashboardDto = {
  period: {
    year: 2026,
    month: 10,
    label: 'October 2026',
    startDate: '2026-10-01',
    endDate: '2026-10-31',
  },
  benchmarks: {
    targetFoodCostPercentage: 35,
    targetMarginPercentage: 65,
    isConfigured: false,
  },
  bookings: {
    totalCount: 0,
    committedCount: 0,
    tentativeCount: 0,
    otherCount: 0,
    contractedValue: 0,
    totalPax: 0,
  },
  revenue: {
    netContractedRevenue: 0,
    averageBookingValue: null,
    bookingCount: 0,
  },
  collections: {
    amount: 0,
    receiptCount: 0,
    previousPeriodAmount: 0,
    changePercentage: null,
    collectionRatioPercentage: null,
  },
  receivables: {
    outstandingAmount: 0,
    bookingCount: 0,
    overdueCount: 0,
    overdueAmount: 0,
    agingBuckets: [
      { key: 'NotYetDue', label: 'Not yet due', bookingCount: 0, amount: 0 },
      { key: 'Days1To30', label: '1-30 days', bookingCount: 0, amount: 0 },
      { key: 'Days31To60', label: '31-60 days', bookingCount: 0, amount: 0 },
      { key: 'Days61To90', label: '61-90 days', bookingCount: 0, amount: 0 },
      { key: 'Over90Days', label: '90+ days', bookingCount: 0, amount: 0 },
    ],
  },
  dishCosting: {
    totalMenuCount: 0,
    costedMenuCount: 0,
    uncostedMenuCount: 0,
    coveragePercentage: 0,
  },
  margin: {
    catalogueRevenueAmount: 0,
    catalogueFoodCostAmount: 0,
    catalogueMarginAmount: 0,
    catalogueMarginPercentage: null,
    varianceToBenchmarkPercentage: null,
  },
  foodCostByCategory: [],
  upcomingBookings: [],
  ledger: {
    collectedLast7DaysAmount: 0,
    collectedLast7DaysReceiptCount: 0,
    awaitingDepositBookingCount: 0,
    awaitingDepositAmount: 0,
    receiptsIssuedCount: 0,
  },
  generatedAt: '2026-10-19T10:00:00Z',
}
