import { useCallback, useEffect, useMemo, useState } from 'react'
import { RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { StatusFooter } from '@/features/dashboard/components/StatusFooter'
import type { ApiConnectionState } from '@/features/dashboard/components/StatusFooter'
import { cn } from '@/utils/cn'
import type {
  CustomerArStateFilter,
  CustomerTypeFilter,
} from '@/types/customer.types'
import { CustomerBanner } from './components/CustomerBanner'
import { CustomerDirectoryTable } from './components/CustomerDirectoryTable'
import { CustomerDrawer } from './components/CustomerDrawer'
import type { CustomerDrawerMode } from './components/CustomerDrawer'
import { CustomerFilterBar } from './components/CustomerFilterBar'
import { CustomerKpiGrid } from './components/CustomerKpiGrid'
import { CustomerMasterSkeleton } from './components/CustomerMasterSkeleton'
import { CustomerPagination } from './components/CustomerPagination'
import { useCustomerDirectory } from './hooks/useCustomerDirectory'
import type { CustomerDirectoryFilters } from './hooks/useCustomerDirectory'
import { useCustomerExport, useCustomerSoaDownload } from './hooks/useCustomerDownloads'

/** The page size the directory starts with (spec §4.6). */
const DEFAULT_PAGE_SIZE = 10

/** How long the search term rests before it is sent to the API. */
const SEARCH_DEBOUNCE_MS = 300

interface DrawerState {
  mode: CustomerDrawerMode
  customerId: number | null
}

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/**
 * The Customer Master & Accounts Ledger screen (spec §4).
 *
 * The screen renders exclusively from `GET /api/customers/directory` and the
 * customer's own detail and statement endpoints. Every figure — the KPI
 * summary, the balances, the counts, the aging state and its label — is the
 * server's; the browser formats and displays, it never computes (DEC-002).
 */
export function CustomerMasterPage() {
  const [filters, setFilters] = useState<CustomerDirectoryFilters>({
    search: '',
    customerType: '',
    arState: '',
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  })
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [drawer, setDrawer] = useState<DrawerState | null>(null)
  const [actionErrors, setActionErrors] = useState<string[]>([])

  const queryFilters = useMemo<CustomerDirectoryFilters>(
    () => ({ ...filters, search: debouncedSearch }),
    [filters, debouncedSearch],
  )

  const { data, isPending, isError, error, isFetching, refetch } =
    useCustomerDirectory(queryFilters)

  const exportDirectory = useCustomerExport()
  const downloadSoa = useCustomerSoaDownload()

  // The search term rests briefly before it is sent, so a burst of keystrokes
  // costs one request instead of one request per letter.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(filters.search)
      setFilters((current) => (current.page === 1 ? current : { ...current, page: 1 }))
    }, SEARCH_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [filters.search])

  const handleSearchChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, search: value }))
  }, [])

  const handleCustomerTypeChange = useCallback((value: CustomerTypeFilter) => {
    setFilters((current) => ({ ...current, customerType: value, page: 1 }))
  }, [])

  const handleArStateChange = useCallback((value: CustomerArStateFilter) => {
    setFilters((current) => ({ ...current, arState: value, page: 1 }))
  }, [])

  const handlePageChange = useCallback((page: number) => {
    setFilters((current) => ({ ...current, page }))
  }, [])

  const handlePageSizeChange = useCallback((pageSize: number) => {
    setFilters((current) => ({ ...current, pageSize, page: 1 }))
  }, [])

  const openCustomer = useCallback((customerId: number) => {
    setActionErrors([])
    setDrawer({ mode: 'view', customerId })
  }, [])

  const startCreate = useCallback(() => {
    setActionErrors([])
    setDrawer({ mode: 'create', customerId: null })
  }, [])

  const closeDrawer = useCallback(() => setDrawer(null), [])

  const handleSaved = useCallback(() => {
    setDrawer(null)
  }, [])

  const handleExport = useCallback(() => {
    setActionErrors([])
    exportDirectory.mutate(
      {
        search: debouncedSearch,
        customerType: filters.customerType,
        arState: filters.arState,
      },
      {
        onError: (mutationError) => setActionErrors(readErrorMessages(mutationError)),
      },
    )
  }, [debouncedSearch, filters.customerType, filters.arState, exportDirectory])

  const handlePrintSoa = useCallback(
    (customerId: number) => {
      setActionErrors([])
      downloadSoa.mutate(customerId, {
        onError: (mutationError) => setActionErrors(readErrorMessages(mutationError)),
      })
    },
    [downloadSoa],
  )

  const connection: ApiConnectionState = isError
    ? 'offline'
    : isFetching
      ? 'refreshing'
      : 'live'

  return (
    <div className="flex flex-col gap-space-xl">
      <CustomerBanner
        onExport={handleExport}
        isExporting={exportDirectory.isPending}
        onCreate={startCreate}
      />

      {actionErrors.length > 0 ? (
        <AlertBanner
          title="The requested document could not be produced"
          errors={actionErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setActionErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {isPending ? <CustomerMasterSkeleton /> : null}

      {isError ? (
        <AlertBanner
          title="The customer directory could not be loaded"
          errors={readErrorMessages(error)}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={() => void refetch()}>
              Retry
            </Button>
          }
        />
      ) : null}

      {data ? (
        <>
          <CustomerKpiGrid summary={data.summary} />

          <CustomerFilterBar
            search={filters.search}
            onSearchChange={handleSearchChange}
            customerType={filters.customerType}
            onCustomerTypeChange={handleCustomerTypeChange}
            arState={filters.arState}
            onArStateChange={handleArStateChange}
            quickViewCounts={data.quickViewCounts}
          />

          {/*
            The directory keeps the full width until a customer is selected.
            The detail panel is the only thing that earns the 7/5 split
            (spec §4.7) — a directory that shares its row with an empty panel
            would clip the AR Ledger column, which carries the screen's
            headline financial figure.
          */}
          <div className="grid grid-cols-1 xl:grid-cols-12 gap-gutter items-start">
            <div
              data-testid="customer-directory-column"
              className={cn(
                'flex flex-col gap-space-md',
                drawer ? 'xl:col-span-7' : 'xl:col-span-12',
              )}
            >
              <CustomerDirectoryTable
                items={data.items}
                totalCount={data.totalCount}
                isLoading={isPending}
                selectedCustomerId={drawer?.customerId ?? null}
                onOpen={openCustomer}
                onEdit={openCustomer}
                onDownloadSoa={handlePrintSoa}
                hint={drawer ? undefined : 'Select a client to open its profile'}
                footer={
                  <CustomerPagination
                    page={data.page}
                    pageSize={data.pageSize}
                    totalCount={data.totalCount}
                    totalPages={data.totalPages}
                    onPageChange={handlePageChange}
                    onPageSizeChange={handlePageSizeChange}
                    isFetching={isFetching}
                  />
                }
              />
            </div>

            {drawer ? (
              <div
                data-testid="customer-drawer-column"
                className="xl:col-span-5 flex flex-col"
              >
                <CustomerDrawer
                  key={`${drawer.mode}-${drawer.customerId ?? 'new'}`}
                  mode={drawer.mode}
                  customerId={drawer.customerId}
                  onClose={closeDrawer}
                  onSaved={handleSaved}
                  onPrintSoa={handlePrintSoa}
                />
              </div>
            ) : null}
          </div>
        </>
      ) : null}

      <StatusFooter generatedAt={data?.generatedAt ?? ''} connection={connection} />
    </div>
  )
}
