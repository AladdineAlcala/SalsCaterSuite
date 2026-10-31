import { FileText, MapPin, Pencil, Users, UtensilsCrossed } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import { EM_DASH, formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import { formatEventDateTime } from '@/utils/dateFormatter'
import { ratioPercentage } from '@/utils/progressRatio'
import type { BookingRegisterItem } from '@/types/booking.types'
import { PAX_UNIT, PLANNING_ESTIMATE_NOTE, formatClockTime } from '../bookingVocabulary'
import { BookingOverdueChip } from './BookingOverdueChip'
import { BookingPaymentStateChip } from './BookingPaymentStateChip'
import { BookingSelectionStateChip } from './BookingSelectionStateChip'
import { BookingStatusChip } from './BookingStatusChip'

export interface BookingDossierCardProps {
  item: BookingRegisterItem
  /** The card is rendered as a chronological rail entry. */
  isTimeline?: boolean
  /** Downloads the printable contract of this booking. */
  onDownloadContract: (transactionId: number) => void
  /** Opens the menu editor of this booking. */
  onOpenMenu: (transactionId: number) => void
  /** Opens the booking edit form. */
  onOpenEdit?: (transactionId: number) => void
}

/**
 * One dossier card of the register (spec §5.2 item 4).
 *
 * **Every figure on the card is the server's.** The client formats the
 * figures the API published and derives nothing (AC-BKG-02).
 *
 * The **countdown** is the server's `daysUntilEvent` — an integer the API
 * computes from its own clock. The browser never derives it (spec §5.2).
 *
 * The reference's per-card **Dish Costing** and **Manage Event** actions are
 * **not** rendered: the booking edit screen is a separate requirement and is
 * not part of this delivery (spec §1.4). The **Menu selection** action is: it
 * opens the menu editor for this booking (AC-BKG-63).
 *
 * The card carries the **selection state** chip and the **add-on count**, so
 * the operator can see from the register which events still need their dishes
 * chosen and which carry extras.
 *
 * The reference's footer BEO chip ("BEO Ready", "BEO Dispatched", "Cost Audit
 * Req") is replaced by an explicit **capability-gap notice**: there is no BEO
 * entity and no BEO production state (DEC-036, DEC-104).
 *
 * The reference's footer margin is rendered as the tier's **planning
 * estimate** — the tier's standard-run gross profit percentage, never a
 * per-event margin (DEC-103).
 */
export function BookingDossierCard({
  item,
  isTimeline = false,
  onDownloadContract,
  onOpenMenu,
  onOpenEdit,
}: BookingDossierCardProps) {
  /*
   * The progress track is a **display bound** between two figures the server
   * published: the collected amount over the net amount due. It is not a
   * derived business figure.
   */
  const collectedRatio = ratioPercentage(item.amountPaid, item.netAmountDue)

  const countdown =
    item.daysUntilEvent !== null && item.daysUntilEvent > 0
      ? `In ${item.daysUntilEvent} ${item.daysUntilEvent === 1 ? 'day' : 'days'}`
      : null

  const eventMoment = formatEventDateTime(`${item.eventDate}T${item.eventStartTime}`)

  return (
    <article
      className="bg-artisan-surface rounded-xl shadow-elevation-1 border border-artisan-border hover:shadow-elevation-2 transition-all overflow-hidden flex flex-col min-w-0"
      data-testid="booking-dossier-card"
    >
      <div className="p-space-md flex flex-col gap-space-md flex-1">
        {/* Header — the booking number, the lifecycle status and the payment state. */}
        <div className="flex items-start justify-between gap-2 border-b border-surface-container pb-space-sm">
          <div className="flex flex-col gap-1 min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <span className="font-mono text-data-sm font-bold text-espresso-900 break-all">
                {item.bookingNumber}
              </span>
              <BookingStatusChip status={item.status} />
              <BookingPaymentStateChip paymentState={item.paymentState} />
              <BookingSelectionStateChip selectionState={item.selectionState} />
              <BookingOverdueChip isOverdue={item.isOverdue} />
            </div>
            <span className="font-caption text-caption text-espresso-600 break-words">
              {countdown === null ? eventMoment : `${countdown} • ${eventMoment}`}
            </span>
          </div>

          <div className="flex items-center gap-1 shrink-0">
            {onOpenEdit && (
              <button
                type="button"
                onClick={() => onOpenEdit(item.transactionId)}
                title="Edit the booking core details"
                aria-label={`Edit ${item.bookingNumber}`}
                className="p-1.5 rounded-lg hover:bg-surface-container text-espresso-700 hover:text-espresso-900 transition-colors"
              >
                <Pencil className="h-4 w-4" aria-hidden="true" />
              </button>
            )}
            <button
              type="button"
              onClick={() => onDownloadContract(item.transactionId)}
              title="Download the printable catering contract"
              aria-label={`Download the contract for ${item.bookingNumber}`}
              className="p-1.5 rounded-lg hover:bg-surface-container text-espresso-700 hover:text-espresso-900 transition-colors"
            >
              <FileText className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
        </div>

        {/* The client and the occasion. */}
        <div className="flex flex-col gap-1 min-w-0">
          <h3 className="font-headline-sm text-headline-sm text-espresso-900 leading-snug break-words">
            {item.occasion ?? EM_DASH}
          </h3>
          <div className="flex items-center justify-between gap-2 text-espresso-600 text-caption flex-wrap">
            <span className="break-words">
              {item.customerName}
              {item.customerCompany ? ` · ${item.customerCompany}` : ''}
            </span>
            <span className="font-mono text-caption text-espresso-600 break-all">
              {item.customerContact ?? EM_DASH}
            </span>
          </div>
        </div>

        {/* The tier and the venue. */}
        <div className="p-space-sm rounded-xl bg-surface-container-low flex flex-col gap-space-sm min-w-0">
          <div className="flex items-start justify-between gap-2 flex-wrap">
            <div className="min-w-0">
              <div className="text-body-sm font-semibold text-espresso-900 break-words">
                {item.packageName ?? EM_DASH}
              </div>
              <div className="text-caption text-espresso-600 break-words">
                {item.packageType ?? 'No tier type stated'}
              </div>
            </div>
            <span className="px-2.5 py-1 rounded-md bg-artisan-surface text-espresso-900 font-mono text-caption font-bold shadow-sm whitespace-nowrap">
              {formatCount(item.numberOfHeads)} {PAX_UNIT} •{' '}
              {formatCurrency(item.pricePerHead)}
            </span>
          </div>
          <div className="flex items-center gap-1.5 text-espresso-700 text-caption pt-1 border-t border-surface-container min-w-0">
            <MapPin className="h-4 w-4 text-espresso-400 shrink-0" aria-hidden="true" />
            <span className="font-medium text-espresso-900 break-words">{item.venue ?? EM_DASH}</span>
          </div>
        </div>

        {/* The contract-value block. */}
        <div className="p-space-sm rounded-xl bg-surface-container-low/50 flex flex-col gap-space-sm">
          <div className="flex items-center justify-between gap-2 font-mono text-caption font-semibold flex-wrap">
            <span className="text-espresso-600 font-medium">Contract value</span>
            <span className="font-bold text-espresso-900 text-body-sm">
              {formatCurrency(item.netAmountDue)}
            </span>
          </div>
          <ProgressTrack
            ratio={collectedRatio}
            label={`Collected share of the contract value for ${item.bookingNumber}`}
            tone="culinary"
          />
          <div className="flex items-center justify-between gap-2 text-caption font-semibold flex-wrap">
            <span className="text-culinary-700">Paid: {formatCurrency(item.amountPaid)}</span>
            <span className="text-caramel-700">
              Outstanding: {formatCurrency(item.outstandingBalance)}
            </span>
          </div>
        </div>
      </div>

      {/* The footer strip — the BEO gap and the tier's planning estimate. */}
      <div className="px-space-md py-space-sm bg-surface-container-low border-t border-surface-container flex flex-col gap-space-sm">
        <CapabilityGapNotice
          title="BEO state unavailable"
          missing="There is no banquet event order entity, so this booking carries no production or sign-off state."
          requirement="REQ-REP-02"
          footnote="Deliberately excluded by DEC-104 — no BEO entity exists."
          className="px-2 py-1.5"
        >
          <span className="font-caption text-caption text-espresso-600 mt-0.5 inline-flex items-center gap-1">
            <Users className="h-3 w-3" aria-hidden="true" />
            No chef assignment is published for this booking.
          </span>
        </CapabilityGapNotice>

        <div className="flex items-center justify-between gap-2 text-caption flex-wrap">
          <span className="text-espresso-600" title={PLANNING_ESTIMATE_NOTE}>
            Tier planning estimate
          </span>
          <span className="font-mono font-bold text-culinary-700">
            {formatPercentage(item.baseGrossProfitPercentage)}
          </span>
        </div>

        <div className="flex items-center justify-between gap-2 text-caption flex-wrap">
          <span className="text-espresso-600">Add-ons</span>
          <span className="font-mono font-bold text-espresso-900">
            {formatCount(item.addonCount)} {item.addonCount === 1 ? 'line' : 'lines'} ·{' '}
            {formatCurrency(item.addonsTotal)}
          </span>
        </div>

        <Button
          variant="secondary"
          size="sm"
          icon={UtensilsCrossed}
          onClick={() => onOpenMenu(item.transactionId)}
          aria-label={`Menu selection for ${item.bookingNumber}`}
          className="w-full"
        >
          Menu selection
        </Button>

        {isTimeline ? (
          <span className="font-caption text-caption text-espresso-400">
            {eventMoment} • {formatClockTime(item.eventStartTime)} –{' '}
            {formatClockTime(item.eventEndTime)}
          </span>
        ) : null}
      </div>
    </article>
  )
}
