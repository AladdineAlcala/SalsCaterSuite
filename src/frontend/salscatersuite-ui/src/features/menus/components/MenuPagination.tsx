import { ChevronLeft, ChevronRight } from 'lucide-react'
import { cn } from '@/utils/cn'
import { formatCount } from '@/utils/currencyFormatter'

export interface MenuPaginationProps {
  /** 1-based page number. */
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
  isFetching: boolean
}

/** The page sizes the master endpoint accepts (spec §4.7). */
const PAGE_SIZE_OPTIONS = [12, 24, 48]

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
 * The pagination footer (spec §4.7).
 *
 * Paging is entirely server-side: the client sends the page and the page
 * size and renders what came back. The range shown is the range of the
 * current page inside the server-reported result set.
 */
export function MenuPagination({
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
  onPageSizeChange,
  isFetching,
}: MenuPaginationProps) {
  const first = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const last = totalCount === 0 ? 0 : Math.min(page * pageSize, totalCount)
  const entries = buildPageEntries(page, totalPages)

  return (
    <footer className="p-space-md bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 flex flex-col sm:flex-row items-center justify-between gap-2">
      <div className="flex items-center gap-2 font-caption text-caption text-espresso-600 flex-wrap">
        <span>
          Showing{' '}
          <span className="font-bold text-espresso-900">
            {formatCount(first)}&ndash;{formatCount(last)}
          </span>{' '}
          of {formatCount(totalCount)} recipes
        </span>
        <span className="text-espresso-200" aria-hidden="true">
          &bull;
        </span>
        <label htmlFor="menu-page-size">Page size:</label>
        <select
          id="menu-page-size"
          name="pageSize"
          value={pageSize}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
          className="bg-artisan-surface border border-artisan-border rounded px-1.5 py-0.5 text-espresso-800 font-data-sm text-data-sm focus:outline-none"
        >
          {PAGE_SIZE_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        {isFetching ? (
          <span className="font-caption text-caption text-caramel-700 font-semibold">
            Refreshing…
          </span>
        ) : null}
      </div>

      <nav
        aria-label="Menu catalogue pages"
        className="flex items-center gap-1 flex-wrap justify-end"
      >
        <button
          type="button"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
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
          className="px-2.5 py-1 rounded bg-artisan-surface border border-artisan-border text-espresso-700 hover:bg-surface-container font-caption text-caption disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-1"
        >
          Next
          <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
        </button>
      </nav>
    </footer>
  )
}
