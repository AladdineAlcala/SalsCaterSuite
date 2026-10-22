import { TelemetryPill } from './TelemetryPill'
import type { PeriodOption } from '@/utils/dateFormatter'

export interface DashboardHeaderProps {
  periodKey: string
  periodLabel: string
  periodOptions: PeriodOption[]
  onPeriodChange: (period: string) => void
  targetFoodCostPercentage: number | null
  collectionRatioPercentage: number | null
  isRefreshing?: boolean
}

/**
 * The dashboard header (spec §9.2 item 2): eyebrow, H1, subtitle and the
 * quick telemetry pill.
 */
export function DashboardHeader({
  periodKey,
  periodLabel,
  periodOptions,
  onPeriodChange,
  targetFoodCostPercentage,
  collectionRatioPercentage,
  isRefreshing = false,
}: DashboardHeaderProps) {
  return (
    <section className="flex flex-col xl:flex-row xl:items-end justify-between gap-space-md">
      <div className="flex flex-col gap-1 min-w-0">
        <div className="flex items-center gap-2">
          <span
            className="inline-block w-2 h-2 rounded-full bg-culinary-600 animate-pulse"
            aria-hidden="true"
          />
          <span className="font-eyebrow text-eyebrow text-culinary-800 uppercase tracking-wider">
            Catering Telemetry / Commissary Operations
          </span>
        </div>
        <h1 className="font-headline-xl text-headline-xl-mobile md:text-headline-xl text-espresso-900 tracking-tight">
          Sal&apos;s CaterSuite Operations Dashboard
        </h1>
        <p className="font-body-md text-body-md text-espresso-600 max-w-2xl">
          Banquet bookings, menu costing coverage, receivables position and the payment ledger for
          the active reporting period. Every figure is calculated by the API from authoritative
          records.
        </p>
      </div>

      <TelemetryPill
        periodKey={periodKey}
        periodLabel={periodLabel}
        periodOptions={periodOptions}
        onPeriodChange={onPeriodChange}
        targetFoodCostPercentage={targetFoodCostPercentage}
        collectionRatioPercentage={collectionRatioPercentage}
        isRefreshing={isRefreshing}
      />
    </section>
  )
}
