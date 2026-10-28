import { Utensils } from 'lucide-react'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { EmptyState } from '@/components/feedback/EmptyState'
import { formatCurrency, formatDecimal } from '@/utils/currencyFormatter'
import type { BookingMenuCourse } from '@/types/booking.types'
import type { BookingMenuDraftItem } from '../bookingVocabulary'
import {
  PREP_INSTRUCTION_MAX_LENGTH,
  NOT_STATED,
  assessMenuQuota,
  validatePrepInstruction,
} from '../bookingVocabulary'

export interface BookingMenuCourseBoardProps {
  /** The tier's course rules with their qualified pools (contract §15.1). */
  courses: BookingMenuCourse[]
  /** The course named by the active tab, or `null` for every course. */
  activeCourseId: number | null
  /** The dish-pool search term. */
  search: string
  /** The draft selection. */
  selections: BookingMenuDraftItem[]
  /** `true` when the menu-change window has closed. */
  isLocked: boolean
  onAddDish: (courseId: number, menuId: string) => void
  onRemoveDish: (courseId: number, menuId: string) => void
  onPrepInstructionChange: (courseId: number, menuId: string, value: string) => void
}

const PICKER_CLASSES =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm disabled:opacity-60 disabled:cursor-not-allowed'

const INPUT_CLASSES =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm disabled:opacity-60 disabled:cursor-not-allowed'

/**
 * The course list (spec §5.3).
 *
 * One card per course rule, in the tier's order. The card carries the rule,
 * its **qualified pool** (the only dishes that can be chosen — AC-BKG-52), the
 * chosen dish with its ingredient note, portion, food cost and the chef's
 * per-event instruction, and the **quota count as text** (WCAG 1.4.1).
 *
 * The **allergen chip area is a capability gap**: `Menu` carries no allergen
 * field, so no tag is invented (DEC-150).
 */
export function BookingMenuCourseBoard({
  courses,
  activeCourseId,
  search,
  selections,
  isLocked,
  onAddDish,
  onRemoveDish,
  onPrepInstructionChange,
}: BookingMenuCourseBoardProps) {
  const visibleCourses =
    activeCourseId === null
      ? courses
      : courses.filter((course) => course.courseId === activeCourseId)

  const quotas = assessMenuQuota(
    courses.map((course) => ({
      courseId: course.courseId,
      courseName: course.courseName,
      requiredQuota: course.requiredQuota,
    })),
    selections,
  )

  const term = search.trim().toLowerCase()

  if (visibleCourses.length === 0) {
    return (
      <EmptyState
        icon={Utensils}
        title="This tier states no course rule"
        description="There is no qualified pool to choose from, so the standard run is empty too — no dish selection can be recorded for this booking."
      />
    )
  }

  return (
    <div className="flex flex-col gap-space-md min-w-0" data-testid="booking-menu-course-board">
      {visibleCourses.map((course, index) => {
        const quota = quotas.find((entry) => entry.courseId === course.courseId)
        const chosen = selections.filter(
          (selection) => selection.courseId === course.courseId,
        )
        const chosenIds = new Set(chosen.map((selection) => selection.menuId))
        const available = course.pool
          .filter((dish) => !chosenIds.has(dish.menuId))
          .filter(
            (dish) =>
              term.length === 0 || dish.menuName.toLowerCase().includes(term),
          )
        const quotaMet = chosen.length >= course.requiredQuota

        return (
          <article
            key={course.courseId}
            data-testid="booking-menu-course-card"
            className="bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 p-space-md flex flex-col gap-space-sm min-w-0"
          >
            <div className="flex items-start justify-between gap-2 flex-wrap">
              <div className="flex items-start gap-2 min-w-0">
                <span className="font-mono text-caption font-bold text-culinary-700 shrink-0 mt-0.5">
                  {String(index + 1).padStart(2, '0')}
                </span>
                <div className="min-w-0">
                  <h2 className="font-headline-sm text-headline-sm text-espresso-900 break-words">
                    {course.courseName}
                  </h2>
                  <p className="font-caption text-caption text-espresso-600 break-words">
                    {course.ruleDescription}
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-2 flex-wrap">
                <span className="font-caption text-caption text-espresso-600">
                  {quota
                    ? `${quota.selectedCount} of ${quota.requiredQuota} selected`
                    : `Quota ${course.requiredQuota}`}
                </span>
              </div>
            </div>

            <p className="font-caption text-caption text-espresso-400">
              Portion{' '}
              {course.portionGrams === null
                ? NOT_STATED
                : `${formatDecimal(course.portionGrams)} g / guest`}
            </p>

            {chosen.length === 0 ? (
              <p className="font-body-sm text-body-sm text-espresso-600 rounded-lg bg-surface-container-low border border-dashed border-artisan-border p-space-sm">
                This event is served the tier&apos;s standard run for{' '}
                {course.courseName.toLowerCase()}. Choose a dish to override it.
              </p>
            ) : (
              <ul className="flex flex-col gap-space-sm">
                {chosen.map((selection) => {
                  const dish = course.pool.find((entry) => entry.menuId === selection.menuId)
                  const name = dish?.menuName ?? selection.menuId
                  const instructionId = `booking-menu-prep-${course.courseId}-${selection.menuId}`
                  const instructionError = validatePrepInstruction(selection.prepInstruction)

                  return (
                    <li
                      key={selection.menuId}
                      className="rounded-lg border border-artisan-border bg-surface-container-low p-space-sm flex flex-col gap-1.5 min-w-0"
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <p className="font-body-sm text-body-sm font-bold text-espresso-900 break-words">
                            {name}
                          </p>
                          <p className="font-caption text-caption text-espresso-600 break-words">
                            {dish?.note ?? 'No ingredient note is stated.'}
                          </p>
                          <p className="font-caption text-caption text-espresso-400">
                            Food cost per guest {formatCurrency(dish?.foodCost)}
                            {dish?.price === null || dish?.price === undefined
                              ? ''
                              : ` · menu price ${formatCurrency(dish.price)}`}
                          </p>
                        </div>
                        <button
                          type="button"
                          onClick={() => onRemoveDish(course.courseId, selection.menuId)}
                          disabled={isLocked}
                          aria-label={`Remove ${name} from ${course.courseName}`}
                          className="font-caption text-caption text-espresso-600 underline hover:text-espresso-900 shrink-0 disabled:opacity-60 disabled:cursor-not-allowed"
                        >
                          Remove
                        </button>
                      </div>

                      <label
                        htmlFor={instructionId}
                        className="font-caption text-caption text-espresso-400 font-semibold uppercase"
                      >
                        Chef prep instruction — {course.courseName}
                      </label>
                      <input
                        id={instructionId}
                        name={instructionId}
                        type="text"
                        value={selection.prepInstruction}
                        maxLength={PREP_INSTRUCTION_MAX_LENGTH}
                        disabled={isLocked}
                        placeholder="Optional — e.g. sauce on the side"
                        aria-invalid={instructionError ? true : undefined}
                        aria-describedby={
                          instructionError ? `${instructionId}-error` : undefined
                        }
                        onChange={(event) =>
                          onPrepInstructionChange(
                            course.courseId,
                            selection.menuId,
                            event.target.value,
                          )
                        }
                        className={INPUT_CLASSES}
                      />
                      {instructionError ? (
                        <p
                          id={`${instructionId}-error`}
                          role="alert"
                          className="font-caption text-caption text-error"
                        >
                          {instructionError}
                        </p>
                      ) : null}
                    </li>
                  )
                })}
              </ul>
            )}

            {course.pool.length === 0 ? (
              <p className="font-caption text-caption text-espresso-600">
                No dish is qualified for this course, so nothing can be selected from it.
              </p>
            ) : (
              <div className="flex flex-col gap-1 min-w-0">
                <label
                  htmlFor={`booking-menu-pick-${course.courseId}`}
                  className="font-caption text-caption text-espresso-400 font-semibold uppercase"
                >
                  Change dish for {course.courseName}
                </label>
                <select
                  id={`booking-menu-pick-${course.courseId}`}
                  name={`booking-menu-pick-${course.courseId}`}
                  value=""
                  disabled={isLocked || quotaMet || available.length === 0}
                  onChange={(event) => {
                    if (event.target.value.length > 0) {
                      onAddDish(course.courseId, event.target.value)
                    }
                  }}
                  className={PICKER_CLASSES}
                >
                  <option value="">
                    {quotaMet
                      ? 'The course quota is met'
                      : available.length === 0
                        ? 'No qualified dish matches the search'
                        : 'Choose a qualified dish'}
                  </option>
                  {available.map((dish) => (
                    <option key={dish.menuId} value={dish.menuId}>
                      {dish.menuName}
                    </option>
                  ))}
                </select>
                <p className="font-caption text-caption text-espresso-400">
                  {course.pool.length} qualified{' '}
                  {course.pool.length === 1 ? 'dish' : 'dishes'} in this course
                  {term.length > 0 ? ` · ${available.length} match the search` : ''}
                </p>
              </div>
            )}

            <CapabilityGapNotice
              title="Allergen declaration unavailable"
              missing="No allergen field is stored against a dish, so this course cannot declare one."
              footnote="Deliberately excluded by DEC-150 — allergen data is not modelled."
              className="px-2 py-1.5"
            />
          </article>
        )
      })}
    </div>
  )
}
