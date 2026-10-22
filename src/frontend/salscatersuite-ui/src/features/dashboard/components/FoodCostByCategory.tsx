import { UtensilsCrossed } from 'lucide-react'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Badge } from '@/components/ui/Badge'
import { formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import { clampRatio, progressAriaValue } from '@/utils/progressRatio'
import type { DashboardFoodCostCategory } from '@/types/dashboard.types'

export interface FoodCostByCategoryProps {
  categories: DashboardFoodCostCategory[]
}

/**
 * Column 2 of the operations matrix — the food-cost share per course category
 * (spec §9.2 item 5, KPI-16).
 *
 * The bar width is the server's `sharePercentage`, clamped to 0–100 for
 * display. Rows with a null `courseId` are grouped by the API under
 * "Uncategorised" and are rendered like any other row.
 */
export function FoodCostByCategory({ categories }: FoodCostByCategoryProps) {
  return (
    <section className="bg-artisan-surface border border-artisan-border rounded-xl p-space-lg shadow-elevation-1 flex flex-col justify-between gap-space-md">
      <div className="flex flex-col gap-space-md">
        <div className="flex flex-wrap items-start justify-between gap-2 pb-1">
          <div className="min-w-0">
            <h3 className="font-headline-sm text-headline-sm text-espresso-900">
              Food Cost by Category
            </h3>
            <p className="font-caption text-caption text-espresso-600">
              Aggregated ingredient share of total menu cost
            </p>
          </div>
          {/*
           * The reference mockup labelled this panel "Price Volatility", but the
           * panel shows the catalogue's COST MIX per course - there is no
           * volatility, trend or price-history data in the payload. The chip
           * therefore states what the panel actually reports.
           */}
          <Badge tone="neutral">Catalogue Cost Mix</Badge>
        </div>

        {categories.length === 0 ? (
          <EmptyState
            icon={UtensilsCrossed}
            title="No menus costed yet"
            description="No menu carries a food cost, so there is no cost distribution to show."
          />
        ) : (
          <ul className="flex flex-col gap-3.5">
            {categories.map((category) => {
              const clamped = clampRatio(category.sharePercentage)
              const ariaValue = progressAriaValue(clamped)
              return (
                <li key={category.courseId ?? category.courseName} className="flex flex-col gap-1">
                  <div className="flex items-center justify-between gap-2 font-body-sm text-body-sm">
                    <span className="text-espresso-900 font-semibold truncate">
                      {category.courseName}
                    </span>
                    <span className="font-mono text-data-sm text-espresso-900 font-bold tabular-nums shrink-0">
                      {formatPercentage(category.sharePercentage)}
                    </span>
                  </div>
                  {clamped !== null && ariaValue !== null ? (
                    <div
                      className="w-full h-2 rounded-full bg-surface-container overflow-hidden"
                      role="progressbar"
                      aria-label={`Food cost share for ${category.courseName}`}
                      aria-valuenow={ariaValue}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    >
                      <div
                        className="h-full bg-caramel-600 rounded-full"
                        style={{ width: `${clamped}%` }}
                      />
                    </div>
                  ) : null}
                  <span className="font-mono text-data-sm text-espresso-600 tabular-nums">
                    {formatCurrency(category.foodCostAmount)}
                  </span>
                </li>
              )
            })}
          </ul>
        )}
      </div>

      <div className="pt-space-sm bg-artisan-subtle p-3 rounded-lg flex items-center justify-between gap-2">
        <span className="font-caption text-caption text-espresso-600">
          Calculated server-side from the menu catalogue
        </span>
      </div>
    </section>
  )
}
