import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/utils/cn'
import { formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { BookingRegisterTotals } from '@/types/booking.types'
import { BOOKING_PAGE_SIZES } from '../bookingVocabulary'

export interface BookingTotalsStripProps {
  /** The totals strip — every figure is over the whole filtered population. */
  totals: BookingRegisterTotals
  /** The 1-based page. */
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
  isFetching: boolean
}

/** How many numbered page buttons are shown around the current page. */
const PAGE_WINDOW = 5

type PageEntry = number | 'gap'

/**
 * The numbered page buttons: the first and the last page are always
 * reachable, with a window of consecutive pages around the current one.
 */
function buildPageEntries(current: number, total: number): PageEntry[] {
  if (total <= PAGE_WINDOW + 2) {
    return Array.from({ length: total }, (_unused, index) => index + 1)
  }

  const entries: PageEntry[] = [1]
  const start = Math.max(2, Math.min(current - 2, total - PAGE_WINDOW - 1))
  const end = Math.min(total - 1, start + PAGE_WINDOW - 1)

  if (start > 2) entries.push('gap')
  for (let page = start; page <= end; page += 1) entries.push(page)
  if (end < total - 1) entries.push('gap')
  entries.push(total)

  return entries
}

/**
 * The totals strip and the pagination dock (spec §5.2 item 5).
 *
 * Every figure in the strip is the server's — over the **whole filtered
 * population**, never over the page. The client never sums the dossier cards
 * to produce a total (DEC-002, AC-BKG-01).
 *
 * The collected and the outstanding figures **sum** to the net contracted
 * value, because the server computes all three from one population
 * (AC-BKG-06).
 */
export function BookingTotalsStrip({
  totals,
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
  onPageSizeChange,
  isFetching,
}: BookingTotalsStripProps) {
  const first = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const last = totalCount === 0 ? 0 : Math.min(page * pageSize, totalCount)
  const entries = buildPageEntries(page, totalPages)

  return (
    <div className="rounded-xl bg-artisan-surface shadow-elevation-1 border border-artisan-border overflow-hidden flex flex-col">
      <div className="px-space-md py-space-sm bg-surface-container-low flex flex-col sm:flex-row items-center justify-between gap-3 border-b border-surface-container">
        <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-caption font-medium">
          <span className="font-bold text-espresso-900 uppercase text-caption tracking-wider">
            Table totals:
          </span>
          <span className="flex items-center gap-1.5">
            <span className="text-espresso-600">Bookings:</span>
            <span className="font-mono text-body-sm font-bold text-espresso-900">
              {formatCount(totals.bookingCount)}
            </span>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="text-espresso-600">Contract value:</span>
            <span className="font-mono text-body-sm font-bold text-espresso-900">
              {formatCurrency(totals.netContractedValue)}
            </span>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="text-espresso-600">Collected:</span>
            <span className="font-mono text-body-sm font-bold text-culinary-700">
              {formatCurrency(totals.collectedAmount)}
            </span>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="text-espresso-600">Receivables:</span>
            <span className="font-mono text-body-sm font-bold text-caramel-700">
              {formatCurrency(totals.outstandingAmount)}
            </span>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="text-espresso-600">Headcount:</span>
            <span className="font-mono text-body-sm font-bold text-espresso-900">
              {formatCount(totals.totalPax)}
            </span>
          </span>
        </div>

        {isFetching ? (
          <span className="font-caption text-caption text-caramel-700 font-semibold">
            Refreshing…
          </span>
        ) : null}
      </div>

      <div className="px-space-md py-space-sm bg-artisan-surface flex flex-col sm:flex-row items-center justify-between gap-3">
        <div className="flex items-center gap-3 flex-wrap">
          <span className="text-caption font-medium text-espresso-600">
            Showing{' '}
            <span className="font-bold text-espresso-900">
              {formatCount(first)}&ndash;{formatCount(last)}
            </span>{' '}
            of {formatCount(totalCount)} bookings
          </span>
          <div className="flex items-center gap-1.5 text-caption text-espresso-600">
            <label htmlFor="booking-page-size">Rows:</label>
            <select
              id="booking-page-size"
              name="pageSize"
              value={pageSize}
              onChange={(event) => onPageSizeChange(Number(event.target.value))}
              className="bg-surface-container text-espresso-900 font-mono text-caption font-semibold rounded px-1.5 py-0.5 border border-artisan-border focus:outline-none focus:ring-2 focus:ring-culinary-500/15"
            >
              {BOOKING_PAGE_SIZES.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </div>
        </div>

        <nav aria-label="Bookings register pages" className="flex items-center gap-1 flex-wrap">
          <button
            type="button"
            onClick={() => onPageChange(page - 1)}
            disabled={page <= 1}
            aria-label="Previous page"
            className="px-2.5 py-1 rounded bg-artisan-surface border border-artisan-border text-espresso-700 hover:bg-surface-container font-caption text-caption disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-1"
          >
            <ChevronLeft className="h-3.5 w-3.5" aria-hidden="true" />
            Previous
          </button>

          {entries.map((entry, index) =>
            entry === 'gap' ? (
              <span
                key={`gap-${index}`}
                className="px-1 text-espresso-400 font-caption text-caption"
                aria-hidden="true"
              >
                …
              </span>
            ) : (
              <button
                key={entry}
                type="button"
                onClick={() => onPageChange(entry)}
                aria-current={entry === page ? 'page' : undefined}
                className={cn(
                  'px-2.5 py-1 rounded font-caption text-caption font-bold',
                  entry === page
                    ? 'bg-culinary-600 text-white'
                    : 'bg-artisan-surface border border-artisan-border text-espresso-700 hover:bg-surface-container',
                )}
              >
                {entry}
              </button>
            ),
          )}

          <button
            type="button"
            onClick={() => onPageChange(page + 1)}
            disabled={page >= totalPages}
            aria-label="Next page"
            className="px-2.5 py-1 rounded bg-artisan-surface border border-artisan-border text-espresso-700 hover:bg-surface-container font-caption text-caption disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-1"
          >
            Next
            <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
          </button>
        </nav>
      </div>
    </div>
  )
}
