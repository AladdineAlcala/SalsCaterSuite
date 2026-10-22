import type { LucideIcon } from 'lucide-react'
import { Inbox } from 'lucide-react'
import { cn } from '@/utils/cn'

export interface EmptyStateProps {
  title: string
  description?: string
  icon?: LucideIcon
  className?: string
}

/**
 * Explicit empty state (spec §9.3 "Empty"). Every panel that can legitimately
 * hold no records states so, rather than rendering a zero that reads like a
 * real measurement.
 */
export function EmptyState({
  title,
  description,
  icon: Icon = Inbox,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-artisan-border bg-surface-container-low px-4 py-6 text-center',
        className,
      )}
    >
      <Icon className="h-5 w-5 text-espresso-400" aria-hidden="true" />
      <p className="font-body-sm text-body-sm font-semibold text-espresso-700">{title}</p>
      {description ? (
        <p className="font-caption text-caption text-espresso-600 max-w-xs">{description}</p>
      ) : null}
    </div>
  )
}
