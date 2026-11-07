import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { StatusFooter } from '@/features/dashboard/components/StatusFooter'
import type { ApiConnectionState } from '@/features/dashboard/components/StatusFooter'
import type { BookingSortOption } from '@/types/booking.types'
import { BookingDossierGrid } from './components/BookingDossierGrid'
import { BookingEmptyState } from './components/BookingEmptyState'
import { BookingFilterBar } from './components/BookingFilterBar'
import { BookingKpiGrid } from './components/BookingKpiGrid'
import { BookingOperationalDock } from './components/BookingOperationalDock'
import { BookingRegisterHeader } from './components/BookingRegisterHeader'
import { BookingRegisterSkeleton } from './components/BookingRegisterSkeleton'
import { BookingTierPills } from './components/BookingTierPills'
import { BookingTotalsStrip } from './components/BookingTotalsStrip'
import { useBookingContractDownload, useBookingExport, useBookingRegister, useBookingSummary } from './hooks/useBookingRegister'
import type { BookingRegisterFilters } from './hooks/useBookingRegister'
import { bookingStatusFilterLabel, getPaymentStateMeta } from './bookingVocabulary'
import type { BookingRegisterView } from './bookingVocabulary'

/** The page size the register starts with (contract §14.1). */
const DEFAULT_PAGE_SIZE = 10

/** How long the search term rests before it is sent to the API. */
const SEARCH_DEBOUNCE_MS = 300

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/**
 * Names the active filters so the empty state says what is being excluded
 * (spec §5.2 "States", AC-BKG-09).
 */
function describeFilters(filters: BookingRegisterFilters): string | undefined {
  const parts: string[] = []

  if (filters.search.trim().length > 0) {
    parts.push(`the search “${filters.search.trim()}”`)
  }
  if (filters.status) {
    parts.push(`the ${bookingStatusFilterLabel(filters.status)} status`)
  }
  if (filters.packageType) {
    parts.push(`the ${filters.packageType} tier`)
  }
  if (filters.venue) {
    parts.push(`the ${filters.venue} venue`)
  }
  if (filters.paymentState) {
    parts.push(`the ${getPaymentStateMeta(filters.paymentState).label} payment state`)
  }
  if (filters.overdue) {
    parts.push('the overdue filter')
  }
  if (filters.fromDate) {
    parts.push(`an event date from ${filters.fromDate}`)
  }
  if (filters.toDate) {
    parts.push(`an event date up to ${filters.toDate}`)
  }

  return parts.length > 0 ? parts.join(' and ') : undefined
}

/**
 * The Bookings & Events Register — Screen A (spec §5.2).
 *
 * The screen renders exclusively from `GET /api/bookings` and
 * `GET /api/bookings/summary`. Every figure — the KPI cards, the totals
 * strip, the facet counts, the dossier cards, the capacity load and the
 * receivables — is the server's; the browser formats and displays, it never
 * computes (DEC-002).
 *
 * Every filter is **server-side**, so the totals strip and the facet counts
 * always describe the same population as the page.
 */
export function BookingsEventsPage() {
  const navigate = useNavigate()

  const [filters, setFilters] = useState<BookingRegisterFilters>({
    search: '',
    status: '',
    packageType: '',
    venue: '',
    paymentState: '',
    overdue: false,
    fromDate: '',
    toDate: '',
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
    sort: 'EventDateAsc',
  })
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [view, setView] = useState<BookingRegisterView>('grid')
  const [actionErrors, setActionErrors] = useState<string[]>([])

  const queryFilters = useMemo<BookingRegisterFilters>(
    () => ({ ...filters, search: debouncedSearch }),
    [filters, debouncedSearch],
  )

  const registerQuery = useBookingRegister(queryFilters)
  const summaryQuery = useBookingSummary(queryFilters)

  const exportRegister = useBookingExport()
  const downloadContract = useBookingContractDownload()

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

  const handleStatusChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, status: value, page: 1 }))
  }, [])

  const handleTierChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, packageType: value, page: 1 }))
  }, [])

  const handleVenueChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, venue: value, page: 1 }))
  }, [])

  const handlePaymentStateChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, paymentState: value, page: 1 }))
  }, [])

  const handleOverdueChange = useCallback((value: boolean) => {
    setFilters((current) => ({ ...current, overdue: value, page: 1 }))
  }, [])

  const handleFromDateChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, fromDate: value, page: 1 }))
  }, [])

  const handleToDateChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, toDate: value, page: 1 }))
  }, [])

  const handlePageChange = useCallback((page: number) => {
    setFilters((current) => ({ ...current, page }))
  }, [])

  const handlePageSizeChange = useCallback((pageSize: number) => {
    setFilters((current) => ({ ...current, pageSize, page: 1 }))
  }, [])

  const handleViewChange = useCallback((next: BookingRegisterView) => {
    setView(next)
    /*
     * The timeline is a **server-side sort**: it asks the API for the
     * chronological order instead of re-ordering one page in the browser,
     * which would misrepresent the population as a chronology (spec §5.2).
     */
    setFilters((current) => ({ ...current, sort: 'EventDateAsc' as BookingSortOption, page: 1 }))
  }, [])

  const handleExport = useCallback(() => {
    setActionErrors([])
    exportRegister.mutate(queryFilters, {
      onError: (error) => setActionErrors(readErrorMessages(error)),
    })
  }, [exportRegister, queryFilters])

  const handleDownloadContract = useCallback(
    (transactionId: number) => {
      setActionErrors([])
      downloadContract.mutate(transactionId, {
        onError: (error) => setActionErrors(readErrorMessages(error)),
      })
    },
    [downloadContract],
  )

  const handleCreate = useCallback(() => {
    setActionErrors([])
    navigate('/bookings/new')
  }, [navigate])

  const handleOpenMenu = useCallback(
    (transactionId: number) => {
      setActionErrors([])
      navigate(`/bookings/${transactionId}/menu`)
    },
    [navigate],
  )

  const handleOpenEdit = useCallback(
    (transactionId: number) => {
      setActionErrors([])
      navigate(`/bookings/${transactionId}/edit`)
    },
    [navigate],
  )

  const handleRetry = useCallback(() => {
    setActionErrors([])
    void registerQuery.refetch()
    void summaryQuery.refetch()
  }, [registerQuery, summaryQuery])

  const isPending = registerQuery.isPending || summaryQuery.isPending
  const isFetching = registerQuery.isFetching || summaryQuery.isFetching
  const hasFailed = Boolean(registerQuery.error || summaryQuery.error)

  const loadErrors = useMemo(() => {
    if (!hasFailed) return []
    const messages = [
      ...(registerQuery.error ? readErrorMessages(registerQuery.error) : []),
      ...(summaryQuery.error ? readErrorMessages(summaryQuery.error) : []),
    ]
    return Array.from(new Set(messages))
  }, [hasFailed, registerQuery.error, summaryQuery.error])

  const connection: ApiConnectionState = hasFailed
    ? 'offline'
    : isFetching
      ? 'refreshing'
      : 'live'

  const register = registerQuery.data
  const items = register?.items ?? []
  const facets = register?.quickViewCounts
  const activeFilterDescription = describeFilters(filters)

  /*
   * The two empty states are distinct (AC-BKG-09). A register that holds no
   * booking at all is only reachable with no filter applied; a filtered
   * register that matches nothing names the filters that excluded everything.
   */
  const isPopulationEmpty = (register?.totalCount ?? 0) === 0
  const hasActiveFilters = activeFilterDescription !== undefined

  return (
    <div className="flex flex-col gap-space-xl">
      <BookingRegisterHeader
        onExport={handleExport}
        onCreate={handleCreate}
        isExporting={exportRegister.isPending}
      />

      {actionErrors.length > 0 ? (
        <AlertBanner
          title="The request could not be completed"
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

      {isPending ? <BookingRegisterSkeleton /> : null}

      {hasFailed ? (
        <AlertBanner
          title="The bookings register could not be loaded"
          errors={loadErrors}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={handleRetry}>
              Retry
            </Button>
          }
        />
      ) : null}

      {summaryQuery.data ? <BookingKpiGrid summary={summaryQuery.data} /> : null}

      {register ? (
        <>
          <div className="flex flex-col gap-space-md">
            <BookingTierPills
              facets={facets?.packageTypes ?? []}
              activeType={filters.packageType}
              onTypeChange={handleTierChange}
            />

            <BookingFilterBar
              search={filters.search}
              onSearchChange={handleSearchChange}
              status={filters.status}
              onStatusChange={handleStatusChange}
              fromDate={filters.fromDate}
              onFromDateChange={handleFromDateChange}
              toDate={filters.toDate}
              onToDateChange={handleToDateChange}
              venue={filters.venue}
              onVenueChange={handleVenueChange}
              paymentState={filters.paymentState}
              onPaymentStateChange={handlePaymentStateChange}
              overdue={filters.overdue}
              onOverdueChange={handleOverdueChange}
              view={view}
              onViewChange={handleViewChange}
              statusFacets={facets?.statuses ?? []}
              venueFacets={facets?.venues ?? []}
              paymentStateFacets={facets?.paymentStates ?? []}
            />
          </div>

          {isPopulationEmpty && !hasActiveFilters ? (
            <BookingEmptyState mode="catalogue" onCreate={handleCreate} />
          ) : null}

          {isPopulationEmpty && hasActiveFilters ? (
            <BookingEmptyState
              mode="filtered"
              filterDescription={activeFilterDescription}
              onCreate={handleCreate}
            />
          ) : null}

          {!isPopulationEmpty ? (
            <BookingDossierGrid
              items={items}
              view={view}
              onDownloadContract={handleDownloadContract}
              onOpenMenu={handleOpenMenu}
              onOpenEdit={handleOpenEdit}
            />
          ) : null}

          <BookingTotalsStrip
            totals={
              register.summary ?? {
                bookingCount: 0,
                netContractedValue: 0,
                collectedAmount: 0,
                outstandingAmount: 0,
                totalPax: 0,
              }
            }
            page={register.page}
            pageSize={register.pageSize}
            totalCount={register.totalCount}
            totalPages={register.totalPages}
            onPageChange={handlePageChange}
            onPageSizeChange={handlePageSizeChange}
            isFetching={isFetching}
          />
        </>
      ) : null}

      {summaryQuery.data ? <BookingOperationalDock summary={summaryQuery.data} /> : null}

      <StatusFooter
        generatedAt={summaryQuery.data?.generatedAt ?? register?.generatedAt ?? ''}
        connection={connection}
      />
    </div>
  )
}
