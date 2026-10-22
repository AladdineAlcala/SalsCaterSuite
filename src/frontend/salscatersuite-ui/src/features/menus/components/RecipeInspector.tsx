import { Pencil } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import { EmptyState } from '@/components/feedback/EmptyState'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { EM_DASH, formatCurrency, formatPercentage, formatPercentagePoints } from '@/utils/currencyFormatter'
import type { MenuCostingItem } from '@/types/menu.types'
import { menuTargetLabel } from '../menuStatus'

export interface RecipeInspectorProps {
  /** The dish the operator selected, or `null` when nothing is selected. */
  item: MenuCostingItem | null
  onEdit: (menuId: string) => void
}

/**
 * The recipe inspector (spec §4.6).
 *
 * It shows the dish's authoritative costing — the unit cost, the selling
 * price, the food-cost percentage, the gross margin, the progress track and
 * the variance against the server's benchmark — and the dish's own record.
 *
 * The reference's **Print** action is not implemented: there is no dish
 * print/BEO document (the existing `KitchenBeoDocument` is a booking BEO).
 */
export function RecipeInspector({ item, onEdit }: RecipeInspectorProps) {
  if (!item) {
    return (
      <Card elevation={1} className="flex flex-col gap-space-md">
        <EmptyState
          title="No dish selected"
          description="Select a dish from the catalogue to inspect its costing."
        />
      </Card>
    )
  }

  return (
    <Card
      data-testid="menu-recipe-inspector"
      elevation={1}
      className="flex flex-col gap-space-md"
    >
      {/* Inspector header */}
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex items-center gap-2 flex-wrap">
            <Badge tone="success" shape="pill">
              Recipe Inspector
            </Badge>
            <span className="font-data-sm text-data-sm text-espresso-400">{item.menuId}</span>
          </div>
          <h2 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight leading-tight break-words">
            {item.menuName}
          </h2>
        </div>
        <div className="text-right shrink-0">
          <span className="font-caption text-caption text-espresso-400 uppercase block">
            Unit Cost
          </span>
          <span className="font-data-lg text-data-lg text-espresso-900 font-bold">
            {formatCurrency(item.foodCost)}
          </span>
          <span className="font-caption text-caption text-espresso-600 block">
            Selling {formatCurrency(item.sellingPrice)}
          </span>
        </div>
      </div>

      {/* Portion price allocation */}
      <div className="bg-surface-container-low rounded-xl p-4 space-y-3">
        <div className="flex items-center justify-between gap-2 font-body-sm text-body-sm">
          <span className="text-espresso-700 font-medium">Portion Price Allocation:</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatCurrency(item.sellingPrice)} / pax
          </span>
        </div>

        <div className="space-y-1">
          <div className="flex justify-between gap-2 font-caption text-caption flex-wrap">
            <span className="text-caramel-700 font-bold">
              Food Cost: {formatPercentage(item.foodCostPercentage)} (
              {formatCurrency(item.foodCost)})
            </span>
            <span className="text-culinary-700 font-bold">
              Gross Margin: {formatPercentage(item.grossMarginPercentage)} (
              {formatCurrency(item.grossProfit)})
            </span>
          </div>
          <ProgressTrack
            ratio={item.foodCostPercentage}
            label="Food cost against the benchmark target"
            tone={item.isWithinTarget ? 'culinary' : 'caramel'}
          />
          <div className="flex justify-between gap-2 font-caption text-caption text-espresso-400 pt-0.5 flex-wrap">
            <span>
              Target ≤ {formatPercentage(item.targetFoodCostPercentage)} (
              {formatPercentagePoints(item.marginVariancePercentage)} variance)
            </span>
            <span>{menuTargetLabel(item.isWithinTarget)}</span>
          </div>
        </div>
      </div>

      {/* The dish's own record */}
      <div className="space-y-2">
        <span className="font-eyebrow text-eyebrow uppercase text-espresso-700 block">
          Dish Record
        </span>
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="flex flex-col gap-0.5">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Course
            </dt>
            <dd className="font-body-sm text-body-sm text-espresso-900">{item.courseName}</dd>
          </div>
          <div className="flex flex-col gap-0.5">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Plating Photo
            </dt>
            <dd className="font-body-sm text-body-sm text-espresso-900">
              {item.image ? (
                <img
                  src={item.image}
                  alt={item.menuName}
                  className="h-24 w-full object-cover rounded-lg border border-artisan-border"
                />
              ) : (
                EM_DASH
              )}
            </dd>
          </div>
          <div className="flex flex-col gap-0.5 sm:col-span-2">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Note
            </dt>
            <dd className="font-body-sm text-body-sm text-espresso-900">
              {item.note && item.note.length > 0 ? item.note : EM_DASH}
            </dd>
          </div>
        </dl>
      </div>

      {/* Actions */}
      <div className="flex flex-col gap-2">
        <Button variant="primary" icon={Pencil} onClick={() => onEdit(item.menuId)}>
          Edit Recipe
        </Button>
      </div>

      {/* The capability gaps the reference fills with the recipe engine (AC-M13). */}
      <CapabilityGapNotice
        title="Ingredient & batch breakdown unavailable"
        missing="The approved data model carries a single manually entered food cost per dish. There is no ingredient, SKU, supplier, purchase price or batch concept, so no 100-pax breakdown can be produced."
        requirement="REQ-MNU-03"
      />
      <CapabilityGapNotice
        title="Yield calibration unavailable"
        missing="Trimming loss, cooking shrinkage and buffet buffer are not stored anywhere, so no yield calibration can be shown."
        requirement="REQ-MNU-03"
      />
      <CapabilityGapNotice
        title="Plating specification unavailable"
        missing="There is no plating standard, kitchen station, critical-control-point or allergen record for a dish."
        requirement="REQ-MNU-03"
      />
    </Card>
  )
}
