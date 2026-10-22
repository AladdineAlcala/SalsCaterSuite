import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { StatusFooter } from '@/features/dashboard/components/StatusFooter'
import type { ApiConnectionState } from '@/features/dashboard/components/StatusFooter'
import type {
  MenuSortOption,
  MenuStatusFilter,
} from '@/types/menu.types'
import { DishCardGrid } from './components/DishCardGrid'
import { MenuBanner } from './components/MenuBanner'
import { MenuCourseTabs } from './components/MenuCourseTabs'
import { MenuEmptyState } from './components/MenuEmptyState'
import { MenuFilterBar } from './components/MenuFilterBar'
import { MenuKpiGrid } from './components/MenuKpiGrid'
import { MenuMasterSkeleton } from './components/MenuMasterSkeleton'
import { MenuPagination } from './components/MenuPagination'
import { RecipeInspector } from './components/RecipeInspector'
import { useMenuExport } from './hooks/useMenuExport'
import { useMenuMaster } from './hooks/useMenuMaster'
import type { MenuMasterFilters } from './hooks/useMenuMaster'
import { menuStatusFilterLabel } from './menuStatus'

/** The page size the catalogue starts with (spec §4.7). */
const DEFAULT_PAGE_SIZE = 12

/** How long the search term rests before it is sent to the API. */
const SEARCH_DEBOUNCE_MS = 300

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/**
 * Names the active filter so the empty state says what is being excluded
 * (spec §4.8).
 */
function describeFilters(
  filters: MenuMasterFilters,
  courses: { courseId: number; courseName: string }[],
): string | undefined {
  const parts: string[] = []

  if (filters.search.trim().length > 0) {
    parts.push(`the search “${filters.search.trim()}”`)
  }
  if (filters.courseId !== null) {
    const course = courses.find((entry) => entry.courseId === filters.courseId)
    parts.push(`the ${course?.courseName ?? 'selected course'} course`)
  }
  if (filters.status) {
    parts.push(`the ${menuStatusFilterLabel(filters.status)} filter`)
  }

  return parts.length > 0 ? parts.join(' and ') : undefined
}

/**
 * The Menu & Food Costing Master screen (spec §4).
 *
 * The screen renders exclusively from `GET /api/menus/master` and the
 * catalogue export endpoint. Every figure — the KPI summary, the course
 * counts, the food-cost percentage, the gross profit, the margin and the
 * variance — is the server's; the browser formats and displays, it never
 * computes (DEC-002).
 */
export function MenuCostingMasterPage() {
  const navigate = useNavigate()
  const [filters, setFilters] = useState<MenuMasterFilters>({
    search: '',
    courseId: null,
    status: '',
    sort: 'FoodCostAsc',
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE,
  })
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [selectedMenuId, setSelectedMenuId] = useState<string | null>(null)
  const [actionErrors, setActionErrors] = useState<string[]>([])

  const queryFilters = useMemo<MenuMasterFilters>(
    () => ({ ...filters, search: debouncedSearch }),
    [filters, debouncedSearch],
  )

  const { data, isPending, isError, error, isFetching, refetch } =
    useMenuMaster(queryFilters)

  const exportCatalogue = useMenuExport()

  // The search term rests briefly before it is sent, so a burst of keystrokes
  // costs one request instead of one request per letter.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(filters.search)
      setFilters((current) => (current.page === 1 ? current : { ...current, page: 1 }))
    }, SEARCH_DEBOUNCE_MS)
    return () => window.clearTimeout(timer)
  }, [filters.search])

  const handleSearchChange = useCallback((value: string) => {
    setFilters((current) => ({ ...current, search: value }))
  }, [])

  const handleCourseChange = useCallback((courseId: number | null) => {
    setFilters((current) => ({ ...current, courseId, page: 1 }))
  }, [])

  const handleStatusChange = useCallback((value: MenuStatusFilter) => {
    setFilters((current) => ({ ...current, status: value, page: 1 }))
  }, [])

  const handleSortChange = useCallback((value: MenuSortOption) => {
    setFilters((current) => ({ ...current, sort: value, page: 1 }))
  }, [])

  const handlePageChange = useCallback((page: number) => {
    setFilters((current) => ({ ...current, page }))
  }, [])

  const handlePageSizeChange = useCallback((pageSize: number) => {
    setFilters((current) => ({ ...current, pageSize, page: 1 }))
  }, [])

  const handleSelectDish = useCallback((menuId: string) => {
    setSelectedMenuId(menuId)
  }, [])

  const handleCreate = useCallback(() => {
    setActionErrors([])
    navigate('/menus/new')
  }, [navigate])

  const handleEdit = useCallback(
    (menuId: string) => {
      setActionErrors([])
      navigate(`/menus/${encodeURIComponent(menuId)}/edit`)
    },
    [navigate],
  )

  const handleExport = useCallback(() => {
    setActionErrors([])
    exportCatalogue.mutate(
      {
        search: debouncedSearch,
        courseId: filters.courseId,
        status: filters.status,
        sort: filters.sort,
      },
      {
        onError: (mutationError) => setActionErrors(readErrorMessages(mutationError)),
      },
    )
  }, [debouncedSearch, filters.courseId, filters.status, filters.sort, exportCatalogue])

  // The selected dish falls back to the first card of the current page, so
  // the inspector is never empty while the catalogue holds dishes.
  const selectedItem = useMemo(() => {
    if (!data || data.items.length === 0) return null
    return data.items.find((item) => item.menuId === selectedMenuId) ?? data.items[0]
  }, [data, selectedMenuId])

  const connection: ApiConnectionState = isError
    ? 'offline'
    : isFetching
      ? 'refreshing'
      : 'live'

  /*
   * The two empty states are distinct (spec §4.8).
   *
   * `courses.total` is the count of the search- and status-filtered scope
   * with **no course filter**, so it stays positive while a course filter
   * excludes every dish. The catalogue itself is empty only when the scope
   * holds nothing **and** no filter is active — a filter that matches
   * nothing is a filtered result, not an empty catalogue.
   */
  const hasActiveFilter =
    filters.search.trim().length > 0 || filters.courseId !== null || filters.status !== ''
  const isCatalogueEmpty =
    Boolean(data) && data!.totalCount === 0 && data!.courses.total === 0 && !hasActiveFilter

  return (
    <div className="flex flex-col gap-space-xl">
      <MenuBanner
        targetFoodCostPercentage={data ? data.summary.targetFoodCostPercentage : null}
        isBenchmarkConfigured={data ? data.summary.isBenchmarkConfigured : false}
        onRecalculate={() => void refetch()}
        isRecalculating={isFetching}
        onExport={handleExport}
        isExporting={exportCatalogue.isPending}
        onCreate={handleCreate}
      />

      {actionErrors.length > 0 ? (
        <AlertBanner
          title="The catalogue export could not be produced"
          errors={actionErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setActionErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {isPending ? <MenuMasterSkeleton /> : null}

      {isError ? (
        <AlertBanner
          title="The menu catalogue could not be loaded"
          errors={readErrorMessages(error)}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={() => void refetch()}>
              Retry
            </Button>
          }
        />
      ) : null}

      {data ? <MenuKpiGrid summary={data.summary} /> : null}

      {data ? (
        <div className="grid grid-cols-1 xl:grid-cols-12 gap-gutter items-start">
          <div className="xl:col-span-7 flex flex-col gap-space-md min-w-0">
            <Card elevation={1} className="p-space-md flex flex-col gap-space-sm">
              <MenuCourseTabs
                courses={data.courses}
                activeCourseId={filters.courseId}
                onCourseChange={handleCourseChange}
              />
              <MenuFilterBar
                search={filters.search}
                onSearchChange={handleSearchChange}
                status={filters.status}
                onStatusChange={handleStatusChange}
                sort={filters.sort}
                onSortChange={handleSortChange}
                targetFoodCostPercentage={data.summary.targetFoodCostPercentage}
              />
            </Card>

            {isCatalogueEmpty ? (
              <MenuEmptyState mode="catalogue" onCreate={handleCreate} />
            ) : null}

            {!isCatalogueEmpty && data.items.length === 0 ? (
              <MenuEmptyState
                mode="filtered"
                filterDescription={describeFilters(filters, data.courses.items)}
                onCreate={handleCreate}
              />
            ) : null}

            {data.items.length > 0 ? (
              <>
                <DishCardGrid
                  items={data.items}
                  selectedMenuId={selectedItem ? selectedItem.menuId : null}
                  onSelect={handleSelectDish}
                />
                <MenuPagination
                  page={data.page}
                  pageSize={data.pageSize}
                  totalCount={data.totalCount}
                  totalPages={data.totalPages}
                  onPageChange={handlePageChange}
                  onPageSizeChange={handlePageSizeChange}
                  isFetching={isFetching}
                />
              </>
            ) : null}
          </div>

          <div className="xl:col-span-5 xl:sticky xl:top-20 min-w-0">
            <RecipeInspector item={selectedItem} onEdit={handleEdit} />
          </div>
        </div>
      ) : null}

      <StatusFooter generatedAt={data?.generatedAt ?? ''} connection={connection} />
    </div>
  )
}
