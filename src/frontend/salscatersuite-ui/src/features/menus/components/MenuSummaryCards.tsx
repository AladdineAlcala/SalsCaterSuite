import type { ReactNode } from 'react'
import { CircleCheck, TriangleAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { EM_DASH, formatCurrency, formatPercentage, formatPercentagePoints } from '@/utils/currencyFormatter'
import type { MenuCostingPreview } from '@/types/menu.types'
import { menuTargetLabel, menuTargetTone } from '../menuStatus'

export interface MenuSummaryCardsProps {
  /** The server-computed preview (spec §6.2). */
  preview: MenuCostingPreview | undefined
  isPreviewing: boolean
}

interface SummaryCardProps {
  label: string
  value: string
  caption?: string
  /** A chip rendered under the caption, e.g. the benchmark's compliance state. */
  badge?: ReactNode
}

/** One summary tile of the recipe form (spec §5.4). */
function SummaryCard({ label, value, caption, badge }: SummaryCardProps) {
  return (
    <Card elevation={1} className="p-space-md flex flex-col gap-1 min-w-0">
      <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider font-semibold">
        {label}
      </span>
      <span className="font-data-lg text-data-lg text-espresso-900 tabular-nums truncate">
        {value}
      </span>
      {caption ? (
        <span className="font-caption text-caption text-espresso-600">{caption}</span>
      ) : null}
      {badge}
    </Card>
  )
}

/**
 * The summary cards (spec §5.4).
 *
 * The benchmark target, the selling price, the food cost, the gross profit
 * and the food-cost percentage against the target all come from the preview
 * response, which carries the server's benchmark (DEC-040).
 */
export function MenuSummaryCards({ preview, isPreviewing }: MenuSummaryCardsProps) {
  const hasPreview = preview !== undefined
  const target = hasPreview ? formatPercentage(preview.targetFoodCostPercentage, 2) : EM_DASH

  return (
    <section className="flex flex-col gap-space-sm" data-testid="menu-summary-cards">
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Recipe Summary
        </h2>
        {isPreviewing ? (
          <span className="font-caption text-caption text-caramel-700 font-semibold">
            Calculating…
          </span>
        ) : null}
      </div>

      <div className="grid grid-cols-2 xl:grid-cols-5 gap-gutter">
        <SummaryCard
          label="Benchmark Target"
          value={target}
          badge={
            hasPreview ? (
              <Badge
                tone={menuTargetTone(preview.isWithinTarget)}
                icon={preview.isWithinTarget ? CircleCheck : TriangleAlert}
              >
                {menuTargetLabel(preview.isWithinTarget)}
              </Badge>
            ) : undefined
          }
        />
        <SummaryCard
          label="Selling Price"
          value={hasPreview ? formatCurrency(preview.sellingPrice) : EM_DASH}
          caption="per pax"
        />
        <SummaryCard
          label="Food Cost"
          value={hasPreview ? formatCurrency(preview.foodCost) : EM_DASH}
          caption="per pax"
        />
        <SummaryCard
          label="Gross Profit"
          value={hasPreview ? formatCurrency(preview.grossProfit) : EM_DASH}
          caption={
            hasPreview
              ? `${formatPercentage(preview.grossMarginPercentage, 2)} gross margin`
              : undefined
          }
        />
        <SummaryCard
          label="Food Cost %"
          value={hasPreview ? formatPercentage(preview.foodCostPercentage, 2) : EM_DASH}
          caption={
            hasPreview
              ? `Target ≤ ${target} • ${formatPercentagePoints(preview.marginVariancePercentage)}`
              : undefined
          }
        />
      </div>
    </section>
  )
}
