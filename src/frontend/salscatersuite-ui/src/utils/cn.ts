import { clsx } from 'clsx'
import type { ClassValue } from 'clsx'

/**
 * Class-name helper.
 *
 * `clsx` only - and deliberately so.
 *
 * `tailwind-merge` v3 ships the Tailwind CSS v4 conflict map, but it does not
 * know this project's custom `DESIGN.md` token scale. Measured against the
 * real scale it silently drops classes: `twMerge('text-caption',
 * 'text-espresso-600')` returns only `text-espresso-600`, because the v4 map
 * reads `text-caption` as a font-size and `text-espresso-600` as a colour and
 * treats them as the same group. The same happens to the custom spacing and
 * radius tokens (`p-space-md` / `p-space-lg`, `rounded-radius-card` /
 * `rounded-radius-hero`), which it declines to merge at all.
 *
 * That is precisely the failure this helper exists to avoid, so `cn()` stays on
 * `clsx` alone. Enabling `tailwind-merge` is a separate decision and must not
 * be done as a "cleanup" - see DEC-098.
 */
export function cn(...inputs: ClassValue[]): string {
  return clsx(inputs)
}
