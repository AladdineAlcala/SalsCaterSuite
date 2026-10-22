import { Badge } from '@/components/ui/Badge'
import type { BookingPaymentState } from '@/types/booking.types'
import { getPaymentStateMeta } from '../bookingVocabulary'

export interface BookingPaymentStateChipProps {
  /** The BR-28 payment state the API published. */
  paymentState: BookingPaymentState | string | null | undefined
  className?: string
}

/**
 * The booking **payment-state** chip.
 *
 * The payment state is a **separate vocabulary** from the lifecycle status
 * (DEC-117): `AwaitingDeposit`, `PartiallyPaid` and `Settled` answer "how far
 * along is the payment?", while the lifecycle status answers "where is the
 * booking?". The two never share a name.
 *
 * The chip always carries its text label beside the tone (WCAG 1.4.1).
 */
export function BookingPaymentStateChip({ paymentState, className }: BookingPaymentStateChipProps) {
  const meta = getPaymentStateMeta(paymentState)
  return (
    <Badge tone={meta.tone} icon={meta.icon} className={className}>
      {meta.label}
    </Badge>
  )
}
