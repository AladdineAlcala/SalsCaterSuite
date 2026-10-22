import { Plus, Save, X } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import type { MenuCourse } from '@/types/menu.types'
import type { PackageCourseRuleDraft, PackageCourseRuleDraftErrors } from '../packageTypes'
import { INCLUSION_TAG_MAX_LENGTH, RULE_DESCRIPTION_MAX_LENGTH } from '../packageTypes'

export interface PackageCourseRuleEditorProps {
  /** The draft the editor holds. */
  draft: PackageCourseRuleDraft
  /** Replaces the draft after a keystroke. */
  onChange: (draft: PackageCourseRuleDraft) => void
  /** The published course taxonomy the operator chooses from. */
  courses: MenuCourse[]
  /** The course ids already used by another rule of the pending set. */
  usedCourseIds: number[]
  /** The per-field client validation messages. */
  errors: PackageCourseRuleDraftErrors
  /** Adds the draft to the pending ruleset. */
  onSubmit: () => void
  /** Closes the editor without changing the pending ruleset. */
  onCancel: () => void
  /** `true` while the ruleset is being saved. */
  isSaving: boolean
  /** `true` when the editor holds a new rule rather than an existing one. */
  isNew: boolean
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-md text-body-md w-full'

const LABEL_CLASSES =
  'font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider'

/**
 * The course-rule editor (spec §5.2, column 2).
 *
 * The screen holds the whole unsaved ruleset and the save applies all of it at
 * once, because a ruleset is read as one document (DEC-085). Each field has an
 * explicit label and its error is linked with `aria-describedby`.
 *
 * The **upgrade add-on** is entered by its identifier: the approved model
 * publishes the add-on's description, price and unit on the rule, but no
 * add-on catalogue endpoint exists, so the client never invents one.
 */
export function PackageCourseRuleEditor({
  draft,
  onChange,
  courses,
  usedCourseIds,
  errors,
  onSubmit,
  onCancel,
  isSaving,
  isNew,
}: PackageCourseRuleEditorProps) {
  const availableCourses = courses.filter(
    (course) => !usedCourseIds.includes(course.courseId) || String(course.courseId) === draft.courseId,
  )

  // A rule whose course is not in the published taxonomy still shows its own
  // course, so the editor never silently drops the rule the server holds.
  const courseOptions =
    availableCourses.some((course) => String(course.courseId) === draft.courseId)
      ? availableCourses
      : [
          ...availableCourses,
          { courseId: Number(draft.courseId), courseName: draft.courseName || 'Current course' },
        ]

  const handleField = (field: keyof PackageCourseRuleDraft, value: string) => {
    onChange({ ...draft, [field]: value })
  }

  const handleCourseChange = (value: string) => {
    const course = courses.find((entry) => String(entry.courseId) === value)
    onChange({
      ...draft,
      courseId: value,
      courseName: course?.courseName ?? draft.courseName,
    })
  }

  return (
    <Card
      elevation={1}
      className="flex flex-col gap-space-md border-culinary-200"
      data-testid="package-course-rule-editor"
    >
      <h3 className="font-headline-sm text-headline-sm text-espresso-900">
        {isNew ? 'Add a course rule' : 'Edit the course rule'}
      </h3>

      <div className="flex flex-col gap-1">
        <label htmlFor="package-rule-course" className={LABEL_CLASSES}>
          Course category
        </label>
        <select
          id="package-rule-course"
          name="courseId"
          value={draft.courseId}
          onChange={(event) => handleCourseChange(event.target.value)}
          aria-required="true"
          aria-invalid={errors.courseId ? true : undefined}
          aria-describedby={
            errors.courseId ? 'package-rule-course-error' : 'package-rule-course-hint'
          }
          className={INPUT_CLASSES}
        >
          <option value="">Choose a course category</option>
          {courseOptions.map((course) => (
            <option key={course.courseId} value={String(course.courseId)}>
              {course.courseName}
            </option>
          ))}
        </select>
        <p id="package-rule-course-hint" className="font-caption text-caption text-espresso-600">
          The course categories the server publishes. A course already used by another rule
          of this tier is not offered.
        </p>
        {errors.courseId ? (
          <p id="package-rule-course-error" role="alert" className="font-caption text-caption text-error">
            {errors.courseId}
          </p>
        ) : null}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="flex flex-col gap-1">
          <label htmlFor="package-rule-quota" className={LABEL_CLASSES}>
            Required quota
          </label>
          <input
            id="package-rule-quota"
            name="requiredQuota"
            type="number"
            inputMode="numeric"
            step="1"
            min="1"
            value={draft.requiredQuota}
            onChange={(event) => handleField('requiredQuota', event.target.value)}
            aria-required="true"
            aria-invalid={errors.requiredQuota ? true : undefined}
            aria-describedby={
              errors.requiredQuota ? 'package-rule-quota-error' : 'package-rule-quota-hint'
            }
            className={INPUT_CLASSES}
          />
          <p id="package-rule-quota-hint" className="font-caption text-caption text-espresso-600">
            The number of dishes the client must take from this course. The compliance
            verdict compares the selected count with the quota exactly.
          </p>
          {errors.requiredQuota ? (
            <p
              id="package-rule-quota-error"
              role="alert"
              className="font-caption text-caption text-error"
            >
              {errors.requiredQuota}
            </p>
          ) : null}
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="package-rule-portion" className={LABEL_CLASSES}>
            Standard portion (grams)
          </label>
          <input
            id="package-rule-portion"
            name="portionGrams"
            type="number"
            inputMode="numeric"
            step="1"
            min="0"
            value={draft.portionGrams}
            onChange={(event) => handleField('portionGrams', event.target.value)}
            aria-invalid={errors.portionGrams ? true : undefined}
            aria-describedby={
              errors.portionGrams ? 'package-rule-portion-error' : 'package-rule-portion-hint'
            }
            className={INPUT_CLASSES}
          />
          <p id="package-rule-portion-hint" className="font-caption text-caption text-espresso-600">
            Leave empty when the course has no standard portion.
          </p>
          {errors.portionGrams ? (
            <p
              id="package-rule-portion-error"
              role="alert"
              className="font-caption text-caption text-error"
            >
              {errors.portionGrams}
            </p>
          ) : null}
        </div>
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="package-rule-description" className={LABEL_CLASSES}>
          Rule description
        </label>
        <textarea
          id="package-rule-description"
          name="ruleDescription"
          rows={3}
          value={draft.ruleDescription}
          onChange={(event) => handleField('ruleDescription', event.target.value)}
          maxLength={RULE_DESCRIPTION_MAX_LENGTH}
          aria-required="true"
          aria-invalid={errors.ruleDescription ? true : undefined}
          aria-describedby={
            errors.ruleDescription
              ? 'package-rule-description-error'
              : 'package-rule-description-hint'
          }
          className="px-3.5 py-2.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-md text-body-md w-full"
        />
        <p id="package-rule-description-hint" className="font-caption text-caption text-espresso-600">
          The rule the client is held to, in plain words.
        </p>
        {errors.ruleDescription ? (
          <p
            id="package-rule-description-error"
            role="alert"
            className="font-caption text-caption text-error"
          >
            {errors.ruleDescription}
          </p>
        ) : null}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="flex flex-col gap-1">
          <label htmlFor="package-rule-inclusion" className={LABEL_CLASSES}>
            Inclusion tag
          </label>
          <input
            id="package-rule-inclusion"
            name="inclusionTag"
            type="text"
            value={draft.inclusionTag}
            onChange={(event) => handleField('inclusionTag', event.target.value)}
            maxLength={INCLUSION_TAG_MAX_LENGTH}
            aria-invalid={errors.inclusionTag ? true : undefined}
            aria-describedby={
              errors.inclusionTag ? 'package-rule-inclusion-error' : 'package-rule-inclusion-hint'
            }
            className={INPUT_CLASSES}
          />
          <p id="package-rule-inclusion-hint" className="font-caption text-caption text-espresso-600">
            Your own label for the category&apos;s inclusion status. Leave empty for no tag.
          </p>
          {errors.inclusionTag ? (
            <p
              id="package-rule-inclusion-error"
              role="alert"
              className="font-caption text-caption text-error"
            >
              {errors.inclusionTag}
            </p>
          ) : null}
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="package-rule-upgrade" className={LABEL_CLASSES}>
            Tier-1 upgrade add-on
          </label>
          <input
            id="package-rule-upgrade"
            name="upgradeAddonId"
            type="number"
            inputMode="numeric"
            step="1"
            min="1"
            value={draft.upgradeAddonId}
            onChange={(event) => handleField('upgradeAddonId', event.target.value)}
            aria-invalid={errors.upgradeAddonId ? true : undefined}
            aria-describedby={
              errors.upgradeAddonId ? 'package-rule-upgrade-error' : 'package-rule-upgrade-hint'
            }
            className={INPUT_CLASSES}
          />
          <p id="package-rule-upgrade-hint" className="font-caption text-caption text-espresso-600">
            The add-on identifier. The server publishes its description, price and unit on
            the rule. Leave empty when the course has no upgrade.
          </p>
          {errors.upgradeAddonId ? (
            <p
              id="package-rule-upgrade-error"
              role="alert"
              className="font-caption text-caption text-error"
            >
              {errors.upgradeAddonId}
            </p>
        ) : null}
        </div>
      </div>

      <div className="flex items-center gap-2 flex-wrap">
        <Button variant="primary" icon={isNew ? Plus : Save} onClick={onSubmit} disabled={isSaving}>
          {isNew ? 'Add to the ruleset' : 'Update the rule'}
        </Button>
        <Button variant="secondary" icon={X} onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </Card>
  )
}
