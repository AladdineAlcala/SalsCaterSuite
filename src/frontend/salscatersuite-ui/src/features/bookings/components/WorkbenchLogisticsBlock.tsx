import { MapPinned, Truck } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { Card } from '@/components/ui/Card'
import { EM_DASH, formatCurrency } from '@/utils/currencyFormatter'
import type { AreaDto } from '@/types/package.types'
import type { BookingQuote } from '@/types/booking.types'
import type { BookingWorkbenchDraft, BookingWorkbenchErrors } from '../bookingVocabulary'
import { NOT_STATED } from '../bookingVocabulary'
import { WorkbenchCheckbox, WorkbenchSelectField } from './WorkbenchFields'

export interface WorkbenchLogisticsBlockProps {
  draft: BookingWorkbenchDraft
  errors: BookingWorkbenchErrors
  onChange: (patch: Partial<BookingWorkbenchDraft>) => void
  /** The municipality catalogue the picker reads. */
  municipalities: AreaDto[]
  /** The server's quote — the source of the surcharge and the coverage verdict. */
  quote: BookingQuote | null
}

/**
 * The venue & spatial logistics block (spec §5.3, BR-17, BR-18).
 *
 * The municipality, its distance, its logistics note and its per-event
 * logistics cost come from the **municipality catalogue**, and the surcharge
 * comes from the **server's quote** — the client never multiplies a rate by a
 * headcount itself (DEC-002, BR-26).
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
}: WorkbenchLogisticsBlockProps) {
  const municipality =
    municipalities.find((area) => String(area.areaId) === draft.extendedAreaId) ?? null

  const coverage = quote?.coverage ?? null
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
          {municipality ? <Badge tone="warning">Out-of-town</Badge> : null}
        </div>

        <WorkbenchSelectField
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

        {municipality ? (
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
                {quote?.extendedAreaRate === null || quote?.extendedAreaRate === undefined
                  ? NOT_STATED
                  : `${formatCurrency(quote.extendedAreaRate)} per head`}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="text-espresso-400">Surcharge</dt>
              <dd className="font-mono text-espresso-900 text-right">
                {quote?.extendedAreaAmount === null || quote?.extendedAreaAmount === undefined
                  ? EM_DASH
                  : formatCurrency(quote.extendedAreaAmount)}
              </dd>
            </div>
            <div className="flex items-start justify-between gap-2">
              <dt className="text-espresso-400">Tier coverage</dt>
              <dd className="text-espresso-800 text-right break-words">{coverageLabel}</dd>
            </div>
          </dl>
        ) : null}

        {municipality ? (
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
