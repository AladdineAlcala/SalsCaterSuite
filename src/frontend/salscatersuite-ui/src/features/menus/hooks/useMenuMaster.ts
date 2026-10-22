import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type {
  MenuCostingItem,
  MenuMasterResponse,
  MenuStatusFilter,
  MenuSortOption,
} from '@/types/menu.types'

export const MENU_MASTER_QUERY_KEY = 'menus-master' as const
export const MENU_DETAIL_QUERY_KEY = 'menu-detail' as const

/** The filters the client owns; every figure in the response stays server-side. */
export interface MenuMasterFilters {
  /** Free-text search over the dish code, the dish name and the note. */
  search: string
  /** One course position, or `null` for the whole catalogue. */
  courseId: number | null
  /** The costing status, or an empty string for no status filter. */
  status: MenuStatusFilter
  /** The sort key. */
  sort: MenuSortOption
  /** 1-based page number. */
  page: number
  pageSize: number
}

/**
 * `GET /api/menus/master` (spec §6.1).
 *
 * The client sends the filters and the paging window; the server resolves
 * every figure it returns — the KPI summary, the course counts, the food-cost
 * percentage, the margin and the variance. The client never sums, slices or
 * derives anything from the payload (DEC-002).
 */
export function useMenuMaster(filters: MenuMasterFilters) {
  return useQuery({
    queryKey: [MENU_MASTER_QUERY_KEY, filters],
    queryFn: async (): Promise<MenuMasterResponse> => {
      const response = await apiClient.get<MenuMasterResponse>(
        API_ENDPOINTS.menus.master,
        {
          params: {
            search: filters.search.trim() || undefined,
            courseId: filters.courseId ?? undefined,
            status: filters.status || undefined,
            sort: filters.sort,
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
    // operator does not lose their place in the catalogue.
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
 * `GET /api/menus/{id}` (spec §6.4).
 *
 * The edit screen loads the dish the API published; its food cost, gross
 * profit and margin are the server's. The query is disabled while no dish is
 * requested.
 */
export function useMenuDetail(menuId: string | null) {
  return useQuery({
    queryKey: [MENU_DETAIL_QUERY_KEY, menuId],
    queryFn: async (): Promise<MenuCostingItem> => {
      if (menuId === null) {
        throw new ApiError(0, ['No dish is selected.'])
      }
      const response = await apiClient.get<MenuCostingItem>(
        API_ENDPOINTS.menus.detail(menuId),
      )
      return response.data
    },
    enabled: menuId !== null,
    staleTime: 30_000,
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
