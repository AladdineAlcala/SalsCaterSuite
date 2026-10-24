import { Link } from 'react-router-dom'
import { ArrowLeft, FileDown, RotateCcw, Save } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'

export interface BookingMenuHeaderProps {
  /** The contractual booking number. `null` while the booking is unread. */
  bookingNumber: string | null
  onReset: () => void
  onSave: () => void
  isSaving: boolean
  /** `false` while the draft cannot be saved (non-compliant or locked). */
  canSave: boolean
  /** `true` when the menu-change window has closed. */
  isLocked: boolean
}

/**
 * The menu editor's header bar (spec §5.3).
 *
 * Three actions: **Reset defaults** (clears the selection, subject to the
 * lock), **Tasting sheet (PDF)** — inactive, because no tasting-sheet document
 * exists (DEC-150) — and **Save selection**. The reference's *"Save & Lock
 * Selection"* is split deliberately: the button saves, and the **lock is
 * automatic at the cutoff** (DEC-149).
 */
export function BookingMenuHeader({
  bookingNumber,
  onReset,
  onSave,
  isSaving,
  canSave,
  isLocked,
}: BookingMenuHeaderProps) {
  return (
    <div className="bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 p-space-md flex flex-col gap-space-md">
      <Link
        to="/bookings"
        className="inline-flex items-center gap-1.5 font-caption text-caption font-semibold text-espresso-600 hover:text-espresso-900 w-fit rounded focus:outline-none focus:ring-2 focus:ring-culinary-500/20"
      >
        <ArrowLeft className="h-3.5 w-3.5" aria-hidden="true" />
        Back to the register
      </Link>

      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-space-md">
        <div className="flex flex-col gap-1 min-w-0">
          <p className="font-caption text-caption font-bold uppercase tracking-wider text-caramel-700">
            Live Menu Workbench
          </p>
          <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight break-words">
            Menu selection
          </h1>
          <p className="font-body-sm text-body-sm text-espresso-600">
            {bookingNumber === null
              ? 'The booking is being read from the server.'
              : `Booking ${bookingNumber} · the dishes this event is served.`}
          </p>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <Button
            variant="tertiary"
            icon={RotateCcw}
            onClick={onReset}
            disabled={isLocked || isSaving}
          >
            Reset defaults
          </Button>
          <Button
            variant="secondary"
            icon={FileDown}
            inactive
            title="No tasting-sheet document exists in this cycle (DEC-150)."
          >
            Tasting sheet (PDF)
          </Button>
          <Button
            variant="primary"
            icon={Save}
            onClick={onSave}
            disabled={!canSave || isSaving}
          >
            {isSaving ? 'Saving…' : 'Save selection'}
          </Button>
        </div>
      </div>

      <CapabilityGapNotice
        title="Tasting sheet unavailable"
        missing="No tasting-sheet document is generated: the definition, its approval and its layout are not ruled, so the action stays inactive rather than producing an invented sheet."
        footnote="Deliberately excluded by DEC-150 — the tasting sheet is not modelled."
      />
    </div>
  )
}
