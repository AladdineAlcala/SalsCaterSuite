import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type {
  CustomerArStateFilter,
  CustomerDirectoryResponse,
  CustomerTypeFilter,
} from '@/types/customer.types'

export const CUSTOMER_DIRECTORY_QUERY_KEY = 'customers-directory' as const

/** The filters the client owns; every figure in the response stays server-side. */
export interface CustomerDirectoryFilters {
  /** Free-text search over name, company and contact number. */
  search: string
  customerType: CustomerTypeFilter
  arState: CustomerArStateFilter
  /** 1-based page number. */
  page: number
  pageSize: number
}

/**
 * `GET /api/customers/directory` (spec §5.1).
 *
 * The client sends the filters and the paging window; the server resolves
 * every figure it returns — the KPI summary, the aging state and its label
 * (DEC-002). The client never sums, slices or derives anything from the
 * payload.
 */
export function useCustomerDirectory(filters: CustomerDirectoryFilters) {
  return useQuery({
    queryKey: [CUSTOMER_DIRECTORY_QUERY_KEY, filters],
    queryFn: async (): Promise<CustomerDirectoryResponse> => {
      const response = await apiClient.get<CustomerDirectoryResponse>(
        API_ENDPOINTS.customers.directory,
        {
          params: {
            search: filters.search.trim() || undefined,
            customerType: filters.customerType || undefined,
            arState: filters.arState || undefined,
            page: filters.page,
            pageSize: filters.pageSize,
          },
        },
      )
      return response.data
    },
    staleTime: 30_000,
    refetchOnWindowFocus: true,
    // The previous page stays on screen while the next one loads, so the
    // operator does not lose their place in the directory.
    placeholderData: keepPreviousData,
    // A 4xx is a business answer and will not improve on retry.
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
