import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/utils/cn'

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode
  /** Elevation ladder (DESIGN.md "Elevation & Depth"). */
  elevation?: 1 | 2 | 3
  /** Removes the default padding so a card can host its own section layout. */
  bare?: boolean
}

const ELEVATION_CLASSES: Record<1 | 2 | 3, string> = {
  1: 'shadow-elevation-1',
  2: 'shadow-elevation-2',
  3: 'shadow-elevation-3',
}

/**
 * The card primitive: white surface, 1px linen hairline, 12px radius
 * (DESIGN.md "Metric & KPI Cards").
 */
export function Card({
  children,
  elevation = 2,
  bare = false,
  className,
  ...rest
}: CardProps) {
  return (
    <div
      className={cn(
        'bg-artisan-surface border border-artisan-border rounded-xl',
        ELEVATION_CLASSES[elevation],
        !bare && 'p-space-lg',
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  )
}
