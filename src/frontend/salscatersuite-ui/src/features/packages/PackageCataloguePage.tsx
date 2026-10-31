import { useCallback, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { StatusFooter } from '@/features/dashboard/components/StatusFooter'
import type { ApiConnectionState } from '@/features/dashboard/components/StatusFooter'
import type { PackageSortOption, PackageStatusFilter } from '@/types/package.types'
import { PackageBanner } from './components/PackageBanner'
import { PackageCardGrid } from './components/PackageCardGrid'
import { PackageCatalogueSkeleton } from './components/PackageCatalogueSkeleton'
import { PackageEmptyState } from './components/PackageEmptyState'
import { PackageFilterBar } from './components/PackageFilterBar'
import { PackageInspector } from './components/PackageInspector'
import { PackageKpiGrid } from './components/PackageKpiGrid'
import { PackageTypeTabs } from './components/PackageTypeTabs'
import {
  usePackageList,
  usePackagePaxApplicables,
  usePackageSummary,
} from './hooks/usePackageCatalogue'
import { useTogglePackageActive } from './hooks/usePackageMutations'
import {
  filterPackageTiers,
  packageStatusFilterLabel,
  sortPackageTiers,
  summarisePackageTypes,
} from './packageTypes'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/**
 * Names the active filter so the empty state says what is being excluded
 * (spec §5.2 item 6).
 */
function describeFilters(
  search: string,
  packageType: string | null,
  status: PackageStatusFilter,
): string | undefined {
  const parts: string[] = []

  if (search.trim().length > 0) {
    parts.push(`the search “${search.trim()}”`)
  }
  if (packageType !== null) {
    parts.push(`the ${packageType} tier type`)
  }
  if (status) {
    parts.push(`the ${packageStatusFilterLabel(status)} filter`)
  }

  return parts.length > 0 ? parts.join(' and ') : undefined
}

/**
 * The Packages & Buffets Catalogue screen (spec §5.2).
 *
 * The screen renders the server's tier list and the **server-computed**
 * catalogue aggregate. Every KPI figure — the tier counts, the average
 * per-guest rate, the rate range and the headcount floor — comes from
 * `GET /api/packages/summary`; the browser never sums the tier list to
 * produce one (DEC-002, DEC-068).
 *
 * Filtering and ordering are applied in the browser over the server's unpaged
 * list, and no filter is sent to an endpoint that does not accept one
 * (AC-P04).
 */
export function PackageCataloguePage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [packageType, setPackageType] = useState<string | null>(null)
  const [status, setStatus] = useState<PackageStatusFilter>('')
  const [sort, setSort] = useState<PackageSortOption>('RateDesc')
  const [selectedPackageId, setSelectedPackageId] = useState<number | null>(null)
  const [actionErrors, setActionErrors] = useState<string[]>([])

  const listQuery = usePackageList()
  const summaryQuery = usePackageSummary()
  const paxApplicablesQuery = usePackagePaxApplicables()

  const paxApplicableLabels = useMemo(
    () =>
      new Map(
        (paxApplicablesQuery.data ?? []).map((item) => [
          item.numPaxId,
          item.packageApplicablePax,
        ]),
      ),
    [paxApplicablesQuery.data],
  )
  const getPaxClassificationLabel = useCallback(
    (numPaxId: number) => {
      if (paxApplicablesQuery.isPending) return 'Loading classification…'
      return paxApplicableLabels.get(numPaxId) ?? 'Classification unavailable'
    },
    [paxApplicablesQuery.isPending, paxApplicableLabels],
  )

  const { refetch: refetchList } = listQuery
  const { refetch: refetchSummary } = summaryQuery

  const toggleActive = useTogglePackageActive()

  const tiers = useMemo(() => listQuery.data ?? [], [listQuery.data])

  const typeTabs = useMemo(() => summarisePackageTypes(tiers), [tiers])

  const visibleTiers = useMemo(
    () =>
      sortPackageTiers(
        filterPackageTiers(tiers, { search, packageType, status }),
        sort,
      ),
    [tiers, search, packageType, status, sort],
  )

  const handleSearchChange = useCallback((value: string) => {
    setSearch(value)
  }, [])

  const handleTypeChange = useCallback((value: string | null) => {
    setPackageType(value)
  }, [])

  const handleStatusChange = useCallback((value: PackageStatusFilter) => {
    setStatus(value)
  }, [])

  const handleSortChange = useCallback((value: PackageSortOption) => {
    setSort(value)
  }, [])

  const handleSelectTier = useCallback((id: number) => {
    setSelectedPackageId(id)
  }, [])

  const handleCreate = useCallback(() => {
    setActionErrors([])
    navigate('/packages/new')
  }, [navigate])

  const handleEdit = useCallback(
    (id: number) => {
      setActionErrors([])
      navigate(`/packages/${encodeURIComponent(String(id))}/edit`)
    },
    [navigate],
  )

  const handleOpenCourseRules = useCallback(
    (id: number) => {
      setActionErrors([])
      navigate(`/packages/${encodeURIComponent(String(id))}/rules`)
    },
    [navigate],
  )

  const handleOpenLocations = useCallback(
    (id: number) => {
      setActionErrors([])
      navigate(`/packages/${encodeURIComponent(String(id))}/locations`)
    },
    [navigate],
  )

  const handleToggleActive = useCallback(
    (id: number) => {
      setActionErrors([])
      toggleActive.mutate(id, {
        onError: (mutationError) => setActionErrors(readErrorMessages(mutationError)),
      })
    },
    [toggleActive],
  )

  const handleRecalculate = useCallback(() => {
    setActionErrors([])
    void refetchList()
    void refetchSummary()
  }, [refetchList, refetchSummary])

  // The selected tier falls back to the first visible card, so the inspector
  // is never empty while the catalogue holds tiers.
  const selectedTier = useMemo(() => {
    if (visibleTiers.length === 0) return null
    return (
      visibleTiers.find((tier) => tier.packageId === selectedPackageId) ?? visibleTiers[0]
    )
  }, [visibleTiers, selectedPackageId])

  const isPending = listQuery.isPending || summaryQuery.isPending
  const isFetching = listQuery.isFetching || summaryQuery.isFetching
  const hasFailed = Boolean(listQuery.error || summaryQuery.error)

  const loadErrors = useMemo(() => {
    if (!hasFailed) return []
    const messages = [
      ...(listQuery.error ? readErrorMessages(listQuery.error) : []),
      ...(summaryQuery.error ? readErrorMessages(summaryQuery.error) : []),
    ]
    return Array.from(new Set(messages))
  }, [hasFailed, listQuery.error, summaryQuery.error])

  const connection: ApiConnectionState = hasFailed
    ? 'offline'
    : isFetching
      ? 'refreshing'
      : 'live'

  /*
   * The two empty states are distinct (spec §5.2 item 6, AC-P07).
   *
   * The list endpoint returns the whole catalogue, so a catalogue that holds
   * no tier at all is unambiguous: the filter narrows a list that is already
   * populated, and that is a *filtered* result, not an empty catalogue.
   */
  const isCatalogueEmpty = tiers.length === 0

  return (
    <div className="flex flex-col gap-space-xl">
      <PackageBanner
        onRecalculate={handleRecalculate}
        isRecalculating={isFetching}
        onCreate={handleCreate}
      />

      {actionErrors.length > 0 ? (
        <AlertBanner
          title="The tier state could not be changed"
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

      {isPending ? <PackageCatalogueSkeleton /> : null}

      {hasFailed ? (
        <AlertBanner
          title="The packages catalogue could not be loaded"
          errors={loadErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={handleRecalculate}
            >
              Retry
            </Button>
          }
        />
      ) : null}

      {summaryQuery.data ? <PackageKpiGrid summary={summaryQuery.data} /> : null}

      {listQuery.data ? (
        <div className="grid grid-cols-1 xl:grid-cols-12 gap-gutter items-start">
          <div className="xl:col-span-7 flex flex-col gap-space-md min-w-0">
            <Card elevation={1} className="p-space-md flex flex-col gap-space-sm">
              <PackageTypeTabs
                types={typeTabs}
                totalCount={tiers.length}
                activeType={packageType}
                onTypeChange={handleTypeChange}
              />
              <PackageFilterBar
                search={search}
                onSearchChange={handleSearchChange}
                status={status}
                onStatusChange={handleStatusChange}
                sort={sort}
                onSortChange={handleSortChange}
              />
            </Card>

            {isCatalogueEmpty ? (
              <PackageEmptyState mode="catalogue" onCreate={handleCreate} />
            ) : null}

            {!isCatalogueEmpty && visibleTiers.length === 0 ? (
              <PackageEmptyState
                mode="filtered"
                filterDescription={describeFilters(search, packageType, status)}
                onCreate={handleCreate}
              />
            ) : null}

            {visibleTiers.length > 0 ? (
              <PackageCardGrid
                tiers={visibleTiers}
                selectedPackageId={selectedTier ? selectedTier.packageId : null}
                onSelect={handleSelectTier}
                getPaxClassificationLabel={getPaxClassificationLabel}
              />
            ) : null}
          </div>

          <div className="xl:col-span-5 xl:sticky xl:top-20 min-w-0">
            <PackageInspector
              tier={selectedTier}
              paxClassificationLabel={
                selectedTier ? getPaxClassificationLabel(selectedTier.numPaxId) : null
              }
              onEdit={handleEdit}
              onToggleActive={handleToggleActive}
              isToggling={toggleActive.isPending}
              onOpenCourseRules={handleOpenCourseRules}
              onOpenLocations={handleOpenLocations}
            />
          </div>
        </div>
      ) : null}

      <StatusFooter generatedAt={summaryQuery.data?.generatedAt ?? ''} connection={connection} />
    </div>
  )
}
