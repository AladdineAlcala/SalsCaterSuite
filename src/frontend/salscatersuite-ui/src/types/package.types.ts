/**
 * TypeScript mirror of the Packages & Buffets contract.
 *
 * Source: `business/packages-catalogue-feature-spec.md` §3 and §4, the
 * contract issued with DEC-067 .. DEC-072.
 *
 * Every monetary figure below is **server-computed** (DEC-002, DEC-068). The
 * browser formats and displays; it never derives a rate average, a rate range
 * or any other monetary aggregate from the tier list. The catalogue aggregate
 * is fetched from `GET /api/packages/summary` so the KPI ribbon and the tier
 * list can never disagree.
 *
 * `null` means *unknown*, never zero. A tier that carries no rate and a
 * summary whose population is empty both publish `null`, and the client
 * renders an em dash for them (contract rule 6, "no null-as-zero").
 */

/**
 * One buffet tier.
 *
 * `packageType` is **free text**. The six `PackageType` enum members
 * (`Regular`, `Wedding`, `Vip`, `Premier`, `Pm`, `Sd`) document the intended
 * vocabulary, but nothing constrains the column and legacy rows may carry
 * other values — so the client renders the stored value verbatim (DEC-070).
 */
export interface PackageDto {
  /** The internal identity key. */
  packageId: number
  /** The human-readable business code, `PKG-{TYPE}-{NN}` (owner request 2026-10-24). */
  packageCode: string
  /** The tier type — free text, rendered verbatim. */
  packageType: string
  /** The tier's display name. */
  description: string
  /** The per-guest rate. `null` = not priced. */
  amountPax: number | null
  /** The minimum headcount. `null` = not set. */
  minPax: number | null
  /**
   * The required applicable-pax classification ID (DEC-173). It is
   * **informational** — the lookup's label is never parsed as a numeric range,
   * and the member sets no booking headcount bound, no `MinPax` and no price.
   */
  numPaxId: number
  /**
   * The tier's own per-guest extended-location amount. Available only for Regular/Premier;
   * `null` = not stated.
   */
  extendedLocationAmount: number | null
  /** Whether the tier is selectable. */
  isActive: boolean
}

/**
 * The catalogue aggregate published by `GET /api/packages/summary`.
 *
 * It describes the **whole** catalogue, not the current filter scope, and it
 * is the only source of the screen's KPI figures (DEC-068).
 */
export interface PackageCatalogueSummary {
  /** Every non-soft-deleted tier. */
  totalPackages: number
  /** How many of those tiers are selectable. */
  activePackages: number
  /** How many of those tiers are not selectable. */
  inactivePackages: number
  /** How many of those tiers carry a per-guest rate. */
  pricedPackages: number
  /** KPI-26 — the mean of the priced rates. `null` when no tier is priced. */
  averageAmountPax: number | null
  /** KPI-27 — the lowest priced rate. `null` when no tier is priced. */
  lowestAmountPax: number | null
  /** KPI-27 — the highest priced rate. `null` when no tier is priced. */
  highestAmountPax: number | null
  /** KPI-28 — the lowest minimum headcount. `null` when no tier sets one. */
  lowestMinPax: number | null
  /** KPI-28 — the highest minimum headcount. `null` when no tier sets one. */
  highestMinPax: number | null
  /** The server UTC timestamp of the aggregate. */
  generatedAt: string
}

/** The body of `POST /api/packages` and `PUT /api/packages/{id}`. */
export interface PackageUpsertRequest {
  packageType: string
  description: string
  amountPax: number | null
  minPax: number | null
  /**
   * The required applicable-pax classification ID (DEC-173, contract §12.4/§12.5).
   * Missing, zero, or unknown IDs are rejected by the server; the member is
   * classification metadata only and prices nothing.
   */
  numPaxId: number
  /** Only valid for Regular/Premier; `null` for every other type. */
  extendedLocationAmount: number | null
}

/** The `201` body of `POST /api/packages`. */
export interface PackageCreatedResponse {
  packageId: number
}

/**
 * One applicable-pax classification of the read-only lookup (contract §12.8,
 * DEC-173).
 *
 * The API publishes exactly five fixed rows in ID order with their labels
 * verbatim — `15 to 19 Pax`, `20 to 29 Pax`, `30 to 49 Pax`, `80 to 100 Above`
 * and `Not Applicable to PackMeals`. The labels are never parsed as bounds and
 * option 5 is not coupled to the `Pm` tier type. The client renders whatever
 * the endpoint returned; it never hardcodes the options.
 */
export interface PackagePaxApplicable {
  /** The lookup identity the tier's `numPaxId` references. */
  numPaxId: number
  /** The classification label, stored and returned verbatim. */
  packageApplicablePax: string
}

/* ------------------------------------------------------------------------- *
 * REQ-PKG-02 — the Package Configuration Workbench.
 *
 * Source: `business/package-configuration-workbench-feature-spec.md` §4 and
 * `architecture/approved-api-contracts.md` section 13.
 *
 * The same null discipline applies here as in the catalogue section above:
 * `null` means *unknown*, never zero. A municipality with no stated rate
 * publishes `null` (DEC-086), a dish that is not costed makes the selected
 * food cost `null` (DEC-087) and a tier with no rate publishes `null` for
 * every money figure. The client renders an em dash for all of them.
 * ------------------------------------------------------------------------- */

/**
 * One dish of a course rule's **qualified pool** (contract §13.1).
 *
 * The pool is every non-soft-deleted dish of the rule's course. A dish whose
 * `foodCost` is `null` is in the pool but is **not costed** — it contributes
 * to no average and to no cost figure (DEC-087).
 */
export interface PackageCoursePoolDish {
  menuId: string
  menuName: string
  /** The dish's food cost. `null` when the dish carries none. */
  foodCost: number | null
  /** The dish's selling price. `null` when it carries none. */
  price: number | null
}

/**
 * One enforced course rule of a tier (contract §13.1).
 */
export interface PackageCourseRule {
  packageCourseRuleId: number
  courseId: number
  courseName: string
  /** The number of dishes the client must take from this course. */
  requiredQuota: number
  /** The standard portion per guest in grams. `null` = not specified. */
  portionGrams: number | null
  /** The enforced rule, in plain words. */
  ruleDescription: string
  /** The non-soft-deleted dishes of the course. */
  poolSize: number
  /** The pool's dishes whose food cost is greater than zero (DEC-087). */
  poolCostedCount: number
  /** The mean food cost of the pool's costed dishes. `null` when none is costed. */
  poolAverageFoodCost: number | null
  /** The pool's dishes. */
  pool: PackageCoursePoolDish[]
}

/**
 * The tier's course-coverage ruleset and its planning figures (contract §13.1).
 */
export interface PackageCourseRuleset {
  packageId: number
  packageName: string
  packageCode: string
  packageType: string
  /** The tier's own per-guest extended-location amount. `null` = not stated. */
  extendedLocationAmount: number | null
  /** The per-guest rate. `null` = not priced. */
  amountPax: number | null
  /** The headcount floor. `null` = not set. */
  minPax: number | null
  isActive: boolean
  /** The deployment-configured food-cost benchmark (DEC-040). */
  targetFoodCostPercentage: number
  /** `100 - targetFoodCostPercentage`. */
  targetMarginPercentage: number
  /** `false` when the configuration provider is absent. */
  isBenchmarkConfigured: boolean
  /** `amountPax * targetFoodCostPercentage / 100`. `null` when the tier is unpriced. */
  costCeilingPerPax: number | null
  /** `amountPax * minPax`. `null` when either is unset (DEC-079). */
  minimumContractValue: number | null
  /** The mean of each rule's costed pool, summed. A planning estimate (DEC-083). */
  standardRunFoodCostPerPax: number | null
  /** `standardRunFoodCostPerPax / amountPax * 100`. */
  standardRunFoodCostPercentage: number | null
  /** `(amountPax - standardRunFoodCostPerPax) / amountPax * 100`. */
  baseGrossProfitPercentage: number | null
  /** The tier's rules, ordered by course name. */
  rules: PackageCourseRule[]
  generatedAt: string
}

/** One dish the operator picked for a course (contract §13.3). */
export interface PackageCourseSelection {
  courseId: number
  menuId: string
}

/** One line of the rule-compliance verdict (contract §13.3). */
export interface PackageCourseCompliance {
  courseId: number
  courseName: string
  requiredQuota: number
  selectedCount: number
  isCompliant: boolean
  /** `false` when the selection names a course the tier holds no rule for. */
  hasRule: boolean
}

/** One selected dish and its food cost (contract §13.3). */
export interface PackageCourseSelectionResult {
  courseId: number
  courseName: string
  menuId: string
  menuName: string
  /** The dish's food cost. `null` when the dish is not costed (DEC-087). */
  foodCost: number | null
}

/**
 * The selection simulator and the headcount forecast (contract §13.3).
 *
 * `isQuotaCompliant` and `isCompliant` are **two different verdicts** (DEC-080):
 * the quota verdict covers the rules alone, the overall verdict is the quota
 * verdict **and** the absence of a cost-ceiling breach.
 */
export interface PackageCourseRulePreview {
  packageId: number
  amountPax: number | null
  minPax: number | null
  /** The resolved headcount: the requested one, otherwise the floor, otherwise `null`. */
  pax: number | null
  targetFoodCostPercentage: number
  costCeilingPerPax: number | null
  /** The sum of the selected dishes' food costs. `null` when unknown. */
  selectedFoodCostPerPax: number | null
  /** The selection's cost, otherwise the standard run. `null` when neither is available. */
  foodCostPerPax: number | null
  effectiveFoodCostPercentage: number | null
  /** `false` when the selected food cost is unknown. */
  isCostKnown: boolean
  /** `true` when the selected cost exceeds the ceiling. A breach **warns**. */
  isCeilingBreached: boolean
  /** `max(0, selected - ceiling)`. `null` when either operand is unknown. */
  ceilingShortfallPerPax: number | null
  /** The overage as a percentage of the ceiling. `null` when unknown. */
  ceilingOveragePercentage: number | null
  /** The echoed stress factor. `null` means no stress. */
  stressPercentage: number | null
  stressedFoodCostPerPax: number | null
  stressedFoodCostPercentage: number | null
  grossRevenue: number | null
  foodCostTotal: number | null
  grossProfit: number | null
  grossProfitPercentage: number | null
  /** The quota verdict alone. */
  isQuotaCompliant: boolean
  /** The overall verdict: the quota verdict AND no ceiling breach. */
  isCompliant: boolean
  compliance: PackageCourseCompliance[]
  selections: PackageCourseSelectionResult[]
  generatedAt: string
}

/** One covered municipality (contract §13.4). */
export interface PackageAreaCoverageRow {
  packageAreaCoverageId: number
  areaId: number
  /** `Area.AreaDetails`. */
  areaName: string
  /** The **municipality's** per-pax surcharge (DEC-078). `null` = not stated. */
  additionalRate: number | null
  distanceKm: number | null
  logisticsNote: string | null
  /** The per-event logistics cost. `null` = the band is omitted (DEC-081). */
  logisticsCost: number | null
  isActive: boolean
}

/**
 * The tier's coverage matrix and its planning figures (contract §13.4).
 */
export interface PackageAreaCoverageMatrix {
  packageId: number
  packageName: string
  packageCode: string
  /** The tier's own per-guest extended-location amount. `null` = not stated. */
  extendedLocationAmount: number | null
  amountPax: number | null
  minPax: number | null
  minimumContractValue: number | null
  /** The tier's standard run, read from the course ruleset. */
  standardRunFoodCostPerPax: number | null
  baseGrossProfitPercentage: number | null
  coverageCount: number
  activeCoverageCount: number
  /** The smallest surcharge among the **active** coverage. `null` when none is active. */
  lowestAdditionalRate: number | null
  highestAdditionalRate: number | null
  /** The smallest gross-profit percentage across the active coverage, at the floor. */
  lowestGrossProfitPercentage: number | null
  highestGrossProfitPercentage: number | null
  /** How many active coverage rows yielded a computable gross-profit figure. */
  grossProfitCoverageCount: number
  areas: PackageAreaCoverageRow[]
  generatedAt: string
}

/**
 * The out-of-town pricing simulator (contract §13.7).
 */
export interface PackageLocationPreview {
  packageId: number
  pax: number
  amountPax: number | null
  areaId: number | null
  areaName: string | null
  /** The municipality's per-pax surcharge. `null` when the municipality stated none. */
  additionalRate: number | null
  /** The tier's own per-guest extended-location amount. `null` = not stated. */
  packageExtendedLocationRate: number | null
  baseTotal: number | null
  /** `effectiveAdditionalRate * pax`. `null` when no area is selected. */
  extendedTotal: number | null
  effectiveRatePerPax: number | null
  totalContractValue: number | null
  foodCostPerPax: number | null
  foodCostTotal: number | null
  /** The municipality's per-event logistics cost. `null` = the band is omitted. */
  logisticsCost: number | null
  grossProfit: number | null
  grossProfitPercentage: number | null
  generatedAt: string
}

/** One municipality of the catalogue (contract §13.9). */
export interface AreaDto {
  areaId: number
  areaDetails: string
  /** The per-pax out-of-town surcharge. `null` = not stated (DEC-086). */
  additionalRate: number | null
  distanceKm: number | null
  logisticsNote: string | null
  /** The per-event logistics cost. `null` = not stated. */
  logisticsCost: number | null
}

/** The body of `POST /api/packages/{id}/course-rules` (contract §13.2). */
export interface PackageCourseRuleRequest {
  courseId: number
  requiredQuota: number
  portionGrams: number | null
  ruleDescription: string
}

/** The body of `PUT /api/packages/{id}/course-rules`. */
export interface PackageCourseRulesetRequest {
  rules: PackageCourseRuleRequest[]
}

/** The body of `POST /api/packages/{id}/course-rules/preview`. */
export interface PackageCourseRulePreviewRequest {
  selections: PackageCourseSelection[]
  stressPercentage: number | null
  pax: number | null
}

/** One entry of the coverage set (contract §13.5). */
export interface PackageAreaCoverageEntry {
  areaId: number
  isActive: boolean
}

/** The body of `PUT /api/packages/{id}/areas`. */
export interface PackageAreaCoverageRequest {
  coverages: PackageAreaCoverageEntry[]
}

/** The body of `POST /api/packages/{id}/areas`. */
export interface PackageAreaCoverageAttachRequest {
  areaId: number
  isActive: boolean
}

/** The body of `POST /api/packages/{id}/areas/preview`. */
export interface PackageLocationPreviewRequest {
  pax: number
  areaId: number | null
  /** An **empty array** is a valid "no selection" request; `null` is a `400`. */
  selections: PackageCourseSelection[]
}

/** The body of `POST /api/areas` and `PUT /api/areas/{id}` (contract §13.10). */
export interface AreaRequest {
  areaDetails: string
  additionalRate: number | null
  distanceKm: number | null
  logisticsNote: string | null
  logisticsCost: number | null
}

/** The `201` body of `POST /api/areas`. */
export interface AreaCreatedResponse {
  areaId: number
}

/** The `201` body of `POST /api/packages/{id}/areas`. */
export interface PackageAreaCoverageCreatedResponse {
  packageAreaCoverageId: number
}

/**
 * The `status` filter the client owns. An empty string means *no status
 * filter*.
 */
export type PackageStatusFilter = '' | 'Active' | 'Inactive'

/**
 * The `sort` keys the client owns.
 *
 * The list endpoint is unpaged, so the ordering is applied in the browser
 * over the server's list. An unpriced tier has no rate and therefore sorts
 * **last** in a rate ordering.
 */
export type PackageSortOption = 'RateDesc' | 'RateAsc' | 'NameAsc'
