import { cn } from '@/utils/cn'

export interface SkeletonProps {
  className?: string
}

/**
 * Loading placeholder. Purely decorative — it is hidden from assistive
 * technology because the surrounding region announces its own busy state.
 */
export function Skeleton({ className }: SkeletonProps) {
  return (
    <div
      data-testid="skeleton"
      aria-hidden="true"
      className={cn('animate-pulse rounded bg-surface-container', className)}
    />
  )
}
