import { Loader2 } from 'lucide-react'
import { cn } from '@/utils/cn'

export interface SpinnerLoaderProps {
  label?: string
  className?: string
}

/**
 * Busy indicator. The visible label is announced to assistive technology
 * through a polite live region.
 */
export function SpinnerLoader({
  label = 'Loading',
  className,
}: SpinnerLoaderProps) {
  return (
    <div
      role="status"
      aria-live="polite"
      className={cn('flex items-center justify-center gap-2 text-espresso-600', className)}
    >
      <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
      <span className="font-body-sm text-body-sm">{label}</span>
    </div>
  )
}
