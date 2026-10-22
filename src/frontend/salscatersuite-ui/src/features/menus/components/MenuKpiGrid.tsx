import { PieChart, TrendingUp, TriangleAlert, UtensilsCrossed } from 'lucide-react'
import { EM_DASH, formatCount, formatCurrency, formatPercentage, formatPercentagePoints } from '@/utils/currencyFormatter'
import type { MenuCostingSummary } from '@/types/menu.types'
import { MenuKpiCard } from './MenuKpiCard'

export interface MenuKpiGridProps {
  summary: MenuCostingSummary
}

/**
 * The four KPI cards (spec §4.2).
 *
 * Every number is read from the catalogue summary the API computed. The
 * average food cost percentage and the best-margin course are `null` when the
 * margin population is empty, and they render as an em dash — never as a zero
 * (spec §4.2, contract rule 6).
 *
 * The fourth card is **Margin Alerts**. The reference's fourth card, "Cost
 * Volatility Alert", is not implementable: the catalogue stores one current
 * food cost per dish and holds no purchase price, supplier or price history
 * (DEC-060).
 */
export function MenuKpiGrid({ summary }: MenuKpiGridProps) {
  const target = formatPercentage(summary.targetFoodCostPercentage)

  // The headroom is the server's own difference between the target and the
  // catalogue average, expressed in percentage points. A negative headroom
  // means the catalogue is over target.
  const headroom =
    summary.averageFoodCostHeadroomPercentage === null
      ? EM_DASH
      : summary.averageFoodCostHeadroomPercentage >= 0
        ? `${formatPercentagePoints(summary.averageFoodCostHeadroomPercentage)} headroom`
        : 'Over target'

  const bestMargin = summary.bestMarginCourse

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-gutter">
      {/* 1 — Total Menu Items (KPI-14 coverage) */}
      <MenuKpiCard
        icon={UtensilsCrossed}
        iconTone="neutral"
        label="Total Menu Items"
        value={formatCount(summary.totalDishes)}
        unit="Dishes"
        subLine={`${formatCount(summary.costedDishes)} Costed • ${formatCount(
          summary.uncostedDishes,
        )} Uncosted`}
        trackRatio={summary.costingCoveragePercentage}
        trackLabel="Costing coverage"
        trackTone="culinary"
      />

      {/* 2 — Average Food Cost (KPI-22) */}
      <MenuKpiCard
        icon={PieChart}
        iconTone="culinary"
        label="Average Food Cost"
        value={formatPercentage(summary.averageFoodCostPercentage)}
        subLine={headroom}
        secondaryLine={`Target ≤ ${target}`}
      />

      {/* 3 — Best-Margin Course (KPI-23) */}
      <MenuKpiCard
        icon={TrendingUp}
        iconTone="caramel"
        label="Best-Margin Course"
        value={bestMargin ? bestMargin.courseName : EM_DASH}
        subLine={
          bestMargin
            ? `${formatPercentage(bestMargin.grossMarginPercentage)} margin • ${formatCurrency(
                bestMargin.averageSellingPrice,
              )} avg`
            : EM_DASH
        }
      />

      {/* 4 — Margin Alerts (KPI-24) */}
      <MenuKpiCard
        icon={TriangleAlert}
        iconTone="caramel"
        label="Margin Alerts"
        value={formatCount(summary.marginAlertCount)}
        subLine={`dishes above the ${target} food-cost target`}
      />
    </div>
  )
}
