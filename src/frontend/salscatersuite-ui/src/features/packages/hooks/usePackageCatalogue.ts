import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type {
  PackageCatalogueSummary,
  PackageDto,
} from '@/types/package.types'

export const PACKAGE_LIST_QUERY_KEY = 'packages-list' as const
export const PACKAGE_SUMMARY_QUERY_KEY = 'packages-summary' as const
export const PACKAGE_DETAIL_QUERY_KEY = 'package-detail' as const

/**
 * `GET /api/packages` (spec §4).
 *
 * The endpoint returns the **whole, unpaged** catalogue, so the client owns
 * the filtering and the ordering. That ordering is a presentation concern
 * only — it never derives a monetary figure, and no monetary aggregate is
 * ever computed from this list (DEC-002, DEC-068).
 */
export function usePackageList() {
  return useQuery({
    queryKey: [PACKAGE_LIST_QUERY_KEY],
    queryFn: async (): Promise<PackageDto[]> => {
      const response = await apiClient.get<PackageDto[]>(API_ENDPOINTS.packages.root)
      return response.data
    },
    staleTime: 30_000,
    refetchOnWindowFocus: true,
    // The previous catalogue stays on screen while the next one loads, so the
    // operator does not lose their place.
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

/**
 * `GET /api/packages/summary` (spec §3.2, DEC-068).
 *
 * The catalogue aggregate is **server-computed**. The browser never sums the
 * tier list to produce an average, a range or a headcount floor; a `null`
 * figure means *unknown* and renders as an em dash.
 */
export function usePackageSummary() {
  return useQuery({
    queryKey: [PACKAGE_SUMMARY_QUERY_KEY],
    queryFn: async (): Promise<PackageCatalogueSummary> => {
      const response = await apiClient.get<PackageCatalogueSummary>(
        API_ENDPOINTS.packages.summary,
      )
      return response.data
    },
    staleTime: 30_000,
    refetchOnWindowFocus: true,
    placeholderData: keepPreviousData,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}

/**
 * `GET /api/packages/{id}` (spec §5.3).
 *
 * The edit form loads the tier the API published. The query is disabled while
 * no tier is requested.
 */
export function usePackageDetail(packageId: number | null) {
  return useQuery({
    queryKey: [PACKAGE_DETAIL_QUERY_KEY, packageId],
    queryFn: async (): Promise<PackageDto> => {
      if (packageId === null) {
        throw new ApiError(0, ['No package tier is selected.'])
      }
      const response = await apiClient.get<PackageDto>(
        API_ENDPOINTS.packages.detail(packageId),
      )
      return response.data
    },
    enabled: packageId !== null,
    staleTime: 30_000,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
