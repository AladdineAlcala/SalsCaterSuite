import { MapPin, Pencil } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type {
  PackageAreaCoverageMatrix,
  PackageCourseRuleset,
} from '@/types/package.types'
import { PAX_UNIT, packageActiveLabel, packageActiveTone } from '../packageTypes'
import { PackageTypeChip } from './PackageTypeChip'

export interface PackageSpecRailProps {
  /** The tier's ruleset and its commercial terms (contract §13.1). */
  ruleset: PackageCourseRuleset
  /** The tier's coverage matrix, for the spatial-coverage checklist. */
  coverage: PackageAreaCoverageMatrix | undefined
  /** Opens the tier form, the single editor of the commercial terms (DEC-082). */
  onEditTierTerms: () => void
}

/**
 * The specification rail (spec §5.2, column 1).
 *
 * The rail is **read-only**. The reference presents the tier's name, rate,
 * floor and type as editable inputs, but the repository already ships a tier
 * form that edits exactly those fields with a validator; two editors for one
 * field is a defect, not a feature (DEC-082). The rail therefore shows the
 * tier's live commercial terms and offers an "Edit tier terms" action.
 *
 * The reference's **SKU / code** field has no column in the approved model and
 * is not rendered.
 */
export function PackageSpecRail({ ruleset, coverage, onEditTierTerms }: PackageSpecRailProps) {
  const coveredMunicipalities = coverage?.areas ?? []
  const activeCovered = coveredMunicipalities.filter((row) => row.isActive)

  return (
    <Card elevation={1} className="flex flex-col gap-space-md" data-testid="package-spec-rail">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <Badge tone="info" shape="pill">
            Package Specification
          </Badge>
          <h2 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight leading-tight break-words">
            {ruleset.packageName}
          </h2>
          <p className="font-body-sm text-body-sm text-espresso-600">
            Tier key {ruleset.packageId} · {ruleset.packageType}
          </p>
        </div>
        <Badge tone={packageActiveTone(ruleset.isActive)}>
          {packageActiveLabel(ruleset.isActive)}
        </Badge>
      </div>

      <dl className="grid grid-cols-1 sm:grid-cols-2 gap-3 bg-surface-container-low rounded-xl p-4">
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Net pax rate
          </dt>
          <dd className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            {formatCurrency(ruleset.amountPax)}
          </dd>
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Cost ceiling per pax
          </dt>
          <dd className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            {formatCurrency(ruleset.costCeilingPerPax)}
          </dd>
        </div>
        <div className="flex flex-col gap-0.5 sm:col-span-2">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Minimum contract value
          </dt>
          <dd className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            {formatCurrency(ruleset.minimumContractValue)}
          </dd>
          <dd className="font-caption text-caption text-espresso-600">
            A derived floor on the contract — not a charge, and it never enters a booking
            total.
          </dd>
        </div>
      </dl>

      {/* The tier's commercial terms — read-only here, edited in the tier form. */}
      <div className="flex flex-col gap-2 rounded-xl border border-artisan-border p-4">
        <h3 className="font-headline-sm text-headline-sm text-espresso-900">
          The tier&apos;s commercial terms
        </h3>
        <dl className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <div className="flex flex-col gap-0.5">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Tier type
            </dt>
            <dd>
              <PackageTypeChip packageType={ruleset.packageType} />
            </dd>
          </div>
          <div className="flex flex-col gap-0.5">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Rate per guest
            </dt>
            <dd className="font-data-md text-data-md text-espresso-900 font-bold break-words">
              {formatCurrency(ruleset.amountPax)}
            </dd>
          </div>
          <div className="flex flex-col gap-0.5">
            <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Minimum pax
            </dt>
            <dd className="font-data-md text-data-md text-espresso-900 font-bold">
              {ruleset.minPax === null ? EM_DASH : `${formatCount(ruleset.minPax)} ${PAX_UNIT}`}
            </dd>
          </div>
        </dl>
        <div>
          <Button variant="secondary" size="sm" icon={Pencil} onClick={onEditTierTerms}>
            Edit tier terms
          </Button>
        </div>
      </div>

      {/* The tier's spatial coverage — a checklist, from the coverage matrix payload. */}
      <div className="flex flex-col gap-2 rounded-xl border border-artisan-border p-4">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <h3 className="font-headline-sm text-headline-sm text-espresso-900">
            Spatial coverage
          </h3>
          <span className="font-caption text-caption text-espresso-600">
            {activeCovered.length} of {coveredMunicipalities.length} active
          </span>
        </div>

        {coveredMunicipalities.length === 0 ? (
          <EmptyState
            icon={MapPin}
            title="This tier covers no municipality"
            description="Attach a municipality on the location workbench to offer the tier outside the home zone."
          />
        ) : (
          <ul className="flex flex-col gap-1.5">
            {coveredMunicipalities.map((row) => (
              <li key={row.packageAreaCoverageId} className="flex items-center gap-2 text-espresso-700">
                <input
                  type="checkbox"
                  checked={row.isActive}
                  readOnly
                  aria-label={`${row.areaName} coverage`}
                  className="h-4 w-4 accent-culinary-600"
                />
                <span className="font-body-sm text-body-sm break-words">{row.areaName}</span>
                <span className="font-caption text-caption text-espresso-400">
                  {row.isActive ? 'active' : 'inactive'}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Card>
  )
}
