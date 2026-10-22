import { CircleCheck, RotateCcw, TriangleAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type { PackageCourseRule, PackageCourseRulePreview, PackageCourseSelection } from '@/types/package.types'
import { packageCeilingLabel, packageCeilingTone } from '../packageTypes'

export interface PackageSelectionSimulatorProps {
  /** The tier's rules — one picker per course. */
  rules: PackageCourseRule[]
  /** The dishes the operator has picked. */
  selections: PackageCourseSelection[]
  /** Adds a dish to a course's selection. */
  onAddDish: (courseId: number, menuId: string) => void
  /** Removes a dish from a course's selection. */
  onRemoveDish: (courseId: number, menuId: string) => void
  /** Clears the whole selection. */
  onReset: () => void
  /** The server's preview. */
  preview: PackageCourseRulePreview | undefined
  /** `true` while the preview is in flight. */
  isPreviewing: boolean
  /**
   * `true` while the typed selection has not reached the server yet, or the
   * preview is in flight.
   *
   * While it holds, **no** preview-derived figure is rendered — not the
   * previous selection's peso figures and not its ceiling verdict. The cost
   * position shows the explicit **recalculating** state instead. The dish
   * pickers and the typed selection stay visible, because they are the
   * operator's own input and not the server's answer.
   */
  isRecalculating: boolean
  /** The server's rejection of the preview request, rendered verbatim. */
  previewErrors: string[]
  /** The typed stress percentage. */
  stressPercentage: string
  onStressPercentageChange: (value: string) => void
  /** The client-side rejection of the typed stress percentage. */
  stressError?: string
  /** `true` when the tier carries no rate, so no cost figure can exist. */
  isUnpriced: boolean
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-data-md text-data-md w-full'

/**
 * The selection simulator (spec §5.2, column 3).
 *
 * The dish picker, the calculated dish cost per pax, the effective food-cost
 * percentage, the cost-ceiling position and the stress preview are **all
 * server-computed**: the browser sends the selection and renders what comes
 * back. It never sums a dish's food cost, never divides by the per-guest rate
 * and never multiplies by a stress factor (DEC-002).
 *
 * A selected dish that carries no food cost makes the selected food cost
 * **unknown** — the screen says so instead of showing a misleading zero
 * (DEC-087).
 *
 * **Why a pending preview is never shown as the current one.** The cost
 * position and the ceiling verdict answer the question this screen exists for:
 * *is this client's choice valid?* While the operator's new selection is on its
 * way to the server, the previous selection's peso figures and its "quota
 * satisfied" verdict are still in the browser's memory. Rendering them would
 * let a coordinator read a verdict for the choice they have **already
 * changed** — and a breach is exactly what they need to see. So while the
 * preview is pending the block shows the explicit **recalculating** state
 * instead, and the previous figures are not rendered at all. The dish pickers
 * and the typed selection stay visible: they are the operator's own input, not
 * the server's answer. The location workbench withholds its figures in exactly
 * the same window and for exactly the same reason.
 */
export function PackageSelectionSimulator({
  rules,
  selections,
  onAddDish,
  onRemoveDish,
  onReset,
  preview,
  isPreviewing,
  isRecalculating,
  previewErrors,
  stressPercentage,
  onStressPercentageChange,
  stressError,
  isUnpriced,
}: PackageSelectionSimulatorProps) {
  // The server's view of the selection is authoritative — it echoes the
  // dishes it resolved.
  const hasSelection = preview !== undefined ? preview.selections.length > 0 : selections.length > 0
  const costIsUnknown = preview !== undefined && !preview.isCostKnown

  return (
    <Card
      elevation={1}
      className="flex flex-col gap-space-md"
      data-testid="package-selection-simulator"
    >
      <div className="flex items-center justify-between gap-2 pb-1 flex-wrap">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Selection Simulator
        </h2>
        <div className="flex items-center gap-2">
          <Badge tone="info" shape="pill">
            Server preview
          </Badge>
          <Button
            variant="tertiary"
            size="sm"
            icon={RotateCcw}
            onClick={onReset}
            disabled={!hasSelection}
          >
            Reset selection
          </Button>
        </div>
      </div>

      {rules.length === 0 ? (
        <EmptyState
          title="This tier has no course rule yet"
          description="Add a course rule to give the client something to choose from. Nothing is simulated until a rule exists."
        />
      ) : (
        <div className="flex flex-col gap-3">
          {rules.map((rule) => {
            const chosen = selections.filter(
              (selection) => selection.courseId === rule.courseId,
            )
            const remaining = rule.pool.filter(
              (dish) => !chosen.some((selection) => selection.menuId === dish.menuId),
            )
            const selectId = `package-selection-${rule.courseId}`

            return (
              <div key={rule.courseId} className="flex flex-col gap-1.5">
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <label htmlFor={selectId} className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider">
                    Dish for {rule.courseName}
                  </label>
                  <span className="font-caption text-caption text-espresso-400">
                    Quota {rule.requiredQuota} · {chosen.length} selected
                  </span>
                </div>

                {rule.pool.length === 0 ? (
                  <p className="font-body-sm text-body-sm text-espresso-600">
                    No dish is qualified for this course, so nothing can be selected from it.
                  </p>
                ) : (
                  <>
                    <select
                      id={selectId}
                      name={`course-${rule.courseId}`}
                      value=""
                      onChange={(event) => {
                        if (event.target.value.length > 0) {
                          onAddDish(rule.courseId, event.target.value)
                        }
                      }}
                      aria-describedby={`${selectId}-hint`}
                      className={INPUT_CLASSES}
                    >
                      <option value="">Choose a qualified dish</option>
                      {remaining.map((dish) => (
                        <option key={dish.menuId} value={dish.menuId}>
                          {dish.menuName}
                        </option>
                      ))}
                    </select>
                    <p id={`${selectId}-hint`} className="font-caption text-caption text-espresso-600">
                      The qualified pool of this course — {rule.poolSize} dishes,{' '}
                      {rule.poolCostedCount} of them costed.
                    </p>
                  </>
                )}

                {chosen.length > 0 ? (
                  <ul className="flex flex-col gap-1">
                    {chosen.map((selection) => {
                      const dish = rule.pool.find((entry) => entry.menuId === selection.menuId)
                      const name = dish?.menuName ?? selection.menuId
                      return (
                        <li
                          key={selection.menuId}
                          className="flex items-center justify-between gap-2 rounded border border-artisan-border bg-surface-container-low px-2 py-1"
                        >
                          <span className="font-body-sm text-body-sm text-espresso-700 break-words">
                            {name}
                          </span>
                          <button
                            type="button"
                            onClick={() => onRemoveDish(rule.courseId, selection.menuId)}
                            aria-label={`Remove ${name} from ${rule.courseName}`}
                            className="font-caption text-caption text-espresso-600 underline hover:text-espresso-900 shrink-0"
                          >
                            Remove
                          </button>
                        </li>
                      )
                    })}
                  </ul>
                ) : null}
              </div>
            )
          })}
        </div>
      )}

      {previewErrors.length > 0 ? (
        <div role="alert" className="flex flex-col gap-1">
          {previewErrors.map((message) => (
            <p key={message} className="font-body-sm text-body-sm text-error break-words">
              {message}
            </p>
          ))}
        </div>
      ) : null}

      {/* The cost position — every figure is the server's. */}
      {isRecalculating ? (
        /*
         * The pending state. The previous selection's cost position is **not**
         * shown: a verdict or a peso figure priced for a selection the operator
         * has already changed would be read as the current one. This is the
         * same affordance the location matrix uses for its money column.
         */
        <div className="flex flex-col gap-2 rounded-lg bg-surface-container-low p-3">
          <p className="font-caption text-caption text-espresso-500">Recalculating…</p>
        </div>
      ) : (
        <div className="flex flex-col gap-2 rounded-lg bg-surface-container-low p-3">
          <div className="flex items-center justify-between gap-2">
            <span className="font-body-sm text-body-sm text-espresso-700">
              Dish cost per pax
            </span>
            <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
              {preview ? formatCurrency(preview.selectedFoodCostPerPax) : EM_DASH}
            </span>
          </div>

          {!hasSelection ? (
            <p className="font-caption text-caption text-espresso-600">
              No dish is selected yet, so the selection has no cost. The server publishes the
              standard run as the food cost of a proposal without a selection.
            </p>
          ) : null}

          {hasSelection && costIsUnknown ? (
            <p className="font-caption text-caption text-caramel-700">
              The food cost of this selection is unknown — one of the selected dishes carries
              no food cost, so no cost figure can be published. The quota verdict still
              counts the dish.
            </p>
          ) : null}

          <div className="flex items-center justify-between gap-2">
            <span className="font-body-sm text-body-sm text-espresso-700">
              Effective food cost
            </span>
            <span className="font-data-md text-data-md text-espresso-900 font-bold">
              {preview ? formatPercentage(preview.effectiveFoodCostPercentage, 2) : EM_DASH}
            </span>
          </div>

          <div className="flex items-center justify-between gap-2">
            <span className="font-body-sm text-body-sm text-espresso-700">Cost ceiling per pax</span>
            <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
              {preview ? formatCurrency(preview.costCeilingPerPax) : EM_DASH}
            </span>
          </div>

          <div className="flex items-center justify-between gap-2">
            <span className="font-body-sm text-body-sm text-espresso-700">
              Shortfall over the ceiling
            </span>
            <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
              {preview ? formatCurrency(preview.ceilingShortfallPerPax) : EM_DASH}
            </span>
          </div>

          <div className="flex items-center justify-between gap-2">
            <span className="font-body-sm text-body-sm text-espresso-700">
              Percentage over the ceiling
            </span>
            <span className="font-data-md text-data-md text-espresso-900 font-bold">
              {preview ? formatPercentage(preview.ceilingOveragePercentage, 2) : EM_DASH}
            </span>
          </div>

          <div className="flex items-center justify-between gap-2 pt-1 border-t border-artisan-border">
            <span className="font-body-sm text-body-sm text-espresso-700">Cost ceiling position</span>
            {preview ? (
              <Badge
                tone={packageCeilingTone(preview.isCeilingBreached)}
                icon={preview.isCeilingBreached ? TriangleAlert : CircleCheck}
              >
                {packageCeilingLabel(preview.isCeilingBreached)}
              </Badge>
            ) : null}
          </div>

          {preview?.isCeilingBreached ? (
            <p className="font-caption text-caption text-caramel-700">
              The selection breaches the cost ceiling. This is a warning: no price is changed
              and the selection is not refused — it is simply marked non-compliant.
            </p>
          ) : null}
        </div>
      )}

      {/* The stress preview — a what-if over the selected dishes' food cost. */}
      <div className="flex flex-col gap-1">
        <label
          htmlFor="package-stress-percentage"
          className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
        >
          Stress preview (percent)
        </label>
        <input
          id="package-stress-percentage"
          name="stressPercentage"
          type="number"
          inputMode="decimal"
          step="any"
          min="0"
          value={stressPercentage}
          onChange={(event) => onStressPercentageChange(event.target.value)}
          aria-invalid={stressError ? true : undefined}
          aria-describedby={
            stressError ? 'package-stress-percentage-error' : 'package-stress-percentage-hint'
          }
          className={INPUT_CLASSES}
        />
        <p id="package-stress-percentage-hint" className="font-caption text-caption text-espresso-600">
          A hypothetical increase applied to the selected dishes&apos; food cost. It changes
          nothing that is stored.
        </p>
        {stressError ? (
          <p
            id="package-stress-percentage-error"
            role="alert"
            className="font-caption text-caption text-error"
          >
            {stressError}
          </p>
        ) : null}
        {isRecalculating ? (
          <p className="font-caption text-caption text-espresso-500">Recalculating…</p>
        ) : (
          <>
            <div className="flex items-center justify-between gap-2">
              <span className="font-body-sm text-body-sm text-espresso-700">
                Stressed cost per pax
              </span>
              <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
                {preview ? formatCurrency(preview.stressedFoodCostPerPax) : EM_DASH}
              </span>
            </div>
            <div className="flex items-center justify-between gap-2">
              <span className="font-body-sm text-body-sm text-espresso-700">
                Stressed food cost
              </span>
              <span className="font-data-md text-data-md text-espresso-900 font-bold">
                {preview ? formatPercentage(preview.stressedFoodCostPercentage, 2) : EM_DASH}
              </span>
            </div>
          </>
        )}
      </div>

      {isUnpriced ? (
        <p className="font-caption text-caption text-espresso-600">
          This tier carries no per-guest rate, so no cost figure and no ceiling can be
          produced. The quota verdict still applies.
        </p>
      ) : null}

      {isPreviewing ? (
        <p className="font-caption text-caption text-espresso-600">Recalculating the preview…</p>
      ) : null}
    </Card>
  )
}
