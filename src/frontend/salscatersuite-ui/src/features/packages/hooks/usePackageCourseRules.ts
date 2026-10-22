import { useEffect, useMemo, useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError, apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import type { MenuCourse } from '@/types/menu.types'
import type {
  PackageCourseRulePreview,
  PackageCourseRulePreviewRequest,
  PackageCourseRuleset,
  PackageCourseRulesetRequest,
  PackageCourseSelection,
} from '@/types/package.types'

export const PACKAGE_COURSE_RULESET_QUERY_KEY = 'package-course-ruleset' as const
export const PACKAGE_COURSE_PREVIEW_QUERY_KEY = 'package-course-rule-preview' as const
export const PACKAGE_COURSE_TAXONOMY_QUERY_KEY = 'package-course-taxonomy' as const

/** How long the preview inputs rest before the server is asked for a preview. */
const PREVIEW_DEBOUNCE_MS = 300

/**
 * The stable identity of a selection set.
 *
 * The preview is keyed by this string rather than by the array reference, so
 * re-selecting the same dishes never re-asks the server and a new selection
 * always does.
 */
function selectionKey(selections: PackageCourseSelection[]): string {
  return selections.map((selection) => `${selection.courseId}:${selection.menuId}`).join('|')
}

/** A 4xx is a business answer and will not improve on retry. */
function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false
  }
  return failureCount < 1
}

/**
 * `GET /api/packages/{id}/course-rules` (contract §13.1).
 *
 * The ruleset carries the tier's enforced course rules, each rule's
 * **qualified pool** (the non-deleted dishes of the rule's course) and the
 * tier's planning figures. Every money and every percentage it publishes is
 * server-computed (DEC-002); the client only formats and displays them.
 */
export function usePackageCourseRuleset(packageId: number | null) {
  return useQuery({
    queryKey: [PACKAGE_COURSE_RULESET_QUERY_KEY, packageId],
    queryFn: async (): Promise<PackageCourseRuleset> => {
      if (packageId === null) {
        throw new ApiError(0, ['No package tier is selected.'])
      }
      const response = await apiClient.get<PackageCourseRuleset>(
        API_ENDPOINTS.packages.courseRules(packageId),
      )
      return response.data
    },
    enabled: packageId !== null,
    staleTime: 30_000,
    placeholderData: keepPreviousData,
    retry: shouldRetry,
  })
}

/**
 * The published course taxonomy.
 *
 * The workbench needs the course a rule governs, and the only server-published
 * population of course identifiers and names is the seeded course positions the
 * menu master already reads. The client never invents a course and never
 * guesses an identifier (DEC-013 / BR-13).
 */
export function usePackageCourseTaxonomy() {
  return useQuery({
    queryKey: [PACKAGE_COURSE_TAXONOMY_QUERY_KEY],
    queryFn: async (): Promise<MenuCourse[]> => {
      const response = await apiClient.get<MenuCourse[]>(API_ENDPOINTS.menus.courses)
      return response.data
    },
    staleTime: 300_000,
    retry: shouldRetry,
  })
}

/**
 * `PUT /api/packages/{id}/course-rules` (contract §13.2).
 *
 * The submitted set **replaces** the tier's ruleset; an entry that is not
 * submitted is soft-deleted by the server, never destroyed (DEC-085). A
 * rejected save rejects the mutation; the caller renders the server's
 * `errors[]` verbatim and keeps the operator's typed input (BR-11).
 */
export function useSavePackageCourseRules() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      packageId,
      request,
    }: {
      packageId: number
      request: PackageCourseRulesetRequest
    }): Promise<PackageCourseRuleset> => {
      const response = await apiClient.put<PackageCourseRuleset>(
        API_ENDPOINTS.packages.courseRules(packageId),
        request,
      )
      return response.data
    },
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({
        queryKey: [PACKAGE_COURSE_RULESET_QUERY_KEY, variables.packageId],
      })
    },
  })
}

/**
 * `POST /api/packages/{id}/course-rules/preview` (contract §13.3).
 *
 * The simulator and the headcount forecast are **server-computed**: the browser
 * sends the operator's dish selection, the stress percentage and the headcount
 * and renders the figures that come back. It never derives a food-cost
 * percentage, a ceiling breach, a gross profit or a contract total of its own
 * (DEC-002).
 *
 * The inputs rest briefly before the server is asked, so a burst of keystrokes
 * costs one request instead of one request per digit.
 *
 * The query carries **no** placeholder data: the previous selection's figures
 * must never reach the screen while the new ones are in flight. The
 * {@link isRecalculating} flag below is the explicit "recalculating" state the
 * screen shows instead — the same shape the location preview exposes.
 */
export function usePackageCourseRulePreview(
  packageId: number | null,
  selections: PackageCourseSelection[],
  stressPercentage: number | null,
  pax: number | null,
) {
  const key = useMemo(() => selectionKey(selections), [selections])

  const [debounced, setDebounced] = useState({
    selections,
    key,
    stressPercentage,
    pax,
  })

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebounced({ selections, key, stressPercentage, pax })
    }, PREVIEW_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [key, selections, stressPercentage, pax])

  const query = useQuery({
    queryKey: [
      PACKAGE_COURSE_PREVIEW_QUERY_KEY,
      packageId,
      debounced.key,
      debounced.stressPercentage,
      debounced.pax,
    ],
    queryFn: async (): Promise<PackageCourseRulePreview> => {
      if (packageId === null) {
        throw new ApiError(0, ['No package tier is selected.'])
      }
      const body: PackageCourseRulePreviewRequest = {
        selections: debounced.selections,
        stressPercentage: debounced.stressPercentage,
        pax: debounced.pax,
      }
      const response = await apiClient.post<PackageCourseRulePreview>(
        API_ENDPOINTS.packages.courseRulePreview(packageId),
        body,
      )
      return response.data
    },
    enabled: packageId !== null,
    staleTime: 0,
    retry: shouldRetry,
  })

  /**
   * `true` while the typed selection has not reached the server yet, or the
   * preview is in flight.
   *
   * The screen shows an explicit **recalculating** state while it holds, so a
   * verdict or a peso figure priced for the **previous** selection is never
   * presented as the current one.
   */
  const isDebouncing =
    debounced.key !== key ||
    debounced.stressPercentage !== stressPercentage ||
    debounced.pax !== pax

  const isRecalculating = isDebouncing || query.isFetching

  return { ...query, isRecalculating }
}
