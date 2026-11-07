import { useCallback } from 'react'
import { ChefHat, Eraser, Utensils } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH, formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type {
  AreaDto,
  PackageCourseRuleset,
  PackageDto,
  PackagePaxApplicable,
} from '@/types/package.types'
import type { BookingQuote } from '@/types/booking.types'
import type { BookingWorkbenchDraft, BookingWorkbenchErrors } from '../bookingVocabulary'
import {
  NOT_STATED,
  PAX_UNIT,
  PLANNING_ESTIMATE_NOTE,
  RATE_DIVERGENCE_NOTE,
  resolveTierQuoteRate,
  supportsExtendedArea,
} from '../bookingVocabulary'
import type { BookingQuoteHeldRate } from '../bookingVocabulary'
import { WorkbenchComboboxField, WorkbenchSelectField, WorkbenchTextField } from './WorkbenchFields'

export interface WorkbenchTierEngineProps {
  draft: BookingWorkbenchDraft
  errors: BookingWorkbenchErrors
  onChange: (patch: Partial<BookingWorkbenchDraft>) => void
  /** The tier list the picker reads — active tiers of the selected type. */
  tiers: PackageDto[]
  /** `true` while a changed type/area key is still loading or placeholder data stands in. */
  isLoadingTiers: boolean
  /** The API-fed applicable-pax lookup; it is descriptive and never gates selection. */
  paxApplicables: PackagePaxApplicable[]
  /** `true` while the first applicable-pax lookup request is pending. */
  isLoadingPaxApplicables: boolean
  /** The municipality catalogue the area filter reads. */
  municipalities: AreaDto[]
  /** The distinct stored tier types the type filter offers (M7). */
  packageTypes: string[]
  /** The selected tier's approved course ruleset. */
  ruleset: PackageCourseRuleset | null
  isLoadingRuleset: boolean
  /** The server's quote — the source of the quoted rate and the base value. */
  quote: BookingQuote | null
  /** The last quote result, held with the package ID of the request that produced it. */
  lastResolvedQuote: BookingQuoteHeldRate | null
  /** `true` while the quote is being re-priced. */
  isRecalculating: boolean
}

/**
 * Column 2 — the package tier & course engine (spec §5.2).
 *
 * The tier controls read top-to-bottom in the enforced cascade
 * **Package type → Area filter → Buffet tier** (AC-BKG-92..95): the type is
 * required; for the eligible `Regular`/`Premier` types the required Area
 * filter sits directly above the picker (DEC-156, DEC-157); every type or
 * area change clears the selected tier in the same state update, and a
 * non-empty area prefills the event municipality without turning the
 * surcharge on (DEC-171).
 *
 * The **approved course specification** is the tier's real course-coverage
 * ruleset and its qualified pools (contract §13.1) — the approved data.
 *
 * The reference's **chef batching prep yield** has no source — the recipe,
 * ingredient and yield engine is blocked (REQ-MNU-03, DEC-059) — so it is a
 * capability gap.
 */
export function WorkbenchTierEngine({
  draft,
  errors,
  onChange,
  tiers,
  isLoadingTiers,
  paxApplicables,
  isLoadingPaxApplicables,
  municipalities,
  packageTypes,
  ruleset,
  isLoadingRuleset,
  quote,
  lastResolvedQuote,
  isRecalculating,
}: WorkbenchTierEngineProps) {
  const selectedTier = tiers.find((tier) => String(tier.packageId) === draft.packageId) ?? null
  const paxApplicableLabels = new Map(
    paxApplicables.map((item) => [item.numPaxId, item.packageApplicablePax]),
  )
  const paxClassificationLabel = (numPaxId: number) =>
    isLoadingPaxApplicables
      ? 'Loading classification…'
      : paxApplicableLabels.get(numPaxId) ?? 'Classification unavailable'

  const isTypeChosen = draft.packageType.trim().length > 0
  const isAreaOffered = isTypeChosen && supportsExtendedArea(draft.packageType)
  const isAreaChosen = draft.areaFilterId.trim().length > 0
  const gateSatisfied = isTypeChosen && !(isAreaOffered && !isAreaChosen)

  const areaFilterName =
    municipalities.find((area) => String(area.areaId) === draft.areaFilterId)?.areaDetails ?? null

  /*
   * The cascade updates are **atomic** (AC-BKG-94, DEC-171): one change event
   * emits one draft patch, so the tier is cleared together with the control
   * that moved — never through a refetch or a follow-up effect.
   */
  const handleTypeChange = useCallback(
    (value: string) => {
      onChange({
        packageType: value,
        packageId: '',
        // A switch between the two eligible types keeps the chosen area;
        // every non-eligible type hides and clears it.
        ...(supportsExtendedArea(value) ? {} : { areaFilterId: '' }),
      })
    },
    [onChange],
  )

  const handleAreaChange = useCallback(
    (value: string) => {
      onChange({
        areaFilterId: value,
        packageId: '',
        // DEC-171 — a non-empty area prefills the event municipality in the
        // same update; clearing the filter never reverse-syncs it back out,
        // and the prefill never turns the surcharge switch on.
        ...(value.length > 0 ? { extendedAreaId: value } : {}),
      })
    },
    [onChange],
  )

  const clearArea = useCallback(() => {
    onChange({ areaFilterId: '', packageId: '' })
  }, [onChange])

  const clearType = useCallback(() => {
    onChange({ packageType: '', packageId: '', areaFilterId: '' })
  }, [onChange])

  /*
   * The Rate per head readout (AC-BKG-89..91, AC-BKG-98): the quote for the
   * selected tier wins, the last resolved rate stands in while the quote is
   * re-priced for the same tier, and the catalogue `amountPax` backs the
   * readout before any quote exists — "not stated" when neither is stated.
   */
  const rateResolution = resolveTierQuoteRate({
    selectedPackageId: selectedTier === null ? null : selectedTier.packageId,
    catalogueRate: selectedTier === null ? null : selectedTier.amountPax,
    quote,
    lastResolvedQuote,
  })
  const rateText =
    rateResolution.statedRate === null ? NOT_STATED : formatCurrency(rateResolution.statedRate)

  /*
   * The no-stale exception is the **rate cell alone** (AC-BKG-90). Every
   * other quote-derived display — the deactivation verdict, the headcount
   * note and the planning estimate — shows nothing from a previous request
   * while the quote is being re-priced.
   */
  const isQuoteCurrent = quote !== null && !isRecalculating

  return (
    <div className="flex flex-col gap-space-md min-w-0">
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <h2 className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5">
            <Utensils className="h-4 w-4 text-culinary-700" aria-hidden="true" />
            Package tier &amp; headcount
          </h2>
          {isQuoteCurrent && !quote.isTierActive ? (
            <Badge tone="danger">The tier is deactivated</Badge>
          ) : null}
        </div>

        <WorkbenchSelectField
          id="booking-package-type"
          label="Package type"
          value={draft.packageType}
          onChange={handleTypeChange}
          options={packageTypes.map((type) => ({ value: type, label: type }))}
          placeholder="Select a package type"
          error={errors.packageType}
          hint={
            isTypeChosen
              ? 'Only the tiers of this type are offered. The out-of-town area filter follows the type.'
              : undefined
          }
          disabled={packageTypes.length === 0}
          required
        />

        {isAreaOffered ? (
          <WorkbenchComboboxField
            id="booking-area-filter"
            label="Area filter"
            value={draft.areaFilterId}
            onChange={handleAreaChange}
            options={municipalities.map((area) => ({
              value: String(area.areaId),
              label: area.areaDetails,
            }))}
            placeholder="Select an event area"
            error={errors.areaFilterId}
            hint="The tiers must cover the event area — the selector copies it to the event municipality"
            required
          />
        ) : null}

        <WorkbenchSelectField
          id="booking-package"
          label="Buffet tier"
          value={draft.packageId}
          onChange={(value) => onChange({ packageId: value })}
          options={tiers.map((tier) => ({
            value: String(tier.packageId),
            label: `${tier.description} · ${tier.packageType} · ${paxClassificationLabel(tier.numPaxId)}`,
          }))}
          placeholder={
            isLoadingTiers
              ? areaFilterName
                ? `Loading the tiers for ${areaFilterName}…`
                : 'Loading the tier catalogue…'
              : isTypeChosen
                ? 'Select a tier'
                : 'Select a package type first'
          }
          error={errors.packageId}
          disabled={!gateSatisfied || isLoadingTiers}
          hint={
            isTypeChosen
              ? isAreaOffered && !isAreaChosen
                ? 'Choose the event area to load its tiers'
                : 'The tiers of the selected type, narrowed to the event area when one is chosen'
              : 'Choose the package type to load its tiers'
          }
        />

        {gateSatisfied && !isLoadingTiers && tiers.length === 0 ? (
          // The previous area's list never stands in here — the loading gate
          // above holds the options until the new key settles (AC-BKG-95).
          <div className="flex flex-col gap-2">
            <p
              role="note"
              className="font-caption text-caption text-espresso-600 rounded-lg bg-surface-container-low p-space-sm border border-artisan-border"
            >
              {isAreaOffered
                ? `No active tier of type ${draft.packageType} covers ${
                    areaFilterName ?? 'the selected area'
                  }. Clear the area or clear the type to see the whole catalogue.`
                : `No active tier of type ${draft.packageType} is offered. Clear the type to see the whole catalogue.`}
            </p>
            <div className="flex items-center gap-2 flex-wrap">
              {isAreaOffered ? (
                <Button variant="secondary" size="sm" icon={Eraser} onClick={clearArea}>
                  Clear the area
                </Button>
              ) : null}
              <Button variant="secondary" size="sm" icon={Eraser} onClick={clearType}>
                Clear the type
              </Button>
            </div>
          </div>
        ) : null}

        {selectedTier ? (
          <>
            <div
              data-testid="booking-selected-tier"
              className="rounded-xl bg-surface-container-low border border-artisan-border p-space-sm flex flex-col gap-space-sm min-w-0"
            >
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
                    {rateText}
                  </span>
                  {/*
                   * The polite cue names the re-pricing without blanking the
                   * rate (AC-BKG-90) — the cell itself never falls back to an
                   * em dash while the quote is recalculated.
                   */}
                  <span
                    aria-live="polite"
                    data-testid="booking-rate-recalculating"
                    className="font-caption text-caption text-espresso-400 min-h-[1em]"
                  >
                    {isRecalculating ? 'Re-pricing…' : ''}
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

              <div className="flex flex-col">
                <span className="font-caption text-caption text-espresso-400 uppercase">
                  Applicable pax classification
                </span>
                <span className="font-body-sm text-body-sm text-espresso-700 break-words">
                  {paxClassificationLabel(selectedTier.numPaxId)}
                </span>
              </div>

              {isQuoteCurrent && quote.headcountFloorNote ? (
                <p className="font-caption text-caption text-caramel-700 break-words">
                  {quote.headcountFloorNote}
                </p>
              ) : null}

              {rateResolution.isDivergent ? (
                <p
                  data-testid="booking-rate-divergence"
                  aria-live="polite"
                  className="font-caption text-caption text-espresso-600 break-words"
                >
                  {RATE_DIVERGENCE_NOTE}
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

        {isQuoteCurrent ? (
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
