import { cn } from '@/utils/cn'
import { formatCount } from '@/utils/currencyFormatter'
import type { BookingFacetCount } from '@/types/booking.types'

export interface BookingTierPillsProps {
  /** The tier facet — each count is the server's (contract §14.1). */
  facets: BookingFacetCount[]
  /** The active tier type, or an empty string for no tier filter. */
  activeType: string
  onTypeChange: (value: string) => void
}

/**
 * The tier pills (spec §5.2 item 3).
 *
 * Every pill carries the **server's facet count**. A facet is computed with
 * its own dimension removed, so each count answers "how many would I get if I
 * chose this?" (BR-32, AC-BKG-03).
 *
 * The pill label is the **stored** tier type, rendered verbatim — the client
 * never rewrites it into a house vocabulary.
 */
export function BookingTierPills({ facets, activeType, onTypeChange }: BookingTierPillsProps) {
  return (
    <div
      className="flex items-center gap-2 overflow-x-auto pb-1"
      role="group"
      aria-label="Tier type"
      data-testid="booking-tier-pills"
    >
      <button
        type="button"
        onClick={() => onTypeChange('')}
        aria-pressed={activeType === ''}
        className={cn(
          'px-4 py-2 rounded-full text-body-md font-semibold shrink-0 shadow-sm transition-all',
          activeType === ''
            ? 'bg-espresso-900 text-artisan-surface'
            : 'bg-artisan-surface text-espresso-700 hover:bg-surface-container border border-artisan-border',
        )}
      >
        All tiers
      </button>

      {facets.map((facet) => {
        const isActive = activeType === facet.key
        return (
          <button
            key={facet.key}
            type="button"
            onClick={() => onTypeChange(isActive ? '' : facet.key)}
            aria-pressed={isActive}
            className={cn(
              'px-4 py-2 rounded-full text-body-md font-medium shrink-0 shadow-sm transition-all',
              'flex items-center gap-1.5',
              isActive
                ? 'bg-espresso-900 text-artisan-surface'
                : 'bg-artisan-surface text-espresso-700 hover:bg-surface-container border border-artisan-border',
            )}
          >
            <span>{facet.key}</span>
            <span
              className={cn(
                'px-1.5 py-0.5 rounded-full font-mono text-caption font-bold',
                isActive
                  ? 'bg-artisan-surface/20 text-artisan-surface'
                  : 'bg-surface-container text-espresso-700',
              )}
            >
              {formatCount(facet.count)}
            </span>
          </button>
        )
      })}
    </div>
  )
}
