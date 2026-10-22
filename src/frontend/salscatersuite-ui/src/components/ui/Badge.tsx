import type { ReactNode } from 'react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '@/utils/cn'
import type { StatusTone } from '@/utils/bookingStatus'

export type BadgeTone = StatusTone | 'info'

export interface BadgeProps {
  tone?: BadgeTone
  icon?: LucideIcon
  children: ReactNode
  className?: string
  /** `pill` for the design's rounded-full stage pills, `soft` for square chips. */
  shape?: 'pill' | 'soft'
}

const TONE_CLASSES: Record<BadgeTone, string> = {
  success: 'bg-culinary-50 text-culinary-800 border-culinary-200',
  warning: 'bg-caramel-50 text-caramel-700 border-caramel-200',
  info: 'bg-surface-container text-espresso-700 border-artisan-border',
  neutral: 'bg-artisan-subtle text-espresso-700 border-artisan-border',
  danger: 'bg-error-container text-on-error-container border-error-container',
}

/**
 * Status chip. The tone is always paired with a text label (and an icon when
 * one is supplied) — status is never conveyed by colour alone (WCAG 1.4.1).
 */
export function Badge({
  tone = 'neutral',
  icon: Icon,
  children,
  className,
  shape = 'soft',
}: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 px-2 py-0.5 border font-caption text-caption font-bold whitespace-nowrap',
        shape === 'pill' ? 'rounded-full' : 'rounded',
        TONE_CLASSES[tone],
        className,
      )}
    >
      {Icon ? <Icon className="h-3.5 w-3.5" aria-hidden="true" /> : null}
      {children}
    </span>
  )
}
