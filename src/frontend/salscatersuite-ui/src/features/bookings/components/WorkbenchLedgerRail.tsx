import { BadgeCheck, Gauge, Save } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import { formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { BookingQuote } from '@/types/booking.types'
import type { BookingWorkbenchDraft, BookingWorkbenchErrors } from '../bookingVocabulary'
import { NOT_STATED, PAX_UNIT, PAYMENT_MEANS_OPTIONS } from '../bookingVocabulary'
import { WorkbenchSelectField, WorkbenchTextField } from './WorkbenchFields'
import { WorkbenchChecklist } from './WorkbenchChecklist'

export interface WorkbenchLedgerRailProps {
  draft: BookingWorkbenchDraft
  errors: BookingWorkbenchErrors
  onChange: (patch: Partial<BookingWorkbenchDraft>) => void
  /** The server's quote. `null` while the booking cannot be priced yet. */
  quote: BookingQuote | null
  /** `true` while the quote is being re-priced. */
  isRecalculating: boolean
  /** The quote's own error, when the server refused it. */
  quoteErrors: string[]
  onSaveDraft: () => void
  onConfirm: () => void
  isSaving: boolean
  canSave: boolean
  canConfirm: boolean
}

/**
 * Column 3 — the financial & seal rail (spec §5.3).
 *
 * **The live ledger is the quote endpoint's output.** Every figure on this
 * rail — the base amount, the surcharge, the discount, the gross, the net, the
 * deposit and the balance — is the server's; the browser never prices a
 * booking (DEC-002, BR-31).
 *
 * While the quote is in flight the rail shows an explicit **recalculating**
 * state — never a figure priced for a different headcount, and never a zero in
 * its place (AC-BKG-15).
 *
 * The **capacity gauge** reads "not stated" when no capacity is configured:
 * the capacity is configuration and absent by default (DEC-102).
 *
 * The **required deposit** is `null` when no percentage is configured and
 * reads "not stated" — the reference's `30` never appears (DEC-101).
 */
export function WorkbenchLedgerRail({
  draft,
  errors,
  onChange,
  quote,
  isRecalculating,
  quoteErrors,
  onSaveDraft,
  onConfirm,
  isSaving,
  canSave,
  canConfirm,
}: WorkbenchLedgerRailProps) {
  const capacity = quote?.capacity ?? null

  return (
    <div className="flex flex-col gap-space-md min-w-0">
      {/* The capacity gauge. */}
      <Card elevation={1} className="flex flex-col gap-space-sm min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <h2 className="font-headline-sm text-headline-sm text-espresso-900 flex items-center gap-1.5">
            <Gauge className="h-4 w-4 text-culinary-700" aria-hidden="true" />
            Commissary capacity
          </h2>
          <span className="font-mono text-caption font-extrabold text-culinary-700">
            {capacity === null
              ? NOT_STATED
              : `${formatCount(capacity.committedPax)} / ${
                  capacity.isConfigured ? formatCount(capacity.capacityPax) : NOT_STATED
                } ${PAX_UNIT}`}
          </span>
        </div>
        <ProgressTrack
          ratio={capacity?.utilisationPercentage ?? null}
          label="Commissary capacity utilisation"
          tone="caramel"
        />
        <p className="font-caption text-caption text-espresso-600">
          {capacity !== null && capacity.isConfigured
            ? 'The near-term committed headcount against the configured capacity.'
            : 'No commissary capacity is configured, so the load is published without a ceiling.'}
        </p>
      </Card>

      {/* The contract & ledger rail. */}
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">Contract &amp; ledger</h2>

        <WorkbenchTextField
          id="booking-discount-code"
          label="Discount code"
          value={draft.discountCode}
          onChange={(value) => onChange({ discountCode: value })}
          error={errors.discountCode}
          placeholder="Optional"
          hint="A code from the discount catalogue"
        />

        <WorkbenchTextField
          id="booking-deposit-amount"
          label="Stated deposit"
          type="number"
          value={draft.depositAmount}
          onChange={(value) => onChange({ depositAmount: value })}
          error={errors.depositAmount}
          hint="What the client agreed to leave as a deposit"
        />

        {quoteErrors.length > 0 ? (
          <ul role="alert" className="list-disc pl-4 flex flex-col gap-0.5">
            {quoteErrors.map((message) => (
              <li key={message} className="font-caption text-caption text-error break-words">
                {message}
              </li>
            ))}
          </ul>
        ) : null}

        {isRecalculating ? (
          <p
            role="status"
            className="font-body-sm text-body-sm text-caramel-700 font-semibold"
            data-testid="booking-quote-recalculating"
          >
            Recalculating the quote…
          </p>
        ) : null}

        {quote === null && !isRecalculating ? (
          <p className="font-body-sm text-body-sm text-espresso-600">
            The ledger appears once the booking can be priced.
          </p>
        ) : null}

        {quote !== null && !isRecalculating ? (
          <dl className="flex flex-col gap-2">
            <div className="flex items-center justify-between gap-2 border-b border-artisan-border pb-2">
              <dt className="font-body-sm text-body-sm text-espresso-600">Base catering package</dt>
              <dd className="font-mono text-body-sm font-bold text-espresso-900">
                {formatCurrency(quote.baseAmount)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2 border-b border-artisan-border pb-2">
              <dt className="font-body-sm text-body-sm text-espresso-600">
                Out-of-town surcharge
                {quote.extendedAreaName ? (
                  <span className="block font-caption text-caption text-espresso-400">
                    {quote.extendedAreaName}
                  </span>
                ) : null}
              </dt>
              <dd className="font-mono text-body-sm font-bold text-espresso-900">
                {formatCurrency(quote.extendedAreaAmount)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2 border-b border-artisan-border pb-2">
              <dt className="font-body-sm text-body-sm text-espresso-600">
                Discount
                {quote.discountCode ? (
                  <span className="block font-caption text-caption text-espresso-400">
                    {quote.discountCode}
                  </span>
                ) : null}
              </dt>
              <dd className="font-mono text-body-sm font-bold text-espresso-900">
                {formatCurrency(quote.discountAmount)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2 border-b border-artisan-border pb-2">
              <dt className="font-body-sm text-body-sm text-espresso-600">Gross amount</dt>
              <dd className="font-mono text-body-sm font-bold text-espresso-900">
                {formatCurrency(quote.grossAmount)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2 bg-surface-container rounded-lg p-2.5 border border-artisan-border">
              <dt className="font-body-sm text-body-sm font-extrabold text-espresso-900 uppercase">
                Total net contract
              </dt>
              <dd className="font-mono text-data-md font-extrabold text-culinary-700">
                {formatCurrency(quote.netAmountDue)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="font-body-sm text-body-sm text-espresso-600">Required deposit</dt>
              <dd className="font-mono text-body-sm font-bold text-espresso-900">
                {quote.requiredDepositAmount === null
                  ? NOT_STATED
                  : formatCurrency(quote.requiredDepositAmount)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="font-body-sm text-body-sm text-espresso-600">Effective deposit</dt>
              <dd className="font-mono text-body-sm font-bold text-espresso-900">
                {formatCurrency(quote.depositAmount)}
              </dd>
            </div>
            <div className="flex items-center justify-between gap-2">
              <dt className="font-body-sm text-body-sm text-espresso-600">Remaining balance due</dt>
              <dd className="font-mono text-body-sm font-bold text-espresso-700">
                {formatCurrency(quote.outstandingBalance)}
              </dd>
            </div>
          </dl>
        ) : null}
      </Card>

      {/* The logged instrument. */}
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <h2 className="font-headline-sm text-headline-sm text-espresso-900">Logged instrument</h2>
          <Badge tone="info">Recorded with the confirmed booking</Badge>
        </div>

        <WorkbenchSelectField
          id="booking-payment-means"
          label="Payment means"
          value={draft.paymentMeans}
          onChange={(value) =>
            onChange({ paymentMeans: value as BookingWorkbenchDraft['paymentMeans'] })
          }
          options={PAYMENT_MEANS_OPTIONS.map((option) => ({
            value: option.value,
            label: option.label,
          }))}
          placeholder="Cash"
        />

        <WorkbenchTextField
          id="booking-check-no"
          label="Check or reference number"
          value={draft.checkNo}
          onChange={(value) => onChange({ checkNo: value })}
          error={errors.checkNo}
          placeholder="Optional"
        />

        <WorkbenchTextField
          id="booking-initial-payment"
          label="Initial payment amount"
          type="number"
          value={draft.initialPaymentAmount}
          onChange={(value) => onChange({ initialPaymentAmount: value })}
          error={errors.initialPaymentAmount}
          hint="A draft never carries a payment"
        />

        <WorkbenchTextField
          id="booking-particular"
          label="Particulars"
          value={draft.particular}
          onChange={(value) => onChange({ particular: value })}
          error={errors.particular}
          placeholder="Optional"
        />
      </Card>

      {/* The pre-dispatch validation checklist. */}
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Pre-dispatch validation
        </h2>
        <WorkbenchChecklist checks={quote?.checks ?? []} />
      </Card>

      {/* The two actions. */}
      <div className="flex flex-col gap-2">
        <Button
          variant="primary"
          icon={BadgeCheck}
          onClick={onConfirm}
          disabled={!canConfirm || isSaving}
        >
          {isSaving ? 'Saving…' : 'Confirm booking'}
        </Button>
        <Button variant="secondary" icon={Save} onClick={onSaveDraft} disabled={!canSave || isSaving}>
          {isSaving ? 'Saving…' : 'Save draft'}
        </Button>
        <p className="font-caption text-caption text-espresso-400">
          {quote === null
            ? 'No amount can be confirmed before the booking is priced.'
            : 'A draft is created in the Pending status; a confirmed booking is created in the Confirmed status.'}
        </p>
      </div>
    </div>
  )
}
