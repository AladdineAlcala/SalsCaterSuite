import { useRef } from 'react'
import { PackagePlus, X } from 'lucide-react'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { CurrencyInput } from '@/components/ui/CurrencyInput'
import { EmptyState } from '@/components/feedback/EmptyState'
import { SpinnerLoader } from '@/components/feedback/SpinnerLoader'
import { useFocusTrap } from '@/components/navigation/useFocusTrap'
import { formatCurrency } from '@/utils/currencyFormatter'
import type {
  AddonCategoryDto,
  AddonMasterItemDto,
  BookingAddonLine,
} from '@/types/booking.types'
import type { BookingAddonDraftLine } from '../bookingVocabulary'
import {
  addonPricingBasisLabel,
  validateAddonLineAmount,
} from '../bookingVocabulary'

export interface BookingAddonsPanelProps {
  /**
   * The add-on master (contract §16.1), read **only while the picker modal is
   * open** (spec §5.4, DEC-170). It carries the new rows the modal offers and
   * the description fallback of a selected line.
   */
  master: AddonMasterItemDto[]
  /** The booking's frozen lines — the fallback description of a removed row. */
  savedLines: BookingAddonLine[]
  /** The draft lines — the only rows the panel itself renders. */
  lines: BookingAddonDraftLine[]
  /** The **saved** line total the server published. */
  savedAddonsTotal: number
  /** `true` while the draft differs from the saved set. */
  isDirty: boolean
  /** `true` while the modal's master read is in flight. */
  isLoading: boolean
  /** `true` while the modal's category read is in flight. */
  isCategoryLoading: boolean
  /** The category read's errors, already reduced to messages. */
  categoryErrors: string[]
  /** The active add-on master read's errors, already reduced to messages. */
  masterErrors: string[]
  /** The category the modal's server filter is narrowed to. `null` = all. */
  activeCategoryId: number | null
  /** The category master (contract §16.2), read only while the modal is open. */
  categories: AddonCategoryDto[]
  /** `true` while the **Choose add-ons** modal is open. */
  isPickerOpen: boolean
  isLocked: boolean
  onOpenPicker: () => void
  onClosePicker: () => void
  onCategoryChange: (categoryId: number | null) => void
  onCategoryRetry: () => void
  onMasterRetry: () => void
  onToggle: (addonId: number, isSelected: boolean) => void
  onAmountChange: (addonId: number, value: string) => void
}

const INPUT_CLASSES =
  'w-full h-10 px-3 rounded-lg bg-surface-container-lowest text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm disabled:opacity-60 disabled:cursor-not-allowed'

/**
 * Add-ons & upgrades (spec §5.4, as amended by DEC-170).
 *
 * The full catalogue is **not** rendered here anymore. The panel shows the
 * booking's selected add-on lines only — with each line's editable total —
 * and a **Choose add-ons** action that opens a modal whose table lists the
 * active master, filterable by category (contract §16.1's required
 * `headcount`, optional `categoryId` — the client never prices a line).
 *
 * Selecting in the modal touches **the draft only**; nothing is written until
 * the page's save action replays the existing full replace (contract §16.4).
 *
 * The **subtotal is the server's**. While the draft differs from the saved set
 * the panel says "pending save" rather than summing the lines itself.
 */
export function BookingAddonsPanel({
  master,
  savedLines,
  lines,
  savedAddonsTotal,
  isDirty,
  isLoading,
  isCategoryLoading,
  categoryErrors,
  masterErrors,
  activeCategoryId,
  categories,
  isPickerOpen,
  isLocked,
  onOpenPicker,
  onClosePicker,
  onCategoryChange,
  onCategoryRetry,
  onMasterRetry,
  onToggle,
  onAmountChange,
}: BookingAddonsPanelProps) {
  const pickerRef = useRef<HTMLDivElement>(null)
  useFocusTrap(pickerRef, isPickerOpen, onClosePicker)

  const selectedIds = new Set(lines.map((line) => line.addonId))
  const isMasterEmpty = !isLoading && masterErrors.length === 0 && master.length === 0
  const isCategoryNarrowed = activeCategoryId !== null

  return (
    <section
      aria-labelledby="booking-addons-heading"
      data-testid="booking-addons-panel"
      className="bg-artisan-surface border border-artisan-border rounded-xl shadow-elevation-1 p-space-md flex flex-col gap-space-sm min-w-0"
    >
      <div className="flex items-center justify-between gap-2 flex-wrap">
        <h2
          id="booking-addons-heading"
          className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5"
        >
          <PackagePlus className="h-4 w-4 text-culinary-700" aria-hidden="true" />
          Add-ons &amp; upgrades
        </h2>
        {isDirty ? <Badge tone="warning">Pending save</Badge> : null}
      </div>

      {lines.length > 0 ? (
        <ul className="flex flex-col gap-2">
          {lines.map((line) => {
            const item = master.find((entry) => entry.addonId === line.addonId)
            const frozen = savedLines.find((entry) => entry.addonId === line.addonId)
            const description =
              item?.description ?? frozen?.description ?? `Add-on ${line.addonId}`
            const amountId = `booking-menu-addon-amount-${line.addonId}`
            const error = validateAddonLineAmount(line.amount)

            return (
              <li
                key={line.addonId}
                className="flex flex-col gap-1 rounded-lg border border-artisan-border bg-surface-container-low p-space-sm min-w-0"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-body-sm text-body-sm font-semibold text-espresso-900 break-words">
                      {description}
                    </p>
                    <p className="font-caption text-caption text-espresso-600">
                      {frozen
                        ? `${addonPricingBasisLabel(frozen.pricingBasis)} · frozen line total ${formatCurrency(frozen.amount)}`
                        : 'A new line — the server freezes it on save'}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => onToggle(line.addonId, false)}
                    disabled={isLocked}
                    aria-label={`Remove ${description}`}
                    className="font-caption text-caption text-espresso-600 underline hover:text-espresso-900 shrink-0 disabled:opacity-60 disabled:cursor-not-allowed"
                  >
                    Remove
                  </button>
                </div>

                <label
                  htmlFor={amountId}
                  className="font-caption text-caption text-espresso-400 font-semibold uppercase"
                >
                  Line total for {description}
                </label>
                <CurrencyInput
                  id={amountId}
                  name={amountId}
                  value={line.amount}
                  disabled={isLocked}
                  aria-invalid={error ? true : undefined}
                  aria-describedby={error ? `${amountId}-error` : undefined}
                  onValueChange={(value) => onAmountChange(line.addonId, value)}
                  className={INPUT_CLASSES}
                />
                {error ? (
                  <p
                    id={`${amountId}-error`}
                    role="alert"
                    className="font-caption text-caption text-error"
                  >
                    {error}
                  </p>
                ) : null}
              </li>
            )
          })}
        </ul>
      ) : null}

      {lines.length === 0 && !isLocked ? (
        <p className="font-body-sm text-body-sm text-espresso-600 rounded-lg bg-surface-container-low border border-artisan-border p-space-sm">
          This event carries no add-on yet. Use&nbsp;Choose&nbsp;add-ons to pick from
          the catalogue.
        </p>
      ) : null}

      <Button
        variant="secondary"
        size="sm"
        icon={PackagePlus}
        disabled={isLocked}
        aria-haspopup="dialog"
        aria-expanded={isPickerOpen}
        data-testid="booking-addons-open-picker"
        onClick={onOpenPicker}
      >
        Choose add-ons
      </Button>

      <div className="flex items-center justify-between gap-2 border-t border-surface-container pt-space-sm">
        <span className="font-caption text-caption text-espresso-600">
          Saved add-on subtotal
        </span>
        <span className="font-mono text-body-sm font-bold text-espresso-900">
          {formatCurrency(savedAddonsTotal)}
        </span>
      </div>
      <p className="font-caption text-caption text-espresso-400">
        The line totals above are edited here and confirmed by the server when the
        selection is saved, which also recalculates the total contract value.
      </p>

      {isPickerOpen ? (
        <div
          className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/50 p-space-sm"
          data-testid="booking-addons-picker-overlay"
        >
          <div
            ref={pickerRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby="booking-addon-picker-title"
            data-testid="booking-addons-picker"
            className="bg-artisan-surface border border-artisan-border rounded-2xl shadow-elevation-3 w-full max-w-2xl max-h-[85vh] flex flex-col overflow-hidden"
          >
            <div className="flex items-start justify-between gap-2 p-space-md pb-space-sm">
              <h3
                id="booking-addon-picker-title"
                className="font-headline-sm text-headline-sm text-espresso-900"
              >
                Choose add-ons
              </h3>
              <button
                type="button"
                onClick={onClosePicker}
                aria-label="Close the add-on picker"
                className="p-1 rounded-md text-espresso-600 hover:text-espresso-900 hover:bg-surface-container-low focus-visible:outline-2 focus-visible:outline-culinary-600"
              >
                <X className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>

            <div className="px-space-md pb-space-sm">
              <label
                htmlFor="booking-addon-picker-category"
                className="font-caption text-caption text-espresso-400 font-semibold uppercase"
              >
                Filter by category
              </label>
              <select
                id="booking-addon-picker-category"
                className={INPUT_CLASSES}
                value={isCategoryNarrowed ? String(activeCategoryId) : ''}
                onChange={(event) =>
                  onCategoryChange(
                    event.target.value === '' ? null : Number(event.target.value),
                  )
                }
              >
                <option value="">All categories</option>
                {categories.map((category) => (
                  <option key={category.addonCategoryId} value={category.addonCategoryId}>
                    {category.addonCategoryName}
                  </option>
                ))}
              </select>
              {isCategoryLoading ? (
                <p className="font-caption text-caption text-espresso-400 mt-1">
                  Reading the categories…
                </p>
              ) : null}
              {categoryErrors.length > 0 ? (
                <div className="mt-2">
                  <AlertBanner
                    title="The categories could not be read"
                    errors={categoryErrors}
                    action={
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={onCategoryRetry}
                      >
                        Retry
                      </Button>
                    }
                  />
                </div>
              ) : null}
            </div>

            <div className="mx-space-md mb-space-md border border-artisan-border rounded-xl overflow-x-auto">
              <table className="w-full min-w-[36rem] text-left">
                <caption className="sr-only">
                  Active add-ons. Pick the ones this event should include.
                </caption>
                <thead>
                  <tr className="bg-surface-container-low font-caption text-caption text-espresso-400 uppercase">
                    <th scope="col" className="px-3 py-2 w-10">
                      <span className="sr-only">Include</span>
                    </th>
                    <th scope="col" className="px-3 py-2">
                      Add-on
                    </th>
                    <th scope="col" className="px-3 py-2">
                      Basis
                    </th>
                    <th scope="col" className="px-3 py-2 text-right">
                      Catalogue amount
                    </th>
                    <th scope="col" className="px-3 py-2 text-right">
                      Line total for this booking
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {master.length > 0
                    ? master.map((item) => (
                        <tr
                          key={item.addonId}
                          className="border-t border-artisan-border bg-surface-container-lowest"
                        >
                          <td className="px-3 py-2 align-top">
                            <input
                              type="checkbox"
                              checked={selectedIds.has(item.addonId)}
                              onChange={(event) =>
                                onToggle(item.addonId, event.target.checked)
                              }
                              className="h-4 w-4 rounded border-artisan-border text-culinary-600 focus:ring-2 focus:ring-culinary-500/20"
                              aria-label={`Include ${item.description}`}
                            />
                          </td>
                          <td className="px-3 py-2 align-top min-w-0">
                            <p className="font-body-sm text-body-sm font-semibold text-espresso-900 break-words">
                              {item.description}
                            </p>
                            <p className="font-caption text-caption text-espresso-600">
                              {item.addonCategoryName === null
                                ? 'No category'
                                : item.addonCategoryName}
                              {item.unit === null ? '' : ` · ${item.unit}`}
                            </p>
                          </td>
                          <td className="px-3 py-2 align-top font-caption text-caption text-espresso-600">
                            {addonPricingBasisLabel(item.pricingBasis)}
                          </td>
                          <td className="px-3 py-2 align-top text-right font-mono text-caption text-espresso-700">
                            {formatCurrency(item.amount)}
                          </td>
                          <td className="px-3 py-2 align-top text-right font-mono text-caption font-bold text-espresso-800">
                            {formatCurrency(item.lineAmountForBooking)}
                          </td>
                        </tr>
                      ))
                    : null}
                </tbody>
              </table>

              {isLoading ? (
                <div aria-busy="true" className="p-space-md">
                  <SpinnerLoader label="Loading the add-on master…" />
                </div>
              ) : null}

              {masterErrors.length > 0 ? (
                <div className="p-space-sm">
                  <AlertBanner
                    title="The add-on master could not be loaded"
                    errors={masterErrors}
                    action={
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={onMasterRetry}
                      >
                        Retry
                      </Button>
                    }
                  />
                </div>
              ) : null}

              {isMasterEmpty && isCategoryNarrowed ? (
                <div className="p-space-md">
                  <EmptyState
                    icon={PackagePlus}
                    title="Nothing in this category"
                    description="The category holds no active add-on. Choose All categories to see every active add-on."
                  />
                </div>
              ) : null}

              {isMasterEmpty && !isCategoryNarrowed ? (
                <div className="p-space-md">
                  <EmptyState
                    icon={PackagePlus}
                    title="No active add-ons are available"
                    description="The catalogue holds no active add-on to offer this booking. Add or activate add-ons in Settings → Add-ons."
                  />
                </div>
              ) : null}
            </div>

            <div className="flex justify-end gap-2 border-t border-surface-container p-space-md">
              <Button variant="primary" size="sm" onClick={onClosePicker}>
                Close
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  )
}
