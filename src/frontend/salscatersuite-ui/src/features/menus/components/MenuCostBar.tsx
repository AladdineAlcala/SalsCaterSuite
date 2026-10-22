import { Save } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { EM_DASH, formatCurrency, formatPercentage, formatPercentagePoints } from '@/utils/currencyFormatter'
import type { MenuCostingPreview } from '@/types/menu.types'

export interface MenuCostBarProps {
  /** The server-computed preview (spec §6.2). */
  preview: MenuCostingPreview | undefined
  onSave: () => void
  isSaving: boolean
  /** `false` while the form cannot be submitted. */
  canSave: boolean
  /** The label of the primary action. */
  saveLabel: string
}

/**
 * The sticky cost bar (spec §5.6).
 *
 * It shows the **server's** preview figures — the food-cost percentage, the
 * gross profit per pax, the gross margin percentage and the food-cost
 * percentage against the target.
 *
 * The reference's bar is a dark surface; this bar keeps the application's
 * light design language so it stays consistent with the rest of the suite.
 *
 * The delete action is **not** offered: the destructive operation is the
 * owner's call (spec §5.6).
 */
export function MenuCostBar({
  preview,
  onSave,
  isSaving,
  canSave,
  saveLabel,
}: MenuCostBarProps) {
  const hasPreview = preview !== undefined
  const target = hasPreview ? formatPercentage(preview.targetFoodCostPercentage, 2) : EM_DASH

  return (
    <div
      data-testid="menu-cost-bar"
      className="sticky bottom-0 z-30 bg-artisan-surface border-t border-artisan-border shadow-elevation-2 -mx-gutter-mobile md:-mx-margin px-gutter-mobile md:px-margin py-space-md flex flex-col md:flex-row items-center justify-between gap-3"
    >
      <div className="flex items-center gap-4 flex-wrap">
        <div>
          <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider block">
            Food Cost %
          </span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {hasPreview ? formatPercentage(preview.foodCostPercentage, 2) : EM_DASH}
          </span>
        </div>
        <div className="hidden sm:block h-6 w-px bg-artisan-border" aria-hidden="true" />
        <div>
          <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider block">
            Gross Profit / Pax
          </span>
          <span className="font-data-md text-data-md text-culinary-700 font-bold">
            {hasPreview ? formatCurrency(preview.grossProfit) : EM_DASH}
          </span>
        </div>
        <div className="hidden sm:block h-6 w-px bg-artisan-border" aria-hidden="true" />
        <div>
          <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider block">
            Gross Margin
          </span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {hasPreview ? formatPercentage(preview.grossMarginPercentage, 2) : EM_DASH}
          </span>
        </div>
        <div className="hidden sm:block h-6 w-px bg-artisan-border" aria-hidden="true" />
        <div>
          <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider block">
            Against Target
          </span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {hasPreview
              ? `${formatPercentagePoints(preview.marginVariancePercentage)} vs ≤ ${target}`
              : EM_DASH}
          </span>
        </div>
      </div>

      <div className="flex items-center gap-3 w-full md:w-auto justify-end">
        <Button
          variant="primary"
          icon={Save}
          onClick={onSave}
          disabled={isSaving || !canSave}
        >
          {isSaving ? 'Saving…' : saveLabel}
        </Button>
      </div>
    </div>
  )
}
