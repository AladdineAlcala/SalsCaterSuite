import { Search } from 'lucide-react'
import type { PackageSortOption, PackageStatusFilter } from '@/types/package.types'

export interface PackageFilterBarProps {
  /** The free-text search term, matched against the tier name and type. */
  search: string
  onSearchChange: (value: string) => void
  /** The active-state filter, or an empty string for no filter. */
  status: PackageStatusFilter
  onStatusChange: (value: PackageStatusFilter) => void
  /** The ordering applied to the server's list. */
  sort: PackageSortOption
  onSortChange: (value: PackageSortOption) => void
}

/** The sort keys the catalogue offers (spec §5.2 item 3). */
const SORT_OPTIONS: { value: PackageSortOption; label: string }[] = [
  { value: 'RateDesc', label: 'Rate per guest (High to Low)' },
  { value: 'RateAsc', label: 'Rate per guest (Low to High)' },
  { value: 'NameAsc', label: 'Tier name (A to Z)' },
]

/**
 * The search, status and sort bar (spec §5.2 item 3).
 *
 * The search matches the tier name and the tier type, case-insensitively.
 * Every control filters the server's list in the browser — the list endpoint
 * accepts no filter of its own, and no filter is ever sent to an endpoint that
 * does not accept one (AC-P04).
 */
export function PackageFilterBar({
  search,
  onSearchChange,
  status,
  onStatusChange,
  sort,
  onSortChange,
}: PackageFilterBarProps) {
  return (
    <section className="bg-artisan-surface p-space-md rounded-xl shadow-elevation-1 border border-artisan-border flex flex-col gap-space-sm">
      <div className="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-space-sm">
        <div className="relative flex-1 md:max-w-md">
          <label htmlFor="package-search" className="sr-only">
            Search packages
          </label>
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 text-espresso-400 h-5 w-5 pointer-events-none"
            aria-hidden="true"
          />
          <input
            id="package-search"
            name="packageSearch"
            type="search"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="Search tier name or type…"
            className="w-full h-10 pl-10 pr-4 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm transition-all"
          />
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center gap-1.5 px-3 h-10 rounded-lg bg-surface-container-lowest border border-artisan-border font-body-sm text-body-sm text-espresso-700">
            <label
              htmlFor="package-status-filter"
              className="font-caption text-caption text-espresso-400 font-semibold uppercase"
            >
              Status
            </label>
            <select
              id="package-status-filter"
              name="status"
              value={status}
              onChange={(event) => onStatusChange(event.target.value as PackageStatusFilter)}
              className="bg-transparent font-medium text-espresso-900 focus:outline-none cursor-pointer"
            >
              <option value="">All Tiers</option>
              <option value="Active">Active only</option>
              <option value="Inactive">Inactive only</option>
            </select>
          </div>

          <div className="flex items-center gap-1.5 px-3 h-10 rounded-lg bg-surface-container-lowest border border-artisan-border font-body-sm text-body-sm text-espresso-700">
            <label
              htmlFor="package-sort"
              className="font-caption text-caption text-espresso-400 font-semibold uppercase"
            >
              Sort
            </label>
            <select
              id="package-sort"
              name="sort"
              value={sort}
              onChange={(event) => onSortChange(event.target.value as PackageSortOption)}
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
