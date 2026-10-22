import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { PackageCreatedResponse, PackageUpsertRequest } from '@/types/package.types'
import {
  PACKAGE_DETAIL_QUERY_KEY,
  PACKAGE_LIST_QUERY_KEY,
  PACKAGE_SUMMARY_QUERY_KEY,
} from './usePackageCatalogue'

export interface UpdatePackageVariables {
  packageId: number
  request: PackageUpsertRequest
}

/** Invalidates the catalogue list and the server-computed aggregate. */
function invalidateCatalogue(queryClient: ReturnType<typeof useQueryClient>): void {
  void queryClient.invalidateQueries({ queryKey: [PACKAGE_LIST_QUERY_KEY] })
  void queryClient.invalidateQueries({ queryKey: [PACKAGE_SUMMARY_QUERY_KEY] })
}

/**
 * `POST /api/packages` (spec §5.3).
 *
 * On success the catalogue list and the aggregate are invalidated so the
 * server recomputes both (AC-P12).
 */
export function useCreatePackage() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (request: PackageUpsertRequest): Promise<number> => {
      const response = await apiClient.post<PackageCreatedResponse>(
        API_ENDPOINTS.packages.create,
        request,
      )
      return response.data.packageId
    },
    onSuccess: () => {
      invalidateCatalogue(queryClient)
    },
  })
}

/**
 * `PUT /api/packages/{id}` (spec §5.3).
 *
 * A rejected save rejects the mutation; the caller renders the server's
 * `errors[]` verbatim and keeps the operator's typed input (AC-P11).
 */
export function useUpdatePackage() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ packageId, request }: UpdatePackageVariables): Promise<void> => {
      await apiClient.put(API_ENDPOINTS.packages.update(packageId), request)
    },
    onSuccess: (_data, variables) => {
      invalidateCatalogue(queryClient)
      void queryClient.invalidateQueries({
        queryKey: [PACKAGE_DETAIL_QUERY_KEY, variables.packageId],
      })
    },
  })
}

/**
 * `PATCH /api/packages/{id}/toggle-active` (spec §4, DEC-071).
 *
 * The endpoint activates an inactive tier and deactivates an active one. It is
 * presented as an honest activate / deactivate toggle — never as an archive,
 * because an archive cannot be undone by this endpoint.
 */
export function useTogglePackageActive() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (packageId: number): Promise<boolean> => {
      const response = await apiClient.patch<{ isActive: boolean }>(
        API_ENDPOINTS.packages.toggleActive(packageId),
      )
      return response.data.isActive
    },
    onSuccess: () => {
      invalidateCatalogue(queryClient)
    },
  })
}
