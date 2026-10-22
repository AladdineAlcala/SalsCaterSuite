import { useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { CustomerDetail } from '@/types/customer.types'

export const CUSTOMER_DETAIL_QUERY_KEY = 'customer-detail' as const

/**
 * `GET /api/customers/{id}` (spec §5.2).
 *
 * The profile, the booking counts, the ledger figures and the upcoming
 * booking all come from the server. The query is disabled while no customer
 * is selected.
 */
export function useCustomerDetail(customerId: number | null) {
  return useQuery({
    queryKey: [CUSTOMER_DETAIL_QUERY_KEY, customerId],
    queryFn: async (): Promise<CustomerDetail> => {
      if (customerId === null) {
        throw new ApiError(0, ['No customer is selected.'])
      }
      const response = await apiClient.get<CustomerDetail>(
        API_ENDPOINTS.customers.detail(customerId),
      )
      return response.data
    },
    enabled: customerId !== null,
    staleTime: 30_000,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
