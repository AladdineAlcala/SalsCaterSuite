import type { BadgeTone } from '@/components/ui/Badge'

/**
 * The aging vocabulary of the Customer Master (KPI-13 / DEC-041 / DEC-052).
 *
 * The **label** that accompanies an aging key is always the server's own
 * display string — the client never counts days and never derives a bucket
 * (DEC-002, AC-C3). The key only selects a *tone*.
 */
export const AGING_TONES: Record<string, BadgeTone> = {
  Clean: 'success',
  NotYetDue: 'info',
  Days1To30: 'warning',
  Days31To60: 'warning',
  Days61To90: 'danger',
  Over90Days: 'danger',
}

/**
 * The text tone of a money figure that carries an aging bucket.
 *
 * A settled balance reads as culinary, an owed one as caramel, and a balance
 * that has aged past sixty days as the error tone. `NotYetDue` stays caramel
 * — the money is owed, it simply is not overdue yet.
 */
export const AGING_TEXT_CLASSES: Record<string, string> = {
  Clean: 'text-culinary-700',
  NotYetDue: 'text-caramel-700',
  Days1To30: 'text-caramel-700',
  Days31To60: 'text-caramel-700',
  Days61To90: 'text-error',
  Over90Days: 'text-error',
}

/** The chip tone of an aging bucket. An unknown key degrades to neutral. */
export function agingTone(agingBucket: string | null | undefined): BadgeTone {
  if (!agingBucket) return 'neutral'
  return AGING_TONES[agingBucket] ?? 'neutral'
}

/** The text tone of a figure that carries an aging bucket. */
export function agingTextClass(agingBucket: string | null | undefined): string {
  if (!agingBucket) return 'text-caramel-700'
  return AGING_TEXT_CLASSES[agingBucket] ?? 'text-caramel-700'
}
