/**
 * BR-10 booking lifecycle status → display metadata.
 *
 * The dashboard shows the **booking lifecycle status** published by the API
 * (`upcomingBookings[].status`). There is no BEO entity in the domain model,
 * so no BEO production state ("BEO Ready" / "Prep Pending" / "Finalized") is
 * ever rendered (spec §3.2, AC-F6).
 *
 * Every status is communicated by a **text label plus an icon**, never by
 * colour alone (spec §9.1, WCAG 1.4.1).
 */

import {
  CalendarClock,
  CalendarX,
  CheckCircle2,
  ChefHat,
  CircleCheck,
  CircleCheckBig,
  CircleSlash,
  Clock,
  UserX,
  Wallet,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { BookingStatus } from '@/types/dashboard.types'

export type StatusTone = 'success' | 'warning' | 'info' | 'neutral' | 'danger'

export interface BookingStatusMeta {
  /** Human label rendered inside the chip. */
  label: string
  tone: StatusTone
  icon: LucideIcon
}

export const BOOKING_STATUS_META: Record<BookingStatus, BookingStatusMeta> = {
  Pending: { label: 'Pending', tone: 'warning', icon: Clock },
  Confirmed: { label: 'Confirmed', tone: 'success', icon: CheckCircle2 },
  DepositPaid: { label: 'Deposit Paid', tone: 'success', icon: Wallet },
  FullyPaid: { label: 'Fully Paid', tone: 'success', icon: CircleCheckBig },
  InProgress: { label: 'In Progress', tone: 'info', icon: ChefHat },
  Completed: { label: 'Completed', tone: 'neutral', icon: CircleCheck },
  Cancelled: { label: 'Cancelled', tone: 'danger', icon: CircleSlash },
  NoShow: { label: 'No Show', tone: 'danger', icon: UserX },
  Rescheduled: { label: 'Rescheduled', tone: 'warning', icon: CalendarClock },
  Expired: { label: 'Expired', tone: 'neutral', icon: CalendarX },
}

/**
 * Resolves the display metadata for a status. An unrecognised value (a status
 * added server-side after this client shipped) degrades to a neutral chip that
 * still shows the raw status text.
 */
export function getBookingStatusMeta(
  status: BookingStatus | string | null | undefined,
): BookingStatusMeta {
  if (status && status in BOOKING_STATUS_META) {
    return BOOKING_STATUS_META[status as BookingStatus]
  }
  return {
    label: status && status.length > 0 ? status : 'Unknown',
    tone: 'neutral',
    icon: CircleSlash,
  }
}
