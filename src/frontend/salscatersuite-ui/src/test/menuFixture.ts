/**
 * TEST FIXTURE — not production data.
 *
 * A fixed, hand-written set of Menu & Food Costing payloads used by the
 * component tests. Every value is chosen so the assertions can check the exact
 * rendering of the formatters, the server's null semantics and the error
 * contract.
 *
 * This file lives under `src/test/` and is never imported by application code.
 */
import type {
  MenuCostingItem,
  MenuCostingPreview,
  MenuCourse,
  MenuMasterResponse,
} from '@/types/menu.types'

export const menuCoursesFixture: MenuCourse[] = [
  { courseId: 1, courseName: 'Appetizer' },
  { courseId: 2, courseName: 'Soup' },
  { courseId: 3, courseName: 'Salad' },
  { courseId: 4, courseName: 'Main Course' },
  { courseId: 5, courseName: 'Dessert' },
  { courseId: 6, courseName: 'Beverage' },
]

export const menuDetailFixture: MenuCostingItem = {
  menuId: 'DS-104',
  menuName: 'Prime Roast Angus Ribeye with Truffle Demi-Glace',
  courseId: 4,
  courseName: 'Main Course',
  note: 'Carvery pass, hot hold above sixty degrees.',
  image: null,
  sellingPrice: 420.0,
  foodCost: 138.6,
  grossProfit: 281.4,
  foodCostPercentage: 33.0,
  grossMarginPercentage: 67.0,
  targetFoodCostPercentage: 32.0,
  isWithinTarget: false,
  marginVariancePercentage: 1.0,
}

export const menuMasterFixture: MenuMasterResponse = {
  summary: {
    totalDishes: 181,
    costedDishes: 142,
    uncostedDishes: 39,
    costingCoveragePercentage: 78.4,
    averageFoodCostPercentage: 28.6,
    averageFoodCostHeadroomPercentage: 3.4,
    targetFoodCostPercentage: 32.0,
    targetMarginPercentage: 68.0,
    isBenchmarkConfigured: true,
    averageSellingPrice: 320.0,
    marginAlertCount: 6,
    bestMarginCourse: {
      courseId: 4,
      courseName: 'Main Course',
      grossMarginPercentage: 74.2,
      averageSellingPrice: 485.0,
      dishCount: 42,
    },
  },
  courses: {
    total: 181,
    items: [
      { courseId: 1, courseName: 'Appetizer', dishCount: 12 },
      { courseId: 2, courseName: 'Soup', dishCount: 9 },
      { courseId: 3, courseName: 'Salad', dishCount: 11 },
      { courseId: 4, courseName: 'Main Course', dishCount: 42 },
      { courseId: 5, courseName: 'Dessert', dishCount: 19 },
      { courseId: 6, courseName: 'Beverage', dishCount: 14 },
    ],
  },
  items: [
    menuDetailFixture,
    {
      menuId: 'DS-082',
      menuName: 'Baked Jumbo Prawns Thermidor with Gruyère Crust',
      courseId: 4,
      courseName: 'Main Course',
      note: null,
      image: null,
      sellingPrice: 380.0,
      foodCost: 104.5,
      grossProfit: 275.5,
      foodCostPercentage: 27.5,
      grossMarginPercentage: 72.5,
      targetFoodCostPercentage: 32.0,
      isWithinTarget: true,
      marginVariancePercentage: -4.5,
    },
    {
      menuId: 'DS-045',
      menuName: 'Pan-Seared Rosemary Chicken Supreme in Creamy Dijon',
      courseId: 4,
      courseName: 'Main Course',
      note: null,
      image: null,
      sellingPrice: 210.0,
      foodCost: 48.3,
      grossProfit: 161.7,
      foodCostPercentage: 23.0,
      grossMarginPercentage: 77.0,
      targetFoodCostPercentage: 32.0,
      isWithinTarget: true,
      marginVariancePercentage: -9.0,
    },
    {
      menuId: 'DS-119',
      menuName: 'Slow-Braised USDA Beef Short Plate Kare-Kare Artisanal',
      courseId: 4,
      courseName: 'Main Course',
      note: null,
      image: null,
      sellingPrice: 295.0,
      foodCost: 91.45,
      grossProfit: 203.55,
      foodCostPercentage: 31.0,
      grossMarginPercentage: 69.0,
      targetFoodCostPercentage: 32.0,
      isWithinTarget: true,
      marginVariancePercentage: -1.0,
    },
  ],
  page: 1,
  pageSize: 12,
  totalCount: 181,
  totalPages: 16,
  generatedAt: '2026-10-20T09:15:00.1234567Z',
}

/**
 * The same catalogue with an empty margin population.
 *
 * `averageFoodCostPercentage`, `averageFoodCostHeadroomPercentage` and
 * `bestMarginCourse` are `null` — the client renders an em dash, never a zero
 * (spec §4.2, contract rule 6).
 */
export const menuNullSummaryFixture: MenuMasterResponse = {
  ...menuMasterFixture,
  summary: {
    ...menuMasterFixture.summary,
    totalDishes: 0,
    costedDishes: 0,
    uncostedDishes: 0,
    costingCoveragePercentage: 0,
    averageFoodCostPercentage: null,
    averageFoodCostHeadroomPercentage: null,
    averageSellingPrice: 0,
    marginAlertCount: 0,
    bestMarginCourse: null,
  },
  items: [],
  totalCount: 0,
  totalPages: 0,
}

/** A catalogue that holds no dish at all. */
export const emptyMenuCatalogueFixture: MenuMasterResponse = {
  ...menuNullSummaryFixture,
  courses: { total: 0, items: [] },
}

/**
 * A catalogue that holds dishes, but whose current filter matches none of
 * them. The two empty states are distinct (spec §4.8).
 *
 * `courses.total` stays positive — it is the pre-course count of the
 * search- and status-filtered scope — so the screen knows the catalogue is
 * not empty.
 */
export const noMatchMenuMasterFixture: MenuMasterResponse = {
  ...menuNullSummaryFixture,
  courses: menuMasterFixture.courses,
}

/** The server-computed margin preview (spec §6.2). */
export const menuPreviewFixture: MenuCostingPreview = {
  sellingPrice: 380.0,
  foodCost: 104.2,
  grossProfit: 275.8,
  foodCostPercentage: 27.42,
  grossMarginPercentage: 72.58,
  targetFoodCostPercentage: 32.0,
  targetMarginPercentage: 68.0,
  isWithinTarget: true,
  marginVariancePercentage: -4.58,
}
