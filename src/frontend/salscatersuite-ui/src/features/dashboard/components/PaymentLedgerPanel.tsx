import { HandCoins } from 'lucide-react'
import { EmptyState } from '@/components/feedback/EmptyState'
import { formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { DashboardLedger, DashboardReceivables } from '@/types/dashboard.types'

export interface PaymentLedgerPanelProps {
  ledger: DashboardLedger
  receivables: DashboardReceivables
  isRefreshing?: boolean
}

/** The buckets the business treats as overdue (KPI-13, DEC-041). */
const OVERDUE_BUCKET_KEYS = new Set(['Days31To60', 'Days61To90', 'Over90Days'])

interface LedgerRowProps {
  label: string
  value: string
  caption?: string
  valueTone?: 'default' | 'caramel'
}

function LedgerRow({ label, value, caption, valueTone = 'default' }: LedgerRowProps) {
  return (
    <div className="p-3 rounded-lg bg-surface-container-low flex items-center justify-between gap-2">
      <div className="flex flex-col min-w-0">
        <span className="font-caption text-caption uppercase text-espresso-400 font-bold">
          {label}
        </span>
        <span
          className={`font-mono text-data-md font-bold tabular-nums ${
            valueTone === 'caramel' ? 'text-caramel-700' : 'text-espresso-900'
          }`}
        >
          {value}
        </span>
      </div>
      {caption ? (
        <span className="font-caption text-caption text-espresso-600 shrink-0">{caption}</span>
      ) : null}
    </div>
  )
}

/**
 * Column 3 of the operations matrix - the payment ledger and receivables
 * position (spec §9.2 item 5, KPI-13 / KPI-17 / KPI-18 / KPI-19).
 *
 * The mockup's "+14.2%" 7-day delta, the "30% downpayment" percentage and the
 * "Compliant" stamp are deliberately absent: the API publishes no prior
 * 7-day comparison, the deposit percentage is unresolved (DEC-009) and there
 * is no document-compliance engine (spec §3.2).
 *
 * The mockup's dead "View Aging" affordance is replaced by the server's own
 * AR aging breakdown (KPI-13), so the "Overdue (>30 days)" figure on the
 * receivables card is actionable on this screen. Every bucket figure comes
 * from the API - the browser computes no day counts (spec §6.7).
 */
export function PaymentLedgerPanel({ ledger, receivables, isRefreshing = false }: PaymentLedgerPanelProps) {
  const hasLedgerActivity = Boolean(
    ledger.collectedLast7DaysAmount ||
      ledger.awaitingDepositBookingCount ||
      ledger.receiptsIssuedCount,
  )
  const agingBuckets = receivables.agingBuckets ?? []
  const hasAging = agingBuckets.some(
    (bucket) => bucket.bookingCount > 0 || bucket.amount > 0,
  )

  return (
    <section className="bg-artisan-surface border border-artisan-border rounded-xl p-space-lg shadow-elevation-1 flex flex-col justify-between gap-space-md">
      <div className="flex flex-col gap-space-md">
        <div className="flex flex-wrap items-start justify-between gap-2 pb-1">
          <div className="min-w-0">
            <h3 className="font-headline-sm text-headline-sm text-espresso-900">
              Payment Ledger &amp; Receivables
            </h3>
            <p className="font-caption text-caption text-espresso-600">
              Cash-in over the trailing week and contract receivables
            </p>
          </div>
        </div>

        {hasLedgerActivity ? (
          <div className="flex flex-col gap-3">
            <LedgerRow
              label="Collected (Past 7 Days)"
              value={formatCurrency(ledger.collectedLast7DaysAmount)}
              caption={`${formatCount(ledger.collectedLast7DaysReceiptCount)} receipts`}
            />
            <LedgerRow
              label="Bookings Awaiting Deposit"
              value={formatCurrency(ledger.awaitingDepositAmount)}
              caption={`${formatCount(ledger.awaitingDepositBookingCount)} bookings`}
              valueTone="caramel"
            />
            <LedgerRow
              label="Official Receipts Issued"
              value={`${formatCount(ledger.receiptsIssuedCount)} vouchers`}
              caption="OR- receipt series"
            />
          </div>
        ) : (
          <EmptyState
            icon={HandCoins}
            title="No ledger activity"
            description="No payment was recorded in the trailing seven days and no booking is awaiting a deposit."
          />
        )}

        {hasAging ? (
          <div className="flex flex-col gap-2">
            <div className="flex items-center justify-between gap-2">
              <span className="font-eyebrow text-eyebrow uppercase text-espresso-400 tracking-wider">
                Receivables Aging
              </span>
              <span className="font-caption text-caption text-espresso-600">
                {formatCount(receivables.bookingCount)} ledgers
              </span>
            </div>
            <ul
              className="flex flex-col gap-1.5"
              aria-label="Receivables aging buckets"
            >
              {agingBuckets.map((bucket) => {
                const isOverdue = OVERDUE_BUCKET_KEYS.has(bucket.key)
                return (
                  <li
                    key={bucket.key}
                    className="flex items-center justify-between gap-2"
                  >
                    <span className="font-caption text-caption text-espresso-600 truncate">
                      {bucket.label}{' '}
                      <span className="text-espresso-400">({formatCount(bucket.bookingCount)})</span>
                    </span>
                    <span
                      className={`font-mono text-data-sm font-bold tabular-nums shrink-0 ${
                        isOverdue ? 'text-caramel-700' : 'text-espresso-900'
                      }`}
                    >
                      {formatCurrency(bucket.amount)}
                    </span>
                  </li>
                )
              })}
            </ul>
          </div>
        ) : null}
      </div>

      <div className="flex items-center gap-2 pt-space-xs text-espresso-700 font-caption text-caption font-semibold">
        <span
          className={`relative flex h-2 w-2 ${
            isRefreshing ? 'bg-caramel-500' : 'bg-culinary-600'
          } rounded-full`}
          aria-hidden="true"
        />
        <span>
          {isRefreshing
            ? 'Refreshing ledger figures…'
            : 'Ledger figures calculated server-side for the active period'}
        </span>
      </div>
    </section>
  )
}
