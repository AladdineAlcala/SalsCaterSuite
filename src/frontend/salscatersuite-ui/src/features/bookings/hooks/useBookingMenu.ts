import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type {
  AddonCategoryDto,
  AddonMasterItemDto,
  BookingAddonSetDto,
  BookingAddonSetRequest,
  BookingDetails,
  BookingMenuSelectionDto,
  BookingMenuSelectionRequest,
} from '@/types/booking.types'

export const BOOKING_DETAILS_QUERY_KEY = 'booking-details' as const
export const BOOKING_MENU_QUERY_KEY = 'booking-menu' as const
export const BOOKING_ADDONS_QUERY_KEY = 'booking-addons' as const
export const ADDON_MASTER_QUERY_KEY = 'addon-master' as const
export const ADDON_CATEGORY_QUERY_KEY = 'addon-category' as const

/** A 4xx is a business answer and will not improve on retry. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 1
}

/**
 * `GET /api/bookings/{id}` (contract §1).
 *
 * The booking's **frozen** price snapshot. The menu editor reads it for the
 * ledger's money chain and the event banner's identity — the selection
 * endpoint itself never prices anything (BR-37).
 */
export function useBookingDetails(transactionId: number | null) {
  return useQuery({
    queryKey: [BOOKING_DETAILS_QUERY_KEY, transactionId],
    queryFn: async (): Promise<BookingDetails> => {
      if (transactionId === null) {
        throw new ApiError(0, ['No booking is selected.'])
      }
      const response = await apiClient.get<BookingDetails>(
        `${API_ENDPOINTS.bookings.root}/${transactionId}`,
      )
      return response.data
    },
    enabled: transactionId !== null,
    staleTime: 30_000,
    retry: shouldRetry,
  })
}

/**
 * `GET /api/bookings/{id}/menus` (contract §15.1).
 *
 * The stored selection, the tier's qualified pools and the lock state. The
 * pools travel with the payload, so the client never fanned out a second call.
 */
export function useBookingMenu(transactionId: number | null) {
  return useQuery({
    queryKey: [BOOKING_MENU_QUERY_KEY, transactionId],
    queryFn: async (): Promise<BookingMenuSelectionDto> => {
      if (transactionId === null) {
        throw new ApiError(0, ['No booking is selected.'])
      }
      const response = await apiClient.get<BookingMenuSelectionDto>(
        API_ENDPOINTS.bookings.menus(transactionId),
      )
      return response.data
    },
    enabled: transactionId !== null,
    staleTime: 15_000,
    retry: shouldRetry,
  })
}

/** `GET /api/bookings/{id}/addons` (contract §16.3). */
export function useBookingAddonSet(transactionId: number | null) {
  return useQuery({
    queryKey: [BOOKING_ADDONS_QUERY_KEY, transactionId],
    queryFn: async (): Promise<BookingAddonSetDto> => {
      if (transactionId === null) {
        throw new ApiError(0, ['No booking is selected.'])
      }
      const response = await apiClient.get<BookingAddonSetDto>(
        API_ENDPOINTS.bookings.addons(transactionId),
      )
      return response.data
    },
    enabled: transactionId !== null,
    staleTime: 15_000,
    retry: shouldRetry,
  })
}

/**
 * `GET /api/addons` (contract §16.1).
 *
 * The booking picker is read-only; master administration lives in Settings
 * (DEC-168). The endpoint publishes active rows only. The headcount is sent as
 * the **required** `headcount` parameter so the server publishes
 * `lineAmountForBooking` — the client never multiplies a per-guest price itself
 * (DEC-158). With no positive headcount the query does not fire: the endpoint
 * prices a line for a booking and has no safe default (BR-20).
 */
/**
 * @param enabled gates the fetch — the menu picker reads this master only
 * while the **Choose add-ons** modal is open (spec §5.4, DEC-170), so a
 * page render never requests the catalogue by itself.
 */
export function useAddonMaster(
  pax: number | null,
  categoryId: number | null = null,
  enabled = true,
) {
  const headcount = pax !== null && pax > 0 ? pax : null

  return useQuery({
    queryKey: [ADDON_MASTER_QUERY_KEY, headcount, categoryId],
    queryFn: async (): Promise<AddonMasterItemDto[]> => {
      const response = await apiClient.get<AddonMasterItemDto[]>(API_ENDPOINTS.addons.root, {
        params: {
          headcount,
          categoryId: categoryId ?? undefined,
        },
      })
      return response.data
    },
    enabled: enabled && headcount !== null,
    staleTime: 120_000,
    retry: shouldRetry,
  })
}

/**
 * `GET /api/addons/categories` (contract §16.2).
 *
 * @param enabled gates the fetch — the modal's category filter is the only
 * consumer, so the categories are read only while the modal is open.
 */
export function useAddonCategories(enabled = true) {
  return useQuery({
    queryKey: [ADDON_CATEGORY_QUERY_KEY],
    queryFn: async (): Promise<AddonCategoryDto[]> => {
      const response = await apiClient.get<AddonCategoryDto[]>(
        API_ENDPOINTS.addons.categories,
      )
      return response.data
    },
    enabled,
    staleTime: 300_000,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}

/** Everything a successful selection write invalidates. */
function invalidateBookingReads(queryClient: ReturnType<typeof useQueryClient>): void {
  void queryClient.invalidateQueries({ queryKey: [BOOKING_MENU_QUERY_KEY] })
  void queryClient.invalidateQueries({ queryKey: [BOOKING_ADDONS_QUERY_KEY] })
  void queryClient.invalidateQueries({ queryKey: [BOOKING_DETAILS_QUERY_KEY] })
  void queryClient.invalidateQueries({ queryKey: ['booking-register'] })
  void queryClient.invalidateQueries({ queryKey: ['booking-summary'] })
}

/**
 * Issues one `GET /api/bookings/{id}` and installs the answer as the
 * details query's data (contract §1).
 *
 * The total contract value only becomes displayable when this **succeeds**
 * (DEC-002): a throw — or anything the caller cannot prove — must leave the
 * cached pre-save snapshot in place but absolutely must not be quoted as the
 * recalculated figure. The read is deliberately a single round trip through
 * the existing endpoint, so a wedged retry cycle can never hand an older
 * failure's answer to a fresh user retry.
 */
export async function readBookingDetailsIntoCache(
  transactionId: number,
  queryClient: ReturnType<typeof useQueryClient>,
): Promise<BookingDetails> {
  const response = await apiClient.get<BookingDetails>(
    `${API_ENDPOINTS.bookings.root}/${transactionId}`,
  )
  queryClient.setQueryData([BOOKING_DETAILS_QUERY_KEY, transactionId], response.data)
  return response.data
}

/**
 * `PUT /api/bookings/{id}/menus` (contract §15.2).
 *
 * The write is a **full replace**, so the request carries the complete
 * selection — including each dish's current prep instruction, which an
 * omission would clear (DEC-155). The response is the **saved** selection.
 */
export function useReplaceBookingMenu(transactionId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (
      request: BookingMenuSelectionRequest,
    ): Promise<BookingMenuSelectionDto> => {
      const response = await apiClient.put<BookingMenuSelectionDto>(
        API_ENDPOINTS.bookings.menus(transactionId),
        request,
      )
      return response.data
    },
    onSuccess: (saved) => {
      queryClient.setQueryData([BOOKING_MENU_QUERY_KEY, transactionId], saved)
      invalidateBookingReads(queryClient)
    },
  })
}

/**
 * `PUT /api/bookings/{id}/addons` (contract §16.4).
 *
 * The write **re-freezes the money** (DEC-152): the server re-caps the frozen
 * discount, leaves the deposit and refuses a write that would overpay. The
 * client never reprices anything — it renders the saved set.
 */
export function useReplaceBookingAddons(transactionId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: BookingAddonSetRequest): Promise<BookingAddonSetDto> => {
      const response = await apiClient.put<BookingAddonSetDto>(
        API_ENDPOINTS.bookings.addons(transactionId),
        request,
      )
      return response.data
    },
    onSuccess: (saved) => {
      queryClient.setQueryData([BOOKING_ADDONS_QUERY_KEY, transactionId], saved)
      invalidateBookingReads(queryClient)
    },
  })
}

/** Saves a binary payload to the browser's download tray. */
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
 * `GET /api/bookings/{id}/beo` (contract §15.5).
 *
 * The Kitchen Banquet Event Order. The document is rendered by the server from
 * the booking's **selection**, so the browser only downloads it.
 */
export function useKitchenBeoDownload() {
  return useMutation({
    mutationFn: async (transactionId: number): Promise<void> => {
      const response = await apiClient.get<Blob>(API_ENDPOINTS.bookings.beo(transactionId), {
        responseType: 'blob',
      })
      saveDownload(
        response.data,
        fileNameFrom(
          response.headers?.['content-disposition'] as string | undefined,
          'kitchen-beo.pdf',
        ),
      )
    },
  })
}
