import { Gauge } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { EM_DASH, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type { PackageCourseRulePreview, PackageCourseRuleset } from '@/types/package.types'
import { PLANNING_ESTIMATE_NOTE } from '../packageTypes'

export interface PackageForecastPanelProps {
  /** The tier's ruleset — the planning figures are the server's. */
  ruleset: PackageCourseRuleset
  /** The server's preview — the headcount figures come from it. */
  preview: PackageCourseRulePreview | undefined
  /** `true` while the preview is in flight. */
  isPreviewing: boolean
  /**
   * `true` while the typed selection has not reached the server yet, or the
   * preview is in flight.
   *
   * While it holds, the contract at the typed headcount is **not** rendered:
   * the figure belongs to the selection the server has priced, not to the one
   * the operator has just changed. The row shows the explicit
   * **recalculating** state instead. The benchmark, the target margin, the
   * standard-run food cost and the base gross profit are **ruleset** figures
   * and stay visible.
   */
  isRecalculating: boolean
  /** The typed headcount. */
  pax: string
  onPaxChange: (value: string) => void
  /** The client-side rejection of the typed headcount. */
  paxError?: string
  /** `true` when the tier carries no rate, so no money figure can exist. */
  isUnpriced: boolean
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-data-md text-data-md w-full'

/**
 * The planning forecast (spec §5.2, column 1).
 *
 * The benchmark, the standard-run food cost and the base gross profit are the
 * server's planning figures; the contract at the typed headcount comes from
 * the preview endpoint. The browser never multiplies a rate by a headcount
 * (DEC-002, AC-P2-28).
 *
 * The standard run is labelled a **planning estimate, not a commitment**
 * wherever it appears (DEC-083).
 *
 * **Why a pending preview is never shown as the current one.** The contract at
 * the typed headcount is priced against the server's picture of the selection,
 * so while the operator's new selection is on its way to the server the
 * figure in the browser belongs to the selection they have **already
 * changed**. It is withheld and the row shows the explicit **recalculating**
 * state instead. The planning figures around it — the benchmark, the target
 * margin, the standard-run food cost and the base gross profit — come from
 * the **ruleset**, not from the preview, and they stay visible.
 */
export function PackageForecastPanel({
  ruleset,
  preview,
  isPreviewing,
  isRecalculating,
  pax,
  onPaxChange,
  paxError,
  isUnpriced,
}: PackageForecastPanelProps) {
  return (
    <Card elevation={1} className="flex flex-col gap-space-md" data-testid="package-forecast-panel">
      <div className="flex items-center justify-between gap-2 pb-1">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Planning Forecast
        </h2>
        <Badge tone="info" shape="pill">
          Server figures
        </Badge>
      </div>

      <dl className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Food cost benchmark
          </dt>
          <dd className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatPercentage(ruleset.targetFoodCostPercentage, 2)}
          </dd>
          {ruleset.isBenchmarkConfigured ? null : (
            <dd className="font-caption text-caption text-espresso-600">
              The deployment has no configured benchmark.
            </dd>
          )}
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Target margin
          </dt>
          <dd className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatPercentage(ruleset.targetMarginPercentage, 2)}
          </dd>
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Standard run food cost per pax
          </dt>
          <dd className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            {formatCurrency(ruleset.standardRunFoodCostPerPax)}
          </dd>
          <dd className="font-caption text-caption text-espresso-600">
            {formatPercentage(ruleset.standardRunFoodCostPercentage, 2)} of the per-guest rate
          </dd>
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Base gross profit
          </dt>
          <dd className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatPercentage(ruleset.baseGrossProfitPercentage, 2)}
          </dd>
        </div>
      </dl>

      <p className="font-caption text-caption text-espresso-600 flex items-start gap-1.5">
        <Gauge className="h-3.5 w-3.5 shrink-0 mt-0.5" aria-hidden="true" />
        {PLANNING_ESTIMATE_NOTE}
      </p>

      <div className="flex flex-col gap-1">
        <label
          htmlFor="package-forecast-pax"
          className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
        >
          Headcount
        </label>
        <input
          id="package-forecast-pax"
          name="pax"
          type="number"
          inputMode="numeric"
          step="1"
          min="1"
          value={pax}
          onChange={(event) => onPaxChange(event.target.value)}
          aria-describedby={paxError ? 'package-forecast-pax-error' : 'package-forecast-pax-hint'}
          aria-invalid={isUnpriced || paxError ? true : undefined}
          className={INPUT_CLASSES}
        />
        <p id="package-forecast-pax-hint" className="font-caption text-caption text-espresso-600">
          {isUnpriced
            ? 'This tier carries no per-guest rate, so no contract figure can be produced.'
            : 'Leave empty to use the tier’s own headcount floor.'}
        </p>
        {paxError ? (
          <p id="package-forecast-pax-error" role="alert" className="font-caption text-caption text-error">
            {paxError}
          </p>
        ) : null}
      </div>

      <div className="flex items-center justify-between gap-2 rounded-lg bg-surface-container-low p-3">
        <span className="font-body-sm text-body-sm text-espresso-700">Contract at that headcount</span>
        <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
          {isRecalculating ? (
            <span className="font-caption text-caption text-espresso-500">Recalculating…</span>
          ) : preview ? (
            formatCurrency(preview.grossRevenue)
          ) : (
            EM_DASH
          )}
        </span>
      </div>

      {isPreviewing ? (
        <p className="font-caption text-caption text-espresso-600">Recalculating…</p>
      ) : null}
    </Card>
  )
}
