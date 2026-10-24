import { Landmark } from 'lucide-react'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import { formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type {
  BookingDetails,
  BookingDiscountSource,
  BookingMenuSelectionDto,
} from '@/types/booking.types'
import { discountSourceLabel } from '../bookingVocabulary'

export interface BookingContractLedgerProps {
  /** The booking's frozen price snapshot (contract §1). */
  details: BookingDetails | null
  /** The selection's published add-on total and planning figures (contract §15.1). */
  menu: BookingMenuSelectionDto | null
}

/**
 * The contract ledger (spec §5.3).
 *
 * Every line is the **server's frozen figure**: the base package, the frozen
 * add-on total, the out-of-town surcharge with the per-guest rate it was
 * derived from, the discount with **the rule that produced it**, the VAT
 * disclosure (absent when no rate is configured) and the net amount due
 * (DEC-002, BR-26, DEC-143).
 *
 * The discount's source is the **server's** published `discountSource` (DEC-154),
 * and the band's description and rate per head are the server's too (DEC-160):
 * the ledger composes the wording and never hardcodes a bound. The frozen
 * snapshot is the fallback only while the menu payload is still loading.
 */
export function BookingContractLedger({ details, menu }: BookingContractLedgerProps) {
  const discountSource: BookingDiscountSource =
    menu?.discountSource ??
    (details === null || details.discountAmount <= 0
      ? 'None'
      : details.discountCode
        ? 'Code'
        : 'HeadcountBand')

  const marginPercentage =
    menu?.selectedGrossProfitPercentage ?? menu?.standardRunGrossProfitPercentage ?? null
  const foodCostPerPax =
    menu?.selectedFoodCostPerPax ?? menu?.standardRunFoodCostPerPax ?? null

  const rowClass =
    'flex items-center justify-between gap-2 border-b border-white/10 pb-2 text-caption'
  const labelClass = 'text-white/70'
  const valueClass = 'font-mono font-bold text-white'

  return (
    <section
      aria-labelledby="booking-ledger-heading"
      data-testid="booking-contract-ledger"
      className="rounded-xl bg-espresso-900 text-white p-space-md flex flex-col gap-space-sm min-w-0 shadow-elevation-2"
    >
      <h2
        id="booking-ledger-heading"
        className="font-headline-sm text-headline-sm flex items-center gap-1.5"
      >
        <Landmark className="h-4 w-4 text-caramel-200" aria-hidden="true" />
        Contract ledger
      </h2>

      {details === null ? (
        <p className="font-body-sm text-body-sm text-white/70">
          The ledger appears once the booking&apos;s frozen snapshot is read.
        </p>
      ) : (
        <dl className="flex flex-col gap-2">
          <div className={rowClass}>
            <dt className={labelClass}>Base catering package</dt>
            <dd className={valueClass}>{formatCurrency(details.packageAmount)}</dd>
          </div>
          <div className={rowClass}>
            <dt className={labelClass}>Selected add-ons</dt>
            <dd className={valueClass}>{formatCurrency(menu?.addonsTotal)}</dd>
          </div>
          <div className={rowClass}>
            <dt className={labelClass}>
              Out-of-town surcharge
              {details.extendedAreaRatePerGuest === null ? null : (
                <span className="block text-caption text-white/50">
                  {formatCurrency(details.extendedAreaRatePerGuest)} per guest
                </span>
              )}
            </dt>
            <dd className={valueClass}>{formatCurrency(details.extendedAreaAmount)}</dd>
          </div>
          <div className={rowClass}>
            <dt className={labelClass}>
              Discount
              <span className="block text-caption text-white/50">
                {discountSourceLabel(discountSource)}
                {details.discountCode ? ` — ${details.discountCode}` : ''}
              </span>
              {menu?.discountBandLabel ? (
                <span className="block text-caption text-white/50">
                  {menu.discountBandLabel}
                  {menu.discountBandRatePerHead === null
                    ? ''
                    : ` at ${formatCurrency(menu.discountBandRatePerHead)} per head`}
                </span>
              ) : null}
            </dt>
            <dd className={valueClass}>{formatCurrency(details.discountAmount)}</dd>
          </div>
          {menu !== null && menu.includedVatAmount !== null && menu.vatPercentage !== null ? (
            <div className={rowClass}>
              <dt className={labelClass}>
                VAT — included
                <span className="block text-caption text-white/50">
                  Already inside the total at {formatPercentage(menu.vatPercentage)}
                </span>
              </dt>
              <dd className={valueClass}>{formatCurrency(menu.includedVatAmount)}</dd>
            </div>
          ) : null}
          <div className="flex items-center justify-between gap-2 rounded-lg bg-white/10 p-2.5">
            <dt className="font-body-sm text-body-sm font-extrabold uppercase">
              Total contract value
            </dt>
            <dd className="font-mono text-data-md font-extrabold text-caramel-200">
              {formatCurrency(details.amountDue)}
            </dd>
          </div>
          <div className={rowClass}>
            <dt className={labelClass}>
              Agreed deposit
              <span className="block text-caption text-white/50">
                The configured required percentage is not published by the details projection.
              </span>
            </dt>
            <dd className={valueClass}>{formatCurrency(details.depositAmount)}</dd>
          </div>
          <div className={rowClass}>
            <dt className={labelClass}>Balance due</dt>
            <dd className={valueClass}>{formatCurrency(details.balance)}</dd>
          </div>
        </dl>
      )}

      <div className="flex flex-col gap-1 border-t border-white/10 pt-space-sm">
        <div className="flex items-center justify-between gap-2">
          <span className="font-caption text-caption text-white/70 uppercase">
            Commissary cost headroom
          </span>
          <span className="font-mono text-caption font-bold text-caramel-200">
            {formatPercentage(marginPercentage)}
          </span>
        </div>
        <ProgressTrack
          ratio={marginPercentage}
          label="Commissary cost headroom — the published gross profit percentage"
          tone="caramel"
        />
        <p className="font-caption text-caption text-white/60">
          Food cost {formatCurrency(foodCostPerPax)} per guest. A planning figure from the
          selection, never a charge on the contract.
        </p>
      </div>
    </section>
  )
}
