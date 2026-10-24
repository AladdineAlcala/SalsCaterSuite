import { CalendarClock, Search } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { formatCount } from '@/utils/currencyFormatter'
import { formatEventDate } from '@/utils/dateFormatter'
import { PAX_UNIT } from '../bookingVocabulary'

export interface BookingMenuFilterPanelProps {
  search: string
  onSearchChange: (value: string) => void
  /** The published lock date — always a concrete date (DEC-128). */
  lockDate: string
  /** The booking's headcount. `null` until the details are read. */
  pax: number | null
  /** How many qualified dishes the tier publishes across its pools. */
  poolSize: number
  /** How many of them the search leaves visible. */
  matchedCount: number
  /** The whole editor is read-only when the window has closed. */
  isLocked: boolean
}

const INPUT_CLASSES =
  'w-full h-10 pl-9 pr-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm'

/**
 * The filter panel (spec §5.3).
 *
 * A **search over the dish pool**, the **cutoff badge** — the menu-change
 * window's date, which is always published — and the **covers** (the
 * headcount). The reference's "180+ commissary recipes" is not repeated as a
 * claim: the panel states the pool's real size, which the server published.
 */
export function BookingMenuFilterPanel({
  search,
  onSearchChange,
  lockDate,
  pax,
  poolSize,
  matchedCount,
  isLocked,
}: BookingMenuFilterPanelProps) {
  return (
    <div className="bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 p-space-md flex flex-col gap-space-sm min-w-0">
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <Badge tone="warning" icon={CalendarClock}>
          BEO cutoff: {formatEventDate(lockDate)}
        </Badge>
        <div className="flex items-center gap-2 flex-wrap">
          <Badge tone="info">
            {formatCount(pax)} {PAX_UNIT} covered
          </Badge>
          <Badge tone="neutral">{formatCount(poolSize)} qualified dishes</Badge>
        </div>
      </div>

      <div className="flex flex-col gap-1 min-w-0">
        <label
          htmlFor="booking-menu-search"
          className="font-caption text-caption text-espresso-400 font-semibold uppercase"
        >
          Search the dish pool
        </label>
        <div className="relative min-w-0">
          <Search
            className="h-4 w-4 text-espresso-400 absolute left-3 top-1/2 -translate-y-1/2"
            aria-hidden="true"
          />
          <input
            id="booking-menu-search"
            name="booking-menu-search"
            type="search"
            value={search}
            disabled={isLocked}
            placeholder="Search a qualified dish by name"
            onChange={(event) => onSearchChange(event.target.value)}
            className={INPUT_CLASSES}
          />
        </div>
        <p className="font-caption text-caption text-espresso-400">
          {search.trim().length === 0
            ? 'Every qualified dish of the tier is listed course by course.'
            : `${formatCount(matchedCount)} of ${formatCount(poolSize)} qualified dishes match the search.`}
        </p>
      </div>
    </div>
  )
}
