import { MapPinned, Pencil, Power, RotateCcw, Trash2 } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { PackageAreaCoverageRow, PackageLocationPreview } from '@/types/package.types'
import { NOT_STATED, packageCoverageLabel, packageCoverageTone } from '../packageTypes'

export interface PackageCoverageMatrixProps {
  /** The working copy of the covered municipalities. */
  rows: PackageAreaCoverageRow[]
  /** The server's per-municipality preview at the current headcount. */
  previews: Record<string, PackageLocationPreview>
  /** The headcount the extended totals are priced at. */
  pax: number | null
  /**
   * `true` while the typed headcount has not reached the server yet, or the
   * quote is in flight.
   *
   * The extended-total column shows an explicit **recalculating** state while
   * it holds: a figure priced for a different headcount is never presented as
   * the current one, and a zero is never shown in its place.
   */
  isRecalculating: boolean
  /** The area ids staged for removal. */
  pendingRemovalAreaIds: number[]
  /** Activates or deactivates one coverage row. */
  onToggleActive: (row: PackageAreaCoverageRow) => void
  /** Stages one coverage row for removal. */
  onRemove: (row: PackageAreaCoverageRow) => void
  /** Withdraws a staged removal. */
  onRestore: (row: PackageAreaCoverageRow) => void
  /** Opens the municipality editor for one row. */
  onEditMunicipality: (row: PackageAreaCoverageRow) => void
  /** `true` when the working copy differs from the server's state. */
  hasUnsavedChanges: boolean
}

/**
 * The out-of-town surcharge matrix (spec §5.3).
 *
 * Every figure in the table is the server's. The **rate is the municipality's**
 * — one rate per municipality, shared by every tier that covers it (DEC-078) —
 * and the extended total is the server's own figure at the current headcount,
 * never a rate multiplied in the browser (DEC-002).
 *
 * The table carries a caption and column headers, and each row action's
 * accessible name includes the municipality.
 */
export function PackageCoverageMatrix({
  rows,
  previews,
  pax,
  isRecalculating,
  pendingRemovalAreaIds,
  onToggleActive,
  onRemove,
  onRestore,
  onEditMunicipality,
  hasUnsavedChanges,
}: PackageCoverageMatrixProps) {
  return (
    <Card elevation={1} className="flex flex-col gap-space-md" data-testid="package-coverage-matrix">
      <div className="flex items-center justify-between gap-2 flex-wrap pb-1">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Out-of-Town Surcharge Matrix
        </h2>
        <span className="font-caption text-caption text-espresso-600">
          {rows.length} covered ·{' '}
          {hasUnsavedChanges ? 'unsaved changes pending' : 'saved'}
        </span>
      </div>

      {rows.length === 0 ? (
        <EmptyState
          icon={MapPinned}
          title="This tier covers no municipality"
          description="Attach a municipality from the catalogue below to offer this tier outside the home zone. Nothing is invented here."
        />
      ) : (
        <div className="overflow-x-auto -mx-1 px-1">
          <table className="w-full min-w-full text-left border-collapse">
            <caption className="text-left font-caption text-caption text-espresso-600 pb-2">
              The municipalities this tier covers, the municipality&apos;s per-pax
              surcharge and the extended total at {pax === null ? 'the current headcount' : `${formatCount(pax)} heads`}.
            </caption>
            <thead>
              <tr className="border-b border-artisan-border">
                <th scope="col" className="py-2 pr-3 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold">
                  Municipality
                </th>
                <th scope="col" className="py-2 pr-3 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold">
                  Coverage
                </th>
                <th scope="col" className="py-2 pr-3 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold">
                  Rate / pax
                </th>
                <th scope="col" className="py-2 pr-3 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold">
                  Extended total
                </th>
                <th scope="col" className="py-2 pr-3 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold">
                  Distance
                </th>
                <th scope="col" className="py-2 pr-3 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold">
                  Dispatch note
                </th>
                <th scope="col" className="py-2 font-caption text-caption uppercase tracking-wider text-espresso-400 font-semibold">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const preview = previews[String(row.areaId)]
                const isPendingRemoval = pendingRemovalAreaIds.includes(row.areaId)
                return (
                  <tr key={row.packageAreaCoverageId} className="border-b border-artisan-border align-top">
                    <th scope="row" className="py-2.5 pr-3 font-body-sm text-body-sm text-espresso-900 font-semibold break-words">
                      {row.areaName}
                    </th>
                    <td className="py-2.5 pr-3">
                      <div className="flex flex-col gap-1 items-start">
                        <Badge tone={packageCoverageTone(row.isActive)}>
                          {packageCoverageLabel(row.isActive)}
                        </Badge>
                        {isPendingRemoval ? (
                          <Badge tone="warning">Pending removal</Badge>
                        ) : null}
                      </div>
                    </td>
                    <td className="py-2.5 pr-3 font-data-sm text-data-sm text-espresso-900">
                      {row.additionalRate === null ? (
                        <span className="text-espresso-600">{NOT_STATED}</span>
                      ) : (
                        formatCurrency(row.additionalRate)
                      )}
                    </td>
                    <td className="py-2.5 pr-3 font-data-sm text-data-sm text-espresso-900 break-words">
                      {isRecalculating ? (
                        <span className="font-caption text-caption text-espresso-500">
                          Recalculating…
                        </span>
                      ) : preview ? (
                        formatCurrency(preview.extendedTotal)
                      ) : (
                        EM_DASH
                      )}
                    </td>
                    <td className="py-2.5 pr-3 font-data-sm text-data-sm text-espresso-900">
                      {row.distanceKm === null ? EM_DASH : `${formatCount(row.distanceKm)} km`}
                    </td>
                    <td className="py-2.5 pr-3 font-body-sm text-body-sm text-espresso-600 break-words">
                      {row.logisticsNote ?? EM_DASH}
                    </td>
                    <td className="py-2.5">
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <Button
                          variant="secondary"
                          size="sm"
                          icon={Power}
                          onClick={() => onToggleActive(row)}
                          aria-label={`${row.isActive ? 'Deactivate' : 'Activate'} the coverage of ${row.areaName}`}
                        >
                          {row.isActive ? 'Deactivate' : 'Activate'}
                        </Button>
                        <Button
                          variant="tertiary"
                          size="sm"
                          icon={Pencil}
                          onClick={() => onEditMunicipality(row)}
                          aria-label={`Edit the municipality ${row.areaName}`}
                        >
                          Edit
                        </Button>
                        {isPendingRemoval ? (
                          <Button
                            variant="tertiary"
                            size="sm"
                            icon={RotateCcw}
                            onClick={() => onRestore(row)}
                            aria-label={`Restore ${row.areaName} to the coverage`}
                          >
                            Restore
                          </Button>
                        ) : (
                          <Button
                            variant="tertiary"
                            size="sm"
                            icon={Trash2}
                            onClick={() => onRemove(row)}
                            aria-label={`Remove ${row.areaName} from the coverage`}
                          >
                            Remove
                          </Button>
                        )}
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      <p className="font-caption text-caption text-espresso-600">
        The rate is the <strong>municipality&apos;s</strong> rate, shared by every tier that
        covers it — a coverage row cannot override it, and editing it changes no booking.
      </p>
    </Card>
  )
}
