import { Link } from 'react-router-dom'
import { CalendarCheck, Hourglass, TrendingUp, UtensilsCrossed } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import {
  EM_DASH,
  formatCount,
  formatCurrency,
  formatPercentage,
  formatPercentagePoints,
} from '@/utils/currencyFormatter'
import { benchmarkPercentage, ratioPercentage } from '@/utils/progressRatio'
import type { DashboardDto } from '@/types/dashboard.types'
import { KpiCard } from './KpiCard'

export interface KpiGridProps {
  data: DashboardDto
}

/**
 * The four KPI cards (spec §9.2 item 3).
 *
 * Every number below is read from the API payload — nothing is hardcoded and
 * no financial total is derived in the browser (DEC-002).
 */
export function KpiGrid({ data }: KpiGridProps) {
  const { bookings, benchmarks, dishCosting, margin, receivables } = data

  const committedShare = ratioPercentage(bookings.committedCount, bookings.totalCount)
  const marginAgainstBenchmark = benchmarkPercentage(
    margin.catalogueMarginPercentage,
    benchmarks.targetMarginPercentage,
  )
  const overdueShare = ratioPercentage(receivables.overdueAmount, receivables.outstandingAmount)

  const bookingSupport = (() => {
    const parts = [
      `${formatCount(bookings.committedCount)} Committed`,
      `${formatCount(bookings.tentativeCount)} Tentative`,
    ]
    if (bookings.otherCount) {
      parts.push(`${formatCount(bookings.otherCount)} Other`)
    }
    return parts.join(', ')
  })()

  const benchmarkSupport =
    benchmarks.targetMarginPercentage === null
      ? 'Benchmark Target: Not configured'
      : `Benchmark Target: ${formatPercentage(benchmarks.targetMarginPercentage)}`

  const marginFoot = (() => {
    if (!benchmarks.isConfigured) {
      return (
        <span className="font-caption text-caption text-caramel-700 font-bold">
          No benchmark configured
        </span>
      )
    }
    if (margin.varianceToBenchmarkPercentage === null) {
      return (
        <span className="font-caption text-caption text-espresso-600 font-bold">
          Variance {EM_DASH}
        </span>
      )
    }
    // A margin BELOW the benchmark is a variance the business must act on, so it
    // is rendered in the amber "action required" tone, not the success tone.
    // Status is never conveyed by colour alone: the sign is always shown.
    const belowBenchmark = margin.varianceToBenchmarkPercentage < 0
    return (
      <span
        className={`font-caption text-caption font-bold ${
          belowBenchmark ? 'text-caramel-700' : 'text-culinary-700'
        }`}
      >
        {formatPercentagePoints(margin.varianceToBenchmarkPercentage)} vs benchmark
      </span>
    )
  })()

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-gutter">
      {/* 1 — Active Bookings */}
      <KpiCard
        icon={CalendarCheck}
        iconTone="culinary"
        headline={formatCount(bookings.totalCount)}
        headlineUnit="Bookings"
        support={bookingSupport}
        foot={
          <>
            <span className="font-mono text-data-sm font-semibold tabular-nums text-espresso-900">
              {formatCurrency(bookings.contractedValue)}
            </span>
            <span className="font-caption text-caption text-espresso-600">contracted pipeline</span>
          </>
        }
        progress={{
          ratio: committedShare,
          label: 'Committed share of the period booking population',
          tone: 'culinary',
        }}
        action={
          <Link
            to="/bookings"
            title="Bookings & Events — not yet implemented"
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-artisan-subtle text-espresso-700 font-caption text-caption hover:bg-surface-container-high transition-colors"
          >
            <span>Schedule</span>
            <span aria-hidden="true">&rarr;</span>
          </Link>
        }
      />

      {/* 2 — Dish Costing Coverage */}
      <KpiCard
        icon={UtensilsCrossed}
        iconTone="neutral"
        headline={formatPercentage(dishCosting.coveragePercentage)}
        headlineUnit="Costed"
        support={`${formatCount(dishCosting.costedMenuCount)} of ${formatCount(
          dishCosting.totalMenuCount,
        )} Live Dish Costs`}
        foot={
          <span className="inline-flex items-center gap-1 font-caption text-caption text-caramel-700 font-bold">
            <span className="w-1.5 h-1.5 rounded-full bg-caramel-500" aria-hidden="true" />
            {formatCount(dishCosting.uncostedMenuCount)} need costing
          </span>
        }
        progress={{
          ratio: dishCosting.coveragePercentage,
          label: 'Share of the menu catalogue that carries a food cost',
          tone: 'caramel',
        }}
        action={
          <Link
            to="/menus"
            title="Menu & Food Costing — not yet implemented"
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-artisan-subtle text-espresso-700 font-caption text-caption hover:bg-surface-container-high transition-colors"
          >
            <span>Cost Engine</span>
            <span aria-hidden="true">&rarr;</span>
          </Link>
        }
      />

      {/* 3 — Catalogue Margin */}
      <KpiCard
        icon={TrendingUp}
        iconTone="culinary"
        headline={formatPercentage(margin.catalogueMarginPercentage)}
        headlineUnit="Catalogue Margin"
        headlineUnitTone="culinary"
        support={benchmarkSupport}
        foot={marginFoot}
        progress={{
          ratio: marginAgainstBenchmark,
          label: 'Catalogue margin measured against the configured benchmark',
          tone: 'culinary',
        }}
        action={
          <Badge tone="success" shape="pill">
            {benchmarks.targetMarginPercentage === null
              ? 'Target: Not configured'
              : `Target: ${formatPercentage(benchmarks.targetMarginPercentage)}`}
          </Badge>
        }
      />

      {/* 4 — Outstanding Receivables */}
      <KpiCard
        icon={Hourglass}
        iconTone="caramel"
        headline={formatCurrency(receivables.outstandingAmount)}
        headlineUnit="Unpaid"
        headlineUnitTone="caramel"
        support={`Across ${formatCount(receivables.bookingCount)} Event Ledgers`}
        foot={
          <>
            <span className="font-caption text-caption text-caramel-700 font-bold">
              {formatCount(receivables.overdueCount)} Overdue (&gt;30 days)
            </span>
            <span className="font-mono text-data-sm font-semibold tabular-nums text-espresso-900">
              {formatCurrency(receivables.overdueAmount)}
            </span>
          </>
        }
        progress={{
          ratio: overdueShare,
          label: 'Overdue share of total accounts receivable',
          tone: 'caramel',
        }}
        action={
          receivables.overdueCount ? (
            <Badge tone="warning" shape="pill">
              Action Req.
            </Badge>
          ) : (
            <Badge tone="neutral" shape="pill">
              Current
            </Badge>
          )
        }
      />
    </div>
  )
}
