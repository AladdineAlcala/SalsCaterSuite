import { useMutation } from '@tanstack/react-query'
import { apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { MenuStatusFilter, MenuSortOption } from '@/types/menu.types'

/** The filters the catalogue export honours (spec §6.3). */
export interface MenuExportFilters {
  search: string
  courseId: number | null
  status: MenuStatusFilter
  sort: MenuSortOption
}

/**
 * Saves a binary payload to the browser's download tray.
 *
 * The file name is taken from the server's `Content-Disposition` header so the
 * client never invents one.
 */
function saveDownload(blob: Blob, fileName: string): void {
  const objectUrl = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName
  anchor.rel = 'noopener'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(objectUrl)
}

function fileNameFrom(disposition: string | undefined, fallback: string): string {
  if (!disposition) return fallback
  const match = /filename="?([^";]+)"?/i.exec(disposition)
  return match?.[1] ?? fallback
}

/**
 * `GET /api/menus/master/export` (spec §6.3).
 *
 * The XLSX holds the **current filtered result set** — the active search,
 * course, status and sort travel with the request. The export is not paged.
 */
export function useMenuExport() {
  return useMutation({
    mutationFn: async (filters: MenuExportFilters): Promise<void> => {
      const response = await apiClient.get<Blob>(API_ENDPOINTS.menus.masterExport, {
        params: {
          search: filters.search.trim() || undefined,
          courseId: filters.courseId ?? undefined,
          status: filters.status || undefined,
          sort: filters.sort,
        },
        responseType: 'blob',
      })
      saveDownload(
        response.data,
        fileNameFrom(
          response.headers?.['content-disposition'] as string | undefined,
          'menu-food-costing.xlsx',
        ),
      )
    },
  })
}
