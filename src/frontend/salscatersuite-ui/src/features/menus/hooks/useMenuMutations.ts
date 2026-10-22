import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { MenuCreatedResponse, MenuUpsertRequest } from '@/types/menu.types'
import { MENU_DETAIL_QUERY_KEY, MENU_MASTER_QUERY_KEY } from './useMenuMaster'

export interface UpdateMenuVariables {
  menuId: string
  request: MenuUpsertRequest
}

/**
 * `POST /api/menus` (spec §5.7).
 *
 * On success the master query is invalidated so the catalogue, the KPI
 * summary and the course counts are recomputed by the server (AC-M14).
 */
export function useCreateMenu() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: MenuUpsertRequest): Promise<string> => {
      const response = await apiClient.post<MenuCreatedResponse>(
        API_ENDPOINTS.menus.create,
        request,
      )
      return response.data.menuId
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [MENU_MASTER_QUERY_KEY] })
    },
  })
}

/**
 * `PUT /api/menus/{id}` (spec §5.7).
 *
 * A rejected save rejects the mutation; the caller renders the server's
 * `errors[]` verbatim and keeps the operator's typed input (AC-M16).
 */
export function useUpdateMenu() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ menuId, request }: UpdateMenuVariables): Promise<void> => {
      await apiClient.put(API_ENDPOINTS.menus.update(menuId), request)
    },
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: [MENU_MASTER_QUERY_KEY] })
      void queryClient.invalidateQueries({
        queryKey: [MENU_DETAIL_QUERY_KEY, variables.menuId],
      })
    },
  })
}
