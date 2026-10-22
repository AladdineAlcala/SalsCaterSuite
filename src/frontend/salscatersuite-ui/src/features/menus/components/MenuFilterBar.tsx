import { Search } from 'lucide-react'
import { formatPercentage } from '@/utils/currencyFormatter'
import type { MenuSortOption, MenuStatusFilter } from '@/types/menu.types'

export interface MenuFilterBarProps {
  search: string
  onSearchChange: (value: string) => void
  status: MenuStatusFilter
  onStatusChange: (value: MenuStatusFilter) => void
  sort: MenuSortOption
  onSortChange: (value: MenuSortOption) => void
  /** The server's benchmark, named on the margin-alert filter (spec §4.4). */
  targetFoodCostPercentage: number | null
}

/** The sort keys the master endpoint accepts (spec §4.4). */
const SORT_OPTIONS: { value: MenuSortOption; label: string }[] = [
  { value: 'FoodCostAsc', label: 'Food Cost % (Low to High)' },
  { value: 'FoodCostDesc', label: 'Food Cost % (High to Low)' },
  { value: 'PriceDesc', label: 'Selling Price (High to Low)' },
  { value: 'PriceAsc', label: 'Selling Price (Low to High)' },
  { value: 'MarginDesc', label: 'Gross Margin % (High to Low)' },
  { value: 'NameAsc', label: 'Dish Name (A to Z)' },
]

/**
 * The search, status and sort bar (spec §4.4).
 *
 * The reference's **Columns** button is not rendered — the card grid has a
 * fixed set of fields and there is no column-visibility model (spec §4.4).
 */
export function MenuFilterBar({
  search,
  onSearchChange,
  status,
  onStatusChange,
  sort,
  onSortChange,
  targetFoodCostPercentage,
}: MenuFilterBarProps) {
  const marginAlertLabel =
    targetFoodCostPercentage === null
      ? 'Margin Alert'
      : `Margin Alert (>${formatPercentage(targetFoodCostPercentage)})`

  return (
    <section className="bg-artisan-surface p-space-md rounded-xl shadow-elevation-1 border border-artisan-border flex flex-col gap-space-sm">
      <div className="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-space-sm">
        <div className="relative flex-1 md:max-w-md">
          <label htmlFor="menu-search" className="sr-only">
            Search dishes
          </label>
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 text-espresso-400 h-5 w-5 pointer-events-none"
            aria-hidden="true"
          />
          <input
            id="menu-search"
            name="menuSearch"
            type="search"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="Search dish, code or note…"
            className="w-full h-10 pl-10 pr-4 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm transition-all"
          />
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center gap-1.5 px-3 h-10 rounded-lg bg-surface-container-lowest border border-artisan-border font-body-sm text-body-sm text-espresso-700">
            <label
              htmlFor="menu-status-filter"
              className="font-caption text-caption text-espresso-400 font-semibold uppercase"
            >
              Status
            </label>
            <select
              id="menu-status-filter"
              name="status"
              value={status}
              onChange={(event) => onStatusChange(event.target.value as MenuStatusFilter)}
              className="bg-transparent font-medium text-espresso-900 focus:outline-none cursor-pointer"
            >
              <option value="">All Dishes</option>
              <option value="Costed">Costed</option>
              <option value="Uncosted">Uncosted</option>
              <option value="MarginAlert">{marginAlertLabel}</option>
            </select>
          </div>

          <div className="flex items-center gap-1.5 px-3 h-10 rounded-lg bg-surface-container-lowest border border-artisan-border font-body-sm text-body-sm text-espresso-700">
            <label
              htmlFor="menu-sort"
              className="font-caption text-caption text-espresso-400 font-semibold uppercase"
            >
              Sort
            </label>
            <select
              id="menu-sort"
              name="sort"
              value={sort}
              onChange={(event) => onSortChange(event.target.value as MenuSortOption)}
              className="bg-transparent font-medium text-espresso-900 focus:outline-none cursor-pointer"
            >
              {SORT_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>
    </section>
  )
}
