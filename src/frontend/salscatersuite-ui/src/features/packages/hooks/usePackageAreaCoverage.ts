import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type {
  PackageAreaCoverageAttachRequest,
  PackageAreaCoverageCreatedResponse,
  PackageAreaCoverageMatrix,
  PackageAreaCoverageRequest,
  PackageLocationPreview,
  PackageLocationPreviewRequest,
} from '@/types/package.types'

export const PACKAGE_AREA_MATRIX_QUERY_KEY = 'package-area-coverage-matrix' as const
export const PACKAGE_LOCATION_PREVIEW_QUERY_KEY = 'package-location-preview' as const

/**
 * How long the typed headcount rests before the server is asked for a quote.
 *
 * It is the **same** interval the course preview uses, so the two screens
 * behave identically: a burst of keystrokes costs one request instead of one
 * request per digit.
 */
const PREVIEW_DEBOUNCE_MS = 300

/** A 4xx is a business answer and will not improve on retry. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 1
}

/**
 * `GET /api/packages/{id}/areas` (contract §13.4).
 *
 * The coverage matrix carries the tier's covered municipalities, their
 * municipality-owned surcharge, and the tier's planning figures — including
 * the gross-profit range across the **active** coverage and the number of rows
 * that contributed to it.
 */
export function usePackageAreaCoverage(packageId: number | null) {
  return useQuery({
    queryKey: [PACKAGE_AREA_MATRIX_QUERY_KEY, packageId],
    queryFn: async (): Promise<PackageAreaCoverageMatrix> => {
      if (packageId === null) {
        throw new ApiError(0, ['No package tier is selected.'])
      }
      const response = await apiClient.get<PackageAreaCoverageMatrix>(
        API_ENDPOINTS.packages.areas(packageId),
      )
      return response.data
    },
    enabled: packageId !== null,
    staleTime: 30_000,
    retry: shouldRetry,
  })
}

/**
 * `PUT /api/packages/{id}/areas` (contract §13.5).
 *
 * The submitted set **replaces** the tier's coverage; a row that is not
 * submitted is soft-deleted by the server (DEC-085). An empty set is a
 * deliberate, documented meaning — it clears the tier's coverage — so the
 * client always sends an explicit array and never omits the member.
 */
export function useSavePackageAreaCoverage() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      packageId,
      request,
    }: {
      packageId: number
      request: PackageAreaCoverageRequest
    }): Promise<PackageAreaCoverageMatrix> => {
      const response = await apiClient.put<PackageAreaCoverageMatrix>(
        API_ENDPOINTS.packages.areas(packageId),
        request,
      )
      return response.data
    },
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({
        queryKey: [PACKAGE_AREA_MATRIX_QUERY_KEY, variables.packageId],
      })
    },
  })
}

/**
 * `POST /api/packages/{id}/areas` (contract §13.6).
 *
 * Attaches one municipality to the tier. The server refuses a municipality the
 * tier already covers and a municipality that does not exist; both messages
 * are rendered verbatim.
 */
export function useAttachPackageArea() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      packageId,
      request,
    }: {
      packageId: number
      request: PackageAreaCoverageAttachRequest
    }): Promise<number> => {
      const response = await apiClient.post<PackageAreaCoverageCreatedResponse>(
        API_ENDPOINTS.packages.attachArea(packageId),
        request,
      )
      return response.data.packageAreaCoverageId
    },
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({
        queryKey: [PACKAGE_AREA_MATRIX_QUERY_KEY, variables.packageId],
      })
    },
  })
}

/**
 * `POST /api/packages/{id}/areas/preview` (contract §13.7).
 *
 * The out-of-town pricing simulator is **server-computed**: the browser sends
 * the headcount and the municipality and renders the breakdown that comes back
 * — the base total, the extended total, the effective rate per pax, the
 * contract value, the food cost, the logistics cost, the gross profit and its
 * percentage. It never multiplies a rate by a headcount (DEC-002, AC-P2-28).
 *
 * Every covered municipality is priced at the current headcount so the
 * coverage matrix can publish the same server figure in each row. The
 * requests travel together inside one query, so the whole set is cached,
 * refetched and invalidated as a unit.
 *
 * The **typed** headcount is debounced before it reaches the query, exactly as
 * the course preview debounces its own inputs: the operator keeps seeing what
 * they typed, while a burst of keystrokes costs **one** batch of requests
 * instead of one batch per digit.
 */
export function usePackageLocationPreviews(
  packageId: number | null,
  pax: number | null,
  areaIds: number[],
) {
  const areaKey = areaIds.join(',')

  const [debouncedPax, setDebouncedPax] = useState(pax)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedPax(pax), PREVIEW_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [pax])

  const query = useQuery({
    queryKey: [PACKAGE_LOCATION_PREVIEW_QUERY_KEY, packageId, debouncedPax, areaKey],
    queryFn: async (): Promise<Record<string, PackageLocationPreview>> => {
      if (packageId === null) {
        throw new ApiError(0, ['No package tier is selected.'])
      }
      if (debouncedPax === null || debouncedPax < 1) {
        throw new ApiError(0, ['The headcount must be at least 1.'])
      }

      const requestedAreaIds = areaKey.length === 0 ? [] : areaKey.split(',').map(Number)

      const responses = await Promise.all(
        requestedAreaIds.map(async (areaId) => {
          const body: PackageLocationPreviewRequest = {
            pax: debouncedPax,
            areaId,
            // An empty array is a valid "no selection" request; a null member
            // is a rejected payload (contract §13.7).
            selections: [],
          }
          const response = await apiClient.post<PackageLocationPreview>(
            API_ENDPOINTS.packages.areaPreview(packageId),
            body,
          )
          return [String(areaId), response.data] as const
        }),
      )

      return Object.fromEntries(responses)
    },
    enabled:
      packageId !== null &&
      debouncedPax !== null &&
      debouncedPax >= 1 &&
      areaKey.length > 0,
    staleTime: 0,
    retry: shouldRetry,
  })

  /**
   * `true` while the typed headcount has not reached the server yet, or the
   * quote is in flight.
   *
   * The screen shows an explicit **recalculating** state while it holds, so a
   * figure priced for a different headcount is never presented as the current
   * one — and a zero is never shown in its place.
   */
  const isRecalculating = debouncedPax !== pax || query.isFetching

  return { ...query, isRecalculating }
}

/**
 * `GET /api/packages/{id}/spec-sheet` (contract §13.8).
 *
 * Streams the QuestPDF specification sheet of the tier: its identity and
 * commercial terms, its course rules with their qualified pools, its covered
 * municipalities and its planning figures.
 */
export function usePackageSpecSheetDownload() {
  return useMutation({
    mutationFn: async (packageId: number): Promise<void> => {
      const response = await apiClient.get<Blob>(API_ENDPOINTS.packages.specSheet(packageId), {
        responseType: 'blob',
      })
      saveDownload(response.data, fileNameFrom(response.headers?.['content-disposition'] as string | undefined))
    },
  })
}

/** Saves a binary payload to the browser's download tray. */
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

/** Reads the file name from the server's `Content-Disposition` header. */
function fileNameFrom(disposition: string | undefined): string {
  if (!disposition) return 'package-specification-sheet.pdf'
  const match = /filename="?([^";]+)"?/i.exec(disposition)
  return match?.[1] ?? 'package-specification-sheet.pdf'
}
