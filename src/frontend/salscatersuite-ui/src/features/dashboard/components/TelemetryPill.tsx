import { useId } from 'react'
import { Info } from 'lucide-react'
import { formatPercentage } from '@/utils/currencyFormatter'
import type { PeriodOption } from '@/utils/dateFormatter'

export interface TelemetryPillProps {
  /** The active `YYYY-MM` key. */
  periodKey: string
  /** The label the server resolved for the active period. */
  periodLabel: string
  periodOptions: PeriodOption[]
  onPeriodChange: (period: string) => void
  /** `Dashboard:Benchmarks:TargetFoodCostPercentage` — never hardcoded. */
  targetFoodCostPercentage: number | null
  /** `Collections ÷ Net contracted revenue` — null when the revenue is zero. */
  collectionRatioPercentage: number | null
  isRefreshing?: boolean
}

const COLLECTION_HINT =
  'Indicative only: collections are a cash-basis flow while net contracted revenue is an accrual-basis flow, so this ratio can exceed 100% when prior-period receivables are collected in-period.'

/**
 * The quick telemetry pill (spec §9.2 item 2).
 *
 * Shows the active reporting period with a month selector, the server-supplied
 * target food-cost percentage and the server-supplied collection ratio.
 *
 * The strip wraps rather than overflowing: each readout is kept together as one
 * unit so the strip never forces a horizontal scrollbar onto the page
 * (AC-F15).
 */
export function TelemetryPill({
  periodKey,
  periodLabel,
  periodOptions,
  onPeriodChange,
  targetFoodCostPercentage,
  collectionRatioPercentage,
  isRefreshing = false,
}: TelemetryPillProps) {
  const selectId = useId()
  const collectionHintId = useId()

  return (
    <div className="flex flex-wrap items-center gap-x-space-xs sm:gap-x-space-sm gap-y-1 bg-artisan-surface border border-artisan-border px-space-md py-2.5 rounded-xl shadow-elevation-1 text-espresso-700 self-start lg:self-auto max-w-full shrink-0">
      <div className="flex items-center gap-1.5 whitespace-nowrap">
        <label
          htmlFor={selectId}
          className="font-caption text-caption uppercase text-espresso-400 font-bold"
        >
          Active Period:
        </label>
        <select
          id={selectId}
          value={periodKey}
          onChange={(event) => onPeriodChange(event.target.value)}
          className="font-mono text-data-sm text-espresso-900 font-bold bg-transparent rounded border-0 py-0 pl-1 pr-6 cursor-pointer"
        >
          {periodOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.value === periodKey ? periodLabel : option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="flex items-center gap-1.5 whitespace-nowrap">
        <span className="text-espresso-200" aria-hidden="true">
          |
        </span>
        <span className="font-caption text-caption uppercase text-espresso-400 font-bold">
          Target FC:
        </span>
        <span className="font-mono text-data-sm text-culinary-700 font-bold tabular-nums">
          {formatPercentage(targetFoodCostPercentage)}
        </span>
      </div>

      <div className="flex items-center gap-1.5 whitespace-nowrap">
        <span className="text-espresso-200" aria-hidden="true">
          |
        </span>
        <span className="font-caption text-caption uppercase text-espresso-400 font-bold">
          Collection:
        </span>
        <span
          className="font-mono text-data-sm text-espresso-900 font-bold tabular-nums"
          aria-describedby={collectionHintId}
        >
          {formatPercentage(collectionRatioPercentage)}
        </span>
        <span title={COLLECTION_HINT}>
          <Info className="h-3.5 w-3.5 text-espresso-400" aria-hidden="true" />
        </span>
        <span id={collectionHintId} className="sr-only">
          {COLLECTION_HINT}
        </span>
      </div>

      {isRefreshing ? (
        <div className="flex items-center gap-1.5 whitespace-nowrap">
          <span className="text-espresso-200" aria-hidden="true">
            |
          </span>
          <span className="font-caption text-caption uppercase text-caramel-700 font-bold">
            Refreshing…
          </span>
        </div>
      ) : null}
    </div>
  )
}
