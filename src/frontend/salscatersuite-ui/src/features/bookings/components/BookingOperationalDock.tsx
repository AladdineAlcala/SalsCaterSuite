import { Gauge, HandCoins, NotebookPen } from 'lucide-react'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { Card } from '@/components/ui/Card'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import { formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type { BookingRegisterSummary } from '@/types/booking.types'
import { NOT_STATED, PAX_UNIT } from '../bookingVocabulary'

export interface BookingOperationalDockProps {
  summary: BookingRegisterSummary
}

/**
 * The three-card operational dock (spec §5.2 item 6).
 *
 * - **Commissary load** — KPI-38: the near-term committed pax against the
 *   configured capacity. The capacity is **configuration and absent by
 *   default** (DEC-102), so an unconfigured gauge reads "not stated" — the
 *   reference's `400` never appears (AC-BKG-18).
 * - **BEO & event orders** — an explicit **capability-gap notice**: there is
 *   no BEO entity and no per-booking dish selection to print (DEC-104).
 * - **AR & SOA telemetry** — KPI-39: the outstanding total, the count of
 *   bookings carrying a balance and the overdue figures. The reference's "3
 *   Notices Sent" has no source and is replaced by the **overdue count**.
 */
export function BookingOperationalDock({ summary }: BookingOperationalDockProps) {
  const { capacity, receivables } = summary

  const capacityLabel = capacity.isConfigured ? formatCount(capacity.capacityPax) : NOT_STATED

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-gutter items-start">
      {/* 1 — Commissary load (KPI-38). */}
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2 min-w-0">
            <span className="w-8 h-8 rounded-lg bg-culinary-50 text-culinary-700 flex items-center justify-center shrink-0">
              <Gauge className="h-4 w-4" aria-hidden="true" />
            </span>
            <h2 className="text-body-md font-bold text-espresso-900">Commissary Load</h2>
          </div>
        </div>

        <div className="flex flex-col gap-space-sm min-w-0">
          <div className="flex items-baseline justify-between gap-2 flex-wrap">
            <span className="font-mono text-data-md font-extrabold text-culinary-700">
              {formatCount(capacity.committedPax)} / {capacityLabel} {PAX_UNIT}
            </span>
          </div>
          <ProgressTrack
            ratio={capacity.utilisationPercentage}
            label="Commissary capacity utilisation"
            tone="caramel"
          />
          <p className="font-caption text-caption text-espresso-600">
            {capacity.isConfigured
              ? `${formatPercentage(capacity.utilisationPercentage)} of the configured capacity is committed.`
              : 'No commissary capacity is configured, so the load is published without a ceiling.'}
          </p>
        </div>
      </Card>

      {/* 2 — BEO & event orders: an explicit capability gap (DEC-104). */}
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2 min-w-0">
            <span className="w-8 h-8 rounded-lg bg-caramel-50 text-caramel-700 flex items-center justify-center shrink-0">
              <NotebookPen className="h-4 w-4" aria-hidden="true" />
            </span>
            <h2 className="text-body-md font-bold text-espresso-900">BEO &amp; Event Orders</h2>
          </div>
        </div>

        <CapabilityGapNotice
          title="Banquet event orders unavailable"
          missing="There is no banquet event order entity and no per-booking dish selection, so no production state, sign-off telemetry or batch action can be published."
          requirement="REQ-REP-02"
          footnote="Deliberately excluded by DEC-104 — the reference's BEO queue is never fabricated."
        />
      </Card>

      {/* 3 — AR & SOA telemetry (KPI-39). */}
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2 min-w-0">
            <span className="w-8 h-8 rounded-lg bg-culinary-50 text-culinary-700 flex items-center justify-center shrink-0">
              <HandCoins className="h-4 w-4" aria-hidden="true" />
            </span>
            <h2 className="text-body-md font-bold text-espresso-900">AR &amp; SOA Telemetry</h2>
          </div>
        </div>

        <div className="flex flex-col gap-space-sm min-w-0">
          <div className="flex items-baseline justify-between gap-2 flex-wrap">
            <span className="font-mono text-data-md font-extrabold text-espresso-900">
              {formatCurrency(receivables.outstandingAmount)}
            </span>
            <span className="font-caption text-caption text-caramel-700 font-bold">
              {formatCount(receivables.overdueCount)} overdue
            </span>
          </div>
          <p className="font-caption text-caption text-espresso-600 break-words">
            {formatCount(receivables.bookingCount)} bookings carry an outstanding balance •{' '}
            {formatCurrency(receivables.overdueAmount)} is overdue.
          </p>
        </div>
      </Card>
    </div>
  )
}
