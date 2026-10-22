import { Card } from '@/components/ui/Card'
import type { BookingWorkbenchDraft, BookingWorkbenchErrors } from '../bookingVocabulary'
import { WorkbenchTextField } from './WorkbenchFields'

export interface WorkbenchScheduleMatrixProps {
  draft: BookingWorkbenchDraft
  errors: BookingWorkbenchErrors
  onChange: (patch: Partial<BookingWorkbenchDraft>) => void
}

/**
 * The dispatch schedule matrix (spec §5.3, DEC-106).
 *
 * Four server-modelled times sit on the event: the **call time** and the
 * **pack-out time** are the two new frozen columns, the **program** is the
 * booking's existing event start and the **service end** is its event end.
 *
 * Every control holds a clock time and the model stores a `time(0)`, so the
 * day is the event's day — the client never derives a schedule.
 */
export function WorkbenchScheduleMatrix({
  draft,
  errors,
  onChange,
}: WorkbenchScheduleMatrixProps) {
  return (
    <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
      <h2 className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5">
        Dispatch schedule matrix
      </h2>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-space-sm">
        <WorkbenchTextField
          id="booking-call-time"
          label="Call time"
          type="time"
          value={draft.callTime}
          onChange={(value) => onChange({ callTime: value })}
          error={errors.callTime}
          hint="The kitchen call time"
        />

        <WorkbenchTextField
          id="booking-start-time"
          label="Program"
          type="time"
          value={draft.startTime}
          onChange={(value) => onChange({ startTime: value })}
          error={errors.startTime}
          hint="The event start"
        />

        <WorkbenchTextField
          id="booking-end-time"
          label="Service ends"
          type="time"
          value={draft.endTime}
          onChange={(value) => onChange({ endTime: value })}
          error={errors.endTime}
          hint="The event end"
        />

        <WorkbenchTextField
          id="booking-packout-time"
          label="Pack-out"
          type="time"
          value={draft.packoutTime}
          onChange={(value) => onChange({ packoutTime: value })}
          error={errors.packoutTime}
          hint="The pack-out time"
        />
      </div>
    </Card>
  )
}
