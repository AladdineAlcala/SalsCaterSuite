import { useEffect, useMemo, useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { CustomerDirectoryResponse } from '@/types/customer.types'
import type { AreaDto, PackageCourseRuleset, PackageDto } from '@/types/package.types'
import type {
  BookingQuote,
  BookingQuoteRequest,
  CreateBookingRequest,
  CreateBookingResponse,
} from '@/types/booking.types'

export const BOOKING_QUOTE_QUERY_KEY = 'booking-quote' as const
export const BOOKING_TIER_CATALOGUE_QUERY_KEY = 'booking-tier-catalogue' as const
export const BOOKING_MUNICIPALITY_CATALOGUE_QUERY_KEY =
  'booking-municipality-catalogue' as const
export const BOOKING_CLIENT_CATALOGUE_QUERY_KEY = 'booking-client-catalogue' as const
export const BOOKING_COURSE_RULESET_QUERY_KEY = 'booking-course-ruleset' as const

/** How long the quote inputs rest before the server is asked for a quote. */
const QUOTE_DEBOUNCE_MS = 300

/**
 * How many clients the picker offers at once.
 *
 * A paging window, not a business figure — the operator narrows the list with
 * the search box.
 */
const CLIENT_PICKER_PAGE_SIZE = 25

/** A 4xx is a business answer and will not improve on retry. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 1
}

/**
 * The stable identity of a quote request.
 *
 * The quote is keyed by this string rather than by the object reference, so
 * re-asking for the same inputs never re-prices and a changed input always
 * does.
 */
function quoteKey(request: BookingQuoteRequest | null): string {
  if (request === null) return ''
  return [
    request.customerId,
    request.packageId,
    request.noOfPerson,
    request.occasion,
    request.venue,
    request.eventDate,
    request.startTime,
    request.endTime,
    request.callTime ?? '',
    request.packoutTime ?? '',
    request.extendedAreaId ?? '',
    request.applyExtendedAmount,
    request.discountCode ?? '',
    request.depositAmount ?? '',
    request.initialPaymentAmount ?? '',
  ].join('|')
}

/**
 * `POST /api/bookings/quote` (contract §14.3).
 *
 * The quote is a **what-if**: it prices the proposed booking and persists
 * nothing. Every figure it returns is server-computed — the browser never
 * derives a base amount, a surcharge, a discount or a balance of its own
 * (DEC-002, BR-31).
 *
 * The inputs rest briefly before the server is asked, so a burst of keystrokes
 * costs one request instead of one request per digit.
 *
 * The query carries **no** placeholder data: the previous inputs' figures must
 * never reach the screen while the new ones are in flight. The
 * {@link isRecalculating} flag below is the explicit "recalculating" state the
 * rail shows instead — never a stale figure and never a zero (AC-BKG-15).
 */
export function useBookingQuote(request: BookingQuoteRequest | null) {
  const key = useMemo(() => quoteKey(request), [request])

  const [debounced, setDebounced] = useState<{
    request: BookingQuoteRequest | null
    key: string
  }>({ request, key })

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebounced({ request, key })
    }, QUOTE_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [key, request])

  const query = useQuery({
    queryKey: [BOOKING_QUOTE_QUERY_KEY, debounced.key],
    queryFn: async (): Promise<BookingQuote> => {
      if (debounced.request === null) {
        throw new ApiError(0, ['The booking cannot be priced yet.'])
      }
      const response = await apiClient.post<BookingQuote>(
        API_ENDPOINTS.bookings.quote,
        debounced.request,
      )
      return response.data
    },
    enabled: debounced.request !== null,
    staleTime: 0,
    retry: shouldRetry,
  })

  /** `true` while the typed inputs have not reached the server yet. */
  const isDebouncing = debounced.key !== key

  return { ...query, isRecalculating: isDebouncing || query.isFetching }
}

/**
 * `POST /api/bookings` (contract §14.4).
 *
 * The extended create — a **draft** (`confirm = false`, a `Pending` booking)
 * or a **confirmed** booking. A confirmed booking that carries an initial
 * payment records it in the same commit, so it is never a booking without its
 * payment (BR-33).
 *
 * A rejected payload persists nothing, and the server's `errors[]` are
 * rendered verbatim (BR-11).
 */
export function useCreateBooking() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: CreateBookingRequest): Promise<CreateBookingResponse> => {
      const response = await apiClient.post<CreateBookingResponse>(
        API_ENDPOINTS.bookings.create,
        request,
      )
      return response.data
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['booking-register'] })
      void queryClient.invalidateQueries({ queryKey: ['booking-summary'] })
    },
  })
}

/**
 * `GET /api/packages` — the buffet tier catalogue.
 *
 * The workbench's tier picker reads the **existing** catalogue; it never
 * fabricates a tier (AC-BKG-28).
 */
export function useBookingTierCatalogue() {
  return useQuery({
    queryKey: [BOOKING_TIER_CATALOGUE_QUERY_KEY],
    queryFn: async (): Promise<PackageDto[]> => {
      const response = await apiClient.get<PackageDto[]>(API_ENDPOINTS.packages.root)
      return response.data
    },
    staleTime: 300_000,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}

/**
 * `GET /api/areas` — the municipality catalogue.
 *
 * The workbench's municipality picker reads the **existing** catalogue; it
 * never fabricates a municipality (AC-BKG-28).
 */
export function useBookingMunicipalityCatalogue() {
  return useQuery({
    queryKey: [BOOKING_MUNICIPALITY_CATALOGUE_QUERY_KEY],
    queryFn: async (): Promise<AreaDto[]> => {
      const response = await apiClient.get<AreaDto[]>(API_ENDPOINTS.areas.root)
      return response.data
    },
    staleTime: 300_000,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}

/**
 * `GET /api/customers/directory` — the client catalogue the picker reads.
 *
 * The picker searches the **existing** client master; it never fabricates a
 * customer (AC-BKG-28).
 */
export function useBookingClientCatalogue(search: string) {
  return useQuery({
    queryKey: [BOOKING_CLIENT_CATALOGUE_QUERY_KEY, search.trim()],
    queryFn: async (): Promise<CustomerDirectoryResponse> => {
      const response = await apiClient.get<CustomerDirectoryResponse>(
        API_ENDPOINTS.customers.directory,
        {
          params: {
            search: search.trim() || undefined,
            page: 1,
            pageSize: CLIENT_PICKER_PAGE_SIZE,
          },
        },
      )
      return response.data
    },
    staleTime: 60_000,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}

/**
 * `GET /api/packages/{id}/course-rules` — the tier's approved ruleset.
 *
 * The workbench publishes the tier's **course-coverage ruleset and its
 * qualified pools** (contract §13.1) — the real, approved data the reference
 * stands in for with a per-event dish selection (DEC-105).
 */
export function useBookingCourseRuleset(packageId: number | null) {
  return useQuery({
    queryKey: [BOOKING_COURSE_RULESET_QUERY_KEY, packageId],
    queryFn: async (): Promise<PackageCourseRuleset> => {
      if (packageId === null) {
        throw new ApiError(0, ['No package tier is selected.'])
      }
      const response = await apiClient.get<PackageCourseRuleset>(
        API_ENDPOINTS.packages.courseRules(packageId),
      )
      return response.data
    },
    enabled: packageId !== null,
    staleTime: 60_000,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}
