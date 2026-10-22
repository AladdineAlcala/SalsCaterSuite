import type { MenuCourse, MenuCostingItem, MenuUpsertRequest } from '@/types/menu.types'
import { Card } from '@/components/ui/Card'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'

/** The editable attributes of a dish (spec §5.2). */
export interface MenuFormValues {
  /** `Menu.MenuId` — the recipe code. */
  menuId: string
  /** `Menu.MenuName` — the official banquet menu name. */
  menuName: string
  /** `Menu.CourseId` — the course position. */
  courseId: number | null
  /** `Menu.Note` — free-text note. */
  note: string
  /** `Menu.Price` — the selling price per pax. */
  price: string
  /** `Menu.FoodCost` — the manually entered food cost. */
  foodCost: string
  /** `Menu.Image` — the plating photo URL. */
  image: string
}

export const EMPTY_MENU_FORM_VALUES: MenuFormValues = {
  menuId: '',
  menuName: '',
  courseId: null,
  note: '',
  price: '',
  foodCost: '',
  image: '',
}

/** Seeds the form from the dish the API published. */
export function menuFormValuesFromItem(item: MenuCostingItem): MenuFormValues {
  return {
    menuId: item.menuId,
    menuName: item.menuName,
    courseId: item.courseId,
    note: item.note ?? '',
    price: String(item.sellingPrice),
    foodCost: String(item.foodCost),
    image: item.image ?? '',
  }
}

/**
 * Reads a typed amount.
 *
 * The browser only parses what the operator typed — it never derives a price
 * or a cost from another figure.
 */
export function parseAmount(value: string): number | null {
  const parsed = Number.parseFloat(value.trim())
  return Number.isFinite(parsed) ? parsed : null
}

/**
 * Maps the form onto the API request body (spec §6.4).
 *
 * `null` when the course has not been chosen — the API requires one.
 */
export function toUpsertRequest(values: MenuFormValues): MenuUpsertRequest | null {
  const price = parseAmount(values.price)
  const foodCost = parseAmount(values.foodCost)
  if (values.courseId === null || price === null || foodCost === null) {
    return null
  }

  return {
    menuId: values.menuId.trim(),
    courseId: values.courseId,
    menuName: values.menuName.trim(),
    note: values.note.trim().length > 0 ? values.note.trim() : null,
    price,
    foodCost,
  }
}

/**
 * The presence checks the form makes before it calls the API.
 *
 * They mirror the **required** column of spec §5.2 only. Every other rule —
 * the length limits, the uniqueness and the sign of the amounts — is the
 * server's to rule, and its `errors[]` are rendered verbatim (AC-M16).
 */
export function validateMenuForm(values: MenuFormValues): string[] {
  const errors: string[] = []

  if (values.menuId.trim().length === 0) {
    errors.push('Recipe ID is required.')
  }
  if (values.menuName.trim().length === 0) {
    errors.push('Official Banquet Menu Name is required.')
  }
  if (values.courseId === null) {
    errors.push('Course Category is required.')
  }
  if (parseAmount(values.price) === null) {
    errors.push('Selling price per pax is required.')
  }
  if (parseAmount(values.foodCost) === null) {
    errors.push('Food cost is required.')
  }

  return errors
}

export interface MenuRecipeFormProps {
  mode: 'create' | 'edit'
  values: MenuFormValues
  onChange: (field: keyof MenuFormValues, value: string | number | null) => void
  /** The seeded course positions the API published. */
  courses: MenuCourse[]
  isLoadingCourses: boolean
  /** The server's message for a duplicate recipe id, shown on the field. */
  menuIdError?: string | null
}

/**
 * The Dish Master Identity card (spec §5.2).
 *
 * Only the attributes that exist on `Menu` are editable. The reference's
 * **Commis Line / Lead**, **Batch Scale Basis**, **Package Tier Suitability**
 * and **Allergen Safety Tags** are not rendered as controls — the first is an
 * unmapped legacy integer, and the others have no model at all. Each of those
 * slots carries an explicit capability-gap notice instead (spec §5.5).
 */
export function MenuRecipeForm({
  mode,
  values,
  onChange,
  courses,
  isLoadingCourses,
  menuIdError,
}: MenuRecipeFormProps) {
  return (
    <Card elevation={1} className="flex flex-col gap-space-md">
      <div className="flex items-center justify-between gap-2 pb-1">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Banquet Dish Master Identity
        </h2>
        <span className="font-caption text-caption px-2 py-0.5 rounded bg-culinary-100 text-culinary-800 font-bold uppercase tracking-wider">
          {mode === 'edit' ? 'Edit Mode' : 'Create Mode'}
        </span>
      </div>

      <div className="flex flex-col gap-3.5">
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-1">
            <label
              htmlFor="menu-recipe-id"
              className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
            >
              Recipe ID
            </label>
            <span className="text-error text-caption" aria-hidden="true">
              *
            </span>
          </div>
          <input
            id="menu-recipe-id"
            name="menuId"
            type="text"
            value={values.menuId}
            onChange={(event) => onChange('menuId', event.target.value)}
            maxLength={50}
            required
            readOnly={mode === 'edit'}
            aria-required="true"
            aria-invalid={menuIdError ? true : undefined}
            aria-describedby={menuIdError ? 'menu-recipe-id-error' : undefined}
            className="h-10 px-3 rounded-lg bg-artisan-surface text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-data-sm text-data-sm w-full read-only:bg-surface-container-low"
          />
          {menuIdError ? (
            <p id="menu-recipe-id-error" role="alert" className="font-caption text-caption text-error">
              {menuIdError}
            </p>
          ) : null}
        </div>

        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-1">
            <label
              htmlFor="menu-name"
              className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
            >
              Official Banquet Menu Name
            </label>
            <span className="text-error text-caption" aria-hidden="true">
              *
            </span>
          </div>
          <input
            id="menu-name"
            name="menuName"
            type="text"
            value={values.menuName}
            onChange={(event) => onChange('menuName', event.target.value)}
            maxLength={150}
            required
            aria-required="true"
            className="h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-md text-body-md font-semibold w-full"
          />
        </div>

        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-1">
            <label
              htmlFor="menu-course"
              className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
            >
              Course Category
            </label>
            <span className="text-error text-caption" aria-hidden="true">
              *
            </span>
          </div>
          <select
            id="menu-course"
            name="courseId"
            value={values.courseId === null ? '' : String(values.courseId)}
            onChange={(event) =>
              onChange(
                'courseId',
                event.target.value === '' ? null : Number(event.target.value),
              )
            }
            required
            aria-required="true"
            className="h-10 px-3 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-md text-body-md w-full"
          >
            <option value="">
              {isLoadingCourses ? 'Loading courses…' : 'Select a course'}
            </option>
            {courses.map((course) => (
              <option key={course.courseId} value={String(course.courseId)}>
                {course.courseName}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label
            htmlFor="menu-note"
            className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
          >
            Notes
          </label>
          <textarea
            id="menu-note"
            name="note"
            rows={3}
            value={values.note}
            onChange={(event) => onChange('note', event.target.value)}
            maxLength={500}
            className="p-2.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm w-full resize-none"
          />
        </div>

        <div className="flex flex-col gap-1">
          <label
            htmlFor="menu-image"
            className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
          >
            Plating Photo URL
          </label>
          <input
            id="menu-image"
            name="image"
            type="url"
            value={values.image}
            onChange={(event) => onChange('image', event.target.value)}
            placeholder="https://…"
            className="h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm w-full"
          />
        </div>
      </div>

      {/* The reference's classification blocks have no model — each says so (spec §5.5). */}
      <CapabilityGapNotice
        title="Kitchen station assignment unavailable"
        missing="The commis line and lead are stored as an unmapped legacy integer with no lookup table, so there is nothing to assign a dish to."
        requirement="REQ-MNU-03"
      />
      <CapabilityGapNotice
        title="Package tier suitability unavailable"
        missing="There is no package tier on the dish and no package-tier rule the dish could be checked against."
        requirement="REQ-MNU-03"
      />
      <CapabilityGapNotice
        title="Allergen safety tags unavailable"
        missing="No allergen is recorded for a dish and no allergen source exists to derive one from."
        requirement="REQ-MNU-03"
      />
    </Card>
  )
}
