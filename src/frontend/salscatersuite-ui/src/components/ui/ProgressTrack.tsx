import { cn } from '@/utils/cn'
import { clampRatio, progressAriaValue } from '@/utils/progressRatio'

export type ProgressTone = 'culinary' | 'caramel' | 'espresso'

export interface ProgressTrackProps {
  /** Percentage; clamped to 0–100 before it is rendered. */
  ratio: number | null | undefined
  /** Accessible name of the progress bar. */
  label: string
  tone?: ProgressTone
  className?: string
}

const FILL_CLASSES: Record<ProgressTone, string> = {
  culinary: 'bg-culinary-600',
  caramel: 'bg-caramel-500',
  espresso: 'bg-espresso-600',
}

/**
 * A bounded progress track.
 *
 * The fill width is clamped to 0–100 so the bar can never overflow its
 * channel. The value is exposed to assistive technology through
 * `role="progressbar"` with `aria-valuenow` / `aria-valuemin` / `aria-valuemax`
 * and an accessible label (spec §9.4).
 *
 * An undefined ratio renders nothing at all rather than a misleading `0%`.
 */
export function ProgressTrack({
  ratio,
  label,
  tone = 'culinary',
  className,
}: ProgressTrackProps) {
  const clamped = clampRatio(ratio)
  const ariaValue = progressAriaValue(clamped)

  if (clamped === null || ariaValue === null) {
    return null
  }

  return (
    <div
      className={cn('w-full h-1.5 rounded-full bg-surface-container overflow-hidden', className)}
      role="progressbar"
      aria-label={label}
      aria-valuenow={ariaValue}
      aria-valuemin={0}
      aria-valuemax={100}
    >
      <div className={cn('h-full rounded-full', FILL_CLASSES[tone])} style={{ width: `${clamped}%` }} />
    </div>
  )
}
