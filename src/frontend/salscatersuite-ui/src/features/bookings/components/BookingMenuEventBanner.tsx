import { CalendarDays, ChefHat, CircleCheck, CircleDashed, Gauge, MapPin, Users } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { EM_DASH, formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import { formatEventDate } from '@/utils/dateFormatter'
import type { BookingDetails, BookingMenuSelectionDto } from '@/types/booking.types'
import { PAX_UNIT, formatClockTime } from '../bookingVocabulary'

export interface BookingMenuEventBannerProps {
  menu: BookingMenuSelectionDto
  /**
   * The booking's frozen snapshot — the event identity, the venue, the
   * headcount and the surcharge. `null` while it is being read.
   */
  details: BookingDetails | null
  /** The draft selection's quota position, a form affordance (BR-15). */
  quota: {
    selected: number
    required: number
    /** `standard-run` when nothing is chosen — the tier's own run applies. */
    mode: 'standard-run' | 'complete' | 'incomplete'
  }
}

/**
 * The event identity banner (spec §5.3).
 *
 * It names the event and publishes three metric badges: the **course quota**,
 * the **batch engine** (its per-course mass is real, its ingredient-level yield
 * is a capability gap) and the **target margin** — the selection's gross profit
 * beside the tier's standard run. Every figure is the server's; the banner
 * never derives a total (DEC-002).
 *
 * The selection endpoint does not publish the client, the venue or the
 * headcount, so they are read from the booking's own details projection
 * (contract §1) and render an em dash until it arrives.
 */
export function BookingMenuEventBanner({
  menu,
  details,
  quota,
}: BookingMenuEventBannerProps) {
  const margin = menu.selectedGrossProfitPercentage ?? menu.standardRunGrossProfitPercentage
  const isSelectionMargin = menu.selectedGrossProfitPercentage !== null

  return (
    <div className="bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 p-space-md flex flex-col gap-space-md min-w-0">
      <div className="flex flex-col lg:flex-row lg:items-start justify-between gap-space-md min-w-0">
        <div className="flex flex-col gap-1 min-w-0">
          <h2 className="font-headline-md text-headline-md text-espresso-900 break-words">
            {details?.occasion ?? EM_DASH}
          </h2>
          <p className="font-caption text-caption text-espresso-600 break-all">
            {menu.bookingNumber} · {details?.customerName ?? EM_DASH}
          </p>
          <p className="font-body-sm text-body-sm text-caramel-700 font-semibold">
            {formatCount(menu.courses.length)}-Course Banquet Quota Active
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Badge
            tone={
              quota.mode === 'complete'
                ? 'success'
                : quota.mode === 'incomplete'
                  ? 'warning'
                  : 'info'
            }
            icon={quota.mode === 'complete' ? CircleCheck : CircleDashed}
          >
            Course quota {formatCount(quota.selected)} / {formatCount(quota.required)} ·{' '}
            {quota.mode === 'complete'
              ? 'Complete'
              : quota.mode === 'incomplete'
                ? 'Not complete'
                : 'Standard run'}
          </Badge>
          <Badge tone="info" icon={ChefHat}>
            Batch engine: {formatCount(details?.numberOfHeads)} {PAX_UNIT}
          </Badge>
          <Badge tone="neutral" icon={Gauge}>
            Target margin {formatPercentage(margin)}
          </Badge>
        </div>
      </div>

      <dl className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-space-sm border-t border-surface-container pt-space-sm min-w-0">
        <div className="flex flex-col gap-0.5 min-w-0">
          <dt className="font-caption text-caption text-espresso-400 uppercase flex items-center gap-1">
            <CalendarDays className="h-3 w-3" aria-hidden="true" />
            Event date
          </dt>
          <dd className="font-body-sm text-body-sm font-semibold text-espresso-900 break-words">
            {formatEventDate(menu.eventDate)}
            {details ? (
              <span className="block font-caption text-caption text-espresso-600">
                {formatClockTime(details.eventStartTime)} –{' '}
                {formatClockTime(details.eventEndTime)}
              </span>
            ) : null}
          </dd>
        </div>

        <div className="flex flex-col gap-0.5 min-w-0">
          <dt className="font-caption text-caption text-espresso-400 uppercase flex items-center gap-1">
            <Users className="h-3 w-3" aria-hidden="true" />
            Headcount
          </dt>
          <dd className="font-body-sm text-body-sm font-semibold text-espresso-900">
            {details === null
              ? EM_DASH
              : `${formatCount(details.numberOfHeads)} ${PAX_UNIT}`}
          </dd>
        </div>

        <div className="flex flex-col gap-0.5 min-w-0">
          <dt className="font-caption text-caption text-espresso-400 uppercase flex items-center gap-1">
            <MapPin className="h-3 w-3" aria-hidden="true" />
            Venue
          </dt>
          <dd className="font-body-sm text-body-sm font-semibold text-espresso-900 break-words">
            {details?.venue ?? EM_DASH}
            {details && details.extendedAreaAmount > 0 ? (
              <span className="block font-caption text-caption text-caramel-700">
                Out-of-town surcharge {formatCurrency(details.extendedAreaAmount)}
                {details.extendedAreaRatePerGuest === null
                  ? null
                  : ` · ${formatCurrency(details.extendedAreaRatePerGuest)} per guest`}
              </span>
            ) : null}
          </dd>
        </div>

        <div className="flex flex-col gap-0.5 min-w-0">
          <dt className="font-caption text-caption text-espresso-400 uppercase">Tier</dt>
          <dd className="font-body-sm text-body-sm font-semibold text-espresso-900 break-words">
            {menu.packageName}
            <span className="block font-caption text-caption text-espresso-600">
              {isSelectionMargin
                ? `Selection margin ${formatPercentage(menu.selectedGrossProfitPercentage)}`
                : `Standard-run margin ${formatPercentage(menu.standardRunGrossProfitPercentage)}`}
            </span>
          </dd>
        </div>
      </dl>
    </div>
  )
}
