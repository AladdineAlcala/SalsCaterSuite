import { Badge } from '@/components/ui/Badge'
import { cn } from '@/utils/cn'
import { EM_DASH } from '@/utils/currencyFormatter'
import { agingTone } from '../customerAging'

export interface CustomerStateChipProps {
  /** The aging-bucket key the server sent — `null` when it sent none. */
  ledgerState: string | null
  /** The server's display string — rendered verbatim. */
  ledgerStateLabel: string | null
  className?: string
}

/**
 * The customer aging-state chip.
 *
 * Status is never conveyed by colour alone (WCAG 1.4.1): the chip always
 * carries its text label. The colour comes from the aging **key**; the text
 * is always the **server's** display string, and the client never computes a
 * day count (DEC-002, AC-C3). An unrecognised key degrades to a neutral tone
 * and still shows the server's label; a payload with neither shows an em dash
 * rather than a fabricated state.
 */
export function CustomerStateChip({
  ledgerState,
  ledgerStateLabel,
  className,
}: CustomerStateChipProps) {
  const tone = agingTone(ledgerState)
  const text = ledgerStateLabel || ledgerState || EM_DASH

  return (
    <Badge tone={tone} shape="soft" className={cn('font-semibold', className)}>
      {text}
    </Badge>
  )
}
