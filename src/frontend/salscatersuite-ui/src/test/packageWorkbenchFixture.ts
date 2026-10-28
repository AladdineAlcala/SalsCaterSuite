/**
 * TEST FIXTURE — not production data.
 *
 * A fixed, hand-written set of Package Configuration Workbench payloads used
 * by the component tests. Every value is chosen so the assertions can check
 * the exact rendering of the formatters, the server's null semantics, the two
 * compliance verdicts and the error contract.
 *
 * This file lives under `src/test/` and is never imported by application code.
 */
import type { MenuCourse } from '@/types/menu.types'
import type {
  AreaDto,
  PackageAreaCoverageMatrix,
  PackageCourseRulePreview,
  PackageCourseRuleset,
  PackageLocationPreview,
} from '@/types/package.types'

/**
 * The course taxonomy the server publishes.
 *
 * It is the population the rule editor offers and the only source of course
 * identifiers and names the client knows.
 */
export const packageCourseTaxonomyFixture: MenuCourse[] = [
  { courseId: 1, courseName: 'Appetizer' },
  { courseId: 4, courseName: 'Beef' },
  { courseId: 5, courseName: 'Salad' },
  { courseId: 9, courseName: 'Dessert' },
]

/**
 * The tier's course-coverage ruleset.
 *
 * Two rules — a Beef course with a quota of two and a Salad course with a
 * quota of one. The Beef pool holds four costed dishes and one dish with no
 * food cost, so the pool's costed count and the uncosted-dish rule are both
 * exercised.
 */
export const packageCourseRulesetFixture: PackageCourseRuleset = {
  packageId: 12,
  packageName: 'Wedding Grand Tier',
  packageCode: 'PKG-WED-01',
  packageType: 'Wedding',
  extendedLocationAmount: null,
  amountPax: 1450,
  minPax: 150,
  isActive: true,
  targetFoodCostPercentage: 35,
  targetMarginPercentage: 65,
  isBenchmarkConfigured: true,
  costCeilingPerPax: 507.5,
  minimumContractValue: 217500,
  standardRunFoodCostPerPax: 329.25,
  standardRunFoodCostPercentage: 22.71,
  baseGrossProfitPercentage: 77.29,
  rules: [
    {
      packageCourseRuleId: 701,
      courseId: 4,
      courseName: 'Beef',
      requiredQuota: 2,
      portionGrams: 120,
      ruleDescription: 'Enforce exactly 2 Beef selections',
      poolSize: 5,
      poolCostedCount: 4,
      poolAverageFoodCost: 275,
      pool: [
        { menuId: 'DS-201', menuName: 'Beef Royale', foodCost: 380, price: 1450 },
        { menuId: 'DS-202', menuName: 'Beef Salpicon', foodCost: 300, price: 1450 },
        { menuId: 'DS-203', menuName: 'Beef Caldereta', foodCost: 240, price: 1450 },
        { menuId: 'DS-204', menuName: 'Beef Steak', foodCost: 180, price: 1450 },
        { menuId: 'DS-205', menuName: 'Beef Sisig', foodCost: 0, price: 1450 },
      ],
    },
    {
      packageCourseRuleId: 702,
      courseId: 5,
      courseName: 'Salad',
      requiredQuota: 1,
      portionGrams: 80,
      ruleDescription: 'One seasonal salad is served',
      poolSize: 3,
      poolCostedCount: 2,
      poolAverageFoodCost: 54.25,
      pool: [
        { menuId: 'DS-301', menuName: 'Caesar Salad', foodCost: 60, price: 1450 },
        { menuId: 'DS-302', menuName: 'Green Salad', foodCost: 48.5, price: 1450 },
        { menuId: 'DS-303', menuName: 'Pasta Salad', foodCost: 0, price: 1450 },
      ],
    },
  ],
  generatedAt: '2026-10-22T02:15:00.1234567Z',
}

/** A tier that holds no course rule at all. */
export const emptyPackageCourseRulesetFixture: PackageCourseRuleset = {
  ...packageCourseRulesetFixture,
  rules: [],
  standardRunFoodCostPerPax: null,
  standardRunFoodCostPercentage: null,
  baseGrossProfitPercentage: null,
}

/**
 * A tier that carries no per-guest rate.
 *
 * Every money figure is `null` — unknown, never zero — so the client renders
 * an em dash and the simulators say why.
 */
export const unpricedPackageCourseRulesetFixture: PackageCourseRuleset = {
  ...packageCourseRulesetFixture,
  amountPax: null,
  costCeilingPerPax: null,
  minimumContractValue: null,
  standardRunFoodCostPercentage: null,
  baseGrossProfitPercentage: null,
}

/**
 * A quota-compliant selection that stays within the cost ceiling.
 *
 * Both verdicts are `true`.
 */
export const packageCourseRulePreviewFixture: PackageCourseRulePreview = {
  packageId: 12,
  amountPax: 1450,
  minPax: 150,
  pax: 150,
  targetFoodCostPercentage: 35,
  costCeilingPerPax: 507.5,
  selectedFoodCostPerPax: 480,
  foodCostPerPax: 480,
  effectiveFoodCostPercentage: 33.1,
  isCostKnown: true,
  isCeilingBreached: false,
  ceilingShortfallPerPax: 0,
  ceilingOveragePercentage: 0,
  stressPercentage: null,
  stressedFoodCostPerPax: null,
  stressedFoodCostPercentage: null,
  grossRevenue: 217500,
  foodCostTotal: 72000,
  grossProfit: 145500,
  grossProfitPercentage: 66.9,
  isQuotaCompliant: true,
  isCompliant: true,
  compliance: [
    {
      courseId: 4,
      courseName: 'Beef',
      requiredQuota: 2,
      selectedCount: 2,
      isCompliant: true,
      hasRule: true,
    },
    {
      courseId: 5,
      courseName: 'Salad',
      requiredQuota: 1,
      selectedCount: 1,
      isCompliant: true,
      hasRule: true,
    },
  ],
  selections: [
    { courseId: 4, courseName: 'Beef', menuId: 'DS-203', menuName: 'Beef Caldereta', foodCost: 240 },
    { courseId: 4, courseName: 'Beef', menuId: 'DS-204', menuName: 'Beef Steak', foodCost: 180 },
    { courseId: 5, courseName: 'Salad', menuId: 'DS-301', menuName: 'Caesar Salad', foodCost: 60 },
  ],
  generatedAt: '2026-10-22T02:16:00.1234567Z',
}

/**
 * A quota-compliant selection that **breaches** the cost ceiling.
 *
 * The two verdicts differ here: the quota verdict is `true` and the overall
 * verdict is `false`, because a breach warns and marks the selection
 * non-compliant (DEC-080).
 */
export const packageCourseRuleBreachPreviewFixture: PackageCourseRulePreview = {
  ...packageCourseRulePreviewFixture,
  selectedFoodCostPerPax: 740,
  foodCostPerPax: 740,
  effectiveFoodCostPercentage: 51.03,
  isCeilingBreached: true,
  ceilingShortfallPerPax: 232.5,
  ceilingOveragePercentage: 45.81,
  foodCostTotal: 111000,
  grossProfit: 106500,
  grossProfitPercentage: 48.97,
  isQuotaCompliant: true,
  isCompliant: false,
  selections: [
    { courseId: 4, courseName: 'Beef', menuId: 'DS-201', menuName: 'Beef Royale', foodCost: 380 },
    { courseId: 4, courseName: 'Beef', menuId: 'DS-202', menuName: 'Beef Salpicon', foodCost: 300 },
    { courseId: 5, courseName: 'Salad', menuId: 'DS-301', menuName: 'Caesar Salad', foodCost: 60 },
  ],
}

/**
 * A selection that names a dish with **no** food cost.
 *
 * The selected food cost is `null` — unknown, never zero — while the quota
 * verdict still counts the dish (DEC-087).
 */
export const packageCourseRuleUncostedPreviewFixture: PackageCourseRulePreview = {
  ...packageCourseRulePreviewFixture,
  selectedFoodCostPerPax: null,
  foodCostPerPax: null,
  effectiveFoodCostPercentage: null,
  isCostKnown: false,
  isCeilingBreached: false,
  ceilingShortfallPerPax: null,
  ceilingOveragePercentage: null,
  foodCostTotal: null,
  grossProfit: null,
  grossProfitPercentage: null,
  selections: [
    { courseId: 4, courseName: 'Beef', menuId: 'DS-203', menuName: 'Beef Caldereta', foodCost: 240 },
    { courseId: 4, courseName: 'Beef', menuId: 'DS-205', menuName: 'Beef Sisig', foodCost: null },
    { courseId: 5, courseName: 'Salad', menuId: 'DS-301', menuName: 'Caesar Salad', foodCost: 60 },
  ],
}

/**
 * A selection that names a course the tier holds **no** rule for.
 *
 * The quota verdict is `false` because of the unruled course.
 */
export const packageCourseRuleUnruledPreviewFixture: PackageCourseRulePreview = {
  ...packageCourseRulePreviewFixture,
  isQuotaCompliant: false,
  isCompliant: false,
  compliance: [
    ...packageCourseRulePreviewFixture.compliance,
    {
      courseId: 9,
      courseName: 'Dessert',
      requiredQuota: 0,
      selectedCount: 1,
      isCompliant: false,
      hasRule: false,
    },
  ],
  selections: [
    ...packageCourseRulePreviewFixture.selections,
    { courseId: 9, courseName: 'Dessert', menuId: 'DS-401', menuName: 'Chocolate Cake', foodCost: 120 },
  ],
}

/**
 * The tier's coverage matrix.
 *
 * Three municipalities: one with a rate and a logistics cost, one with a rate
 * and **no** logistics cost, and one with **no stated rate** that is not
 * active (DEC-078, DEC-081, DEC-086).
 */
export const packageAreaCoverageMatrixFixture: PackageAreaCoverageMatrix = {
  packageId: 12,
  packageName: 'Wedding Grand Tier',
  packageCode: 'PKG-WED-01',
  extendedLocationAmount: null,
  amountPax: 1450,
  minPax: 150,
  minimumContractValue: 217500,
  standardRunFoodCostPerPax: 329.25,
  baseGrossProfitPercentage: 77.29,
  coverageCount: 3,
  activeCoverageCount: 2,
  lowestAdditionalRate: 120,
  highestAdditionalRate: 350,
  lowestGrossProfitPercentage: 58.4,
  highestGrossProfitPercentage: 71.9,
  grossProfitCoverageCount: 2,
  areas: [
    {
      packageAreaCoverageId: 501,
      areaId: 4,
      areaName: 'Antipolo City',
      additionalRate: 120,
      distanceKm: 18,
      logisticsNote: 'Reefer Truck',
      logisticsCost: 3200,
      isActive: true,
    },
    {
      packageAreaCoverageId: 502,
      areaId: 5,
      areaName: 'Batangas City',
      additionalRate: 350,
      distanceKm: 96,
      logisticsNote: 'Reefer Truck & Mobile Gen',
      logisticsCost: null,
      isActive: true,
    },
    {
      packageAreaCoverageId: 503,
      areaId: 6,
      areaName: 'Lucena City',
      additionalRate: null,
      distanceKm: null,
      logisticsNote: null,
      logisticsCost: null,
      isActive: false,
    },
  ],
  generatedAt: '2026-10-22T02:20:00.1234567Z',
}

/** A tier that covers no municipality at all. */
export const emptyPackageAreaCoverageMatrixFixture: PackageAreaCoverageMatrix = {
  ...packageAreaCoverageMatrixFixture,
  coverageCount: 0,
  activeCoverageCount: 0,
  lowestAdditionalRate: null,
  highestAdditionalRate: null,
  lowestGrossProfitPercentage: null,
  highestGrossProfitPercentage: null,
  grossProfitCoverageCount: 0,
  areas: [],
}

/**
 * The out-of-town breakdown of a covered municipality that states a rate and
 * a logistics cost.
 */
export const packageLocationPreviewFixture: PackageLocationPreview = {
  packageId: 12,
  pax: 150,
  amountPax: 1450,
  areaId: 4,
  areaName: 'Antipolo City',
  additionalRate: 120,
  packageExtendedLocationRate: null,
  baseTotal: 217500,
  extendedTotal: 18000,
  effectiveRatePerPax: 1570,
  totalContractValue: 235500,
  foodCostPerPax: 329.25,
  foodCostTotal: 49387.5,
  logisticsCost: 3200,
  grossProfit: 182912.5,
  grossProfitPercentage: 77.67,
  generatedAt: '2026-10-22T02:21:00.1234567Z',
}

/**
 * The breakdown of a covered municipality that states a rate but **no**
 * logistics cost.
 *
 * The logistics band is omitted rather than shown as zero, and the gross
 * profit is `null` because an operand is unknown (DEC-081).
 */
export const packageLocationPreviewNoLogisticsFixture: PackageLocationPreview = {
  packageId: 12,
  pax: 150,
  amountPax: 1450,
  areaId: 5,
  areaName: 'Batangas City',
  additionalRate: 350,
  packageExtendedLocationRate: null,
  baseTotal: 217500,
  extendedTotal: 52500,
  effectiveRatePerPax: 1800,
  totalContractValue: 270000,
  foodCostPerPax: 329.25,
  foodCostTotal: 49387.5,
  logisticsCost: null,
  grossProfit: null,
  grossProfitPercentage: null,
  generatedAt: '2026-10-22T02:23:00.1234567Z',
}

/**
 * The breakdown of a municipality that states **no** rate and **no** logistics
 * cost.
 *
 * The contract value is still complete — an absent surcharge adds nothing —
 * while the gross profit is `null` because the logistics cost is unknown
 * (DEC-081, DEC-086).
 */
export const packageLocationPreviewNoRateFixture: PackageLocationPreview = {
  packageId: 12,
  pax: 150,
  amountPax: 1450,
  areaId: 6,
  areaName: 'Lucena City',
  additionalRate: null,
  packageExtendedLocationRate: null,
  baseTotal: 217500,
  extendedTotal: 0,
  effectiveRatePerPax: 1450,
  totalContractValue: 217500,
  foodCostPerPax: 329.25,
  foodCostTotal: 49387.5,
  logisticsCost: null,
  grossProfit: null,
  grossProfitPercentage: null,
  generatedAt: '2026-10-22T02:22:00.1234567Z',
}

/** The municipality catalogue. */
export const areaCatalogueFixture: AreaDto[] = [
  {
    areaId: 4,
    areaDetails: 'Antipolo City',
    additionalRate: 120,
    distanceKm: 18,
    logisticsNote: 'Reefer Truck',
    logisticsCost: 3200,
  },
  {
    areaId: 5,
    areaDetails: 'Batangas City',
    additionalRate: 350,
    distanceKm: 96,
    logisticsNote: 'Reefer Truck & Mobile Gen',
    logisticsCost: null,
  },
  {
    areaId: 6,
    areaDetails: 'Lucena City',
    additionalRate: null,
    distanceKm: null,
    logisticsNote: null,
    logisticsCost: null,
  },
  {
    areaId: 9,
    areaDetails: 'Tagaytay City',
    additionalRate: 480,
    distanceKm: 64,
    logisticsNote: 'Ambient Van',
    logisticsCost: 4500,
  },
]
