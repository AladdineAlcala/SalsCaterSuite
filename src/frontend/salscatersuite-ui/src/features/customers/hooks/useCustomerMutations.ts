import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { CustomerCreatedResponse, CustomerUpsertRequest } from '@/types/customer.types'
import { CUSTOMER_DIRECTORY_QUERY_KEY } from './useCustomerDirectory'
import { CUSTOMER_DETAIL_QUERY_KEY } from './useCustomerDetail'

export interface UpdateCustomerVariables {
  customerId: number
  request: CustomerUpsertRequest
}

/**
 * `POST /api/customers` (spec §5.6).
 *
 * On success the directory is invalidated so the list **and** the KPI summary
 * are recomputed by the server (AC-C13, F7). The caller decides when the
 * drawer closes.
 */
export function useCreateCustomer() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: CustomerUpsertRequest): Promise<number> => {
      const response = await apiClient.post<CustomerCreatedResponse>(
        API_ENDPOINTS.customers.root,
        request,
      )
      return response.data.customerId
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [CUSTOMER_DIRECTORY_QUERY_KEY] })
    },
  })
}

/**
 * `PUT /api/customers/{id}` (spec §5.7).
 *
 * A rejected save rejects the mutation; the caller renders the server's
 * `errors[]` verbatim and keeps the operator's typed input (AC-C12).
 */
export function useUpdateCustomer() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ customerId, request }: UpdateCustomerVariables): Promise<void> => {
      await apiClient.put(API_ENDPOINTS.customers.detail(customerId), request)
    },
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: [CUSTOMER_DIRECTORY_QUERY_KEY] })
      void queryClient.invalidateQueries({
        queryKey: [CUSTOMER_DETAIL_QUERY_KEY, variables.customerId],
      })
    },
  })
}
