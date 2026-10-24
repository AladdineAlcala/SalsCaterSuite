import { PackagePlus, Plus } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { CurrencyInput } from '@/components/ui/CurrencyInput'
import { EmptyState } from '@/components/feedback/EmptyState'
import { formatCurrency } from '@/utils/currencyFormatter'
import type { AddonMasterItemDto } from '@/types/booking.types'
import type { BookingAddonDraftLine } from '../bookingVocabulary'
import {
  addonCategoryLabel,
  addonPricingBasisLabel,
  validateAddonLineAmount,
} from '../bookingVocabulary'

export interface WorkbenchAddonPickerProps {
  /** The add-on master the picker reads. */
  master: AddonMasterItemDto[]
  /** The add-on lines the operator has chosen. */
  lines: BookingAddonDraftLine[]
  /** Adds an add-on by its identifier, defaulting its line total. */
  onAdd: (addonId: number) => void
  /** Removes an add-on line. */
  onRemove: (addonId: number) => void
  /** Records the operator's line total. */
  onAmountChange: (addonId: number, value: string) => void
  isLoading: boolean
}

const INPUT_CLASSES =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm'

/**
 * The workbench's add-on picker (spec §5.2).
 *
 * The master is **read-only** in this cycle (RSK-066), and it ships **empty**,
 * so an empty master renders the explicit empty state rather than a fabricated
 * list (AC-BKG-73, M12).
 *
 * The **line total** is the operator's input (DEC-158): the picker defaults it
 * to the server's `lineAmountForBooking` and sends it as `amount`. The client
 * never multiplies a per-guest price itself.
 */
export function WorkbenchAddonPicker({
  master,
  lines,
  onAdd,
  onRemove,
  onAmountChange,
  isLoading,
}: WorkbenchAddonPickerProps) {
  const grouped = master.reduce<Map<string, AddonMasterItemDto[]>>((groups, item) => {
    const key = addonCategoryLabel(item.addonCategoryName)
    const bucket = groups.get(key) ?? []
    bucket.push(item)
    groups.set(key, bucket)
    return groups
  }, new Map())

  const available = master.filter(
    (item) => !lines.some((line) => line.addonId === item.addonId),
  )

  return (
    <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5">
          <PackagePlus className="h-4 w-4 text-culinary-700" aria-hidden="true" />
          Add-ons &amp; upgrades
        </h2>
        {lines.length > 0 ? <Badge tone="info">{lines.length} chosen</Badge> : null}
      </div>

      {isLoading ? (
        <p className="font-body-sm text-body-sm text-espresso-600">Loading the add-on master…</p>
      ) : null}

      {!isLoading && master.length === 0 ? (
        <EmptyState
          icon={PackagePlus}
          title="No add-ons are defined yet"
          description="The add-on master is read-only and ships empty in this cycle, so no add-on can be added until the catalogue is populated."
        />
      ) : null}

      {!isLoading && master.length > 0 ? (
        <>
          <label
            htmlFor="booking-addon-picker"
            className="font-caption text-caption text-espresso-400 font-semibold uppercase"
          >
            Add an add-on
          </label>
          <select
            id="booking-addon-picker"
            name="booking-addon-picker"
            value=""
            disabled={available.length === 0}
            onChange={(event) => {
              if (event.target.value.length > 0) {
                onAdd(Number(event.target.value))
              }
            }}
            className={INPUT_CLASSES}
          >
            <option value="">
              {available.length === 0 ? 'Every add-on is already chosen' : 'Choose an add-on'}
            </option>
            {Array.from(grouped.entries()).map(([category, items]) => (
              <optgroup key={category} label={category}>
                {items
                  .filter((item) => available.some((entry) => entry.addonId === item.addonId))
                  .map((item) => (
                    <option key={item.addonId} value={String(item.addonId)}>
                      {item.description} · {formatCurrency(item.amount)} ·{' '}
                      {addonPricingBasisLabel(item.pricingBasis)}
                    </option>
                  ))}
              </optgroup>
            ))}
          </select>
        </>
      ) : null}

      {lines.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {lines.map((line) => {
            const item = master.find((entry) => entry.addonId === line.addonId)
            const amountId = `booking-addon-amount-${line.addonId}`
            const error = validateAddonLineAmount(line.amount)

            return (
              <li
                key={line.addonId}
                className="flex flex-col gap-1 rounded-lg border border-artisan-border bg-surface-container-low p-space-sm min-w-0"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-body-sm text-body-sm font-semibold text-espresso-900 break-words">
                      {item?.description ?? `Add-on ${line.addonId}`}
                    </p>
                    <p className="font-caption text-caption text-espresso-600">
                      {item
                        ? `${addonCategoryLabel(item.addonCategoryName)} · ${addonPricingBasisLabel(item.pricingBasis)}`
                        : 'Not in the master'}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => onRemove(line.addonId)}
                    aria-label={`Remove ${item?.description ?? 'the add-on'}`}
                    className="font-caption text-caption text-espresso-600 underline hover:text-espresso-900 shrink-0"
                  >
                    Remove
                  </button>
                </div>

                <label
                  htmlFor={amountId}
                  className="font-caption text-caption text-espresso-400 uppercase"
                >
                  Line total
                </label>
                <CurrencyInput
                  id={amountId}
                  name={amountId}
                  value={line.amount}
                  onValueChange={(value) => onAmountChange(line.addonId, value)}
                  aria-invalid={error ? true : undefined}
                  aria-describedby={error ? `${amountId}-error` : undefined}
                  className={INPUT_CLASSES}
                />
                {error ? (
                  <p id={`${amountId}-error`} role="alert" className="font-caption text-caption text-error">
                    {error}
                  </p>
                ) : (
                  <p className="font-caption text-caption text-espresso-600">
                    <Plus className="inline h-3 w-3" aria-hidden="true" /> The line total joins the
                    gross. The server freezes it on the booking.
                  </p>
                )}
              </li>
            )
          })}
        </ul>
      ) : null}
    </Card>
  )
}
