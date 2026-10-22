import { useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { CustomerStatement } from '@/types/customer.types'

export const CUSTOMER_STATEMENT_QUERY_KEY = 'customer-statement' as const

/**
 * `GET /api/customers/{id}/statement` (spec §5.3).
 *
 * The statement is only fetched once the operator opens the Ledger History
 * tab, so the directory does not pay for a statement it may never read.
 */
export function useCustomerStatement(customerId: number | null, enabled: boolean) {
  return useQuery({
    queryKey: [CUSTOMER_STATEMENT_QUERY_KEY, customerId],
    queryFn: async (): Promise<CustomerStatement> => {
      if (customerId === null) {
        throw new ApiError(0, ['No customer is selected.'])
      }
      const response = await apiClient.get<CustomerStatement>(
        API_ENDPOINTS.customers.statement(customerId),
      )
      return response.data
    },
    enabled: customerId !== null && enabled,
    staleTime: 30_000,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
