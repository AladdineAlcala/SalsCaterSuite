import { MapPinned, Pencil, Power, UtensilsCrossed } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { PackageDto } from '@/types/package.types'
import { PAX_UNIT, packageActiveLabel, packageActiveTone } from '../packageTypes'
import { PackageTypeChip } from './PackageTypeChip'

export interface PackageInspectorProps {
  /** The tier the operator selected, or `null` when nothing is selected. */
  tier: PackageDto | null
  /** Opens the tier form in edit mode. */
  onEdit: (packageId: number) => void
  /** Activates or deactivates the tier. */
  onToggleActive: (packageId: number) => void
  /** `true` while the activate / deactivate call is in flight. */
  isToggling: boolean
  /** Opens the course-coverage rules workbench (REQ-PKG-02). */
  onOpenCourseRules: (packageId: number) => void
  /** Opens the location and out-of-town matrix (REQ-PKG-02). */
  onOpenLocations: (packageId: number) => void
}

/**
 * The tier inspector (spec §5.2 item 5).
 *
 * It shows the tier's identity — its numeric key, its type, its display name
 * — and its commercial terms: the per-guest rate, the minimum headcount and
 * the selectable state.
 *
 * The reference's **Archive Tier** action is presented as an honest
 * **Deactivate / Activate** toggle, because `PATCH
 * /api/packages/{id}/toggle-active` restores a tier and an archive does not
 * (DEC-071).
 *
 * The reference's course-allocation matrix and its venue-coverage list are
 * **no longer capability gaps**: REQ-PKG-02 publishes them, so they are
 * replaced by the two real workbench actions (DEC-084). The event-inclusions
 * panel still carries a capability-gap notice, because no entity stores it.
 */
export function PackageInspector({
  tier,
  onEdit,
  onToggleActive,
  isToggling,
  onOpenCourseRules,
  onOpenLocations,
}: PackageInspectorProps) {
  if (!tier) {
    return (
      <Card elevation={1} className="flex flex-col gap-space-md">
        <EmptyState
          title="No tier selected"
          description="Select a tier from the catalogue to inspect its rate, its minimum headcount and its state."
        />
      </Card>
    )
  }

  const rate = formatCurrency(tier.amountPax)
  const minimum = tier.minPax === null ? EM_DASH : formatCount(tier.minPax)

  return (
    <Card
      data-testid="package-tier-inspector"
      elevation={1}
      className="flex flex-col gap-space-md"
    >
      {/* Inspector header */}
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex items-center gap-2 flex-wrap">
            <Badge tone="success" shape="pill">
              Tier Inspector
            </Badge>
            <span className="font-data-sm text-data-sm text-espresso-400">{tier.packageId}</span>
          </div>
          <h2 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight leading-tight break-words">
            {tier.description}
          </h2>
        </div>
        <div className="text-right shrink-0">
          <span className="font-caption text-caption text-espresso-400 uppercase block">
            Rate per guest
          </span>
          <span className="font-data-lg text-data-lg text-espresso-900 font-bold">{rate}</span>
        </div>
      </div>

      {/* The tier's commercial terms */}
      <div className="bg-surface-container-low rounded-xl p-4 space-y-3">
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="flex flex-col gap-0.5">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Tier type
            </dt>
            <dd>
              <PackageTypeChip packageType={tier.packageType} />
            </dd>
          </div>
          <div className="flex flex-col gap-0.5">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Minimum pax
            </dt>
            <dd className="font-data-md text-data-md text-espresso-900 font-bold">
              {tier.minPax === null ? EM_DASH : `${minimum} ${PAX_UNIT}`}
            </dd>
          </div>
          <div className="flex flex-col gap-0.5 sm:col-span-2">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              State
            </dt>
            <dd>
              <Badge tone={packageActiveTone(tier.isActive)}>
                {packageActiveLabel(tier.isActive)}
              </Badge>
            </dd>
          </div>
        </dl>
      </div>

      {/* Actions */}
      <div className="flex flex-col sm:flex-row gap-2 flex-wrap">
        <Button variant="primary" icon={Pencil} onClick={() => onEdit(tier.packageId)}>
          Edit Tier
        </Button>
        <Button
          variant="secondary"
          icon={Power}
          onClick={() => onToggleActive(tier.packageId)}
          disabled={isToggling}
        >
          {tier.isActive ? 'Deactivate Tier' : 'Activate Tier'}
        </Button>
      </div>

      {/*
        The reference's course-allocation matrix and venue-coverage list are
        real now: REQ-PKG-02 publishes both, so they are workbench actions
        rather than capability gaps (DEC-084).
      */}
      <div className="flex flex-col gap-2">
        <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
          Configuration workbenches
        </span>
        <div className="flex flex-col sm:flex-row gap-2 flex-wrap">
          <Button
            variant="secondary"
            icon={UtensilsCrossed}
            onClick={() => onOpenCourseRules(tier.packageId)}
          >
            Course Rules
          </Button>
          <Button
            variant="secondary"
            icon={MapPinned}
            onClick={() => onOpenLocations(tier.packageId)}
          >
            Location Coverage
          </Button>
        </div>
      </div>

      {/* The capability gaps the reference fills with content the model cannot carry (DEC-067, DEC-084). */}
      <CapabilityGapNotice
        title="Stored per-tier food cost unavailable"
        missing="A tier stores no food cost and no committed margin, so a per-tier food cost cannot be published. The workbench derives it from a dish selection or from the standard-run estimate."
        requirement="REQ-PKG-01"
      />
      <CapabilityGapNotice
        title="Event inclusions unavailable"
        missing="No inclusion, rental or waitstaff-ratio record is stored against a tier."
        requirement="REQ-PKG-01"
      />
    </Card>
  )
}
