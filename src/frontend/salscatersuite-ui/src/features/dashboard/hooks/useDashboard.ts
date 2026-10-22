import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { DashboardDto } from '@/types/dashboard.types'

export const DASHBOARD_QUERY_KEY = 'dashboard' as const

/**
 * `GET /api/dashboard?period=YYYY-MM`.
 *
 * The period is the only client-side input; the server resolves the period
 * boundaries and computes every figure it returns. The client never derives a
 * financial value from this payload (DEC-002).
 */
export function useDashboard(period: string) {
  return useQuery({
    queryKey: [DASHBOARD_QUERY_KEY, period],
    queryFn: async (): Promise<DashboardDto> => {
      const response = await apiClient.get<DashboardDto>(API_ENDPOINTS.dashboard.root, {
        params: { period },
      })
      return response.data
    },
    staleTime: 60_000,
    refetchOnWindowFocus: true,
    // The previous period stays on screen while the new one loads; the panel
    // is marked "Refreshing…" (spec §9.3 "Stale").
    placeholderData: keepPreviousData,
    // A 4xx is a business/validation answer and will not improve on retry.
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
