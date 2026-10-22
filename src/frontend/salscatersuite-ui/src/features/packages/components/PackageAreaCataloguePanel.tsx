import { MapPinned, Pencil, Plus } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { AreaDto } from '@/types/package.types'
import { NOT_STATED } from '../packageTypes'

export interface PackageAreaCataloguePanelProps {
  /** The municipality catalogue the server published. */
  areas: AreaDto[]
  /** The area ids the tier already covers. */
  coveredAreaIds: number[]
  /** Attaches a municipality to the tier. */
  onAttach: (area: AreaDto) => void
  /** Opens the municipality editor. */
  onEdit: (area: AreaDto) => void
  /** `true` while a municipality is being attached. */
  isAttaching: boolean
}

/**
 * The municipality catalogue panel (spec §5.3).
 *
 * The catalogue is the population every coverage set is chosen from. A
 * municipality that states no rate reads "not stated" — it is not a zero
 * surcharge, and the money chain still yields a complete contract value
 * (DEC-086).
 */
export function PackageAreaCataloguePanel({
  areas,
  coveredAreaIds,
  onAttach,
  onEdit,
  isAttaching,
}: PackageAreaCataloguePanelProps) {
  return (
    <Card
      elevation={1}
      className="flex flex-col gap-space-md"
      data-testid="package-area-catalogue"
    >
      <div className="flex items-center justify-between gap-2 flex-wrap pb-1">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Municipality Catalogue
        </h2>
        <span className="font-caption text-caption text-espresso-600">
          {areas.length} municipalities
        </span>
      </div>

      {areas.length === 0 ? (
        <EmptyState
          icon={MapPinned}
          title="The catalogue holds no municipality"
          description="Add the first municipality with the editor beside this panel. Nothing is invented here."
        />
      ) : (
        <ul className="flex flex-col gap-2">
          {areas.map((area) => {
            const isCovered = coveredAreaIds.includes(area.areaId)
            return (
              <li
                key={area.areaId}
                className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 rounded-lg border border-artisan-border bg-surface-container-low p-3"
              >
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-body-md text-body-md text-espresso-900 break-words">
                      {area.areaDetails}
                    </span>
                    {isCovered ? <Badge tone="success">Covered by this tier</Badge> : null}
                  </div>
                  <p className="font-caption text-caption text-espresso-600 break-words">
                    Rate{' '}
                    {area.additionalRate === null ? NOT_STATED : formatCurrency(area.additionalRate)}{' '}
                    per guest · Distance{' '}
                    {area.distanceKm === null ? EM_DASH : `${formatCount(area.distanceKm)} km`} ·
                    Logistics{' '}
                    {area.logisticsCost === null
                      ? EM_DASH
                      : formatCurrency(area.logisticsCost)}{' '}
                    per event
                  </p>
                  {area.logisticsNote ? (
                    <p className="font-caption text-caption text-espresso-600 break-words">
                      {area.logisticsNote}
                    </p>
                  ) : null}
                </div>
                <div className="flex items-center gap-1.5 shrink-0 flex-wrap">
                  <Button
                    variant="secondary"
                    size="sm"
                    icon={Plus}
                    onClick={() => onAttach(area)}
                    disabled={isCovered || isAttaching}
                    aria-label={`Attach ${area.areaDetails} to this tier`}
                  >
                    {isCovered ? 'Covered' : 'Attach'}
                  </Button>
                  <Button
                    variant="tertiary"
                    size="sm"
                    icon={Pencil}
                    onClick={() => onEdit(area)}
                    aria-label={`Edit the municipality ${area.areaDetails}`}
                  >
                    Edit
                  </Button>
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </Card>
  )
}
