import { RefreshCw } from 'lucide-react'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { getBookingStatusMeta } from '@/utils/bookingStatus'
import { cn } from '@/utils/cn'
import { formatCurrency } from '@/utils/currencyFormatter'
import { formatEventDate } from '@/utils/dateFormatter'
import type { CustomerStatement, CustomerStatementLine } from '@/types/customer.types'
import { agingTextClass, agingTone } from '../customerAging'
import { CustomerStateChip } from './CustomerStateChip'

export interface CustomerLedgerHistoryProps {
  statement: CustomerStatement | undefined
  isLoading: boolean
  errors: string[]
  onRetry: () => void
}

/**
 * One statement line, as a card.
 *
 * The design reference renders the ledger as a **stacked list of cards**, not
 * as a table: the drawer is five columns wide, and a seven-column table
 * cannot fit it — the paid, balance and aging columns would be off-screen.
 * The card carries the same anatomy as the reference (a title, a meta line
 * and the money on the right) and holds every one of the line's data points.
 */
function StatementLineCard({ line }: { line: CustomerStatementLine }) {
  const statusMeta = getBookingStatusMeta(line.status)
  // An empty occasion is a missing occasion — the booking number names it.
  const title = line.occasion && line.occasion.length > 0 ? line.occasion : line.bookingNumber
  // The chip is omitted entirely when the line carries no balance.
  const hasAging = line.agingBucket !== null
  // A settled balance reads as settled; an owed one reads as owed, and an aged
  // one escalates to the overdue tone.
  const balanceTone = line.balance <= 0 ? 'text-culinary-700' : agingTextClass(line.agingBucket)

  return (
    <li className="flex items-start justify-between gap-2 p-2.5 rounded-lg bg-artisan-surface border border-artisan-border">
      <div className="flex flex-col gap-1 min-w-0">
        <span className="font-headline-sm text-body-sm font-bold text-espresso-900 break-words">
          {title}
        </span>
        <span className="font-caption text-caption text-espresso-500 break-words">
          {line.bookingNumber} &bull; {formatEventDate(line.eventDate)}
        </span>
        <div className="flex items-center gap-1.5 flex-wrap">
          {/*
            The BR-10 lifecycle name the server sent, humanised through the
            shared helper — the same label the Operations Dashboard and the
            Upcoming Schedule show. A status the client does not know keeps
            its raw text on a neutral chip.
          */}
          <Badge tone={statusMeta.tone} icon={statusMeta.icon}>
            {statusMeta.label}
          </Badge>
          {hasAging ? (
            /* The server's own bucket label — never computed here. */
            <Badge tone={agingTone(line.agingBucket)}>{line.agingBucketLabel}</Badge>
          ) : null}
        </div>
      </div>

      <div className="flex flex-col items-end gap-1 shrink-0">
        <div className="flex items-baseline gap-1.5">
          <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Amount
          </span>
          <span className="font-data-sm text-data-sm text-espresso-900 font-bold">
            {formatCurrency(line.amount)}
          </span>
        </div>
        <div className="flex items-baseline gap-1.5">
          <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Paid
          </span>
          <span className="font-data-sm text-data-sm text-espresso-700">
            {formatCurrency(line.paid)}
          </span>
        </div>
        <div className="flex items-baseline gap-1.5">
          <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Balance
          </span>
          <span className={cn('font-data-sm text-data-sm font-bold', balanceTone)}>
            {formatCurrency(line.balance)}
          </span>
        </div>
      </div>
    </li>
  )
}

/**
 * The Ledger History tab (spec §4.9).
 *
 * The statement's totals sit above the list, exactly as the design reference
 * does. Every line of the statement is rendered with the server's own
 * figures, the server's status name and the server's aging label — the client
 * never counts days and never derives a bucket (AC-C3).
 */
export function CustomerLedgerHistory({
  statement,
  isLoading,
  errors,
  onRetry,
}: CustomerLedgerHistoryProps) {
  if (errors.length > 0) {
    return (
      <AlertBanner
        title="The statement of account could not be loaded"
        errors={errors}
        action={
          <Button variant="secondary" size="sm" icon={RefreshCw} onClick={onRetry}>
            Retry
          </Button>
        }
      />
    )
  }

  if (isLoading || !statement) {
    return (
      <p className="font-body-sm text-body-sm text-espresso-600" role="status">
        Loading the statement of account…
      </p>
    )
  }

  return (
    <div className="flex flex-col gap-space-md">
      <div className="flex items-center justify-between gap-2 p-2.5 rounded-lg bg-surface-container-low border border-artisan-border flex-wrap">
        <div className="flex flex-col">
          <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Total Billed
          </span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatCurrency(statement.totalBilled)}
          </span>
        </div>
        <div className="flex flex-col">
          <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Total Paid
          </span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatCurrency(statement.totalPaid)}
          </span>
        </div>
        <div className="flex flex-col items-end">
          <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Outstanding Balance
          </span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            {formatCurrency(statement.outstandingBalance)}
          </span>
          <CustomerStateChip
            ledgerState={statement.ledgerState}
            ledgerStateLabel={statement.ledgerStateLabel}
            className="mt-1"
          />
        </div>
      </div>

      {statement.transactions.length === 0 ? (
        <EmptyState
          title="No bookings on this ledger"
          description="The customer has no non-cancelled booking to bill yet."
        />
      ) : (
        <ul className="flex flex-col gap-2">
          {statement.transactions.map((line) => (
            <StatementLineCard key={line.transactionId} line={line} />
          ))}
        </ul>
      )}
    </div>
  )
}
