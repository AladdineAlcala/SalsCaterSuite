import type { LucideIcon } from 'lucide-react'
import { cn } from '@/utils/cn'

export type IconBadgeTone = 'culinary' | 'caramel' | 'neutral'

export interface IconBadgeProps {
  icon: LucideIcon
  tone?: IconBadgeTone
  className?: string
}

const TONE_CLASSES: Record<IconBadgeTone, string> = {
  culinary: 'bg-culinary-50 text-culinary-700',
  caramel: 'bg-caramel-50 text-caramel-700',
  neutral: 'bg-artisan-subtle text-espresso-700',
}

/**
 * The soft square icon container used in a card header
 * (DESIGN.md "Metric & KPI Cards"). The icon itself is decorative — the
 * meaning is carried by the adjacent text — so it is `aria-hidden`.
 */
export function IconBadge({ icon: Icon, tone = 'neutral', className }: IconBadgeProps) {
  return (
    <span
      className={cn(
        'w-10 h-10 rounded-lg flex items-center justify-center shrink-0',
        TONE_CLASSES[tone],
        className,
      )}
    >
      <Icon className="h-5 w-5" aria-hidden="true" />
    </span>
  )
}
