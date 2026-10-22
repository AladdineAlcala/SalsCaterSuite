import type { LucideIcon } from 'lucide-react'
import { IconBadge } from '@/components/ui/IconBadge'
import type { IconBadgeTone } from '@/components/ui/IconBadge'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import type { ProgressTone } from '@/components/ui/ProgressTrack'

export interface BookingKpiCardProps {
  icon: LucideIcon
  iconTone?: IconBadgeTone
  /** The card title, e.g. "Active Pipeline". */
  label: string
  /** The headline figure — already formatted. */
  value: string
  /** The unit that sits beside the headline, when there is one. */
  unit?: string
  /** The sub-line under the headline. */
  subLine: string
  /** A second, quieter line under the sub-line. */
  secondaryLine?: string
  /** The ratio of a coverage track, when the card carries one. */
  trackRatio?: number | null
  /** The accessible name of the coverage track. */
  trackLabel?: string
  trackTone?: ProgressTone
}

/**
 * A Bookings & Events KPI card (spec §5.2 item 2).
 *
 * It follows the delivered anatomy — a soft icon badge, an eyebrow label, a
 * mono headline with its unit, a sub-line and an optional coverage track — and
 * it renders the value the API published. No figure is derived here.
 */
export function BookingKpiCard({
  icon,
  iconTone = 'neutral',
  label,
  value,
  unit,
  subLine,
  secondaryLine,
  trackRatio,
  trackLabel,
  trackTone = 'culinary',
}: BookingKpiCardProps) {
  return (
    <article className="bg-artisan-surface border border-artisan-border rounded-xl p-space-lg shadow-elevation-1 hover:shadow-elevation-2 transition-shadow flex flex-col justify-between gap-space-md min-w-0">
      <div className="flex items-start justify-between">
        <IconBadge icon={icon} tone={iconTone} />
      </div>

      <div className="flex flex-col min-w-0">
        <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider font-semibold">
          {label}
        </span>
        <div className="flex items-baseline gap-2 mt-0.5 flex-wrap">
          <span className="font-mono text-data-lg text-espresso-900 tabular-nums break-words">
            {value}
          </span>
          {unit ? (
            <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider font-semibold">
              {unit}
            </span>
          ) : null}
        </div>
        <p className="font-body-sm text-body-sm text-espresso-600 mt-0.5 break-words">
          {subLine}
        </p>
        {secondaryLine ? (
          <p className="font-caption text-caption text-espresso-400 mt-0.5 break-words">
            {secondaryLine}
          </p>
        ) : null}
        {trackRatio !== undefined && trackLabel ? (
          <div className="mt-2 flex items-center gap-2">
            <ProgressTrack ratio={trackRatio} label={trackLabel} tone={trackTone} />
          </div>
        ) : null}
      </div>
    </article>
  )
}
