import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type {
  BookingRegisterPage,
  BookingRegisterSummary,
  BookingSortOption,
} from '@/types/booking.types'

export const BOOKING_REGISTER_QUERY_KEY = 'booking-register' as const
export const BOOKING_SUMMARY_QUERY_KEY = 'booking-summary' as const

/**
 * The filters the register owns (spec §3.3, BR-32).
 *
 * Every filter is applied **server-side** — the register endpoint accepts
 * them all, and the client never narrows a page in the browser (AC-BKG-03).
 */
export interface BookingRegisterFilters {
  /** Case-insensitive match over the booking number, occasion, customer and venue. */
  search: string
  /** A `BookingStatus` name, or an empty string for no status filter. */
  status: string
  /** A stored tier type, or an empty string for no tier filter. */
  packageType: string
  /** A stored venue, or an empty string for no venue filter. */
  venue: string
  /** A BR-28 payment state name, or an empty string for no payment filter. */
  paymentState: string
  /** The overdue dimension — a separate axis from the payment state. */
  overdue: boolean
  /** `yyyy-MM-dd` — the inclusive lower bound on the event date. */
  fromDate: string
  /** `yyyy-MM-dd` — the inclusive upper bound on the event date. */
  toDate: string
  /** The 1-based page. */
  page: number
  /** The page size — one of the values the contract accepts. */
  pageSize: number
  /** The server-side ordering. */
  sort: BookingSortOption
}

/** The filters the client sends to the server. */
function requestParams(filters: BookingRegisterFilters) {
  return {
    search: filters.search.trim() || undefined,
    status: filters.status || undefined,
    packageType: filters.packageType || undefined,
    venue: filters.venue || undefined,
    paymentState: filters.paymentState || undefined,
    overdue: filters.overdue ? true : undefined,
    fromDate: filters.fromDate || undefined,
    toDate: filters.toDate || undefined,
    page: filters.page,
    pageSize: filters.pageSize,
    sort: filters.sort,
  }
}

/** A 4xx is a business answer and will not improve on retry. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 1
}

/**
 * `GET /api/bookings` (contract §14.1).
 *
 * The register — the paged, filtered dossier together with the totals strip
 * and the facet counts. Every figure it publishes is server-computed; the
 * client formats and displays (DEC-002).
 *
 * The previous page stays on screen while the next one loads, so the operator
 * does not lose their place, and a stale response is marked as a refresh.
 */
export function useBookingRegister(filters: BookingRegisterFilters) {
  return useQuery({
    queryKey: [BOOKING_REGISTER_QUERY_KEY, filters],
    queryFn: async (): Promise<BookingRegisterPage> => {
      const response = await apiClient.get<BookingRegisterPage>(
        API_ENDPOINTS.bookings.root,
        { params: requestParams(filters) },
      )
      return response.data
    },
    staleTime: 30_000,
    refetchOnWindowFocus: true,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}

/**
 * `GET /api/bookings/summary` (contract §14.2).
 *
 * The four KPI cards, the payment-state distribution and the capacity load.
 * The aggregate describes the **same filtered population** as the list, and
 * the browser never derives one from the other (DEC-002).
 */
export function useBookingSummary(filters: BookingRegisterFilters) {
  return useQuery({
    queryKey: [BOOKING_SUMMARY_QUERY_KEY, filters],
    queryFn: async (): Promise<BookingRegisterSummary> => {
      const response = await apiClient.get<BookingRegisterSummary>(
        API_ENDPOINTS.bookings.summary,
        { params: requestParams(filters) },
      )
      return response.data
    },
    staleTime: 30_000,
    refetchOnWindowFocus: true,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}

/**
 * Saves a binary payload to the browser's download tray.
 *
 * The file name is taken from the server's `Content-Disposition` header so the
 * client never invents one.
 */
function saveDownload(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName
  anchor.rel = 'noopener'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(objectUrl)
}

function fileNameFrom(disposition: string | undefined, fallback: string): string {
  if (!disposition) return fallback
  const match = /filename="?([^";]+)"?/i.exec(disposition)
  return match?.[1] ?? fallback
}

/**
 * `GET /api/bookings/export` (contract §14.6).
 *
 * The export covers the **whole filtered population**, never the page, and it
 * honours exactly the filters the register is showing (AC-BKG-11).
 */
export function useBookingExport() {
  return useMutation({
    mutationFn: async (filters: BookingRegisterFilters): Promise<void> => {
      const response = await apiClient.get<Blob>(API_ENDPOINTS.bookings.export, {
        params: requestParams(filters),
        responseType: 'blob',
      })
      saveDownload(
        response.data,
        fileNameFrom(
          response.headers?.['content-disposition'] as string | undefined,
          'bookings-register.xlsx',
        ),
      )
    },
  })
}

/**
 * `GET /api/bookings/{id}/contract` (contract §14.5).
 *
 * The printable contract of one booking. The document renders the shared
 * calculator's figures, so it can never disagree with the ledger beside it.
 */
export function useBookingContractDownload() {
  return useMutation({
    mutationFn: async (transactionId: number): Promise<void> => {
      const response = await apiClient.get<Blob>(
        API_ENDPOINTS.bookings.contract(transactionId),
        { responseType: 'blob' },
      )
      saveDownload(
        response.data,
        fileNameFrom(
          response.headers?.['content-disposition'] as string | undefined,
          'catering-contract.pdf',
        ),
      )
    },
  })
}
