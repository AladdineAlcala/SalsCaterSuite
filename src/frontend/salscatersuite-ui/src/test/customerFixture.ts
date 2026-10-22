/**
 * TEST FIXTURE — not production data.
 *
 * A fixed, hand-written set of Customer Master payloads used by the component
 * tests. Every value is chosen so the assertions can check the exact
 * rendering of the formatters, the server's aging labels and the error
 * contract.
 *
 * This file lives under `src/test/` and is never imported by application code.
 */
import type {
  CustomerDetail,
  CustomerDirectoryResponse,
  CustomerStatement,
} from '@/types/customer.types'

export const customerDirectoryFixture: CustomerDirectoryResponse = {
  summary: {
    totalClients: 248,
    activeArAmount: 684200,
    clientsWithBalance: 14,
    totalContractedValue: 18420000,
    averageValuePerClient: 74274.19,
    overdueAmount: 142000,
    overdueClientCount: 3,
  },
  quickViewCounts: {
    all: 248,
    corporate: 142,
    private: 106,
    withOpenBalance: 14,
    overdue30: 3,
  },
  items: [
    {
      customerId: 18,
      lastName: 'Tan',
      firstName: 'Liza',
      middle: null,
      fullName: 'Tan, Liza',
      company: 'Apex Global Technologies Inc.',
      address: 'Level 28, Apex Tower, BGC, Taguig',
      contact1: '+63 920 441 9090',
      contact2: '(02) 8876-4321',
      dateRegistered: '2024-03-01T00:00:00',
      customerType: 'Corporate',
      bookingCounts: { total: 7, past: 6, upcoming: 1, completed: 6, cancelled: 0 },
      totalContractedValue: 1180000,
      totalPaid: 1180000,
      outstandingBalance: 0,
      ledgerState: 'Clean',
      ledgerStateLabel: 'Paid in full',
    },
    {
      customerId: 42,
      lastName: 'Fernandez',
      firstName: 'Ramon',
      middle: 'Santos',
      fullName: 'Fernandez, Ramon Santos',
      company: null,
      address: 'Unit 14B, Salcedo Park Towers, Makati',
      contact1: '+63 917 882 1109',
      contact2: null,
      dateRegistered: '2024-05-18T00:00:00',
      customerType: 'Private',
      bookingCounts: { total: 2, past: 1, upcoming: 1, completed: 1, cancelled: 0 },
      totalContractedValue: 285000,
      totalPaid: 200000,
      outstandingBalance: 85000,
      ledgerState: 'Days31To60',
      ledgerStateLabel: '31-60 days overdue',
    },
    {
      customerId: 67,
      lastName: 'Morales',
      firstName: 'Helena',
      middle: null,
      fullName: 'Morales, Helena',
      company: 'St. Luke Social Club',
      address: '42 Balete Drive, New Manila, Quezon City',
      contact1: '+63 918 554 2230',
      contact2: '(02) 8723-0101',
      dateRegistered: '2023-11-02T00:00:00',
      customerType: 'Corporate',
      bookingCounts: { total: 4, past: 3, upcoming: 1, completed: 3, cancelled: 0 },
      totalContractedValue: 640000,
      totalPaid: 540000,
      outstandingBalance: 100000,
      ledgerState: 'Over90Days',
      // A display string the client could never derive on its own — it proves
      // the label travels from the server untouched (AC-C3).
      ledgerStateLabel: 'Escalated to Collections',
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 248,
  totalPages: 25,
  generatedAt: '2026-10-20T09:15:00Z',
}

export const emptyCustomerDirectoryFixture: CustomerDirectoryResponse = {
  summary: {
    totalClients: 0,
    activeArAmount: 0,
    clientsWithBalance: 0,
    totalContractedValue: 0,
    averageValuePerClient: null,
    overdueAmount: 0,
    overdueClientCount: 0,
  },
  quickViewCounts: {
    all: 0,
    corporate: 0,
    private: 0,
    withOpenBalance: 0,
    overdue30: 0,
  },
  items: [],
  page: 1,
  pageSize: 10,
  totalCount: 0,
  totalPages: 0,
  generatedAt: '2026-10-20T09:15:00Z',
}

export const customerDetailFixture: CustomerDetail = {
  ...customerDirectoryFixture.items[0],
  upcomingBooking: {
    transactionId: 601,
    bookingNumber: 'BK-20260315-00601',
    occasion: 'Q1 Gala Dinner',
    eventDate: '2026-03-15T00:00:00',
    amountDue: 198000,
  },
}

export const customerStatementFixture: CustomerStatement = {
  customerId: 18,
  customerName: 'Tan, Liza',
  contact: '+63 920 441 9090',
  address: 'Level 28, Apex Tower, BGC, Taguig',
  totalBilled: 1180000,
  totalPaid: 1180000,
  totalRefunded: 0,
  outstandingBalance: 0,
  ledgerState: 'Clean',
  ledgerStateLabel: 'Paid in full',
  transactions: [
    {
      transactionId: 601,
      bookingNumber: 'BK-20260315-00601',
      eventDate: '2026-03-15T00:00:00',
      occasion: 'Q1 Gala Dinner',
      amount: 198000,
      paid: 198000,
      balance: 0,
      status: 'Completed',
      agingBucket: null,
      agingBucketLabel: null,
    },
    {
      transactionId: 588,
      bookingNumber: 'BK-20251204-00588',
      eventDate: '2025-12-04T00:00:00',
      occasion: 'Leadership Summit',
      amount: 245000,
      paid: 150000,
      balance: 95000,
      status: 'DepositPaid',
      agingBucket: 'Over90Days',
      agingBucketLabel: 'Over 90 days overdue',
    },
    {
      transactionId: 577,
      bookingNumber: 'BK-20251106-00577',
      eventDate: null,
      // The API sends an empty string when a booking carries no occasion; it
      // must render as an em dash, never as a blank gap.
      occasion: '',
      amount: 120000,
      paid: 120000,
      balance: 0,
      status: 'Completed',
      agingBucket: null,
      agingBucketLabel: null,
    },
    {
      transactionId: 544,
      bookingNumber: 'BK-20260410-00544',
      eventDate: '2026-04-10T00:00:00',
      occasion: 'Corporate Awards Night',
      amount: 96000,
      paid: 96000,
      balance: 0,
      // A compound BR-10 name: the client renders the humanised label.
      status: 'FullyPaid',
      agingBucket: null,
      agingBucketLabel: null,
    },
  ],
}

/**
 * TEST FIXTURE — a customer whose record does not carry the fields the
 * legacy projection leaves empty: no registration date, no booking counters
 * and no ledger figures. Every one of them must render as an em dash.
 */
export const sparseCustomerDetailFixture: CustomerDetail = {
  customerId: 91,
  lastName: 'Santos',
  firstName: 'Maria',
  middle: null,
  fullName: 'Santos, Maria',
  company: null,
  address: null,
  contact1: null,
  contact2: null,
  dateRegistered: null,
  customerType: null,
  bookingCounts: null,
  totalContractedValue: null,
  totalPaid: null,
  outstandingBalance: null,
  ledgerState: null,
  ledgerStateLabel: null,
  upcomingBooking: null,
}
