import { BadgeCheck, Circle, Save, Stamp } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { cn } from '@/utils/cn'
import { BOOKING_WORKFLOW_STEPS } from '../bookingVocabulary'

export interface WorkbenchHeaderProps {
  /** The booking number the server assigned. `null` until the booking is saved. */
  bookingNumber: string | null
  /**
   * The 1-based index of the step the operator is on.
   *
   * It is a **form affordance** — the first step whose inputs are still
   * incomplete — and never a business figure.
   */
  currentStep: number
  onSaveDraft: () => void
  onConfirm: () => void
  isSaving: boolean
  /** `false` while the draft cannot be saved yet. */
  canSave: boolean
  /** `false` while the booking cannot be confirmed yet. */
  canConfirm: boolean
}

/**
 * The workbench's own header block (spec §5.3).
 *
 * It renders **inside** the application shell — the application keeps one
 * authenticated shell, so the navigation, the session and the route guard are
 * identical on every screen (DEC-110). The reference's separate compact workbench
 * app bar is therefore **not** reproduced; this block carries the booking
 * number, the step indicator and the two actions instead.
 *
 * The booking number is a **placeholder** until the booking is saved: its
 * contractual format embeds the surrogate identifier, so no number can be
 * shown before the write (spec §9).
 */
export function WorkbenchHeader({
  bookingNumber,
  currentStep,
  onSaveDraft,
  onConfirm,
  isSaving,
  canSave,
  canConfirm,
}: WorkbenchHeaderProps) {
  return (
    <div className="bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 p-space-md flex flex-col lg:flex-row lg:items-center justify-between gap-space-md">
      <div className="flex flex-col gap-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
            Reservation Workbench
          </h1>
          {bookingNumber === null ? (
            <Badge tone="neutral" icon={Circle}>
              Booking number assigned on save
            </Badge>
          ) : (
            <Badge tone="info" icon={Stamp}>
              {bookingNumber}
            </Badge>
          )}
        </div>
        <p className="font-body-sm text-body-sm text-espresso-600">
          Compose the client, the tier, the schedule and the deposit. Every peso is priced by
          the server.
        </p>
      </div>

      <div className="flex items-center gap-2 shrink-0 flex-wrap">
        <Button variant="secondary" icon={Save} onClick={onSaveDraft} disabled={!canSave || isSaving}>
          {isSaving ? 'Saving…' : 'Save draft'}
        </Button>
        <Button
          variant="primary"
          icon={BadgeCheck}
          onClick={onConfirm}
          disabled={!canConfirm || isSaving}
        >
          {isSaving ? 'Saving…' : 'Confirm booking'}
        </Button>
      </div>

      <ol
        aria-label="Reservation workflow"
        className="flex flex-wrap items-center gap-x-2 gap-y-1 lg:basis-full lg:border-t lg:border-surface-container lg:pt-space-sm"
      >
        {BOOKING_WORKFLOW_STEPS.map((step, index) => {
          const stepNumber = index + 1
          const isCurrent = stepNumber === currentStep
          const isDone = stepNumber < currentStep
          return (
            <li key={step} className="flex items-center gap-2">
              <span
                className={cn(
                  'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-caption font-bold',
                  isCurrent
                    ? 'bg-culinary-50 text-culinary-800 border border-culinary-200'
                    : 'bg-surface-container text-espresso-600 border border-artisan-border',
                )}
              >
                <span className="font-mono">{stepNumber}</span>
                <span>{step}</span>
                {isCurrent ? (
                  <span className="font-caption text-caption uppercase">in progress</span>
                ) : null}
                {isDone ? (
                  <span className="sr-only">completed</span>
                ) : null}
              </span>
              {index < BOOKING_WORKFLOW_STEPS.length - 1 ? (
                <span className="text-espresso-200" aria-hidden="true">
                  /
                </span>
              ) : null}
            </li>
          )
        })}
      </ol>
    </div>
  )
}
