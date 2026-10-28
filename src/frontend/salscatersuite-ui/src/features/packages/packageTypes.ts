import type { BadgeTone } from '@/components/ui/Badge'
import type {
  PackageCourseRule,
  PackageCourseRuleRequest,
  PackageDto,
  PackageSortOption,
  PackageStatusFilter,
} from '@/types/package.types'

/**
 * The tier-type vocabulary of the packages feature (spec §3.1, DEC-070).
 *
 * `Package.PackageType` is a plain free-text column. The six names below are
 * the members of the `PackageType` enum and they are offered in the form as
 * **suggestions only** — they are never imposed on the stored value and the
 * client never rewrites what the server returned.
 */
export const KNOWN_PACKAGE_TYPES: readonly string[] = [
  'Regular',
  'Wedding',
  'Vip',
  'Premier',
  'Pm',
  'Sd',
]

/**
 * The two tier types that may carry the extended-location amount (owner request
 * 2026-10-24, mirroring `PackageSurchargeEligibility`).
 *
 * The comparison is the server's: trimmed, case-insensitive, **equality** on the
 * stored type column. A type such as "Premier Buffet" is not eligible.
 */
export function isExtendedLocationEligible(packageType: string): boolean {
  const trimmed = packageType.trim().toLowerCase()
  return trimmed === 'regular' || trimmed === 'premier'
}

/**
 * The text that accompanies the active-state chip.
 *
 * The state is always rendered with its label so it is legible without colour
 * perception (WCAG 1.4.1, AC-P05).
 */
export function packageActiveLabel(isActive: boolean): string {
  return isActive ? 'Active' : 'Inactive'
}

/** The chip tone that accompanies {@link packageActiveLabel}. */
export function packageActiveTone(isActive: boolean): BadgeTone {
  return isActive ? 'success' : 'neutral'
}

/** The label of the status filter, so the operator knows what is excluded. */
export function packageStatusFilterLabel(status: PackageStatusFilter): string {
  switch (status) {
    case 'Active':
      return 'Active only'
    case 'Inactive':
      return 'Inactive only'
    default:
      return 'All Tiers'
  }
}

/** The label of a sort key. */
export function packageSortLabel(sort: PackageSortOption): string {
  switch (sort) {
    case 'RateDesc':
      return 'Rate per guest (High to Low)'
    case 'RateAsc':
      return 'Rate per guest (Low to High)'
    case 'NameAsc':
      return 'Tier name (A to Z)'
    default:
      return 'Rate per guest (High to Low)'
  }
}

/**
 * The headcount unit used beside the minimum-pax figure.
 *
 * The unit is a caption, not a figure — it carries no number of its own.
 */
export const PAX_UNIT = 'heads'

/** The filters the client owns over the server's unpaged list (AC-P04). */
export interface PackageCatalogueFilters {
  /** Free-text search over the tier name and the tier type. */
  search: string
  /** One tier type, or `null` for the whole catalogue. */
  packageType: string | null
  /** The active-state filter, or an empty string for no filter. */
  status: PackageStatusFilter
}

/**
 * Applies the client's filters to the server's list.
 *
 * The list endpoint accepts no filter of its own, so the narrowing happens in
 * the browser over the payload the API returned. No monetary figure is ever
 * derived here — the search, the type and the state are the only things that
 * change.
 */
export function filterPackageTiers(
  tiers: PackageDto[],
  filters: PackageCatalogueFilters,
): PackageDto[] {
  const term = filters.search.trim().toLowerCase()

  return tiers.filter((tier) => {
    if (filters.packageType !== null && tier.packageType !== filters.packageType) {
      return false
    }
    if (filters.status === 'Active' && !tier.isActive) return false
    if (filters.status === 'Inactive' && tier.isActive) return false
    if (term.length > 0) {
      const name = tier.description.toLowerCase()
      const type = tier.packageType.toLowerCase()
      const code = (tier.packageCode ?? '').toLowerCase()
      if (!name.includes(term) && !type.includes(term) && !code.includes(term)) return false
    }
    return true
  })
}

/**
 * Orders the server's list for the operator.
 *
 * An unpriced tier has no rate, so it has no place in a rate ordering: it
 * sorts **last** in both rate orderings rather than pretending to be zero
 * (contract rule 6). The name ordering is alphabetical.
 */
export function sortPackageTiers(
  tiers: PackageDto[],
  sort: PackageSortOption,
): PackageDto[] {
  const byName = (left: PackageDto, right: PackageDto) =>
    left.description.localeCompare(right.description)

  if (sort === 'NameAsc') {
    return [...tiers].sort(byName)
  }

  const direction = sort === 'RateDesc' ? -1 : 1

  return [...tiers].sort((left, right) => {
    const leftPriced = left.amountPax !== null
    const rightPriced = right.amountPax !== null
    if (leftPriced !== rightPriced) {
      // The unpriced tier has no rate, so it sorts last.
      return leftPriced ? -1 : 1
    }
    if (leftPriced && rightPriced) {
      const difference = (left.amountPax as number) - (right.amountPax as number)
      if (difference !== 0) return difference * direction
    }
    return byName(left, right)
  })
}

/**
 * The distinct tier types present in the catalogue, each with its live count.
 *
 * The counts are counts of tiers — no monetary figure is derived from them.
 */
export function summarisePackageTypes(tiers: PackageDto[]): {
  packageType: string
  count: number
}[] {
  const counts = new Map<string, number>()
  for (const tier of tiers) {
    counts.set(tier.packageType, (counts.get(tier.packageType) ?? 0) + 1)
  }
  return Array.from(counts.entries())
    .map(([packageType, count]) => ({ packageType, count }))
    .sort((left, right) => left.packageType.localeCompare(right.packageType))
}

/* ------------------------------------------------------------------------- *
 * REQ-PKG-02 — the Package Configuration Workbench vocabulary.
 *
 * The labels below are the workbench's own wording. Every figure they
 * accompany is the server's (DEC-002); the client only chooses the word that
 * sits beside it. A status is always carried by a word beside its tone, never
 * by colour alone (WCAG 1.4.1).
 * ------------------------------------------------------------------------- */

/**
 * The wording used when a measurement is **unknown**.
 *
 * `null` means unknown, never zero (contract rule 6). A municipality that has
 * stated no rate reads "not stated" — it is not a zero surcharge, and the
 * money chain still yields a complete contract value (DEC-086).
 */
export const NOT_STATED = 'not stated'

/**
 * The wording that marks the standard run (DEC-083).
 *
 * It is a planning estimate derived from the qualified pools, never a
 * commitment and never a booked figure.
 */
export const PLANNING_ESTIMATE_NOTE =
  'A planning estimate derived from the qualified pools — not a commitment.'

/** The text that accompanies a coverage chip. */
export function packageCoverageLabel(isActive: boolean): string {
  return isActive ? 'Coverage active' : 'Coverage inactive'
}

/** The chip tone that accompanies {@link packageCoverageLabel}. */
export function packageCoverageTone(isActive: boolean): BadgeTone {
  return isActive ? 'success' : 'neutral'
}

/** The text that accompanies a per-rule compliance chip. */
export function packageComplianceLabel(isCompliant: boolean): string {
  return isCompliant ? 'Quota satisfied' : 'Quota not satisfied'
}

/** The chip tone that accompanies {@link packageComplianceLabel}. */
export function packageComplianceTone(isCompliant: boolean): BadgeTone {
  return isCompliant ? 'success' : 'danger'
}

/** The text that accompanies the package-level quota verdict (DEC-080). */
export function packageQuotaVerdictLabel(isQuotaCompliant: boolean): string {
  return isQuotaCompliant ? 'All course rules satisfied' : 'A course rule is not satisfied'
}

/** The chip tone that accompanies {@link packageQuotaVerdictLabel}. */
export function packageQuotaVerdictTone(isQuotaCompliant: boolean): BadgeTone {
  return isQuotaCompliant ? 'success' : 'danger'
}

/** The text that accompanies the overall compliance verdict (DEC-080). */
export function packageOverallVerdictLabel(isCompliant: boolean): string {
  return isCompliant ? 'Selection compliant' : 'Selection not compliant'
}

/** The chip tone that accompanies {@link packageOverallVerdictLabel}. */
export function packageOverallVerdictTone(isCompliant: boolean): BadgeTone {
  return isCompliant ? 'success' : 'danger'
}

/** The text that accompanies the cost-ceiling flag. */
export function packageCeilingLabel(isBreached: boolean): string {
  return isBreached ? 'Over the cost ceiling' : 'Within the cost ceiling'
}

/** The chip tone that accompanies {@link packageCeilingLabel}. */
export function packageCeilingTone(isBreached: boolean): BadgeTone {
  return isBreached ? 'warning' : 'success'
}

/**
 * The editable form state of one course rule.
 *
 * Every field is a **string**, because a form holds what the operator typed.
 * The numeric fields become numbers only in {@link toCourseRuleRequest}.
 */
export interface PackageCourseRuleDraft {
  /** The course the rule governs, as the identifier the API uses. */
  courseId: string
  /** The course's name, for the card and for the reviewer's reading. */
  courseName: string
  /** The number of dishes the client must take from the course. */
  requiredQuota: string
  /** The standard portion per guest in grams. */
  portionGrams: string
  /** The enforced rule, in plain words. */
  ruleDescription: string
}

/** The per-field client validation messages of the course-rule editor. */
export interface PackageCourseRuleDraftErrors {
  courseId?: string
  requiredQuota?: string
  portionGrams?: string
  ruleDescription?: string
}

/** The column limits the server enforces (contract §13.2). */
export const RULE_DESCRIPTION_MAX_LENGTH = 500

/**
 * Seeds the editor from a rule the server published.
 *
 * A `null` numeric field becomes an empty string so the operator sees an
 * empty box rather than a zero they did not type.
 */
export function courseRuleDraftFromRule(rule: {
  courseId: number
  courseName: string
  requiredQuota: number
  portionGrams: number | null
  ruleDescription: string
}): PackageCourseRuleDraft {
  return {
    courseId: String(rule.courseId),
    courseName: rule.courseName,
    requiredQuota: String(rule.requiredQuota),
    portionGrams: rule.portionGrams === null ? '' : String(rule.portionGrams),
    ruleDescription: rule.ruleDescription,
  }
}

/** A blank draft for a rule the operator is adding. */
export function emptyCourseRuleDraft(): PackageCourseRuleDraft {
  return {
    courseId: '',
    courseName: '',
    requiredQuota: '1',
    portionGrams: '',
    ruleDescription: '',
  }
}

/**
 * Reads a typed number.
 *
 * The browser only parses what the operator typed — it never derives a figure
 * from another figure.
 */
export function parseOptionalNumber(value: string): number | null {
  const trimmed = value.trim()
  if (trimmed.length === 0) return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

/**
 * The presence and sign checks the course-rule editor makes before it calls
 * the API.
 *
 * The wording is the **server's own**, so the operator reads one message
 * whether the request is rejected in the browser or by the API (DEC-075).
 */
export function validateCourseRuleDraft(
  draft: PackageCourseRuleDraft,
): PackageCourseRuleDraftErrors {
  const errors: PackageCourseRuleDraftErrors = {}

  if (draft.courseId.trim().length === 0) {
    errors.courseId = 'A course category must be selected.'
  }

  if (draft.requiredQuota.trim().length === 0) {
    errors.requiredQuota = 'Required quota must be at least 1.'
  } else {
    const quota = parseOptionalNumber(draft.requiredQuota)
    if (quota === null || !Number.isInteger(quota)) {
      errors.requiredQuota = 'Required quota must be at least 1.'
    } else if (quota < 1) {
      errors.requiredQuota = 'Required quota must be at least 1.'
    }
  }

  if (draft.portionGrams.trim().length > 0) {
    const portion = parseOptionalNumber(draft.portionGrams)
    if (portion === null) {
      errors.portionGrams = 'Portion weight must be a number.'
    } else if (portion < 0) {
      errors.portionGrams = 'Portion weight must be zero or greater.'
    }
  }

  if (draft.ruleDescription.trim().length === 0) {
    errors.ruleDescription = 'Rule description is required.'
  } else if (draft.ruleDescription.trim().length > RULE_DESCRIPTION_MAX_LENGTH) {
    errors.ruleDescription = 'Rule description must not exceed 500 characters.'
  }

  return errors
}

/**
 * Maps the editor's state onto the API request body (contract §13.2).
 *
 * `null` when a typed number could not be read — the API requires a whole
 * number where it expects one.
 */
export function toCourseRuleRequest(
  draft: PackageCourseRuleDraft,
): PackageCourseRuleRequest | null {
  const quota = parseOptionalNumber(draft.requiredQuota)
  const courseId = parseOptionalNumber(draft.courseId)
  const portion = parseOptionalNumber(draft.portionGrams)

  if (courseId === null || !Number.isInteger(courseId)) return null
  if (quota === null || !Number.isInteger(quota)) return null
  if (draft.portionGrams.trim().length > 0 && portion === null) return null

  return {
    courseId,
    requiredQuota: quota,
    portionGrams: portion === null ? null : Math.trunc(portion),
    ruleDescription: draft.ruleDescription.trim(),
  }
}

/**
 * The rule as the workbench currently holds it.
 *
 * The **editable** values come from the pending draft; the **pool** figures
 * come from the rule the server published, because only the server knows the
 * qualified pool of a course. A rule that has not been saved yet has no
 * published pool — the card says so rather than showing an empty pool as if
 * the course had no dishes.
 */
export function courseRuleView(
  serverRule: PackageCourseRule | undefined,
  draft: PackageCourseRuleDraft,
): PackageCourseRule {
  const quota = parseOptionalNumber(draft.requiredQuota)
  const portion = parseOptionalNumber(draft.portionGrams)
  const courseId = parseOptionalNumber(draft.courseId)

  return {
    packageCourseRuleId: serverRule?.packageCourseRuleId ?? 0,
    courseId: courseId !== null && Number.isInteger(courseId) ? courseId : 0,
    courseName: draft.courseName.length > 0 ? draft.courseName : (serverRule?.courseName ?? ''),
    requiredQuota:
      quota !== null && Number.isInteger(quota) ? quota : (serverRule?.requiredQuota ?? 0),
    portionGrams: portion !== null ? Math.trunc(portion) : null,
    ruleDescription: draft.ruleDescription,
    poolSize: serverRule?.poolSize ?? 0,
    poolCostedCount: serverRule?.poolCostedCount ?? 0,
    poolAverageFoodCost: serverRule?.poolAverageFoodCost ?? null,
    pool: serverRule?.pool ?? [],
  }
}
