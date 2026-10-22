import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent, KeyboardEvent as ReactKeyboardEvent } from 'react'
import { Check, Pencil, ReceiptText, RefreshCw, UserRound, X } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { Skeleton } from '@/components/ui/Skeleton'
import { useFocusTrap } from '@/components/navigation/useFocusTrap'
import { cn } from '@/utils/cn'
import { useCustomerDetail } from '../hooks/useCustomerDetail'
import { useCustomerStatement } from '../hooks/useCustomerStatement'
import { useCreateCustomer, useUpdateCustomer } from '../hooks/useCustomerMutations'
import { CustomerLedgerHistory } from './CustomerLedgerHistory'
import {
  CustomerProfileForm,
  EMPTY_CUSTOMER_FORM_VALUES,
  formValuesFromCustomer,
  toUpsertRequest,
} from './CustomerProfileForm'
import type { CustomerFormValues } from './CustomerProfileForm'

export type CustomerDrawerMode = 'view' | 'create'

export interface CustomerDrawerProps {
  mode: CustomerDrawerMode
  /** The customer being edited — `null` while creating. */
  customerId: number | null
  onClose: () => void
  /** Called after a successful save — the caller closes the drawer. */
  onSaved: () => void
  /** Downloads the statement of account of the open customer. */
  onPrintSoa: (customerId: number) => void
}

type DrawerTab = 'profile' | 'ledger'

const TABS: Array<{ id: DrawerTab; label: string; icon: typeof UserRound }> = [
  { id: 'profile', label: 'Profile & Contact', icon: UserRound },
  { id: 'ledger', label: 'Ledger History', icon: ReceiptText },
]

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The customer could not be saved.']
}

/**
 * The customer detail drawer (spec §4.7).
 *
 * It holds the header, the two tabs and the footer actions. The profile tab
 * edits only the seven real attributes; the ledger tab renders the statement
 * the API published. The panel traps the keyboard focus and closes on
 * `Escape`.
 */
export function CustomerDrawer({
  mode,
  customerId,
  onClose,
  onSaved,
  onPrintSoa,
}: CustomerDrawerProps) {
  const drawerRef = useRef<HTMLDivElement>(null)
  const [activeTab, setActiveTab] = useState<DrawerTab>('profile')
  const [formValues, setFormValues] = useState<CustomerFormValues>(
    EMPTY_CUSTOMER_FORM_VALUES,
  )
  const [saveErrors, setSaveErrors] = useState<string[]>([])

  const detailQuery = useCustomerDetail(mode === 'view' ? customerId : null)
  const statementQuery = useCustomerStatement(
    mode === 'view' ? customerId : null,
    activeTab === 'ledger',
  )
  const createCustomer = useCreateCustomer()
  const updateCustomer = useUpdateCustomer()

  const isSaving = createCustomer.isPending || updateCustomer.isPending

  useFocusTrap(drawerRef, true, onClose)

  // The form is seeded once per customer. A refetch of the same record never
  // overwrites what the operator has typed (AC-C12).
  useEffect(() => {
    if (detailQuery.data) {
      setFormValues(formValuesFromCustomer(detailQuery.data))
    }
  }, [detailQuery.data?.customerId])

  const handleFieldChange = useCallback(
    (field: keyof CustomerFormValues, value: string) => {
      setFormValues((current) => ({ ...current, [field]: value }))
    },
    [],
  )

  const handleTabKeyDown = useCallback(
    (event: ReactKeyboardEvent<HTMLButtonElement>) => {
      if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft' && event.key !== 'Home' && event.key !== 'End') {
        return
      }
      event.preventDefault()
      const index = TABS.findIndex((tab) => tab.id === activeTab)
      let next = index
      if (event.key === 'ArrowRight') next = (index + 1) % TABS.length
      if (event.key === 'ArrowLeft') next = (index - 1 + TABS.length) % TABS.length
      if (event.key === 'Home') next = 0
      if (event.key === 'End') next = TABS.length - 1
      setActiveTab(TABS[next].id)
    },
    [activeTab],
  )

  const handleSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault()
      if (isSaving) return

      setSaveErrors([])
      const request = toUpsertRequest(formValues)

      try {
        if (mode === 'create') {
          await createCustomer.mutateAsync(request)
        } else if (customerId !== null) {
          await updateCustomer.mutateAsync({ customerId, request })
        } else {
          return
        }
        onSaved()
      } catch (error) {
        // The server's `errors[]` are rendered verbatim and the typed input is
        // kept exactly as it is (AC-C12).
        setSaveErrors(readErrorMessages(error))
      }
    },
    [
      isSaving,
      mode,
      customerId,
      formValues,
      createCustomer,
      updateCustomer,
      onSaved,
    ],
  )

  const customer = detailQuery.data ?? null
  const title =
    mode === 'create' ? 'New Customer' : (customer?.company || customer?.fullName || 'Customer')
  const subtitle =
    mode === 'create'
      ? 'Register a new client profile'
      : customer
        ? customer.company
          ? `${customer.fullName} • #${customer.customerId}`
          : `#${customer.customerId}`
        : 'Loading the customer profile…'

  return (
    <div
      ref={drawerRef}
      role="dialog"
      aria-modal="true"
      aria-labelledby="customer-drawer-title"
      className="bg-artisan-surface rounded-xl shadow-elevation-2 border border-artisan-border overflow-hidden flex flex-col xl:sticky xl:top-20"
    >
      <header className="p-space-md bg-surface-container-low border-b border-artisan-border flex items-start justify-between gap-2">
        <div className="flex items-center gap-2.5 min-w-0">
          <span className="w-8 h-8 rounded-lg bg-culinary-100 text-culinary-800 flex items-center justify-center shrink-0">
            <Pencil className="h-4 w-4" aria-hidden="true" />
          </span>
          <div className="flex flex-col min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h2
                id="customer-drawer-title"
                className="font-headline-sm text-body-md font-bold text-espresso-900 truncate"
              >
                {title}
              </h2>
              <span className="px-2 py-0.5 rounded text-caption font-bold bg-culinary-50 text-culinary-700 border border-culinary-200 uppercase">
                {mode === 'create' ? 'New Customer' : 'Update Mode'}
              </span>
            </div>
            <span className="font-data-sm text-data-sm text-espresso-600 font-semibold truncate">
              {subtitle}
            </span>
          </div>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close customer panel"
          className="text-espresso-400 hover:text-espresso-700 transition-colors p-1 rounded shrink-0"
        >
          <X className="h-5 w-5" aria-hidden="true" />
        </button>
      </header>

      <div role="tablist" aria-label="Customer details" className="flex border-b border-artisan-border bg-surface px-space-md pt-2 gap-4">
        {TABS.map((tab) => {
          const isActive = tab.id === activeTab
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              id={`customer-tab-${tab.id}`}
              aria-selected={isActive}
              aria-controls={`customer-panel-${tab.id}`}
              tabIndex={isActive ? 0 : -1}
              onClick={() => setActiveTab(tab.id)}
              onKeyDown={handleTabKeyDown}
              className={cn(
                'pb-2 font-headline-sm text-body-sm border-b-2 transition-all flex items-center gap-1.5',
                isActive
                  ? 'font-bold text-culinary-700 border-culinary-600'
                  : 'font-medium text-espresso-600 hover:text-espresso-900 border-transparent',
              )}
            >
              <tab.icon className="h-4 w-4" aria-hidden="true" />
              <span>{tab.label}</span>
            </button>
          )
        })}
      </div>

      <form onSubmit={handleSubmit} className="flex flex-col flex-1 min-h-0">
        <div className="flex-1 overflow-y-auto p-space-md">
          {saveErrors.length > 0 ? (
            <div className="mb-space-md">
              <AlertBanner title="The customer could not be saved" errors={saveErrors} />
            </div>
          ) : null}

          {activeTab === 'profile' ? (
            <div
              role="tabpanel"
              id="customer-panel-profile"
              aria-labelledby="customer-tab-profile"
              className="flex flex-col"
            >
              {mode === 'view' && detailQuery.isPending ? (
                <div className="flex flex-col gap-2" role="status" aria-label="Loading the customer profile">
                  <Skeleton className="h-9 w-full" />
                  <Skeleton className="h-9 w-full" />
                  <Skeleton className="h-9 w-2/3" />
                </div>
              ) : null}

              {mode === 'view' && detailQuery.isError ? (
                <AlertBanner
                  title="The customer profile could not be loaded"
                  errors={readErrorMessages(detailQuery.error)}
                  action={
                    <Button
                      variant="secondary"
                      size="sm"
                      icon={RefreshCw}
                      onClick={() => void detailQuery.refetch()}
                    >
                      Retry
                    </Button>
                  }
                />
              ) : null}

              {mode === 'view' && customer ? (
                <CustomerProfileForm
                  mode={mode}
                  values={formValues}
                  onChange={handleFieldChange}
                  customer={customer}
                />
              ) : null}

              {mode === 'create' ? (
                <CustomerProfileForm
                  mode={mode}
                  values={formValues}
                  onChange={handleFieldChange}
                  customer={null}
                />
              ) : null}
            </div>
          ) : (
            <div
              role="tabpanel"
              id="customer-panel-ledger"
              aria-labelledby="customer-tab-ledger"
              className="flex flex-col"
            >
              <CustomerLedgerHistory
                statement={statementQuery.data}
                isLoading={statementQuery.isPending}
                errors={statementQuery.isError ? readErrorMessages(statementQuery.error) : []}
                onRetry={() => void statementQuery.refetch()}
              />
            </div>
          )}
        </div>

        <footer className="pt-3 px-space-md pb-space-md border-t border-artisan-border flex flex-col sm:flex-row items-center justify-between gap-2">
          <Button
            variant="secondary"
            size="sm"
            icon={ReceiptText}
            inactive={mode === 'create' || customerId === null}
            title={
              mode === 'create'
                ? 'Save the customer first to print a statement of account.'
                : 'Download the statement of account as a PDF.'
            }
            onClick={() => {
              if (customerId !== null) onPrintSoa(customerId)
            }}
          >
            Print SOA (PDF)
          </Button>

          <div className="flex items-center gap-2 w-full sm:w-auto">
            <Button variant="tertiary" size="sm" onClick={onClose} className="flex-1 sm:flex-none">
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              icon={Check}
              type="submit"
              disabled={isSaving}
              className="flex-1 sm:flex-none"
            >
              {isSaving
                ? 'Saving…'
                : mode === 'create'
                  ? 'Create Customer'
                  : 'Save & Update'}
            </Button>
          </div>
        </footer>
      </form>
    </div>
  )
}
