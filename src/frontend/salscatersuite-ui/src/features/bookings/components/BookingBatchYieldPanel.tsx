import { FileDown, Layers } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { EmptyState } from '@/components/feedback/EmptyState'
import { formatDecimal } from '@/utils/currencyFormatter'
import type { BookingMenuCourse } from '@/types/booking.types'
import { NOT_STATED } from '../bookingVocabulary'

export interface BookingBatchYieldPanelProps {
  /** The tier's course rules, each carrying its derived batch mass (DEC-145). */
  courses: BookingMenuCourse[]
  onDownloadBeo: () => void
  isDownloading: boolean
}

/**
 * The commissary batch yield panel (spec §5.3).
 *
 * The **batch mass is real**: `PortionGrams × NumberOfHeads / 1000` per rule,
 * derived by the server and `null` when the rule states no portion (DEC-145,
 * AC-BKG-77). It is labelled a **planning figure**.
 *
 * The **ingredient-level yield** (REQ-MNU-03), the **chef sign-off** and the
 * **quality stamp** have no source in the model, so each is an explicit
 * capability-gap notice rather than a fabricated panel (DEC-150, AC-BKG-81).
 *
 * The **BEO** is real: the server renders the Kitchen Banquet Event Order from
 * the booking's selection (contract §15.5, AC-BKG-82).
 */
export function BookingBatchYieldPanel({
  courses,
  onDownloadBeo,
  isDownloading,
}: BookingBatchYieldPanelProps) {
  return (
    <section
      aria-labelledby="booking-batch-heading"
      data-testid="booking-batch-yield-panel"
      className="bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 p-space-md flex flex-col gap-space-sm min-w-0"
    >
      <h2
        id="booking-batch-heading"
        className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5"
      >
        <Layers className="h-4 w-4 text-culinary-700" aria-hidden="true" />
        Commissary batch yield
      </h2>

      {courses.length === 0 ? (
        <EmptyState
          icon={Layers}
          title="No batch mass can be derived"
          description="The tier states no course rule, so no portion and no batch mass are published."
        />
      ) : (
        <dl className="flex flex-col gap-1.5">
          {courses.map((course) => (
            <div
              key={course.courseId}
              className="flex items-center justify-between gap-2 text-caption border-b border-surface-container pb-1.5"
            >
              <dt className="text-espresso-600 break-words">
                {course.courseName}
                <span className="block font-caption text-caption text-espresso-400">
                  {course.portionGrams === null
                    ? 'No portion stated'
                    : `${formatDecimal(course.portionGrams)} g / guest`}
                </span>
              </dt>
              <dd className="font-mono font-bold text-espresso-900 shrink-0">
                {course.batchMassKg === null
                  ? NOT_STATED
                  : `${formatDecimal(course.batchMassKg)} kg`}
              </dd>
            </div>
          ))}
        </dl>
      )}

      <p className="font-caption text-caption text-espresso-400">
        A batch mass is a planning figure derived from the portions and the contract headcount.
      </p>

      <CapabilityGapNotice
        title="Ingredient-level yield unavailable"
        missing="No recipe, ingredient or yield record computes an ingredient quantity, so only the course's batch mass can be published."
        requirement="REQ-MNU-03"
        footnote="Deliberately excluded by DEC-059 — the recipe and yield engine is blocked on the owner’s decisions."
        className="px-2 py-1.5"
      />

      <CapabilityGapNotice
        title="Chef sign-off unavailable"
        missing="The selection carries no approval state, so no chef can sign the menu off."
        footnote="Deliberately excluded by DEC-150 — no approval record is modelled."
        className="px-2 py-1.5"
      />

      <CapabilityGapNotice
        title="Quality stamp unavailable"
        missing="No compliance record exists, so no quality stamp can be attached to the selection."
        footnote="Deliberately excluded by DEC-150 — no compliance record is modelled."
        className="px-2 py-1.5"
      />

      <div className="flex flex-col gap-1 border-t border-surface-container pt-space-sm">
        <Button
          variant="secondary"
          icon={FileDown}
          onClick={onDownloadBeo}
          disabled={isDownloading}
        >
          {isDownloading ? 'Generating…' : 'Generate kitchen production order (PDF)'}
        </Button>
        <p className="font-caption text-caption text-espresso-400">
          The order is rendered from this booking&apos;s selection. A booking with no selection
          prints the tier&apos;s course rules and says that no dish selection is recorded.
        </p>
      </div>
    </section>
  )
}
