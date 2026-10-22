/**
 * TypeScript mirror of the Menu & Food Costing contract.
 *
 * Source: `business/menu-food-costing-feature-spec.md` §6.
 *
 * Every financial figure below is **server-computed** (DEC-002). The browser
 * formats and displays; it computes no margin, no percentage and no cost
 * figure of its own. The margin preview is fetched from
 * `POST /api/menus/costing/preview` so the preview and the saved record can
 * never disagree.
 *
 * `null` means *unknown*, never zero. The summary's
 * `averageFoodCostPercentage`, `averageFoodCostHeadroomPercentage` and
 * `bestMarginCourse` are null when the margin population is empty, and the
 * client renders an em dash for them (contract rule 6, "no null-as-zero").
 */

/** One course position of the `CourseCategory` table. */
export interface MenuCourse {
  courseId: number
  courseName: string
}

/**
 * A course **with its dish count**.
 *
 * The master payload publishes these for the scope that matches the search
 * and status filters with **no course filter**. Every course is listed, even
 * one with no dishes, so the tab strip always shows the full taxonomy.
 */
export interface MenuCourseSummary extends MenuCourse {
  dishCount: number
}

/**
 * The course tab counts (spec §6.1).
 *
 * - `total` is the number of dishes matching the **search and status**
 *   filters with no course filter — the "All Dishes (n)" tab count.
 * - `items` are the same scope, per `CourseCategory`.
 *
 * A dish with no course is counted in `total` but has no tab, so
 * `Σ items[].dishCount` may be less than `total`. The client renders
 * "All Dishes (total)" and never shows the difference.
 */
export interface MenuCourses {
  total: number
  items: MenuCourseSummary[]
}

/**
 * KPI-23 — the course with the highest weighted gross margin.
 *
 * `null` when the margin population is empty.
 */
export interface MenuBestMarginCourse extends MenuCourse {
  grossMarginPercentage: number
  averageSellingPrice: number
  dishCount: number
}

/**
 * The catalogue summary (spec §6.1).
 *
 * It describes the **whole filtered result set**, not the current page.
 */
export interface MenuCostingSummary {
  /** The number of dishes in the filtered set. */
  totalDishes: number
  /** How many of those dishes carry a food cost. */
  costedDishes: number
  /** How many of those dishes carry no food cost. */
  uncostedDishes: number
  /** KPI-14 — costed ÷ total. `0` when the catalogue is empty. */
  costingCoveragePercentage: number
  /** KPI-22 — `null` when the margin population is empty. */
  averageFoodCostPercentage: number | null
  /**
   * `target − actual`, in percentage points. `null` when the actual is
   * `null`; **negative** when the catalogue is over target.
   */
  averageFoodCostHeadroomPercentage: number | null
  /** DEC-040 — the configured food-cost benchmark. The client never hardcodes it. */
  targetFoodCostPercentage: number
  /** The margin the benchmark implies. */
  targetMarginPercentage: number
  /** `false` when the benchmark falls back to the system default. */
  isBenchmarkConfigured: boolean
  /** The average selling price of the filtered set. */
  averageSellingPrice: number
  /** KPI-24 — the dishes whose food cost % exceeds the target. */
  marginAlertCount: number
  /** KPI-23 — `null` when the margin population is empty. */
  bestMarginCourse: MenuBestMarginCourse | null
}

/**
 * One dish of the catalogue with its server-computed costing.
 *
 * The same shape is returned by `GET /api/menus/{id}`.
 */
export interface MenuCostingItem {
  menuId: string
  menuName: string
  courseId: number
  courseName: string
  note: string | null
  /** A URL string — the `Menu.Image` column. */
  image: string | null
  sellingPrice: number
  foodCost: number
  grossProfit: number
  foodCostPercentage: number
  grossMarginPercentage: number
  targetFoodCostPercentage: number
  /** `foodCostPercentage ≤ targetFoodCostPercentage`. */
  isWithinTarget: boolean
  /** `foodCostPercentage − targetFoodCostPercentage`, in percentage points. */
  marginVariancePercentage: number
}

/** `GET /api/menus/master` response. */
export interface MenuMasterResponse {
  summary: MenuCostingSummary
  /**
   * The course tab counts — the search- and status-filtered scope with no
   * course filter applied.
   */
  courses: MenuCourses
  items: MenuCostingItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  /** The server UTC timestamp of the aggregate. */
  generatedAt: string
}

/**
 * `POST /api/menus/costing/preview` response (spec §6.2).
 *
 * The preview carries the server's benchmark, so the form never needs a
 * hardcoded target.
 */
export interface MenuCostingPreview {
  sellingPrice: number
  foodCost: number
  grossProfit: number
  foodCostPercentage: number
  grossMarginPercentage: number
  targetFoodCostPercentage: number
  targetMarginPercentage: number
  isWithinTarget: boolean
  /** In percentage points — positive is over target. */
  marginVariancePercentage: number
}

/** The body of `POST /api/menus` and `PUT /api/menus/{id}`. */
export interface MenuUpsertRequest {
  menuId: string
  courseId: number
  menuName: string
  note: string | null
  price: number
  foodCost: number
}

/** The `201` body of `POST /api/menus`. */
export interface MenuCreatedResponse {
  menuId: string
}

/**
 * The `status` filter accepted by the master endpoint. An empty string means
 * *no status filter*.
 */
export type MenuStatusFilter = '' | 'Costed' | 'Uncosted' | 'MarginAlert'

/** The `sort` key accepted by the master endpoint (spec §4.4). */
export type MenuSortOption =
  | 'FoodCostAsc'
  | 'FoodCostDesc'
  | 'PriceDesc'
  | 'PriceAsc'
  | 'MarginDesc'
  | 'NameAsc'
