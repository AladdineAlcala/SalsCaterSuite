import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { AreaCreatedResponse, AreaDto, AreaRequest } from '@/types/package.types'

export const AREA_CATALOGUE_QUERY_KEY = 'area-catalogue' as const

/** A 4xx is a business answer and will not improve on retry. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 1
}

/**
 * `GET /api/areas` (contract §13.9).
 *
 * The municipality catalogue. It is the population every coverage set is
 * chosen from — a coverage row whose municipality was soft-deleted is
 * excluded from the matrix and from every count.
 */
export function useAreaCatalogue() {
  return useQuery({
    queryKey: [AREA_CATALOGUE_QUERY_KEY],
    queryFn: async (): Promise<AreaDto[]> => {
      const response = await apiClient.get<AreaDto[]>(API_ENDPOINTS.areas.root)
      return response.data
    },
    staleTime: 300_000,
    retry: shouldRetry,
  })
}

/**
 * `POST /api/areas` (contract §13.10).
 *
 * Adds a municipality to the catalogue. A rejected payload persists nothing
 * and the server's `errors[]` are rendered verbatim (BR-11).
 */
export function useCreateArea() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: AreaRequest): Promise<number> => {
      const response = await apiClient.post<AreaCreatedResponse>(API_ENDPOINTS.areas.root, request)
      return response.data.areaId
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [AREA_CATALOGUE_QUERY_KEY] })
    },
  })
}

/**
 * `PUT /api/areas/{id}` (contract §13.11).
 *
 * Updates one municipality. The rate is the **municipality's** rate, shared by
 * every tier that covers it (DEC-078) — a coverage row cannot override it.
 */
export function useUpdateArea() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      areaId,
      request,
    }: {
      areaId: number
      request: AreaRequest
    }): Promise<void> => {
      await apiClient.put(API_ENDPOINTS.areas.detail(areaId), request)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [AREA_CATALOGUE_QUERY_KEY] })
    },
  })
}
