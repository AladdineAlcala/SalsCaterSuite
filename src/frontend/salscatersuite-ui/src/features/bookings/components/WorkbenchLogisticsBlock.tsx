import { MapPinned, Truck } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { Card } from '@/components/ui/Card'
import { EM_DASH, formatCurrency } from '@/utils/currencyFormatter'
import type { AreaDto } from '@/types/package.types'
import type { BookingQuote } from '@/types/booking.types'
import type { BookingWorkbenchDraft, BookingWorkbenchErrors } from '../bookingVocabulary'
import { NOT_STATED, supportsExtendedArea } from '../bookingVocabulary'
import { WorkbenchCheckbox, WorkbenchComboboxField } from './WorkbenchFields'

export interface WorkbenchLogisticsBlockProps {
  draft: BookingWorkbenchDraft
  errors: BookingWorkbenchErrors
  onChange: (patch: Partial<BookingWorkbenchDraft>) => void
  /** The municipality catalogue the picker reads. */
  municipalities: AreaDto[]
  /** The server's quote — the source of the surcharge and the coverage verdict. */
  quote: BookingQuote | null
  /** `true` while the quote is being re-priced — a DEC-171 area prefill, a headcount, a switch. */
  isRecalculating: boolean
  /** The selected tier's stored type. `null` until a tier is chosen. */
  selectedTierType: string | null
}

/**
 * The venue & spatial logistics block (spec §5.2, BR-17, BR-18).
 *
 * The municipality, its distance, its logistics note and its per-event
 * logistics cost come from the **municipality catalogue**, and the surcharge
 * comes from the **server's quote** — the client never multiplies a rate by a
 * headcount itself (DEC-002, BR-26).
 *
 * The **area filter** lives with the tier cascade in the tier engine now; this
 * block keeps the **event municipality** and the surcharge switch, and hides
 * both for a tier whose type cannot carry the amount (DEC-141, AC-BKG-72).
 *
 * The reference's **Included Site Amenities** checklist is a **capability
 * gap**: `Package` carries no inclusions collection, so no amenity is
 * fabricated (DEC-108).
 */
export function WorkbenchLogisticsBlock({
  draft,
  errors,
  onChange,
  municipalities,
  quote,
  isRecalculating,
  selectedTierType,
}: WorkbenchLogisticsBlockProps) {
  const municipality =
    municipalities.find((area) => String(area.areaId) === draft.extendedAreaId) ?? null

  /*
   * The quote's figures and verdicts display only while they are **current**
   * (AC-BKG-90): an out-of-town surcharge or a coverage verdict from a
   * previous request — for example after the DEC-171 area prefill changes
   * the quote inputs — must never stand in while the server re-prices.
   */
  const isQuoteCurrent = quote !== null && !isRecalculating

  const canCarryExtendedArea =
    selectedTierType !== null && supportsExtendedArea(selectedTierType)

  const coverage = isQuoteCurrent ? quote.coverage : null
  const coverageLabel =
    coverage === null
      ? 'Not priced yet'
      : coverage.isCovered === null
        ? 'Unknown — the tier states no coverage set'
        : coverage.isCovered
          ? `Covered by ${coverage.coveredAreaCount} municipalities`
          : 'Not covered by the tier'

  return (
    <div className="flex flex-col gap-space-md min-w-0">
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <h2 className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5">
            <MapPinned className="h-4 w-4 text-caramel-600" aria-hidden="true" />
            Venue &amp; spatial logistics
          </h2>
          {municipality && canCarryExtendedArea ? <Badge tone="warning">Out-of-town</Badge> : null}
        </div>

        {canCarryExtendedArea ? (
          <>
            <WorkbenchComboboxField
              id="booking-municipality"
              label="Municipality"
              value={draft.extendedAreaId}
              onChange={(value) => onChange({ extendedAreaId: value })}
              options={municipalities.map((area) => ({
                value: String(area.areaId),
                label: area.areaDetails,
              }))}
              placeholder="Select a municipality"
              error={errors.extendedAreaId}
              hint="The municipality carries the out-of-town rate"
            />

            <WorkbenchCheckbox
              id="booking-apply-extended"
              label="Apply the out-of-town surcharge"
              checked={draft.applyExtendedAmount}
              onChange={(checked) => onChange({ applyExtendedAmount: checked })}
              error={errors.applyExtendedAmount}
              hint="The switch is applied at creation; the amount is then frozen on the booking"
            />
          </>
        ) : selectedTierType !== null ? (
          <p
            role="note"
            className="font-caption text-caption text-espresso-600 rounded-lg bg-surface-container-low p-space-sm border border-artisan-border"
          >
            The {selectedTierType} type cannot carry the out-of-town amount — only the Regular
            and Premier types can (DEC-141). The municipality selector is hidden for this tier.
          </p>
        ) : null}

        {municipality && canCarryExtendedArea ? (
          <dl className="flex flex-col gap-1.5 text-caption rounded-lg bg-surface-container-low p-space-sm border border-artisan-border">
            <div className="flex items-center justify-between gap-2">
              <dt className="text-espresso-400">Municipality</dt>
              <dd className="font-semibold text-espresso-900 text-right break-words">
                {municipality.areaDetails}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="text-espresso-400">Distance</dt>
              <dd className="font-mono text-espresso-900 text-right">
                {municipality.distanceKm === null ? EM_DASH : `${municipality.distanceKm} km`}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="text-espresso-400">Logistics note</dt>
              <dd className="text-espresso-800 text-right break-words">
                {municipality.logisticsNote ?? EM_DASH}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="text-espresso-400">Logistics cost</dt>
              <dd className="font-mono text-espresso-900 text-right">
                {municipality.logisticsCost === null
                  ? NOT_STATED
                  : formatCurrency(municipality.logisticsCost)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="text-espresso-400">Surcharge rate</dt>
              <dd className="font-mono text-espresso-900 text-right">
                {!isQuoteCurrent
                  ? EM_DASH
                  : quote.extendedAreaRate === null || quote.extendedAreaRate === undefined
                    ? NOT_STATED
                    : `${formatCurrency(quote.extendedAreaRate)} per head`}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="text-espresso-400">Surcharge</dt>
              <dd className="font-mono text-espresso-900 text-right">
                {!isQuoteCurrent
                  ? EM_DASH
                  : quote.extendedAreaAmount === null || quote.extendedAreaAmount === undefined
                    ? EM_DASH
                    : formatCurrency(quote.extendedAreaAmount)}
              </dd>
            </div>
            <div className="flex items-start justify-between gap-2">
              <dt className="text-espresso-400">Tier coverage</dt>
              <dd className="text-espresso-800 text-right break-words">{coverageLabel}</dd>
            </div>          </dl>
        ) : null}

        {municipality && canCarryExtendedArea ? (
          <p className="font-caption text-caption text-espresso-600 flex items-center gap-1.5">
            <Truck className="h-3 w-3" aria-hidden="true" />
            The surcharge is the municipality&apos;s own rate — the same rate the tier quotes.
          </p>
        ) : null}
      </Card>

      <CapabilityGapNotice
        title="Included site amenities unavailable"
        missing="No seating, linen, hardware, staffing or amenity record is stored against a tier or a booking, so the checklist cannot be rendered."
        requirement="REQ-BKG-01"
        footnote="Deliberately excluded by DEC-108 — the package carries no inclusions collection."
      />
    </div>
  )
}
