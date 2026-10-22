import { Search } from 'lucide-react'
import { cn } from '@/utils/cn'
import { formatCount } from '@/utils/currencyFormatter'
import type {
  CustomerArStateFilter,
  CustomerQuickViewCounts,
  CustomerTypeFilter,
} from '@/types/customer.types'

export interface CustomerFilterBarProps {
  search: string
  onSearchChange: (value: string) => void
  customerType: CustomerTypeFilter
  onCustomerTypeChange: (value: CustomerTypeFilter) => void
  arState: CustomerArStateFilter
  onArStateChange: (value: CustomerArStateFilter) => void
  /**
   * The population counts the API publishes for the quick views. They are
   * counted over the search-filtered population with the type and AR-state
   * filters ignored, so every pill always shows a live count (spec §4.3).
   */
  quickViewCounts: CustomerQuickViewCounts | undefined
}

interface QuickView {
  id: string
  label: string
  /** The key of the server's count for this quick view. */
  countKey: keyof CustomerQuickViewCounts
  customerType: CustomerTypeFilter
  arState: CustomerArStateFilter
}

/**
 * The quick views (spec §4.3). Each one is a complete filter scope — a type
 * **and** an AR state — so the operator always sees an unambiguous result set.
 */
const QUICK_VIEWS: QuickView[] = [
  {
    id: 'all',
    label: 'All Clients',
    countKey: 'all',
    customerType: '',
    arState: '',
  },
  {
    id: 'corporate',
    label: 'Corporate',
    countKey: 'corporate',
    customerType: 'Corporate',
    arState: '',
  },
  {
    id: 'private',
    label: 'Private',
    countKey: 'private',
    customerType: 'Private',
    arState: '',
  },
  {
    id: 'open',
    label: 'With Open Balance',
    countKey: 'withOpenBalance',
    customerType: '',
    arState: 'Open',
  },
  {
    id: 'overdue30',
    label: 'Overdue > 30 days',
    countKey: 'overdue30',
    customerType: '',
    arState: 'Overdue30',
  },
]

/**
 * The search & filter bar (spec §4.3).
 *
 * The metro-area filter of the reference is **not** rendered — `Customer`
 * carries no geography (spec §1.2).
 */
export function CustomerFilterBar({
  search,
  onSearchChange,
  customerType,
  onCustomerTypeChange,
  arState,
  onArStateChange,
  quickViewCounts,
}: CustomerFilterBarProps) {
  return (
    <section className="bg-artisan-surface p-space-md rounded-xl shadow-elevation-1 border border-artisan-border flex flex-col gap-space-sm">
      <div className="flex flex-col lg:flex-row items-stretch lg:items-center justify-between gap-space-sm">
        <div className="relative flex-1">
          <label htmlFor="customer-search" className="sr-only">
            Search customers
          </label>
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 text-espresso-400 h-5 w-5 pointer-events-none"
            aria-hidden="true"
          />
          <input
            id="customer-search"
            name="customerSearch"
            type="search"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder="Search by client name, company or contact number…"
            className="w-full h-10 pl-10 pr-4 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm transition-all"
          />
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center gap-1.5 px-3 h-10 rounded-lg bg-surface-container-lowest border border-artisan-border font-body-sm text-body-sm text-espresso-700">
            <label
              htmlFor="customer-type-filter"
              className="font-caption text-caption text-espresso-400 font-semibold uppercase"
            >
              Type
            </label>
            <select
              id="customer-type-filter"
              name="customerType"
              value={customerType}
              onChange={(event) => onCustomerTypeChange(event.target.value as CustomerTypeFilter)}
              className="bg-transparent font-medium text-espresso-900 focus:outline-none cursor-pointer"
            >
              <option value="">All Accounts</option>
              <option value="Corporate">Corporate</option>
              <option value="Private">Private</option>
            </select>
          </div>

          <div className="flex items-center gap-1.5 px-3 h-10 rounded-lg bg-surface-container-lowest border border-artisan-border font-body-sm text-body-sm text-espresso-700">
            <label
              htmlFor="customer-ar-state-filter"
              className="font-caption text-caption text-espresso-400 font-semibold uppercase"
            >
              AR State
            </label>
            <select
              id="customer-ar-state-filter"
              name="arState"
              value={arState}
              onChange={(event) => onArStateChange(event.target.value as CustomerArStateFilter)}
              className="bg-transparent font-medium text-espresso-900 focus:outline-none cursor-pointer"
            >
              <option value="">All Ledgers</option>
              <option value="Clean">Clean</option>
              <option value="Open">Open Balance</option>
              <option value="Overdue30">Overdue &gt; 30 days</option>
              <option value="Overdue60">Overdue &gt; 60 days</option>
            </select>
          </div>
        </div>
      </div>

      <div className="flex items-center gap-2 overflow-x-auto pb-1 pt-1 text-espresso-600 text-body-sm">
        <span className="font-caption text-caption text-espresso-400 uppercase font-bold tracking-wider mr-1">
          Quick Views
        </span>
        {QUICK_VIEWS.map((view) => {
          const isActive = view.customerType === customerType && view.arState === arState
          // The count is the server's population figure for this quick view.
          const count = quickViewCounts?.[view.countKey]
          const countText = count === undefined ? null : formatCount(count)

          return (
            <button
              key={view.id}
              type="button"
              onClick={() => {
                onCustomerTypeChange(view.customerType)
                onArStateChange(view.arState)
              }}
              aria-pressed={isActive}
              // The name is spelled out so the count is announced as part of
              // the label rather than run together with it.
              aria-label={countText ? `${view.label} (${countText})` : undefined}
              className={cn(
                'px-3 py-1 rounded-full font-body-sm text-body-sm font-semibold whitespace-nowrap transition-colors',
                isActive
                  ? 'bg-espresso-900 text-surface-container-lowest shadow-elevation-1'
                  : 'bg-surface-container text-espresso-700 hover:bg-surface-container-high',
              )}
            >
              {view.label}
              {countText ? (
                <span className="font-data-sm text-data-sm"> ({countText})</span>
              ) : null}
            </button>
          )
        })}
      </div>
    </section>
  )
}
