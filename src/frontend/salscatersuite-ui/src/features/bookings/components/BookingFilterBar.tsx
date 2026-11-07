import { LayoutGrid, Rows3, Search } from 'lucide-react'
import { cn } from '@/utils/cn'
import { formatCount } from '@/utils/currencyFormatter'
import type { BookingFacetCount, BookingPaymentState } from '@/types/booking.types'
import {
  BOOKING_STATUS_FILTER_OPTIONS,
  PAYMENT_STATE_FILTER_OPTIONS,
  bookingStatusFilterLabel,
} from '../bookingVocabulary'

export interface BookingFilterBarProps {
  /** The free-text search term. */
  search: string
  onSearchChange: (value: string) => void
  /** The active status filter, or an empty string for no filter. */
  status: string
  onStatusChange: (value: string) => void
  /** The inclusive lower bound on the event date, or an empty string. */
  fromDate: string
  onFromDateChange: (value: string) => void
  /** The inclusive upper bound on the event date, or an empty string. */
  toDate: string
  onToDateChange: (value: string) => void
  /** The active venue filter, or an empty string for no filter. */
  venue: string
  onVenueChange: (value: string) => void
  /** The active payment-state filter, or an empty string for no filter. */
  paymentState: string
  onPaymentStateChange: (value: string) => void
  /** The overdue dimension — a separate axis from the payment state. */
  overdue: boolean
  onOverdueChange: (value: boolean) => void
  /** The presentation of the register's page. */
  view: 'grid' | 'timeline'
  onViewChange: (value: 'grid' | 'timeline') => void
  /** The status facet counts — the server's. */
  statusFacets: BookingFacetCount[]
  /** The venue facet counts — the server's. */
  venueFacets: BookingFacetCount[]
  /** The payment-state facet counts — the server's. */
  paymentStateFacets: BookingFacetCount[]
}

const FIELD_CLASS =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm'

const SELECT_CLASS =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm'

/**
 * The register's command bar (spec §5.2 item 3).
 *
 * Every control is **server-side**: the search, the status, the date range,
 * the venue and the payment state all travel to `GET /api/bookings` as query
 * parameters, and the client never narrows a page in the browser
 * (AC-BKG-03, AC-BKG-04).
 *
 * The **Payment** dropdown offers the **three** BR-28 states and no
 * percentage-based option: the required deposit percentage is configuration
 * and absent by default, so a percentage filter would assert a house rule that
 * does not exist (DEC-101).
 *
 * The **Overdue only** chip is a **separate dimension**, clearly labelled as
 * such — a partly-paid booking is both `DepositPaid` and overdue, so the
 * two can never be folded into one vocabulary (BR-28).
 *
 * The **timeline** toggle is a **server-side sort**: it asks for the
 * chronological order and renders the same server page as a rail. It never
 * re-orders one page in the browser, which would misrepresent the population
 * as a chronology (spec §5.2).
 */
export function BookingFilterBar({
  search,
  onSearchChange,
  status,
  onStatusChange,
  fromDate,
  onFromDateChange,
  toDate,
  onToDateChange,
  venue,
  onVenueChange,
  paymentState,
  onPaymentStateChange,
  overdue,
  onOverdueChange,
  view,
  onViewChange,
  statusFacets,
  venueFacets,
  paymentStateFacets,
}: BookingFilterBarProps) {
  const statusCount = (name: string): number | null => {
    const facet = statusFacets.find((entry) => entry.key === name)
    return facet ? facet.count : null
  }

  const paymentStateCount = (name: BookingPaymentState): number | null => {
    const facet = paymentStateFacets.find((entry) => entry.key === name)
    return facet ? facet.count : null
  }

  return (
    <section className="bg-artisan-surface p-space-md rounded-xl shadow-elevation-1 border border-artisan-border flex flex-col gap-space-sm">
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-space-sm">
        <div className="relative flex-1 lg:max-w-md min-w-0">
          <label htmlFor="booking-search" className="sr-only">
            Search bookings
          </label>
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 text-espresso-400 h-5 w-5 pointer-events-none"
            aria-hidden="true"
          />
          <input
            id="booking-search"
            name="search"
            type="search"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="Search booking code, client, occasion or venue…"
            className={`${FIELD_CLASS} pl-10`}
          />
        </div>

        <div
          className="flex items-center gap-1 self-end lg:self-auto bg-surface-container-low p-1 rounded-lg"
          role="group"
          aria-label="Register view"
        >
          <button
            type="button"
            onClick={() => onViewChange('grid')}
            aria-pressed={view === 'grid'}
            className={cn(
              'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-caption font-semibold transition-colors',
              view === 'grid'
                ? 'bg-artisan-surface text-espresso-900 shadow-sm'
                : 'text-espresso-600 hover:text-espresso-900',
            )}
          >
            <LayoutGrid className="h-4 w-4" aria-hidden="true" />
            Dossier grid
          </button>
          <button
            type="button"
            onClick={() => onViewChange('timeline')}
            aria-pressed={view === 'timeline'}
            className={cn(
              'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-caption font-semibold transition-colors',
              view === 'timeline'
                ? 'bg-artisan-surface text-espresso-900 shadow-sm'
                : 'text-espresso-600 hover:text-espresso-900',
            )}
          >
            <Rows3 className="h-4 w-4" aria-hidden="true" />
            Timeline
          </button>
        </div>
      </div>

      <div className="pt-2 border-t border-surface-container grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-2">
        <div className="flex flex-col gap-1 min-w-0">
          <label
            htmlFor="booking-status-filter"
            className="font-caption text-caption text-espresso-400 font-semibold uppercase"
          >
            Status
          </label>
          <select
            id="booking-status-filter"
            name="status"
            value={status}
            onChange={(event) => onStatusChange(event.target.value)}
            className={SELECT_CLASS}
          >
            <option value="">All statuses</option>
            {BOOKING_STATUS_FILTER_OPTIONS.map((option) => {
              const count = statusCount(option)
              return (
                <option key={option} value={option}>
                  {bookingStatusFilterLabel(option)}
                  {count === null ? '' : ` (${formatCount(count)})`}
                </option>
              )
            })}
          </select>
        </div>

        <div className="flex flex-col gap-1 min-w-0">
          <label
            htmlFor="booking-from-date"
            className="font-caption text-caption text-espresso-400 font-semibold uppercase"
          >
            Event from
          </label>
          <input
            id="booking-from-date"
            name="fromDate"
            type="date"
            value={fromDate}
            onChange={(event) => onFromDateChange(event.target.value)}
            className={FIELD_CLASS}
          />
        </div>

        <div className="flex flex-col gap-1 min-w-0">
          <label
            htmlFor="booking-to-date"
            className="font-caption text-caption text-espresso-400 font-semibold uppercase"
          >
            Event to
          </label>
          <input
            id="booking-to-date"
            name="toDate"
            type="date"
            value={toDate}
            onChange={(event) => onToDateChange(event.target.value)}
            className={FIELD_CLASS}
          />
        </div>

        <div className="flex flex-col gap-1 min-w-0">
          <label
            htmlFor="booking-venue-filter"
            className="font-caption text-caption text-espresso-400 font-semibold uppercase"
          >
            Venue
          </label>
          <select
            id="booking-venue-filter"
            name="venue"
            value={venue}
            onChange={(event) => onVenueChange(event.target.value)}
            className={SELECT_CLASS}
          >
            <option value="">All venues</option>
            {venueFacets.map((facet) => (
              <option key={facet.key} value={facet.key}>
                {facet.key} ({formatCount(facet.count)})
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1 min-w-0">
          <label
            htmlFor="booking-payment-filter"
            className="font-caption text-caption text-espresso-400 font-semibold uppercase"
          >
            Payment
          </label>
          <select
            id="booking-payment-filter"
            name="paymentState"
            value={paymentState}
            onChange={(event) => onPaymentStateChange(event.target.value)}
            className={SELECT_CLASS}
          >
            <option value="">All payment states</option>
            {PAYMENT_STATE_FILTER_OPTIONS.map((option) => {
              const count = paymentStateCount(option.value)
              return (
                <option key={option.value} value={option.value}>
                  {option.label}
                  {count === null ? '' : ` (${formatCount(count)})`}
                </option>
              )
            })}
          </select>
        </div>
      </div>

      <div className="pt-2 border-t border-surface-container flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => onOverdueChange(!overdue)}
          aria-pressed={overdue}
          className={cn(
            'inline-flex items-center gap-1.5 px-3 h-9 rounded-full text-caption font-bold transition-colors border',
            overdue
              ? 'bg-error-container text-on-error-container border-error-container'
              : 'bg-surface-container-lowest text-espresso-700 border-artisan-border hover:bg-surface-container',
          )}
        >
          Overdue only
        </button>
        <span className="font-caption text-caption text-espresso-400">
          Overdue is a dimension of its own — it is not a fourth payment state.
        </span>
      </div>
    </section>
  )
}
