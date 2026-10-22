import { CircleAlert, CircleCheck } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import type { BookingQuoteCheck } from '@/types/booking.types'

export interface WorkbenchChecklistProps {
  /** The five server-derived pre-dispatch checks (BR-29, BR-33). */
  checks: BookingQuoteCheck[]
}

/**
 * The pre-dispatch validation checklist (spec §5.3, BR-33).
 *
 * The five checks are **server-derived** — the browser never evaluates them.
 * Four are **advisory** and warn; only the deactivated-tier check **blocks**,
 * and it says so in words.
 *
 * Every check carries a text label beside its icon, so its verdict is legible
 * without colour perception (WCAG 1.4.1).
 */
export function WorkbenchChecklist({ checks }: WorkbenchChecklistProps) {
  if (checks.length === 0) {
    return (
      <p className="font-body-sm text-body-sm text-espresso-600">
        The pre-dispatch checks appear once the booking can be priced.
      </p>
    )
  }

  return (
    <ul className="flex flex-col gap-2">
      {checks.map((check) => (
        <li key={check.key} className="flex items-start gap-2 min-w-0">
          {check.isSatisfied ? (
            <CircleCheck className="h-4 w-4 text-culinary-600 shrink-0 mt-0.5" aria-hidden="true" />
          ) : (
            <CircleAlert
              className={`h-4 w-4 shrink-0 mt-0.5 ${check.isBlocking ? 'text-error' : 'text-caramel-600'}`}
              aria-hidden="true"
            />
          )}
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-body-sm text-body-sm font-semibold text-espresso-800 break-words">
                {check.label}
              </span>
              <span className="font-caption text-caption text-espresso-600">
                {check.isSatisfied ? 'Satisfied' : check.isBlocking ? 'Blocking' : 'Advisory'}
              </span>
              {check.isBlocking && !check.isSatisfied ? (
                <Badge tone="danger">Blocks the booking</Badge>
              ) : null}
            </div>
            {check.detail ? (
              <p className="font-caption text-caption text-espresso-600 break-words">
                {check.detail}
              </p>
            ) : null}
          </div>
        </li>
      ))}
    </ul>
  )
}
