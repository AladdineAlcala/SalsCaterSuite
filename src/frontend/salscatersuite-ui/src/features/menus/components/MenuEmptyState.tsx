import { Plus, SearchX, UtensilsCrossed } from 'lucide-react'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'

export interface MenuEmptyStateProps {
  /**
   * `catalogue` — the catalogue itself holds no dishes.
   * `filtered` — the catalogue holds dishes, but the active filter matches none.
   */
  mode: 'catalogue' | 'filtered'
  /** The active filter, named so the operator knows what is being excluded. */
  filterDescription?: string
  onCreate: () => void
}

/**
 * The catalogue's empty states (spec §4.8).
 *
 * An empty catalogue and a filtered result with no match are **different**
 * situations and they say so (AC-M5 / F7). Neither ever renders a
 * fabricated card.
 */
export function MenuEmptyState({
  mode,
  filterDescription,
  onCreate,
}: MenuEmptyStateProps) {
  if (mode === 'filtered') {
    return (
      <Card elevation={1}>
        <EmptyState
          icon={SearchX}
          title="No dishes match the current filters"
          description={
            filterDescription
              ? `Nothing in the catalogue matches ${filterDescription}.`
              : 'Nothing in the catalogue matches the current filters.'
          }
        />
      </Card>
    )
  }

  return (
    <Card elevation={1} className="flex flex-col items-center gap-3">
      <EmptyState
        icon={UtensilsCrossed}
        title="The menu catalogue is empty"
        description="No dish has been created yet, so there is nothing to cost."
      />
      <Button variant="primary" icon={Plus} onClick={onCreate}>
        + New Recipe
      </Button>
    </Card>
  )
}
