import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type {
  AddonAdminItem,
  AddonCategoryLookup,
  AddonUpsertRequest,
  DepartmentLookup,
} from '@/types/addon.types'

export const ADDON_MANAGE_QUERY_KEY = 'addon-manage' as const
export const ADDON_CATEGORY_LOOKUP_QUERY_KEY = 'addon-category-lookup' as const
export const DEPARTMENT_LOOKUP_QUERY_KEY = 'department-lookup' as const

function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 1
}

/** `GET /api/addons/manage` — active and inactive, excluding soft-deleted. */
export function useAddonManagementList() {
  return useQuery({
    queryKey: [ADDON_MANAGE_QUERY_KEY],
    queryFn: async (): Promise<AddonAdminItem[]> => {
      const response = await apiClient.get<AddonAdminItem[]>(API_ENDPOINTS.addons.manage)
      return response.data
    },
    staleTime: 15_000,
    retry: shouldRetry,
  })
}

/** `GET /api/addons/categories` — the category lookup the form offers. */
export function useAddonCategoryLookup() {
  return useQuery({
    queryKey: [ADDON_CATEGORY_LOOKUP_QUERY_KEY],
    queryFn: async (): Promise<AddonCategoryLookup[]> => {
      const response = await apiClient.get<AddonCategoryLookup[]>(
        API_ENDPOINTS.addons.categories,
      )
      return response.data
    },
    staleTime: 60_000,
    retry: shouldRetry,
  })
}

/** `GET /api/departments` — the read-only department lookup. */
export function useDepartmentLookup() {
  return useQuery({
    queryKey: [DEPARTMENT_LOOKUP_QUERY_KEY],
    queryFn: async (): Promise<DepartmentLookup[]> => {
      const response = await apiClient.get<DepartmentLookup[]>(
        API_ENDPOINTS.departments.root,
      )
      return response.data
    },
    staleTime: 60_000,
    retry: shouldRetry,
  })
}

/**
 * Invalidates the administration list, the category lookup and the booking picker's master, so
 * a change made in Settings is visible the next time a booking is edited.
 */
function invalidateAddonReads(queryClient: ReturnType<typeof useQueryClient>): void {
  void queryClient.invalidateQueries({ queryKey: [ADDON_MANAGE_QUERY_KEY] })
  void queryClient.invalidateQueries({ queryKey: [ADDON_CATEGORY_LOOKUP_QUERY_KEY] })
  void queryClient.invalidateQueries({ queryKey: ['addon-master'] })
  void queryClient.invalidateQueries({ queryKey: ['addon-category'] })
}

/** `POST /api/addons` — `201 { addonId }`. */
export function useCreateAddon() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: AddonUpsertRequest): Promise<number> => {
      const response = await apiClient.post<{ addonId: number }>(
        API_ENDPOINTS.addons.create,
        request,
      )
      return response.data.addonId
    },
    onSuccess: () => {
      invalidateAddonReads(queryClient)
    },
  })
}

export interface UpdateAddonVariables {
  addonId: number
  request: AddonUpsertRequest
}

/** `PUT /api/addons/{id}` — `204`. */
export function useUpdateAddon() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ addonId, request }: UpdateAddonVariables): Promise<void> => {
      await apiClient.put(API_ENDPOINTS.addons.update(addonId), request)
    },
    onSuccess: () => {
      invalidateAddonReads(queryClient)
    },
  })
}

/** `DELETE /api/addons/{id}` — soft delete, `204`. */
export function useDeleteAddon() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (addonId: number): Promise<void> => {
      await apiClient.delete(API_ENDPOINTS.addons.remove(addonId))
    },
    onSuccess: () => {
      invalidateAddonReads(queryClient)
    },
  })
}
