/**
 * TypeScript mirror of the Customer Master & Accounts Ledger contract.
 *
 * Source: `business/customer-master-feature-spec.md` §5.
 *
 * Every field below is a **server-calculated** value. The client never derives a
 * financial figure, a day count or an aging bucket from this payload (DEC-002,
 * AC-C3) — it formats and displays only. The aging *key* travels beside the
 * aging *label* so the client can pick a colour while the text always comes
 * from the server.
 */

/**
 * The derived customer classification (DEC-051): `Corporate` when the company
 * attribute is non-blank, `Private` otherwise. It is a presentation of an
 * existing attribute and is never persisted.
 */
export type CustomerType = 'Corporate' | 'Private'

/**
 * The customer AR state (DEC-052): the KPI-13 aging-bucket vocabulary plus a
 * `Clean` key. The **label** that accompanies the key is the server's display
 * string and is rendered verbatim.
 */
export type CustomerLedgerState =
  | 'Clean'
  | 'NotYetDue'
  | 'Days1To30'
  | 'Days31To60'
  | 'Days61To90'
  | 'Over90Days'

/** Booking population of one customer, counted by the server. */
export interface CustomerBookingCounts {
  total: number
  past: number
  upcoming: number
  completed: number
  cancelled: number
}

/**
 * The quick-view population counts (spec §4.3).
 *
 * They are computed over the **search-filtered population with the type and
 * AR-state filters ignored**, so every pill always describes the population
 * the operator is looking at — the pills are population counts, not the count
 * of the currently filtered page.
 */
export interface CustomerQuickViewCounts {
  all: number
  corporate: number
  private: number
  withOpenBalance: number
  overdue30: number
}

/**
 * The directory summary. It describes the **whole filtered result set**, not
 * the current page (spec §5.1).
 */
export interface CustomerDirectorySummary {
  /** KPI-20 — count of customer profiles. */
  totalClients: number
  /** KPI-12 — the sum of every customer's outstanding balance. */
  activeArAmount: number
  /** How many of those clients carry an open balance. */
  clientsWithBalance: number
  /** KPI-21 — the sum of the net amount due over the clients' bookings. */
  totalContractedValue: number
  /** `null` when there are no clients — never rendered as zero (spec §4.2). */
  averageValuePerClient: number | null
  /** KPI-13 — the balance older than thirty days. */
  overdueAmount: number
  /** How many clients are overdue. */
  overdueClientCount: number
}

/**
 * One row of the customer directory.
 *
 * The directory projection always populates every figure, so the row shape is
 * non-nullable — with the single exception of `dateRegistered`, which the
 * customer record itself may not carry.
 */
export interface CustomerDirectoryItem {
  customerId: number
  lastName: string
  firstName: string
  middle: string | null
  fullName: string
  company: string | null
  address: string | null
  contact1: string | null
  contact2: string | null
  dateRegistered: string | null
  customerType: CustomerType
  bookingCounts: CustomerBookingCounts
  totalContractedValue: number
  totalPaid: number
  outstandingBalance: number
  ledgerState: CustomerLedgerState
  /** The server's display string for {@link ledgerState} — rendered verbatim. */
  ledgerStateLabel: string
}

/** `GET /api/customers/directory` response. */
export interface CustomerDirectoryResponse {
  summary: CustomerDirectorySummary
  /**
   * The quick-view population counts. They are deliberately **separate** from
   * `summary`, which describes the fully filtered result set so the KPI cards
   * respond to every active filter (spec §4.3).
   */
  quickViewCounts: CustomerQuickViewCounts
  items: CustomerDirectoryItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  /** The server UTC timestamp of the aggregate (spec §4.10). */
  generatedAt: string
}

/**
 * The customer's next non-cancelled booking, when there is one.
 *
 * The occasion is a plain string on the wire; an empty one is a missing
 * occasion and renders as an em dash.
 */
export interface CustomerUpcomingBooking {
  transactionId: number
  bookingNumber: string
  occasion: string
  eventDate: string
  amountDue: number
}

/**
 * `GET /api/customers/{id}` response.
 *
 * The by-id projection populates every figure, but the underlying DTO keeps
 * the legacy list endpoint's shape — where the extension fields are nullable.
 * The client therefore treats them as possibly absent and renders an em dash
 * rather than assuming a zero (contract §9.2 rule 6, "no null-as-zero").
 */
export interface CustomerDetail {
  customerId: number
  lastName: string
  firstName: string
  middle: string | null
  fullName: string
  company: string | null
  address: string | null
  contact1: string | null
  contact2: string | null
  dateRegistered: string | null
  customerType: CustomerType | null
  bookingCounts: CustomerBookingCounts | null
  totalContractedValue: number | null
  totalPaid: number | null
  outstandingBalance: number | null
  ledgerState: CustomerLedgerState | null
  /** The server's display string for {@link ledgerState} — rendered verbatim. */
  ledgerStateLabel: string | null
  upcomingBooking: CustomerUpcomingBooking | null
}

/** One booking line of a statement of account. */
export interface CustomerStatementLine {
  transactionId: number
  bookingNumber: string
  eventDate: string | null
  /** A plain string on the wire; an empty one renders as an em dash. */
  occasion: string
  /** The net amount due for the booking. */
  amount: number
  paid: number
  /** `max(0, amount − paid)` — never negative. */
  balance: number
  /** A BR-10 booking lifecycle name. */
  status: string
  /** The KPI-13 aging bucket key, or `null` when the line is not overdue. */
  agingBucket: string | null
  /** The server's display string for the aging bucket — rendered verbatim. */
  agingBucketLabel: string | null
}

/** `GET /api/customers/{id}/statement` response. */
export interface CustomerStatement {
  customerId: number
  customerName: string
  contact: string | null
  address: string | null
  totalBilled: number
  totalPaid: number
  totalRefunded: number
  outstandingBalance: number
  ledgerState: CustomerLedgerState
  /** The server's display string for {@link ledgerState} — rendered verbatim. */
  ledgerStateLabel: string
  transactions: CustomerStatementLine[]
}

/** The body of `POST /api/customers` and `PUT /api/customers/{id}`. */
export interface CustomerUpsertRequest {
  lastName: string
  firstName: string
  middle: string | null
  address: string | null
  contact1: string | null
  contact2: string | null
  company: string | null
}

/** The `201` body of `POST /api/customers`. */
export interface CustomerCreatedResponse {
  customerId: number
}

/** The `customerType` filter accepted by the directory endpoint. */
export type CustomerTypeFilter = '' | CustomerType

/** The `arState` filter accepted by the directory endpoint. */
export type CustomerArStateFilter = '' | 'Clean' | 'Open' | 'Overdue30' | 'Overdue60'
