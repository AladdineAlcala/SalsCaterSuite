import { ChefHat, Utensils } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH, formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type { PackageCourseRuleset, PackageDto } from '@/types/package.types'
import type { BookingQuote } from '@/types/booking.types'
import type { BookingWorkbenchDraft, BookingWorkbenchErrors } from '../bookingVocabulary'
import { NOT_STATED, PAX_UNIT, PLANNING_ESTIMATE_NOTE } from '../bookingVocabulary'
import { WorkbenchSelectField, WorkbenchTextField } from './WorkbenchFields'

export interface WorkbenchTierEngineProps {
  draft: BookingWorkbenchDraft
  errors: BookingWorkbenchErrors
  onChange: (patch: Partial<BookingWorkbenchDraft>) => void
  /** The buffet tier catalogue the picker reads. */
  tiers: PackageDto[]
  isLoadingTiers: boolean
  /** The distinct stored tier types the type filter offers (M7). */
  packageTypes: string[]
  /** Whether the type filter is currently narrowing the list. */
  isTypeFiltered: boolean
  /** The selected tier's approved course ruleset. */
  ruleset: PackageCourseRuleset | null
  isLoadingRuleset: boolean
  /** The server's quote — the source of the rate and the base value. */
  quote: BookingQuote | null
  /** `true` while the quote is being re-priced. */
  isRecalculating: boolean
}

/**
 * Column 2 — the package tier & course engine (spec §5.3).
 *
 * The **approved course specification** is the tier's real course-coverage
 * ruleset and its qualified pools (contract §13.1) — the approved data.
 *
 * The **package-type filter** narrows the tier list by the stored type (M7).
 * The **area filter** that follows the type lives in the logistics block, and
 * the tier catalogue itself is filtered by the server through `areaId`
 * (DEC-157).
 *
 * The reference's **chef batching prep yield** has no source — the recipe,
 * ingredient and yield engine is blocked (REQ-MNU-03, DEC-059) — so it is a
 * capability gap.
 *
 * The tier's **code** is **not** rendered: `Package` carries no code column,
 * so the block shows the tier's identifier and its description (DEC-119).
 */
export function WorkbenchTierEngine({
  draft,
  errors,
  onChange,
  tiers,
  isLoadingTiers,
  packageTypes,
  isTypeFiltered,
  ruleset,
  isLoadingRuleset,
  quote,
  isRecalculating,
}: WorkbenchTierEngineProps) {
  const selectedTier = tiers.find((tier) => String(tier.packageId) === draft.packageId) ?? null

  return (
    <div className="flex flex-col gap-space-md min-w-0">
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <h2 className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5">
            <Utensils className="h-4 w-4 text-culinary-700" aria-hidden="true" />
            Package tier &amp; headcount
          </h2>
          {quote && !quote.isTierActive ? (
            <Badge tone="danger">The tier is deactivated</Badge>
          ) : null}
        </div>

        <WorkbenchSelectField
          id="booking-package-type"
          label="Package type"
          value={draft.packageType}
          onChange={(value) => onChange({ packageType: value })}
          options={packageTypes.map((type) => ({ value: type, label: type }))}
          placeholder="All package types"
          hint={
            isTypeFiltered
              ? 'Only the tiers of this type are offered. The out-of-town area filter follows the type.'
              : 'Narrow the tier list by its stored package type.'
          }
          disabled={packageTypes.length === 0}
        />

        <WorkbenchSelectField
          id="booking-package"
          label="Buffet tier"
          value={draft.packageId}
          onChange={(value) => onChange({ packageId: value })}
          options={tiers.map((tier) => ({
            value: String(tier.packageId),
            label: `${tier.description} · ${tier.packageType}`,
          }))}
          placeholder={isLoadingTiers ? 'Loading the tier catalogue…' : 'Select a tier'}
          error={errors.packageId}
          disabled={isLoadingTiers}
        />

        {isTypeFiltered && tiers.length === 0 ? (
          <p className="font-caption text-caption text-espresso-600">
            No active tier of this type is offered. Clear the type or the area filter to see the
            whole catalogue.
          </p>
        ) : null}

        {selectedTier ? (
          <>
            <div className="rounded-xl bg-surface-container-low border border-artisan-border p-space-sm flex flex-col gap-space-sm min-w-0">
              <div className="flex items-start justify-between gap-2 flex-wrap">
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-mono text-caption font-bold text-culinary-700">
                      Tier {selectedTier.packageId}
                    </span>
                    <Badge tone="info">{selectedTier.packageType}</Badge>
                  </div>
                  <h3 className="font-headline-sm text-headline-sm text-espresso-900 break-words">
                    {selectedTier.description}
                  </h3>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-space-sm">
                <div className="flex flex-col">
                  <span className="font-caption text-caption text-espresso-400 uppercase">
                    Headcount floor
                  </span>
                  <span className="font-mono text-data-md font-extrabold text-espresso-900">
                    {selectedTier.minPax === null ? NOT_STATED : formatCount(selectedTier.minPax)}
                  </span>
                </div>
                <div className="flex flex-col">
                  <span className="font-caption text-caption text-espresso-400 uppercase">
                    Rate per head
                  </span>
                  <span className="font-mono text-data-md font-extrabold text-espresso-900">
                    {isRecalculating
                      ? '—'
                      : quote === null
                        ? EM_DASH
                        : formatCurrency(quote.pricePerHead)}
                  </span>
                </div>
                <div className="flex flex-col">
                  <span className="font-caption text-caption text-espresso-400 uppercase">
                    Base value
                  </span>
                  <span className="font-mono text-data-md font-extrabold text-culinary-700">
                    {isRecalculating
                      ? '—'
                      : quote === null
                        ? EM_DASH
                        : formatCurrency(quote.baseAmount)}
                  </span>
                </div>
              </div>

              {quote?.headcountFloorNote ? (
                <p className="font-caption text-caption text-caramel-700 break-words">
                  {quote.headcountFloorNote}
                </p>
              ) : null}
            </div>

            <WorkbenchTextField
              id="booking-no-of-person"
              label="Contract headcount"
              type="number"
              value={draft.noOfPerson}
              onChange={(value) => onChange({ noOfPerson: value })}
              error={errors.noOfPerson}
              hint={`The guests the contract covers (${PAX_UNIT})`}
            />
          </>
        ) : null}

        {quote ? (
          <div className="rounded-lg bg-surface-container-low p-space-sm flex flex-col gap-1">
            <span className="font-caption text-caption text-espresso-400 uppercase">
              Tier planning estimate
            </span>
            <span className="font-mono text-body-sm font-bold text-culinary-700">
              Food cost {formatCurrency(quote.standardRunFoodCostPerPax)} per head • gross profit{' '}
              {formatPercentage(quote.baseGrossProfitPercentage)}
            </span>
            <span className="font-caption text-caption text-espresso-400">{PLANNING_ESTIMATE_NOTE}</span>
          </div>
        ) : null}
      </Card>

      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <h2 className="font-headline-sm text-headline-sm text-espresso-900">
            Approved course specification
          </h2>
          {ruleset ? (
            <span className="font-caption text-caption text-espresso-600">
              {ruleset.rules.length} course rules
            </span>
          ) : null}
        </div>

        {isLoadingRuleset ? (
          <p className="font-body-sm text-body-sm text-espresso-600">
            Loading the tier&apos;s course ruleset…
          </p>
        ) : null}

        {!isLoadingRuleset && ruleset === null ? (
          <EmptyState
            icon={Utensils}
            title="No tier selected"
            description="Select a buffet tier to read its approved course rules and their qualified pools."
          />
        ) : null}

        {ruleset && ruleset.rules.length === 0 ? (
          <EmptyState
            icon={Utensils}
            title="This tier states no course rule"
            description="The tier has no course coverage rule, so no course can be offered for this booking."
          />
        ) : null}

        {ruleset && ruleset.rules.length > 0 ? (
          <div className="flex flex-col gap-space-sm">
            {ruleset.rules.map((rule) => (
              <div
                key={rule.packageCourseRuleId}
                className="rounded-lg border border-artisan-border bg-surface-container-lowest p-space-sm flex flex-col gap-1 min-w-0"
              >
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <span className="font-mono text-caption font-bold text-culinary-700 uppercase">
                    {rule.courseName}
                  </span>
                  <Badge tone="info">Quota {formatCount(rule.requiredQuota)}</Badge>
                </div>
                {rule.ruleDescription ? (
                  <p className="font-body-sm text-body-sm text-espresso-700 break-words">
                    {rule.ruleDescription}
                  </p>
                ) : null}
                <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-caption text-espresso-600">
                  <span>
                    Portion{' '}
                    {rule.portionGrams === null ? NOT_STATED : `${rule.portionGrams} g`}
                  </span>
                  <span>Pool {formatCount(rule.poolSize)}</span>
                  <span>Costed {formatCount(rule.poolCostedCount)}</span>
                  <span>Average food cost {formatCurrency(rule.poolAverageFoodCost)}</span>
                </div>
              </div>
            ))}
          </div>
        ) : null}
      </Card>

      <CapabilityGapNotice
        title="Chef batching prep yield unavailable"
        missing="No recipe, ingredient or yield record computes a batch quantity, so the kitchen prep yields cannot be published."
        requirement="REQ-MNU-03"
        footnote="Deliberately excluded by DEC-059 — the recipe and yield engine is blocked on the owner’s decisions."
      >
        <span className="font-caption text-caption text-espresso-600 mt-0.5 inline-flex items-center gap-1">
          <ChefHat className="h-3 w-3" aria-hidden="true" />
          The kitchen works from the tier&apos;s course rules until the yield engine is ruled.
        </span>
      </CapabilityGapNotice>
    </div>
  )
}
