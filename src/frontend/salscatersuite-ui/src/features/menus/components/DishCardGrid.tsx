import { DishCard } from './DishCard'
import type { MenuCostingItem } from '@/types/menu.types'

export interface DishCardGridProps {
  items: MenuCostingItem[]
  /** The dish code the operator has selected, or `null`. */
  selectedMenuId: string | null
  onSelect: (menuId: string) => void
}

/**
 * The dish card grid (spec §4.5).
 *
 * The reference uses a **card grid**, not a table. The grid wraps at every
 * breakpoint so the page never overflows horizontally (AC-M19).
 */
export function DishCardGrid({ items, selectedMenuId, onSelect }: DishCardGridProps) {
  return (
    <div
      data-testid="menu-dish-grid"
      className="grid grid-cols-1 md:grid-cols-2 gap-3.5"
    >
      {items.map((item) => (
        <DishCard
          key={item.menuId}
          item={item}
          isSelected={item.menuId === selectedMenuId}
          onSelect={onSelect}
        />
      ))}
    </div>
  )
}
