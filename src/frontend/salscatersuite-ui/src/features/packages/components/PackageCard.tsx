import { CircleCheck, CircleSlash } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/utils/cn'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { PackageDto } from '@/types/package.types'
import { PAX_UNIT, packageActiveLabel, packageActiveTone } from '../packageTypes'
import { PackageTypeChip } from './PackageTypeChip'

export interface PackageCardProps {
  tier: PackageDto
  /** The card the operator has selected — it carries the active border and ring. */
  isSelected: boolean
  onSelect: (packageId: number) => void
}

/**
 * One buffet tier of the catalogue (spec §5.2 item 4).
 *
 * Every figure is the server's. The active state is a chip that always carries
 * its text label beside the tone, so the state is legible without colour
 * perception (WCAG 1.4.1).
 *
 * The reference's per-tier food-cost chip, gross-margin chip and package code
 * are not rendered — `Package` carries no cost, no margin and no code column
 * (DEC-067).
 */
export function PackageCard({ tier, isSelected, onSelect }: PackageCardProps) {
  const rate = formatCurrency(tier.amountPax)
  const minimum = tier.minPax === null ? EM_DASH : formatCount(tier.minPax)
  const rateLabel =
    tier.amountPax === null ? 'no per-guest rate set' : `rate ${rate} per guest`

  return (
    <button
      type="button"
      onClick={() => onSelect(tier.packageId)}
      aria-pressed={isSelected}
      aria-label={`${tier.description}, ${rateLabel}`}
      className={cn(
        'bg-artisan-surface rounded-xl p-4 shadow-elevation-1 flex flex-col justify-between gap-3 text-left w-full min-w-0 transition-all',
        isSelected
          ? 'border-2 border-culinary-600 ring-2 ring-culinary-600/15'
          : 'border border-artisan-border hover:border-espresso-400',
      )}
    >
      <div className="space-y-2 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-1.5 flex-wrap min-w-0">
            <span className="font-data-sm text-data-sm font-bold text-espresso-900">
              {tier.packageCode}
            </span>
            <PackageTypeChip packageType={tier.packageType} />
          </div>
          <Badge
            tone={packageActiveTone(tier.isActive)}
            icon={tier.isActive ? CircleCheck : CircleSlash}
            className="shrink-0"
          >
            {packageActiveLabel(tier.isActive)}
          </Badge>
        </div>

        <h3 className="font-headline-sm text-headline-sm text-espresso-900 leading-snug break-words">
          {tier.description}
        </h3>
      </div>

      <div className="pt-2 border-t border-surface-container flex items-end justify-between gap-2">
        <div>
          <span className="font-caption text-caption text-espresso-400 block">Rate per guest</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">{rate}</span>
        </div>
        <div className="text-right">
          <span className="font-caption text-caption text-espresso-400 block">Minimum pax</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">{minimum}</span>
          {tier.minPax === null ? null : (
            <span className="font-caption text-caption text-espresso-400 block">{PAX_UNIT}</span>
          )}
        </div>
      </div>
    </button>
  )
}
