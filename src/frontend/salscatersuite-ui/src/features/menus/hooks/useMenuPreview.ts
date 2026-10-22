import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { MenuCostingPreview } from '@/types/menu.types'

export const MENU_PREVIEW_QUERY_KEY = 'menu-costing-preview' as const

/** The two amounts the preview is computed from. */
export interface MenuPreviewInput {
  /** The typed selling price, or `null` while it cannot be read. */
  sellingPrice: number | null
  /** The typed food cost, or `null` while it cannot be read. */
  foodCost: number | null
}

/** How long the typed amounts rest before the server is asked for a preview. */
const PREVIEW_DEBOUNCE_MS = 300

/**
 * `POST /api/menus/costing/preview` (spec §6.2, DEC-002).
 *
 * The margin preview is **server-computed**: the browser sends the two
 * amounts and renders the figures that come back. It never derives a food
 * cost percentage, a gross profit or a variance of its own, so the preview and
 * the saved record can never disagree.
 */
export function useMenuPreview(input: MenuPreviewInput) {
  const [debounced, setDebounced] = useState<MenuPreviewInput>(input)

  // The amounts rest briefly before the server is asked, so a burst of
  // keystrokes costs one request instead of one request per digit.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebounced({ sellingPrice: input.sellingPrice, foodCost: input.foodCost })
    }, PREVIEW_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [input.sellingPrice, input.foodCost])

  const isComplete =
    debounced.sellingPrice !== null && debounced.foodCost !== null

  return useQuery({
    queryKey: [
      MENU_PREVIEW_QUERY_KEY,
      debounced.sellingPrice,
      debounced.foodCost,
    ],
    queryFn: async (): Promise<MenuCostingPreview> => {
      const sellingPrice = debounced.sellingPrice
      const foodCost = debounced.foodCost
      if (sellingPrice === null || foodCost === null) {
        throw new ApiError(0, [
          'The margin preview needs both a selling price and a food cost.',
        ])
      }
      const response = await apiClient.post<MenuCostingPreview>(
        API_ENDPOINTS.menus.costingPreview,
        { sellingPrice, foodCost },
      )
      return response.data
    },
    enabled: isComplete,
    staleTime: 0,
    // A 4xx is a business answer and will not improve on retry.
    retry: (failureCount, error) => {
      if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
        return false
      }
      return failureCount < 1
    },
  })
}
