import { Badge } from '@/components/ui/Badge'
import type { BookingSelectionState } from '@/types/booking.types'
import { getSelectionStateMeta } from '../bookingVocabulary'

export interface BookingSelectionStateChipProps {
  /** Exactly one of the three selection states (BR-28, M2). */
  selectionState: BookingSelectionState | string | null | undefined
  className?: string
}

/**
 * The booking **selection** chip.
 *
 * The three states — *Menu: not chosen*, *Menu: chosen* and *Menu: locked* —
 * are mutually exclusive and are carried by a **text label beside an icon**,
 * never by colour alone (WCAG 1.4.1). An unrecognised value degrades to
 * "Menu: not stated" rather than guessing.
 */
export function BookingSelectionStateChip({
  selectionState,
  className,
}: BookingSelectionStateChipProps) {
  const meta = getSelectionStateMeta(selectionState)
  return (
    <Badge tone={meta.tone} icon={meta.icon} className={className}>
      {meta.label}
    </Badge>
  )
}
