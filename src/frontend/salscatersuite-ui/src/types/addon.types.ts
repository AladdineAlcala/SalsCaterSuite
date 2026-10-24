/**
 * The add-on master administration contracts (the owner's 2026-10-24 Settings → Add-ons
 * request).
 *
 * The list includes **active and inactive** rows and excludes the soft-deleted ones. The
 * booking picker keeps its own active-only `AddonMasterItemDto` (contract §16.1).
 */

export type AddonPricingBasis = 'Flat' | 'PerGuest'

/** One add-on of `GET /api/addons/manage`. */
export interface AddonAdminItem {
  addonId: number
  /** `null` = not stated. */
  addonCategoryId: number | null
  addonCategoryName: string | null
  /** `null` = not stated. */
  deptId: number | null
  deptName: string | null
  /** `null` = not stated; the server treats it as `Flat`. */
  pricingBasis: AddonPricingBasis | null
  description: string
  /** The unit amount — per guest for `PerGuest`, the whole line for `Flat`. */
  unitPrice: number
  unit: string | null
  isActive: boolean
}

/** The body of `POST /api/addons` and `PUT /api/addons/{id}`. */
export interface AddonUpsertRequest {
  addonCategoryId: number | null
  deptId: number | null
  pricingBasis: AddonPricingBasis | null
  description: string
  unitPrice: number
  unit: string | null
  isActive: boolean
}

/** The category master `GET /api/addons/categories`. */
export interface AddonCategoryLookup {
  addonCategoryId: number
  addonCategoryName: string
}

/** The read-only department lookup `GET /api/departments`. */
export interface DepartmentLookup {
  deptId: number
  deptName: string
}
