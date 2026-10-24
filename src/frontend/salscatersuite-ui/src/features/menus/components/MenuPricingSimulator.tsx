import { CircleCheck, TriangleAlert } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { CurrencyInput } from '@/components/ui/CurrencyInput'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { EM_DASH, formatCurrency, formatPercentage, formatPercentagePoints } from '@/utils/currencyFormatter'
import type { MenuCostingPreview } from '@/types/menu.types'
import { menuTargetLabel, menuTargetTone } from '../menuStatus'

export interface MenuPricingSimulatorProps {
  price: string
  onPriceChange: (value: string) => void
  foodCost: string
  onFoodCostChange: (value: string) => void
  /** The server-computed preview (spec §6.2). */
  preview: MenuCostingPreview | undefined
  isPreviewing: boolean
  /** The server's rejection of the preview request. */
  previewError: string | null
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-data-md text-data-md w-full'

/**
 * The Pricing & Margin Simulator (spec §5.3).
 *
 * The live margin preview is **server-computed**: the amounts travel to
 * `POST /api/menus/costing/preview` and the figures that come back are
 * rendered. The browser never derives a food-cost percentage, a gross profit
 * or a variance (DEC-002, AC-M15).
 *
 * The reference's **Simulate +10% Ingredients** action is not implemented — it
 * presupposes an ingredient cost base that does not exist (spec §5.3).
 */
export function MenuPricingSimulator({
  price,
  onPriceChange,
  foodCost,
  onFoodCostChange,
  preview,
  isPreviewing,
  previewError,
}: MenuPricingSimulatorProps) {
  const hasPreview = preview !== undefined
  const sellingPriceIsZero = hasPreview && preview.sellingPrice === 0

  return (
    <Card elevation={1} className="flex flex-col gap-space-md">
      <div className="flex items-center justify-between gap-2 pb-1">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Pricing &amp; Margin Simulator
        </h2>
        <span className="font-data-sm text-data-sm text-culinary-800 font-bold bg-culinary-100 px-2 py-0.5 rounded">
          Server Preview
        </span>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-1">
            <label
              htmlFor="menu-price"
              className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
            >
              Selling Price per Pax
            </label>
            <span className="text-error text-caption" aria-hidden="true">
              *
            </span>
          </div>
          <div className="relative flex items-center">
            <CurrencyInput
              id="menu-price"
              name="price"
              value={price}
              onValueChange={onPriceChange}
              required
              aria-required="true"
              className={INPUT_CLASSES}
            />
          </div>
        </div>

        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-1">
            <label
              htmlFor="menu-food-cost"
              className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
            >
              Food Cost
            </label>
            <span className="text-error text-caption" aria-hidden="true">
              *
            </span>
          </div>
          <div className="relative flex items-center">
            <CurrencyInput
              id="menu-food-cost"
              name="foodCost"
              value={foodCost}
              onValueChange={onFoodCostChange}
              required
              aria-required="true"
              className={INPUT_CLASSES}
            />
          </div>
        </div>
      </div>

      {/*
        The benchmark travels with the preview response. The client never
        hardcodes it, so it is only shown once the server has published it.
      */}
      <div className="flex items-center justify-between gap-2 p-3 rounded-lg bg-surface-container-low">
        <span className="font-body-sm text-body-sm text-espresso-700">Target Food Cost Ceiling</span>
        <span className="font-data-md text-data-md text-espresso-900 font-bold">
          {hasPreview
            ? `≤ ${formatPercentage(preview.targetFoodCostPercentage, 2)}`
            : EM_DASH}
        </span>
      </div>

      {previewError ? (
        <p role="alert" className="font-body-sm text-body-sm text-error">
          {previewError}
        </p>
      ) : null}

      <div className="space-y-2" data-testid="menu-margin-preview">
        <div className="flex items-center justify-between gap-2 font-caption text-caption">
          <span className="font-bold text-espresso-700">Cost &amp; Profit Apportionment</span>
          <span className="text-espresso-600">
            {isPreviewing ? 'Calculating…' : 'Server preview'}
          </span>
        </div>

        {hasPreview ? (
          <>
            <div className="h-3 w-full rounded-full bg-surface-container flex overflow-hidden">
              <div
                className="bg-caramel-500 h-full"
                style={{ width: `${preview.foodCostPercentage}%` }}
                title={`Food Cost ${formatPercentage(preview.foodCostPercentage, 2)}`}
              />
              <div
                className="bg-culinary-600 h-full"
                style={{ width: `${preview.grossMarginPercentage}%` }}
                title={`Gross Margin ${formatPercentage(preview.grossMarginPercentage, 2)}`}
              />
            </div>
            <div className="grid grid-cols-2 gap-2 font-caption text-caption">
              <div className="flex flex-col">
                <span className="flex items-center gap-1">
                  <span className="w-2 h-2 rounded-full bg-caramel-500" aria-hidden="true" />
                  <span className="text-espresso-600">Food Cost</span>
                </span>
                <strong className="font-data-sm text-data-sm text-espresso-900">
                  {formatCurrency(preview.foodCost)} (
                  {formatPercentage(preview.foodCostPercentage, 2)})
                </strong>
              </div>
              <div className="flex flex-col text-right">
                <span className="flex items-center gap-1 justify-end">
                  <span className="w-2 h-2 rounded-full bg-culinary-600" aria-hidden="true" />
                  <span className="text-espresso-600">Gross Margin</span>
                </span>
                <strong className="font-data-sm text-data-sm text-culinary-700">
                  {formatCurrency(preview.grossProfit)} (
                  {formatPercentage(preview.grossMarginPercentage, 2)})
                </strong>
              </div>
            </div>
            <div className="flex flex-wrap items-center justify-between gap-2 pt-1 border-t border-surface-container">
              <span className="font-caption text-caption text-espresso-600">
                Gross Profit {formatCurrency(preview.grossProfit)} • Variance{' '}
                {formatPercentagePoints(preview.marginVariancePercentage)}
              </span>
              <Badge
                tone={menuTargetTone(preview.isWithinTarget)}
                icon={preview.isWithinTarget ? CircleCheck : TriangleAlert}
              >
                {menuTargetLabel(preview.isWithinTarget)}
              </Badge>
            </div>
            {sellingPriceIsZero ? (
              <p className="font-caption text-caption text-espresso-600">
                A zero selling price yields no margin — the percentages above are the
                server&apos;s zero figures, not a margin.
              </p>
            ) : null}
          </>
        ) : (
          <p className="font-body-sm text-body-sm text-espresso-600">
            Enter a selling price and a food cost to see the server&apos;s margin preview.
          </p>
        )}
      </div>

      <CapabilityGapNotice
        title="Recommended package assignment unavailable"
        missing="There is no package-assignment rule and no package catalogue rule for a dish to satisfy, so no tier can be recommended."
        requirement="REQ-MNU-03"
      />
    </Card>
  )
}
