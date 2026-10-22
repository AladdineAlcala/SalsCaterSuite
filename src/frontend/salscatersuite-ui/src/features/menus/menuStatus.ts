import type { BadgeTone } from '@/components/ui/Badge'

/**
 * The presentation of the server's `isWithinTarget` flag (spec §4.5).
 *
 * The flag is a **server** decision — the client never compares the food-cost
 * percentage with the target itself. The label is always rendered with the
 * tone, so the state is legible without colour perception (AC-M18).
 */

/** The text that accompanies the food-cost chip. */
export function menuTargetLabel(isWithinTarget: boolean): string {
  return isWithinTarget ? 'On target' : 'Above target'
}

/** The chip tone that accompanies {@link menuTargetLabel}. */
export function menuTargetTone(isWithinTarget: boolean): BadgeTone {
  return isWithinTarget ? 'success' : 'warning'
}

/**
 * The label of the status filter (spec §4.4).
 *
 * The margin-alert filter is named after the benchmark the server publishes,
 * so the operator sees the threshold that is actually in force.
 */
export function menuStatusFilterLabel(status: string): string {
  switch (status) {
    case 'Costed':
      return 'Costed'
    case 'Uncosted':
      return 'Uncosted'
    case 'MarginAlert':
      return 'Margin Alert'
    default:
      return 'All Dishes'
  }
}
