import { useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { MenuCourse } from '@/types/menu.types'

export const MENU_COURSES_QUERY_KEY = 'menu-courses' as const

/**
 * `GET /api/menus/courses` (spec §5.2).
 *
 * The seeded course positions. The recipe form offers exactly these rows as
 * its Course Category options — the client never invents a course.
 */
export function useMenuCourses() {
  return useQuery({
    queryKey: [MENU_COURSES_QUERY_KEY],
    queryFn: async (): Promise<MenuCourse[]> => {
      const response = await apiClient.get<MenuCourse[]>(API_ENDPOINTS.menus.courses)
      return response.data
    },
    staleTime: 300_000,
    // A 4xx is a business answer and will not improve on retry.
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
