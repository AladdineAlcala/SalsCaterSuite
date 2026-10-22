import type { ReactNode } from 'react'
import { CircleAlert, Info, TriangleAlert } from 'lucide-react'
import { cn } from '@/utils/cn'

export type AlertVariant = 'error' | 'warning' | 'info'

export interface AlertBannerProps {
  /** Short summary of the failure. */
  title: string
  /**
   * The messages published by the API. They are rendered **verbatim** — the
   * banner never rewrites, merges or rewords a server message
   * (spec §9.3 "Error").
   */
  errors?: string[]
  variant?: AlertVariant
  /** Optional action, e.g. a Retry button. */
  action?: ReactNode
  className?: string
}

const VARIANT_CLASSES: Record<AlertVariant, string> = {
  error: 'bg-error-container text-on-error-container border-error-container',
  warning: 'bg-caramel-50 text-caramel-700 border-caramel-200',
  info: 'bg-surface-container text-espresso-700 border-artisan-border',
}

const VARIANT_ICONS: Record<AlertVariant, typeof CircleAlert> = {
  error: CircleAlert,
  warning: TriangleAlert,
  info: Info,
}

/**
 * Inline error / warning banner.
 *
 * `role="alert"` so a failing fetch is announced immediately.
 */
export function AlertBanner({
  title,
  errors = [],
  variant = 'error',
  action,
  className,
}: AlertBannerProps) {
  const Icon = VARIANT_ICONS[variant]

  return (
    <div
      role="alert"
      className={cn(
        'flex flex-col sm:flex-row sm:items-start gap-3 rounded-xl border p-space-md',
        VARIANT_CLASSES[variant],
        className,
      )}
    >
      <Icon className="h-5 w-5 shrink-0 mt-0.5" aria-hidden="true" />
      <div className="flex-1 min-w-0">
        <p className="font-headline-sm text-headline-sm">{title}</p>
        {errors.length > 0 ? (
          <ul className="mt-1 space-y-0.5 list-disc pl-4">
            {errors.map((message) => (
              <li key={message} className="font-body-sm text-body-sm break-words">
                {message}
              </li>
            ))}
          </ul>
        ) : null}
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  )
}
