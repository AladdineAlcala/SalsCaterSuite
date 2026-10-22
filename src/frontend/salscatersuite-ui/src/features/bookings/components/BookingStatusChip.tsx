import { Badge } from '@/components/ui/Badge'
import { getBookingStatusMeta } from '@/utils/bookingStatus'
import type { BookingStatus } from '@/types/dashboard.types'

export interface BookingStatusChipProps {
  /** The BR-10 lifecycle status the API published. */
  status: BookingStatus | string | null | undefined
  className?: string
}

/**
 * The booking **lifecycle** status chip.
 *
 * The vocabulary is the domain's own — the ten `BookingStatus` names resolved
 * through the shared `utils/bookingStatus.ts` — so the reference's `Finalized`
 * and `Invoiced` never appear (AC-BKG-07).
 *
 * The chip always carries its text label beside the tone, so the state is
 * legible without colour perception (WCAG 1.4.1).
 */
export function BookingStatusChip({ status, className }: BookingStatusChipProps) {
  const meta = getBookingStatusMeta(status)
  return (
    <Badge tone={meta.tone} icon={meta.icon} className={className}>
      {meta.label}
    </Badge>
  )
}
