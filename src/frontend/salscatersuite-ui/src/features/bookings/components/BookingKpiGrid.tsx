import { CalendarRange, Layers, TrendingUp, Users } from 'lucide-react'
import { formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import { ratioPercentage } from '@/utils/progressRatio'
import { formatEventDate } from '@/utils/dateFormatter'
import type { BookingRegisterSummary } from '@/types/booking.types'
import { PAX_UNIT, formatSignedPercentage } from '../bookingVocabulary'
import { BookingKpiCard } from './BookingKpiCard'

export interface BookingKpiGridProps {
  summary: BookingRegisterSummary
}

/**
 * The four KPI cards (spec §5.2 item 2, KPI-33 .. KPI-39).
 *
 * Every figure is read from the aggregate the API computed — the browser never
 * sums the dossier cards to produce a total, a share or a headcount figure
 * (DEC-002).
 *
 * The reference's fourth card — the **Kitchen BEO** card with its "6
 * Dispatched" headline and its "2 Pending Executive Chef Sign-off" line — is
 * **not** rendered here: there is no BEO entity and no BEO production state
 * (DEC-036, DEC-104). The committed headcount takes its place.
 *
 * A `null` figure means *unknown*, not zero, so it renders as an em dash
 * (contract rule 7).
 */
export function BookingKpiGrid({ summary }: BookingKpiGridProps) {
  const { pipeline, leadingTier, contractValue, headcount } = summary

  /*
   * The two tracks are **count ratios**, not monetary aggregates: each one
   * compares two figures the server published. The money itself is never
   * re-derived in the browser.
   */
  const confirmedShare = ratioPercentage(pipeline.confirmedCount, pipeline.bookingCount)
  const collectedShare = ratioPercentage(
    contractValue.collectedAmount,
    contractValue.netContractedValue,
  )

  const leadingTierName = leadingTier
    ? (leadingTier.packageName ?? leadingTier.packageType ?? '—')
    : '—'

  const nextEventLine =
    headcount.nextEventDate === null
      ? 'No upcoming event'
      : `Next: ${formatEventDate(headcount.nextEventDate)}`

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-gutter">
      {/* 1 — Active Pipeline (KPI-33) */}
      <BookingKpiCard
        icon={CalendarRange}
        iconTone="culinary"
        label="Active Pipeline"
        value={formatCount(pipeline.bookingCount)}
        unit="Bookings"
        subLine={`${formatCount(pipeline.confirmedCount)} confirmed • ${formatCount(
          pipeline.tentativeCount,
        )} tentative • ${formatCount(pipeline.completedCount)} completed`}
        trackRatio={confirmedShare}
        trackLabel="Confirmed share of the pipeline"
        trackTone="culinary"
      />

      {/* 2 — Leading Tier (KPI-34) */}
      <BookingKpiCard
        icon={Layers}
        iconTone="caramel"
        label="Leading Tier"
        value={leadingTierName}
        subLine={
          leadingTier === null
            ? 'No tier holds a contract value yet'
            : `${formatPercentage(leadingTier.sharePercentage)} of the population’s contract value`
        }
        secondaryLine={
          leadingTier === null
            ? undefined
            : `${formatSignedPercentage(leadingTier.changePercentage)} against the preceding period`
        }
      />

      {/* 3 — Contract Value (KPI-35) */}
      <BookingKpiCard
        icon={TrendingUp}
        iconTone="culinary"
        label="Contract Value"
        value={formatCurrency(contractValue.netContractedValue)}
        subLine={`${formatCurrency(contractValue.collectedAmount)} collected • ${formatCurrency(
          contractValue.outstandingAmount,
        )} outstanding`}
        trackRatio={collectedShare}
        trackLabel="Collected share of the contract value"
        trackTone="culinary"
      />

      {/* 4 — Committed Headcount (KPI-36) */}
      <BookingKpiCard
        icon={Users}
        iconTone="neutral"
        label="Committed Headcount"
        value={formatCount(headcount.totalPax)}
        unit={PAX_UNIT}
        subLine={
          headcount.averagePaxPerBooking === null
            ? 'No average headcount yet'
            : `${formatCount(headcount.averagePaxPerBooking)} ${PAX_UNIT} per booking on average`
        }
        secondaryLine={nextEventLine}
      />
    </div>
  )
}
