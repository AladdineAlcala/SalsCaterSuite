import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { buildPeriodOptions, currentPeriodKey } from '@/utils/dateFormatter'
import { DashboardHeader } from './components/DashboardHeader'
import { DashboardSkeleton } from './components/DashboardSkeleton'
import { FoodCostByCategory } from './components/FoodCostByCategory'
import { KpiGrid } from './components/KpiGrid'
import { OperationalHub } from './components/OperationalHub'
import { PaymentLedgerPanel } from './components/PaymentLedgerPanel'
import { StatusFooter } from './components/StatusFooter'
import type { ApiConnectionState } from './components/StatusFooter'
import { UpcomingSchedule } from './components/UpcomingSchedule'
import { useDashboard } from './hooks/useDashboard'

/**
 * The Operations Dashboard (spec §9).
 *
 * The screen renders exclusively from `GET /api/dashboard`. It reproduces the
 * reference layout — header + telemetry pill, four KPI cards, operational hub,
 * the three-column operations matrix and the status footer — while every
 * figure comes from the payload. Nothing is hardcoded and no financial total
 * is derived in the browser (DEC-002).
 */
export function DashboardPage() {
  const [period, setPeriod] = useState<string>(() => currentPeriodKey())
  const { data, isPending, isError, error, isFetching, refetch } = useDashboard(period)

  const periodOptions = useMemo(() => {
    const options = buildPeriodOptions()
    const active = options.find((option) => option.value === period)
    if (active && data?.period?.label) {
      // The server resolves the period label — it is authoritative.
      active.label = data.period.label
    }
    return options
  }, [period, data?.period?.label])

  if (isPending) {
    return <DashboardSkeleton />
  }

  if (isError) {
    const apiError = error instanceof ApiError ? error : null
    const messages = apiError?.errors.length
      ? apiError.errors
      : ['The operations dashboard could not be loaded.']

    return (
      <div className="flex flex-col gap-space-xl">
        <AlertBanner
          title="The operations dashboard could not be loaded"
          errors={messages}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={() => void refetch()}>
              Retry
            </Button>
          }
        />
        <StatusFooter generatedAt="" connection="offline" />
      </div>
    )
  }

  if (!data) {
    return <DashboardSkeleton />
  }

  const connection: ApiConnectionState = isFetching ? 'refreshing' : 'live'

  return (
    <div className="flex flex-col gap-space-xl">
      <DashboardHeader
        periodKey={period}
        periodLabel={data.period.label}
        periodOptions={periodOptions}
        onPeriodChange={setPeriod}
        targetFoodCostPercentage={data.benchmarks.targetFoodCostPercentage}
        collectionRatioPercentage={data.collections.collectionRatioPercentage}
        isRefreshing={isFetching}
      />

      <KpiGrid data={data} />

      <OperationalHub />

      <section className="flex flex-col gap-space-sm">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2">
            <span className="font-eyebrow text-eyebrow uppercase text-espresso-900 tracking-wider">
              Catering Operations &amp; Cost Architecture
            </span>
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-culinary-50 text-culinary-800 font-caption text-caption font-semibold">
              <span className="w-1.5 h-1.5 rounded-full bg-culinary-500" aria-hidden="true" />
              Live API telemetry
            </span>
          </div>
          <Link
            to="/menus"
            title="Menu & Food Costing — not yet implemented"
            className="font-body-sm text-body-sm text-espresso-600 hover:text-espresso-900 flex items-center gap-1 font-semibold"
          >
            <span>View Costing Methodology</span>
            <span aria-hidden="true">↗</span>
          </Link>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-gutter">
          <UpcomingSchedule bookings={data.upcomingBookings} periodLabel={data.period.label} />
          <FoodCostByCategory categories={data.foodCostByCategory} />
          <PaymentLedgerPanel
            ledger={data.ledger}
            receivables={data.receivables}
            isRefreshing={isFetching}
          />
        </div>
      </section>

      <StatusFooter generatedAt={data.generatedAt} connection={connection} />
    </div>
  )
}
