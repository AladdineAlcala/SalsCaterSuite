import { Package, Plus, SearchX } from 'lucide-react'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'

export interface PackageEmptyStateProps {
  /**
   * `catalogue` — the catalogue itself holds no tier.
   * `filtered` — the catalogue holds tiers, but the active filter matches none.
   */
  mode: 'catalogue' | 'filtered'
  /** The active filter, named so the operator knows what is being excluded. */
  filterDescription?: string
  onCreate: () => void
}

/**
 * The catalogue's empty states (spec §5.2 item 6, AC-P07).
 *
 * An empty catalogue and a filtered result with no match are **different**
 * situations and they say so. Neither ever renders a fabricated tier.
 */
export function PackageEmptyState({
  mode,
  filterDescription,
  onCreate,
}: PackageEmptyStateProps) {
  if (mode === 'filtered') {
    return (
      <Card elevation={1}>
        <EmptyState
          icon={SearchX}
          title="No tiers match the current filters"
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
        icon={Package}
        title="The packages catalogue is empty"
        description="No tier has been configured yet, so there is nothing to quote."
      />
      <Button variant="primary" icon={Plus} onClick={onCreate}>
        + New Package
      </Button>
    </Card>
  )
}
