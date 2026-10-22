import { cn } from '@/utils/cn'
import { formatCount } from '@/utils/currencyFormatter'

export interface PackageTypeTab {
  /** The tier type exactly as the API stored it. */
  packageType: string
  /** How many tiers of that type the catalogue holds. */
  count: number
}

export interface PackageTypeTabsProps {
  /** One entry per distinct tier type present in the catalogue. */
  types: PackageTypeTab[]
  /** The size of the whole catalogue — the "All Tiers" tab count. */
  totalCount: number
  /** The tier type the operator selected, or `null` for the whole catalogue. */
  activeType: string | null
  onTypeChange: (packageType: string | null) => void
}

/**
 * The tier-type tab strip (spec §5.2 item 3, AC-P03).
 *
 * The reference's tab strip lists the package *archetypes*; the approved
 * model carries the tier type as free text, so the strip shows one tab per
 * distinct type **present in the catalogue** with its live count (DEC-070).
 *
 * The counts are counts of tiers — no monetary figure is derived from them.
 */
export function PackageTypeTabs({
  types,
  totalCount,
  activeType,
  onTypeChange,
}: PackageTypeTabsProps) {
  const allCount = formatCount(totalCount)

  return (
    <div
      role="group"
      aria-label="Package tier types"
      className="flex items-center gap-1.5 overflow-x-auto pb-1 text-espresso-600"
    >
      <button
        type="button"
        onClick={() => onTypeChange(null)}
        aria-pressed={activeType === null}
        // The name is spelled out so the count is announced as part of the
        // label rather than run together with it.
        aria-label={`All Tiers (${allCount})`}
        className={cn(
          'px-3 py-1.5 rounded-lg font-headline-sm text-body-sm whitespace-nowrap transition-colors',
          activeType === null
            ? 'bg-culinary-600 text-white shadow-elevation-1'
            : 'bg-surface-container-low hover:bg-surface-container text-espresso-700',
        )}
      >
        All Tiers
        <span className="font-data-sm text-data-sm"> ({allCount})</span>
      </button>

      {types.map((tab) => {
        const isActive = activeType === tab.packageType
        const count = formatCount(tab.count)
        return (
          <button
            key={tab.packageType}
            type="button"
            onClick={() => onTypeChange(tab.packageType)}
            aria-pressed={isActive}
            aria-label={`${tab.packageType} (${count})`}
            className={cn(
              'px-3 py-1.5 rounded-lg font-body-sm text-body-sm whitespace-nowrap transition-colors',
              isActive
                ? 'bg-culinary-600 text-white shadow-elevation-1'
                : 'bg-surface-container-low hover:bg-surface-container text-espresso-700',
            )}
          >
            {tab.packageType}
            <span className="font-data-sm text-data-sm"> ({count})</span>
          </button>
        )
      })}
    </div>
  )
}
