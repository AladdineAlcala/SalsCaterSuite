import { CalendarX } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'

export interface BookingOverdueChipProps {
  /** The overdue dimension the API published (BR-28, DEC-041). */
  isOverdue: boolean
  className?: string
}

/**
 * The overdue chip.
 *
 * Overdue is a **dimension of its own**, not a fourth payment state: a
 * partly-paid booking is `DepositPaid` **and** overdue, so folding the two
 * into one vocabulary would make the states overlap (BR-28).
 *
 * The chip is only rendered when the booking is overdue, and it always carries
 * its text label beside the tone (WCAG 1.4.1).
 */
export function BookingOverdueChip({ isOverdue, className }: BookingOverdueChipProps) {
  if (!isOverdue) return null
  return (
    <Badge tone="danger" icon={CalendarX} className={className}>
      Overdue
    </Badge>
  )
}
