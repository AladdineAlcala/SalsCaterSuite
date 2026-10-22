import { CalendarX, Plus, SearchX } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Card } from '@/components/ui/Card'

export interface BookingEmptyStateProps {
  /**
   * `catalogue` — the register holds no booking at all.
   * `filtered` — the register holds bookings, but none match the filters.
   */
  mode: 'catalogue' | 'filtered'
  /** The filters that excluded every booking — shown in the filtered mode. */
  filterDescription?: string
  onCreate: () => void
}

/**
 * The register's two empty states (spec §5.2 "States", AC-BKG-09).
 *
 * They are **distinct**: a register with no booking at all offers the create
 * action, while a filtered register names the filters that excluded every
 * booking. Neither ever renders a zero that reads like a real measurement.
 */
export function BookingEmptyState({
  mode,
  filterDescription,
  onCreate,
}: BookingEmptyStateProps) {
  if (mode === 'filtered') {
    return (
      <Card elevation={1}>
        <EmptyState
          icon={SearchX}
          title="No booking matches the current filters"
          description={
            filterDescription
              ? `Nothing in the register matches ${filterDescription}.`
              : 'Nothing in the register matches the current filters.'
          }
        />
      </Card>
    )
  }

  return (
    <Card elevation={1} className="flex flex-col items-center gap-3">
      <EmptyState
        icon={CalendarX}
        title="The bookings register is empty"
        description="No booking has been recorded yet, so there is nothing to schedule. Nothing is seeded here."
      />
      <Button variant="primary" icon={Plus} onClick={onCreate}>
        New booking
      </Button>
    </Card>
  )
}
