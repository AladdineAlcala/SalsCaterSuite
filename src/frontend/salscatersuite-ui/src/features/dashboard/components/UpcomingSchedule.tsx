import { CalendarDays } from 'lucide-react'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Badge } from '@/components/ui/Badge'
import { formatCount, formatCurrency } from '@/utils/currencyFormatter'
import { formatEventDate } from '@/utils/dateFormatter'
import { getBookingStatusMeta } from '@/utils/bookingStatus'
import type { DashboardUpcomingBooking } from '@/types/dashboard.types'

export interface UpcomingScheduleProps {
  bookings: DashboardUpcomingBooking[]
  /** The label of the active period, used by the empty state. */
  periodLabel: string
}

/**
 * Column 1 of the operations matrix — the next events (spec §9.2 item 5).
 *
 * The chip shows the **BR-10 booking lifecycle status**. There is no BEO
 * entity in the domain model, so no BEO production state is rendered
 * (spec §3.2, AC-F6).
 */
export function UpcomingSchedule({ bookings, periodLabel }: UpcomingScheduleProps) {
  return (
    <section className="bg-artisan-surface border border-artisan-border rounded-xl p-space-lg shadow-elevation-1 flex flex-col justify-between gap-space-md">
      <div className="flex flex-col gap-space-md">
        <div className="flex flex-wrap items-start justify-between gap-2 pb-1">
          <div className="min-w-0">
            <h3 className="font-headline-sm text-headline-sm text-espresso-900">
              Upcoming Banquet Schedule
            </h3>
            <p className="font-caption text-caption text-espresso-600">
              Next events with an event date on or after today
            </p>
          </div>
          <Badge tone="info">{formatCount(bookings.length)} upcoming</Badge>
        </div>

        {bookings.length === 0 ? (
          <EmptyState
            icon={CalendarDays}
            title="No upcoming events"
            description={`No booking has an event date on or after today for ${periodLabel}.`}
          />
        ) : (
          <ul className="flex flex-col gap-3">
            {bookings.map((booking) => {
              const status = getBookingStatusMeta(booking.status)
              return (
                <li
                  key={booking.transactionId}
                  className="p-space-sm rounded-lg bg-surface-container-low flex flex-col gap-2"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex flex-col min-w-0">
                      {/*
                       * The name wraps rather than truncates: in the three-column
                       * matrix at 1024px the column is narrow, and an ellipsised
                       * customer name ("Dela C...") loses the information the
                       * operations team needs to identify the booking.
                       */}
                      <span className="font-headline-sm text-headline-sm text-espresso-900 leading-snug break-words">
                        {booking.customerName}
                      </span>
                      {booking.occasion ? (
                        <span className="font-caption text-caption text-espresso-600 break-words">
                          {booking.occasion}
                        </span>
                      ) : null}
                    </div>
                    <Badge tone={status.tone} icon={status.icon} className="shrink-0">
                      {status.label}
                    </Badge>
                  </div>

                  <span className="font-caption text-caption text-espresso-600">
                    {formatEventDate(booking.eventDate)} &bull; {formatCount(booking.numberOfHeads)}{' '}
                    pax
                  </span>

                  <div className="flex items-center justify-between gap-2 pt-1">
                    <span className="inline-flex items-center px-1.5 py-0.5 rounded bg-artisan-surface text-espresso-700 font-caption text-caption truncate">
                      {booking.packageName ?? 'No package'}
                    </span>
                    <span className="font-mono text-data-md text-espresso-900 font-bold tabular-nums shrink-0">
                      {formatCurrency(booking.netAmountDue)}
                    </span>
                  </div>

                  <div className="flex items-center justify-between gap-2">
                    <span className="font-caption text-caption text-espresso-600">Balance due</span>
                    <span className="font-mono text-data-sm text-espresso-700 tabular-nums">
                      {formatCurrency(booking.balance)}
                    </span>
                  </div>
                </li>
              )
            })}
          </ul>
        )}
      </div>
    </section>
  )
}
