import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { IconBadge } from '@/components/ui/IconBadge'
import type { IconBadgeTone } from '@/components/ui/IconBadge'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import type { ProgressTone } from '@/components/ui/ProgressTrack'
import { cn } from '@/utils/cn'

export interface KpiCardProgress {
  /** Percentage; clamped to 0–100 by the progress track. */
  ratio: number | null | undefined
  /** Accessible name of the progress bar. */
  label: string
  tone?: ProgressTone
}

export interface KpiCardProps {
  icon: LucideIcon
  iconTone?: IconBadgeTone
  /** The headline figure — already formatted (mono, tabular). */
  headline: string
  /** The unit/label that sits next to the headline. */
  headlineUnit?: string
  headlineUnitTone?: 'default' | 'culinary' | 'caramel'
  /** Supporting sentence under the headline. */
  support: string
  /** Foot line above the progress track. */
  foot: ReactNode
  progress?: KpiCardProgress
  /** Optional action chip in the card header. */
  action?: ReactNode
  className?: string
}

const UNIT_TONE_CLASSES = {
  default: 'text-espresso-400',
  culinary: 'text-culinary-700',
  caramel: 'text-caramel-700',
} as const

/**
 * A KPI card (spec §9.2 item 3).
 *
 * Icon badge, optional action chip, mono headline figure, supporting line,
 * foot line and a **bounded** progress track whose ratio always describes a
 * real measurement.
 */
export function KpiCard({
  icon,
  iconTone = 'neutral',
  headline,
  headlineUnit,
  headlineUnitTone = 'default',
  support,
  foot,
  progress,
  action,
  className,
}: KpiCardProps) {
  return (
    <article
      className={cn(
        'kpi-card bg-artisan-surface border border-artisan-border rounded-xl p-space-lg',
        'shadow-elevation-1 hover:shadow-elevation-2 transition-shadow',
        'flex flex-col justify-between gap-space-md',
        className,
      )}
    >
      <div className="flex flex-col gap-space-sm">
        <div className="flex items-center justify-between gap-2">
          <IconBadge icon={icon} tone={iconTone} />
          {action}
        </div>

        <div>
          <div className="flex items-baseline gap-2 flex-wrap">
            <span className="kpi-headline font-mono text-espresso-900 tabular-nums">
              {headline}
            </span>
            {headlineUnit ? (
              <span
                className={cn(
                  'font-eyebrow text-eyebrow uppercase tracking-wider',
                  UNIT_TONE_CLASSES[headlineUnitTone],
                )}
              >
                {headlineUnit}
              </span>
            ) : null}
          </div>
          <p className="font-body-sm text-body-sm text-espresso-600 mt-0.5">{support}</p>
        </div>
      </div>

      <div className="pt-space-sm">
        <div className="flex items-center justify-between gap-2 text-espresso-700 mb-1.5 flex-wrap">
          {foot}
        </div>
        {progress ? (
          <ProgressTrack ratio={progress.ratio} label={progress.label} tone={progress.tone} />
        ) : null}
      </div>
    </article>
  )
}
