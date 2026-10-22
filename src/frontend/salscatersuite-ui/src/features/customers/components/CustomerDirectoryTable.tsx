import type { ReactNode } from 'react'
import { FileText, Pencil, Users } from 'lucide-react'
import { EmptyState } from '@/components/feedback/EmptyState'
import { Badge } from '@/components/ui/Badge'
import { cn } from '@/utils/cn'
import { formatCount, formatCurrency } from '@/utils/currencyFormatter'
import type { CustomerDirectoryItem } from '@/types/customer.types'
import { CustomerStateChip } from './CustomerStateChip'

export interface CustomerDirectoryTableProps {
  items: CustomerDirectoryItem[]
  /** The true number of records in the filtered result set. */
  totalCount: number
  isLoading: boolean
  selectedCustomerId: number | null
  onOpen: (customerId: number) => void
  onEdit: (customerId: number) => void
  onDownloadSoa: (customerId: number) => void
  /** The pagination strip rendered in the card's footer. */
  footer?: ReactNode
  /**
   * A compact inline hint rendered beside the table title — used when the
   * detail panel is not open, so the directory keeps the full width.
   */
  hint?: ReactNode
}

/**
 * The customer directory table (spec §4.5).
 *
 * The customer code of the reference is **not** rendered — no customer-code
 * concept exists, so the surrogate `customerId` is shown (spec §1.2). Every
 * money figure, count and aging label is the server's.
 */
export function CustomerDirectoryTable({
  items,
  totalCount,
  isLoading,
  selectedCustomerId,
  onOpen,
  onEdit,
  onDownloadSoa,
  footer,
  hint,
}: CustomerDirectoryTableProps) {
  return (
    <div className="bg-artisan-surface rounded-xl shadow-elevation-1 border border-artisan-border overflow-hidden">
      <div className="px-space-md py-space-sm bg-surface-container-low border-b border-artisan-border flex items-center justify-between gap-2 flex-wrap">
        <div className="flex items-center gap-2">
          <span className="font-headline-sm text-body-md text-espresso-900 font-bold">
            Client Directory Ledger
          </span>
          <span className="px-2 py-0.5 rounded bg-surface-container-highest text-espresso-700 font-data-sm text-data-sm font-semibold">
            {formatCount(items.length)} of {formatCount(totalCount)} Listed
          </span>
        </div>
        {hint ? (
          <span className="font-caption text-caption text-espresso-600 flex items-center gap-1.5">
            <Users className="h-3.5 w-3.5" aria-hidden="true" />
            {hint}
          </span>
        ) : null}
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <caption className="sr-only">
            Customer directory — client, contact, booking spend and accounts receivable ledger state
          </caption>
          <thead>
            <tr className="bg-surface border-b border-artisan-border font-caption text-caption text-espresso-400 uppercase tracking-wider">
              <th scope="col" className="py-3 px-space-sm font-bold">
                Customer ID &amp; Type
              </th>
              <th scope="col" className="py-3 px-space-sm font-bold">
                Client / Organization
              </th>
              <th scope="col" className="py-3 px-space-sm font-bold">
                Contact &amp; Address
              </th>
              <th scope="col" className="py-3 px-space-sm font-bold">
                Bookings &amp; Spend
              </th>
              <th scope="col" className="py-3 px-space-sm font-bold text-right">
                AR Ledger
              </th>
              <th scope="col" className="py-3 px-space-sm font-bold text-center">
                Action
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-artisan-border font-body-sm text-body-sm">
            {items.map((item) => {
              const isSelected = item.customerId === selectedCustomerId

              return (
                <tr
                  key={item.customerId}
                  onClick={() => onOpen(item.customerId)}
                  className={cn(
                    'cursor-pointer transition-colors border-l-4',
                    isSelected
                      ? 'bg-culinary-50 border-l-culinary-600'
                      : 'border-l-transparent hover:bg-surface-container-low',
                  )}
                >
                  <td className="py-3 px-space-sm align-top">
                    <div className="flex flex-col gap-1 items-start">
                      <span className="font-data-sm text-data-sm text-espresso-900 font-bold">
                        #{item.customerId}
                      </span>
                      {/*
                        The derived classification (DEC-051). It is a fixed
                        two-value vocabulary the server sends, so the key text
                        itself is rendered. The aging state lives in the AR
                        Ledger column only — never twice in one row.
                      */}
                      <Badge tone={item.customerType === 'Corporate' ? 'info' : 'neutral'} shape="soft">
                        {item.customerType}
                      </Badge>
                    </div>
                  </td>

                  <td className="py-3 px-space-sm align-top">
                    <div className="flex flex-col gap-0.5">
                      <button
                        type="button"
                        onClick={(event) => {
                          event.stopPropagation()
                          onOpen(item.customerId)
                        }}
                        className="text-left font-headline-sm text-body-sm font-bold text-espresso-900 hover:text-culinary-700"
                      >
                        {item.fullName}
                      </button>
                      {item.company ? (
                        <span className="font-caption text-caption text-espresso-600">
                          {item.company}
                        </span>
                      ) : null}
                    </div>
                  </td>

                  <td className="py-3 px-space-sm align-top">
                    <div className="flex flex-col gap-0.5">
                      <span className="font-data-sm text-data-sm text-espresso-800">
                        {item.contact1 ?? '—'}
                      </span>
                      {item.contact2 ? (
                        <span className="font-caption text-caption text-espresso-600">
                          {item.contact2}
                        </span>
                      ) : null}
                      {item.address ? (
                        <span
                          className="block max-w-[6rem] truncate font-caption text-caption text-espresso-400"
                          title={item.address}
                        >
                          {item.address}
                        </span>
                      ) : null}
                    </div>
                  </td>

                  <td className="py-3 px-space-sm align-top">
                    <div className="flex flex-col gap-0.5">
                      <span className="font-data-sm text-data-sm text-espresso-900 font-semibold">
                        {formatCount(item.bookingCounts.past)} past &bull;{' '}
                        {formatCount(item.bookingCounts.upcoming)} upcoming
                      </span>
                      <span className="font-caption text-caption text-espresso-600">
                        Total: {formatCurrency(item.totalContractedValue)}
                      </span>
                    </div>
                  </td>

                  <td className="py-3 px-space-sm align-top text-right">
                    <div className="flex flex-col items-end gap-1">
                      <span
                        className={cn(
                          'font-data-sm text-data-sm font-bold',
                          item.outstandingBalance > 0
                            ? 'text-caramel-700'
                            : 'text-culinary-700',
                        )}
                      >
                        {formatCurrency(item.outstandingBalance)}
                      </span>
                      <CustomerStateChip
                        ledgerState={item.ledgerState}
                        ledgerStateLabel={item.ledgerStateLabel}
                      />
                    </div>
                  </td>

                  <td className="py-3 px-space-sm align-top text-center">
                    <div className="flex items-center justify-center gap-1">
                      <button
                        type="button"
                        title="Edit Customer"
                        aria-label={`Edit ${item.fullName}`}
                        onClick={(event) => {
                          event.stopPropagation()
                          onEdit(item.customerId)
                        }}
                        className="p-1 rounded text-espresso-600 hover:text-culinary-700 hover:bg-surface-container transition-colors"
                      >
                        <Pencil className="h-4 w-4" aria-hidden="true" />
                      </button>
                      <button
                        type="button"
                        title="Download Statement of Account (PDF)"
                        aria-label={`Download SOA for ${item.fullName}`}
                        onClick={(event) => {
                          event.stopPropagation()
                          onDownloadSoa(item.customerId)
                        }}
                        className="p-1 rounded text-espresso-600 hover:text-caramel-700 hover:bg-surface-container transition-colors"
                      >
                        <FileText className="h-4 w-4" aria-hidden="true" />
                      </button>
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>

        {items.length === 0 ? (
          <div className="p-space-md">
            {isLoading ? (
              <EmptyState
                title="Loading the customer directory…"
                description="The directory is being retrieved from the API."
                icon={Users}
              />
            ) : (
              <EmptyState
                title="No customers match the current filters"
                description="Adjust the search term, the account type or the AR state to widen the result set."
                icon={Users}
              />
            )}
          </div>
        ) : null}
      </div>

      {footer}
    </div>
  )
}
