import { Layers, TrendingUp, Users, Wallet } from 'lucide-react'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { PackageCatalogueSummary } from '@/types/package.types'
import { PAX_UNIT } from '../packageTypes'
import { PackageKpiCard } from './PackageKpiCard'

export interface PackageKpiGridProps {
  summary: PackageCatalogueSummary
}

/**
 * The four KPI cards (spec §5.2 item 2, KPI-25 .. KPI-28).
 *
 * Every figure is read from the aggregate the API computed — the browser never
 * sums the tier list to produce an average, a range or a headcount floor
 * (DEC-002, DEC-068).
 *
 * A `null` figure means *unknown*, not zero, so it renders as an em dash
 * (contract rule 6).
 */
export function PackageKpiGrid({ summary }: PackageKpiGridProps) {
  /*
   * The active share is a **count** ratio, not a monetary aggregate: it
   * compares two counts the server published. The rate average, the rate
   * range and the headcount floor all come from the server unchanged.
   */
  const activeShare =
    summary.totalPackages > 0 ? (summary.activePackages / summary.totalPackages) * 100 : null

  const hasRateRange =
    summary.lowestAmountPax !== null && summary.highestAmountPax !== null
  const rateRange = hasRateRange
    ? `${formatCurrency(summary.lowestAmountPax)} to ${formatCurrency(summary.highestAmountPax)}`
    : EM_DASH

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-gutter">
      {/* 1 — Configured Tiers (KPI-25) */}
      <PackageKpiCard
        icon={Layers}
        iconTone="neutral"
        label="Configured Tiers"
        value={formatCount(summary.totalPackages)}
        unit="Tiers"
        subLine={`${formatCount(summary.activePackages)} active / ${formatCount(
          summary.inactivePackages,
        )} inactive`}
        trackRatio={activeShare}
        trackLabel="Active share"
        trackTone="culinary"
      />

      {/* 2 - Average Per-Pax Rate (KPI-26) */}
      <PackageKpiCard
        icon={Wallet}
        iconTone="culinary"
        label="Average Per-Pax Rate"
        value={formatCurrency(summary.averageAmountPax)}
        subLine={`Across ${formatCount(summary.pricedPackages)} priced tiers`}
      />

      {/* 3 - Per-Pax Rate Range (KPI-27) */}
      <PackageKpiCard
        icon={TrendingUp}
        iconTone="caramel"
        label="Per-Pax Rate Range"
        value={rateRange}
        subLine="The spread of the per-guest rates across the catalogue"
      />

      {/* 4 - Minimum Headcount (KPI-28) */}
      <PackageKpiCard
        icon={Users}
        iconTone="neutral"
        label="Minimum Headcount"
        value={formatCount(summary.lowestMinPax)}
        unit={summary.lowestMinPax === null ? undefined : PAX_UNIT}
        subLine={
          summary.highestMinPax === null
            ? 'No tier sets a minimum headcount'
            : `Highest floor: ${formatCount(summary.highestMinPax)} ${PAX_UNIT}`
        }
      />
    </div>
  )
}
