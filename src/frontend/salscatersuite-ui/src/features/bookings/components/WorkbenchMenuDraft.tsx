import { CircleCheck, CircleDashed, RotateCcw, Utensils } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import type { PackageCourseRule } from '@/types/package.types'
import type { BookingMenuDraftItem } from '../bookingVocabulary'
import {
  PREP_INSTRUCTION_MAX_LENGTH,
  assessMenuQuota,
  isMenuSelectionComplete,
  validatePrepInstruction,
} from '../bookingVocabulary'

export interface WorkbenchMenuDraftProps {
  /** The tier's course rules, each carrying its qualified pool. */
  rules: PackageCourseRule[]
  /** The dishes the operator has chosen. */
  selections: BookingMenuDraftItem[]
  /** Adds a dish to a course's draft selection. */
  onAddDish: (courseId: number, menuId: string) => void
  /** Removes a dish from a course's draft selection. */
  onRemoveDish: (courseId: number, menuId: string) => void
  /** Records the chef's per-event instruction for one chosen dish. */
  onPrepInstructionChange: (courseId: number, menuId: string, value: string) => void
  /** Clears the whole draft selection (the standard run then applies). */
  onReset: () => void
}

const INPUT_CLASSES =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm'

/**
 * The workbench's dish picker (spec §5.2, Column 2).
 *
 * It is the **same request shape** the packages simulator uses (AC-BKG-64):
 * the pools are the tier's own qualified pools, and the quota is the tier's
 * own rule. The picker is an **override** of the standard run, never a
 * requirement — an empty draft is legal and saves as "no selection" (DEC-131).
 *
 * The quota count is always **text** ("2 of 2 selected"), so the verdict is
 * legible without colour (WCAG 1.4.1). The server remains the authority; this
 * is the form's own affordance that keeps a save the API would refuse from
 * being offered.
 */
export function WorkbenchMenuDraft({
  rules,
  selections,
  onAddDish,
  onRemoveDish,
  onPrepInstructionChange,
  onReset,
}: WorkbenchMenuDraftProps) {
  const statuses = assessMenuQuota(rules, selections)
  const isComplete = isMenuSelectionComplete(rules, selections)

  return (
    <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5">
          <Utensils className="h-4 w-4 text-culinary-700" aria-hidden="true" />
          Event dish selection
        </h2>
        <div className="flex items-center gap-2 flex-wrap">
          <Badge
            tone={isComplete ? 'success' : 'warning'}
            icon={isComplete ? CircleCheck : CircleDashed}
          >
            {isComplete ? 'Quota complete' : 'Quota not met'}
          </Badge>
          <Button
            variant="tertiary"
            size="sm"
            icon={RotateCcw}
            onClick={onReset}
            disabled={selections.length === 0}
          >
            Reset selection
          </Button>
        </div>
      </div>

      <p className="font-caption text-caption text-espresso-600">
        Choosing nothing serves the tier&apos;s standard run. The selection never changes the
        price — it is a substitution, not a charge.
      </p>

      {rules.length === 0 ? (
        <EmptyState
          icon={Utensils}
          title="This tier states no course rule"
          description="There is no qualified pool to choose from, so no dish selection can be recorded for this booking."
        />
      ) : (
        <div className="flex flex-col gap-3">
          {rules.map((rule) => {
            const chosen = selections.filter(
              (selection) => selection.courseId === rule.courseId,
            )
            const remaining = rule.pool.filter(
              (dish) => !chosen.some((selection) => selection.menuId === dish.menuId),
            )
            const status = statuses.find((entry) => entry.courseId === rule.courseId)
            const selectId = `booking-menu-draft-${rule.courseId}`

            return (
              <div
                key={rule.courseId}
                className="rounded-lg border border-artisan-border bg-surface-container-low p-space-sm flex flex-col gap-2 min-w-0"
              >
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <span className="font-caption text-caption font-bold uppercase text-culinary-700">
                    {rule.courseName}
                  </span>
                  <span className="font-caption text-caption text-espresso-600">
                    {status
                      ? `${status.selectedCount} of ${status.requiredQuota} selected`
                      : `Quota ${rule.requiredQuota}`}
                  </span>
                </div>

                {rule.pool.length === 0 ? (
                  <p className="font-caption text-caption text-espresso-600">
                    No dish is qualified for this course, so nothing can be selected from it.
                  </p>
                ) : (
                  <select
                    id={selectId}
                    name={`booking-course-${rule.courseId}`}
                    value=""
                    disabled={remaining.length === 0}
                    onChange={(event) => {
                      if (event.target.value.length > 0) {
                        onAddDish(rule.courseId, event.target.value)
                      }
                    }}
                    aria-label={`Add a dish for ${rule.courseName}`}
                    className={INPUT_CLASSES}
                  >
                    <option value="">
                      {remaining.length === 0
                        ? 'Every qualified dish is already chosen'
                        : 'Choose a qualified dish'}
                    </option>
                    {remaining.map((dish) => (
                      <option key={dish.menuId} value={dish.menuId}>
                        {dish.menuName}
                      </option>
                    ))}
                  </select>
                )}

                {chosen.length > 0 ? (
                  <ul className="flex flex-col gap-2">
                    {chosen.map((selection) => {
                      const dish = rule.pool.find((entry) => entry.menuId === selection.menuId)
                      const name = dish?.menuName ?? selection.menuId
                      const instructionId = `booking-prep-${rule.courseId}-${selection.menuId}`
                      const instructionError = validatePrepInstruction(selection.prepInstruction)

                      return (
                        <li
                          key={selection.menuId}
                          className="flex flex-col gap-1 rounded border border-artisan-border bg-artisan-surface px-2 py-1.5"
                        >
                          <div className="flex items-center justify-between gap-2">
                            <span className="font-body-sm text-body-sm text-espresso-900 break-words">
                              {name}
                            </span>
                            <button
                              type="button"
                              onClick={() => onRemoveDish(rule.courseId, selection.menuId)}
                              aria-label={`Remove ${name} from ${rule.courseName}`}
                              className="font-caption text-caption text-espresso-600 underline hover:text-espresso-900 shrink-0"
                            >
                              Remove
                            </button>
                          </div>
                          <label
                            htmlFor={instructionId}
                            className="font-caption text-caption text-espresso-400 uppercase"
                          >
                            Chef prep instruction
                          </label>
                          <input
                            id={instructionId}
                            name={instructionId}
                            type="text"
                            value={selection.prepInstruction}
                            maxLength={PREP_INSTRUCTION_MAX_LENGTH}
                            placeholder="Optional — e.g. sauce on the side"
                            onChange={(event) =>
                              onPrepInstructionChange(
                                rule.courseId,
                                selection.menuId,
                                event.target.value,
                              )
                            }
                            aria-invalid={instructionError ? true : undefined}
                            aria-describedby={
                              instructionError ? `${instructionId}-error` : undefined
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
                ) : null}
              </div>
            )
          })}
        </div>
      )}
    </Card>
  )
}
