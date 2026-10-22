import { cn } from '@/utils/cn'

export interface PackageTypeChipProps {
  /**
   * The tier type, rendered **verbatim**.
   *
   * It is free text (DEC-070) and the column allows 50 characters, so the
   * chip must be able to wrap.
   */
  packageType: string
  className?: string
}

/**
 * The tier-type chip.
 *
 * The shared `Badge` sets `whitespace-nowrap`, which is right for a status
 * chip but wrong for the tier type: a 50-character type with no break
 * opportunity (the column allows exactly that) pushed the card past the
 * viewport at 375 px. This chip keeps the same visual language and wraps
 * instead.
 */
export function PackageTypeChip({ packageType, className }: PackageTypeChipProps) {
  return (
    <span
      className={cn(
        'inline-flex max-w-full items-center px-2 py-0.5 border rounded',
        'bg-surface-container text-espresso-700 border-artisan-border',
        'font-caption text-caption font-bold whitespace-normal break-words',
        className,
      )}
    >
      {packageType}
    </span>
  )
}
