import { useMutation } from '@tanstack/react-query'
import { apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { CustomerArStateFilter, CustomerTypeFilter } from '@/types/customer.types'

/** The filters the directory export honours (spec §5.5). */
export interface CustomerExportFilters {
  search: string
  customerType: CustomerTypeFilter
  arState: CustomerArStateFilter
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

function fileNameFrom(
  disposition: string | undefined,
  fallback: string,
): string {
  if (!disposition) return fallback
  const match = /filename="?([^";]+)"?/i.exec(disposition)
  return match?.[1] ?? fallback
}

/**
 * `GET /api/customers/directory/export` (spec §5.5).
 *
 * The XLSX holds the **current filtered result set** — the active search,
 * type and AR-state filters travel with the request.
 */
export function useCustomerExport() {
  return useMutation({
    mutationFn: async (filters: CustomerExportFilters): Promise<void> => {
      const response = await apiClient.get<Blob>(API_ENDPOINTS.customers.directoryExport, {
        params: {
          search: filters.search.trim() || undefined,
          customerType: filters.customerType || undefined,
          arState: filters.arState || undefined,
        },
        responseType: 'blob',
      })
      saveDownload(
        response.data,
        fileNameFrom(
          response.headers?.['content-disposition'] as string | undefined,
          'customer-directory.xlsx',
        ),
      )
    },
  })
}

/**
 * `GET /api/customers/{id}/soa` (spec §5.4).
 *
 * Streams the QuestPDF statement of account of one customer.
 */
export function useCustomerSoaDownload() {
  return useMutation({
    mutationFn: async (customerId: number): Promise<void> => {
      const response = await apiClient.get<Blob>(API_ENDPOINTS.customers.soa(customerId), {
        responseType: 'blob',
      })
      saveDownload(
        response.data,
        fileNameFrom(
          response.headers?.['content-disposition'] as string | undefined,
          'statement-of-account.pdf',
        ),
      )
    },
  })
}
