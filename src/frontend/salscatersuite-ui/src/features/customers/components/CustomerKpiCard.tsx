import type { LucideIcon } from 'lucide-react'
import { IconBadge } from '@/components/ui/IconBadge'
import type { IconBadgeTone } from '@/components/ui/IconBadge'

export interface CustomerKpiCardProps {
  icon: LucideIcon
  iconTone?: IconBadgeTone
  /** The card title, e.g. "Active AR Exposure". */
  label: string
  /** The headline figure — already formatted. */
  value: string
  /** The unit that sits beside the headline, when there is one. */
  unit?: string
  /** The sub-line under the headline. */
  subLine: string
}

/**
 * A customer-master KPI card (spec §4.2).
 *
 * It follows the reference anatomy — a soft icon badge, an eyebrow label, a
 * mono headline with its unit and a sub-line — and it renders the value the
 * API published. No figure is derived here.
 */
export function CustomerKpiCard({
  icon,
  iconTone = 'neutral',
  label,
  value,
  unit,
  subLine,
}: CustomerKpiCardProps) {
  return (
    <article className="kpi-card bg-artisan-surface border border-artisan-border rounded-xl p-space-lg shadow-elevation-1 hover:shadow-elevation-2 transition-shadow flex flex-col justify-between gap-space-md">
      <div className="flex items-start justify-between">
        <IconBadge icon={icon} tone={iconTone} />
      </div>

      <div className="flex flex-col">
        <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider font-semibold">
          {label}
        </span>
        <div className="flex items-baseline gap-2 mt-0.5 flex-wrap">
          <span className="kpi-headline font-mono text-espresso-900 tabular-nums">{value}</span>
          {unit ? (
            <span className="font-caption text-caption text-espresso-400 uppercase tracking-wider font-semibold">
              {unit}
            </span>
          ) : null}
        </div>
        <p className="font-body-sm text-body-sm text-espresso-600 mt-0.5">{subLine}</p>
      </div>
    </article>
  )
}
