import { CircleCheck, TriangleAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/utils/cn'
import { formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type { MenuCostingItem } from '@/types/menu.types'
import { menuTargetLabel, menuTargetTone } from '../menuStatus'

export interface DishCardProps {
  item: MenuCostingItem
  /** The card the operator has selected — it carries the active border and ring. */
  isSelected: boolean
  onSelect: (menuId: string) => void
}

/**
 * One dish of the catalogue (spec §4.5).
 *
 * Every figure is the server's. The food-cost chip is toned by the server's
 * `isWithinTarget` flag, and it always carries its text label so the state is
 * legible without colour perception (AC-M18).
 *
 * The reference's "Portion: 220g / pax" line and its "VIP Tier" chip are not
 * rendered — `Menu` carries no portion attribute and no tier concept
 * (spec §4.5).
 */
export function DishCard({ item, isSelected, onSelect }: DishCardProps) {
  const tone = menuTargetTone(item.isWithinTarget)

  return (
    <button
      type="button"
      onClick={() => onSelect(item.menuId)}
      aria-pressed={isSelected}
      aria-label={`${item.menuName}, food cost ${formatPercentage(item.foodCostPercentage)}`}
      className={cn(
        'bg-artisan-surface rounded-xl p-4 shadow-elevation-1 flex flex-col justify-between gap-3 text-left w-full min-w-0 transition-all',
        isSelected
          ? 'border-2 border-culinary-600 ring-2 ring-culinary-600/15'
          : 'border border-artisan-border hover:border-espresso-400',
      )}
    >
      <div className="space-y-2 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-1.5 flex-wrap min-w-0">
            <span className="font-data-sm text-data-sm font-bold text-espresso-900">
              {item.menuId}
            </span>
            <Badge tone="info">{item.courseName}</Badge>
          </div>
          <Badge
            tone={tone}
            icon={item.isWithinTarget ? CircleCheck : TriangleAlert}
            className="shrink-0"
          >
            {formatPercentage(item.foodCostPercentage)} · {menuTargetLabel(item.isWithinTarget)}
          </Badge>
        </div>

        <h3 className="font-headline-sm text-headline-sm text-espresso-900 leading-snug break-words">
          {item.menuName}
        </h3>
      </div>

      <div className="pt-2 border-t border-surface-container flex items-end justify-between gap-2">
        <div>
          <span className="font-caption text-caption text-espresso-400 block">
            Selling Price / Food Cost
          </span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatCurrency(item.sellingPrice)}
          </span>
          <span className="font-data-sm text-data-sm font-normal text-espresso-600">
            / {formatCurrency(item.foodCost)}
          </span>
        </div>
        <div className="text-right">
          <span className="font-caption text-caption text-espresso-400 block">Margin</span>
          <span className="font-data-md text-data-md text-culinary-700 font-bold">
            {formatCurrency(item.grossProfit)}
          </span>
          <span className="font-caption text-caption text-espresso-400 block">
            {formatPercentage(item.grossMarginPercentage)} GP
          </span>
        </div>
      </div>
    </button>
  )
}
