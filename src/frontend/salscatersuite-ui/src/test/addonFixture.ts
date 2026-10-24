/**
 * TEST FIXTURE — not production data.
 *
 * The add-on master administration payloads used by the Settings → Add-ons tests.
 */
import type {
  AddonAdminItem,
  AddonCategoryLookup,
  DepartmentLookup,
} from '@/types/addon.types'

export const addonAdminFixture: AddonAdminItem[] = [
  {
    addonId: 7,
    addonCategoryId: 2,
    addonCategoryName: 'Live stations',
    deptId: 1,
    deptName: 'Kitchen',
    pricingBasis: 'Flat',
    description: 'Carving station',
    unitPrice: 1500,
    unit: 'event',
    isActive: true,
  },
  {
    addonId: 8,
    addonCategoryId: 3,
    addonCategoryName: 'Dessert',
    deptId: 2,
    deptName: 'Pastry',
    pricingBasis: 'PerGuest',
    description: 'Dessert bar',
    unitPrice: 150,
    unit: 'guest',
    isActive: true,
  },
  {
    addonId: 9,
    addonCategoryId: null,
    addonCategoryName: null,
    deptId: null,
    deptName: null,
    pricingBasis: null,
    description: 'Cocktail welcome',
    unitPrice: 75,
    unit: null,
    isActive: false,
  },
]

export const addonCategoryLookupFixture: AddonCategoryLookup[] = [
  { addonCategoryId: 2, addonCategoryName: 'Live stations' },
  { addonCategoryId: 3, addonCategoryName: 'Dessert' },
]

export const departmentLookupFixture: DepartmentLookup[] = [
  { deptId: 1, deptName: 'Kitchen' },
  { deptId: 2, deptName: 'Pastry' },
]
