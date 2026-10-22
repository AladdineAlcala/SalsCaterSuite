import { PackageCard } from './PackageCard'
import type { PackageDto } from '@/types/package.types'

export interface PackageCardGridProps {
  tiers: PackageDto[]
  /** The tier the operator has selected, or `null`. */
  selectedPackageId: number | null
  onSelect: (packageId: number) => void
}

/**
 * The tier card grid (spec §5.2 item 4).
 *
 * The reference uses a **card grid**, not a table. The grid wraps at every
 * breakpoint so the page never overflows horizontally (AC-P14).
 */
export function PackageCardGrid({
  tiers,
  selectedPackageId,
  onSelect,
}: PackageCardGridProps) {
  return (
    <div
      data-testid="package-tier-grid"
      className="grid grid-cols-1 md:grid-cols-2 gap-3.5"
    >
      {tiers.map((tier) => (
        <PackageCard
          key={tier.packageId}
          tier={tier}
          isSelected={tier.packageId === selectedPackageId}
          onSelect={onSelect}
        />
      ))}
    </div>
  )
}
