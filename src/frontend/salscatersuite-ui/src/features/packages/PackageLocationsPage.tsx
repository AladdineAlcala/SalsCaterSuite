import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import {
  ArrowLeft,
  FileDown,
  Gauge,
  MapPinned,
  Plus,
  RefreshCw,
  RotateCcw,
  Save,
  Users,
} from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { SpinnerLoader } from '@/components/feedback/SpinnerLoader'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EM_DASH, formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type {
  AreaDto,
  AreaRequest,
  PackageAreaCoverageMatrix,
  PackageAreaCoverageRow,
} from '@/types/package.types'
import { PackageAreaCataloguePanel } from './components/PackageAreaCataloguePanel'
import { PackageCourseAllocationCard } from './components/PackageCourseAllocationCard'
import { PackageCoverageMatrix } from './components/PackageCoverageMatrix'
import { PackageKpiCard } from './components/PackageKpiCard'
import { PackageLocationForm } from './components/PackageLocationForm'
import { PackageLocationSimulator } from './components/PackageLocationSimulator'
import { useAreaCatalogue, useCreateArea, useUpdateArea } from './hooks/useAreaCatalogue'
import {
  useAttachPackageArea,
  usePackageAreaCoverage,
  usePackageLocationPreviews,
  usePackageSpecSheetDownload,
  useSavePackageAreaCoverage,
} from './hooks/usePackageAreaCoverage'
import { usePackageCourseRuleset } from './hooks/usePackageCourseRules'
import { NOT_STATED, parseOptionalNumber } from './packageTypes'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/**
 * The Location & Out-of-Town Add-On Matrix — Screen B (spec §5.3).
 *
 * The screen attaches the municipalities a tier can be served in, prices a
 * booking for a chosen municipality and headcount, and shows the gross-profit
 * split.
 *
 * **Every figure is the server's.** The matrix, the four-card strip, the
 * breakdown and the gross-profit telemetry all come from the API; the browser
 * computes no money and no percentage (DEC-002, AC-P2-28).
 */
export function PackageLocationsPage() {
  const navigate = useNavigate()
  const params = useParams<{ packageId?: string }>()

  const rawId = params.packageId ?? null
  const parsedId = rawId === null ? Number.NaN : Number(rawId)
  const packageId = Number.isFinite(parsedId) ? parsedId : null

  const matrixQuery = usePackageAreaCoverage(packageId)
  const rulesetQuery = usePackageCourseRuleset(packageId)
  const catalogueQuery = useAreaCatalogue()
  const saveCoverage = useSavePackageAreaCoverage()
  const attachArea = useAttachPackageArea()
  const createArea = useCreateArea()
  const updateArea = useUpdateArea()
  const downloadSpecSheet = usePackageSpecSheetDownload()

  // The working copy of the coverage set — saved as one document (DEC-085).
  const [pendingRows, setPendingRows] = useState<PackageAreaCoverageRow[]>([])
  const [removedAreaIds, setRemovedAreaIds] = useState<number[]>([])
  const [isDirty, setIsDirty] = useState(false)
  const [seededMatrix, setSeededMatrix] = useState<PackageAreaCoverageMatrix | null>(null)
  const [coverageServerErrors, setCoverageServerErrors] = useState<string[]>([])
  const [coverageNotice, setCoverageNotice] = useState<string | null>(null)

  /* The simulator's inputs. */
  const [selectedAreaId, setSelectedAreaId] = useState<number | null>(null)
  const [pax, setPax] = useState('')

  /* The municipality editor. */
  const [isAreaEditorOpen, setIsAreaEditorOpen] = useState(false)
  const [editingArea, setEditingArea] = useState<AreaDto | null>(null)
  const [areaServerErrors, setAreaServerErrors] = useState<string[]>([])

  const matrix = matrixQuery.data
  const minPax = matrix?.minPax ?? null
  const isUnpriced = matrix !== undefined && matrix.amountPax === null

  /*
   * The working copy is seeded from the matrix the server published.
   *
   * The adjustment happens **during** the render, so the matrix never shows
   * its empty state for a frame while the rows are still being copied. A
   * staged edit is never discarded by a background refetch: the seeding is
   * skipped while the operator has an unsaved change.
   */
  const dirtyRef = useRef(false)
  if (matrix && matrix !== seededMatrix && !dirtyRef.current) {
    setSeededMatrix(matrix)
    setPendingRows(matrix.areas)
    setRemovedAreaIds([])
  }

  /* The headcount the quote is priced at — the typed value, else the floor. */
  const paxNumber = useMemo(() => parseOptionalNumber(pax), [pax])
  const paxError = useMemo(() => {
    if (pax.trim().length === 0) return undefined
    if (paxNumber === null) return 'Pax must be a number.'
    if (paxNumber < 1) return 'Pax must be at least 1.'
    return undefined
  }, [pax, paxNumber])

  const effectivePax = useMemo(() => {
    if (paxError !== undefined) return null
    if (paxNumber !== null && paxNumber >= 1) return paxNumber
    return minPax
  }, [paxError, paxNumber, minPax])

  const areaIds = useMemo(() => pendingRows.map((row) => row.areaId), [pendingRows])

  const previewQuery = usePackageLocationPreviews(packageId, effectivePax, areaIds)
  const previews = previewQuery.data ?? {}

  /*
   * While the typed headcount has not reached the server yet — or the quote is
   * in flight — the screen shows an explicit "recalculating" state instead of
   * a figure priced for a different headcount. A zero is never shown in its
   * place.
   */
  const isRecalculating = previewQuery.isRecalculating

  const previewErrors = useMemo(() => {
    if (!previewQuery.error) return []
    return readErrorMessages(previewQuery.error)
  }, [previewQuery.error])

  // The simulator follows the first covered municipality until the operator
  // chooses another one.
  useEffect(() => {
    if (selectedAreaId !== null) return
    const first = pendingRows[0]
    if (first) setSelectedAreaId(first.areaId)
  }, [pendingRows, selectedAreaId])

  const markDirty = useCallback(() => {
    dirtyRef.current = true
    setIsDirty(true)
    setCoverageNotice(null)
  }, [])

  const handleToggleActive = useCallback(
    (row: PackageAreaCoverageRow) => {
      markDirty()
      setPendingRows((current) =>
        current.map((entry) =>
          entry.areaId === row.areaId ? { ...entry, isActive: !entry.isActive } : entry,
        ),
      )
    },
    [markDirty],
  )

  const handleRemoveRow = useCallback(
    (row: PackageAreaCoverageRow) => {
      markDirty()
      setRemovedAreaIds((current) =>
        current.includes(row.areaId) ? current : [...current, row.areaId],
      )
    },
    [markDirty],
  )

  const handleRestoreRow = useCallback(
    (row: PackageAreaCoverageRow) => {
      markDirty()
      setRemovedAreaIds((current) => current.filter((areaId) => areaId !== row.areaId))
    },
    [markDirty],
  )

  const handleRevert = useCallback(() => {
    const data = matrixQuery.data
    dirtyRef.current = false
    setIsDirty(false)
    setRemovedAreaIds([])
    setPendingRows(data ? data.areas : [])
    setCoverageServerErrors([])
    setCoverageNotice(null)
  }, [matrixQuery.data])

  const handleSaveCoverage = useCallback(() => {
    if (packageId === null) {
      setCoverageServerErrors(['The tier being configured could not be identified.'])
      return
    }

    const coverages = pendingRows
      .filter((row) => !removedAreaIds.includes(row.areaId))
      .map((row) => ({ areaId: row.areaId, isActive: row.isActive }))

    setCoverageServerErrors([])
    setCoverageNotice(null)

    saveCoverage.mutate(
      { packageId, request: { coverages } },
      {
        onSuccess: (saved) => {
          dirtyRef.current = false
          setIsDirty(false)
          setPendingRows(saved.areas)
          setRemovedAreaIds([])
          setCoverageNotice('The coverage set was applied to this tier.')
        },
        onError: (error) => setCoverageServerErrors(readErrorMessages(error)),
      },
    )
  }, [packageId, pendingRows, removedAreaIds, saveCoverage])

  const handleAttach = useCallback(
    (area: AreaDto) => {
      if (packageId === null) return
      setCoverageServerErrors([])
      setCoverageNotice(null)
      attachArea.mutate(
        { packageId, request: { areaId: area.areaId, isActive: true } },
        {
          onSuccess: () => setCoverageNotice(`${area.areaDetails} now covers this tier.`),
          onError: (error) => setCoverageServerErrors(readErrorMessages(error)),
        },
      )
    },
    [packageId, attachArea],
  )

  const handleOpenAreaEditor = useCallback((area: AreaDto | null) => {
    setEditingArea(area)
    setIsAreaEditorOpen(true)
    setAreaServerErrors([])
  }, [])

  const handleCloseAreaEditor = useCallback(() => {
    setIsAreaEditorOpen(false)
    setEditingArea(null)
    setAreaServerErrors([])
  }, [])

  const handleSaveArea = useCallback(
    (request: AreaRequest) => {
      setAreaServerErrors([])
      if (editingArea === null) {
        createArea.mutate(request, {
          onSuccess: () => handleCloseAreaEditor(),
          onError: (error) => setAreaServerErrors(readErrorMessages(error)),
        })
        return
      }
      updateArea.mutate(
        { areaId: editingArea.areaId, request },
        {
          onSuccess: () => handleCloseAreaEditor(),
          onError: (error) => setAreaServerErrors(readErrorMessages(error)),
        },
      )
    },
    [editingArea, createArea, updateArea, handleCloseAreaEditor],
  )

  const handleDownloadSpecSheet = useCallback(() => {
    if (packageId === null) return
    setCoverageServerErrors([])
    downloadSpecSheet.mutate(packageId, {
      onError: (error) => setCoverageServerErrors(readErrorMessages(error)),
    })
  }, [packageId, downloadSpecSheet])

  const handleRetry = useCallback(() => {
    void matrixQuery.refetch()
    void rulesetQuery.refetch()
    void catalogueQuery.refetch()
  }, [matrixQuery, rulesetQuery, catalogueQuery])

  const handleBack = useCallback(() => navigate('/packages'), [navigate])

  const handleOpenRules = useCallback(() => {
    if (packageId === null) return
    navigate(`/packages/${encodeURIComponent(String(packageId))}/rules`)
  }, [navigate, packageId])

  const coveredAreaIds = useMemo(() => pendingRows.map((row) => row.areaId), [pendingRows])

  const activeCoverageCount = useMemo(
    () => pendingRows.filter((row) => row.isActive && !removedAreaIds.includes(row.areaId)).length,
    [pendingRows, removedAreaIds],
  )

  const rateRangeLabel =
    matrix === null || matrix === undefined
      ? EM_DASH
      : matrix.lowestAdditionalRate === null || matrix.highestAdditionalRate === null
        ? NOT_STATED
        : `${formatCurrency(matrix.lowestAdditionalRate)} to ${formatCurrency(matrix.highestAdditionalRate)}`

  const grossProfitRangeLabel =
    matrix === undefined || matrix.lowestGrossProfitPercentage === null
      ? EM_DASH
      : `${formatPercentage(matrix.lowestGrossProfitPercentage, 2)} to ${formatPercentage(
          matrix.highestGrossProfitPercentage,
          2,
        )}`

  return (
    <div className="flex flex-col gap-space-xl">
      <Card
        elevation={1}
        className="flex flex-col lg:flex-row lg:items-center justify-between gap-space-md"
      >
        <div className="flex flex-col gap-1 min-w-0">
          <nav
            aria-label="Breadcrumb"
            className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-400 flex-wrap"
          >
            <span>Catering Operations</span>
            <span aria-hidden="true">/</span>
            <span>Packages &amp; Buffets</span>
            <span aria-hidden="true">/</span>
            <span className="font-semibold text-espresso-850">Location Coverage</span>
          </nav>
          <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
            Location &amp; Out-of-Town Add-On Matrix
          </h1>
          <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
            {matrix
              ? `${matrix.packageName} — ${matrix.coverageCount} covered municipalities, ${matrix.activeCoverageCount} of them active.`
              : 'The municipalities a tier can be served in, with their per-pax surcharge and per-event logistics cost.'}
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0 flex-wrap">
          <Button variant="secondary" icon={ArrowLeft} onClick={handleBack}>
            Back to the catalogue
          </Button>
          <Button variant="secondary" icon={MapPinned} onClick={handleOpenRules}>
            Course rules
          </Button>
          <Button
            variant="primary"
            icon={FileDown}
            onClick={handleDownloadSpecSheet}
            disabled={downloadSpecSheet.isPending || packageId === null}
          >
            {downloadSpecSheet.isPending ? 'Preparing…' : 'Specification sheet'}
          </Button>
        </div>
      </Card>

      {coverageServerErrors.length > 0 ? (
        <AlertBanner
          title="The request was rejected"
          errors={coverageServerErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setCoverageServerErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {coverageNotice ? (
        <AlertBanner
          title="Applied"
          errors={[coverageNotice]}
          variant="info"
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setCoverageNotice(null)}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {matrixQuery.isError ? (
        <AlertBanner
          title="The coverage matrix could not be loaded"
          errors={readErrorMessages(matrixQuery.error)}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={handleRetry}>
              Retry
            </Button>
          }
        />
      ) : null}

      {matrixQuery.isPending ? <SpinnerLoader label="Loading the coverage matrix" /> : null}

      {matrix ? (
        <>
          {/* The four-card strip — every figure is the server's. */}
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-gutter">
            <PackageKpiCard
              icon={Users}
              iconTone="culinary"
              label="Minimum"
              value={minPax === null ? EM_DASH : formatCount(minPax)}
              unit={minPax === null ? undefined : 'heads and above'}
              subLine={`Minimum contract value ${formatCurrency(matrix.minimumContractValue)}`}
              secondaryLine="A derived floor on the contract — not a charge."
            />
            <PackageKpiCard
              icon={Gauge}
              iconTone="culinary"
              label="Base Tariff"
              value={formatCurrency(matrix.amountPax)}
              unit="per guest"
              subLine={`Minimum contract value ${formatCurrency(matrix.minimumContractValue)}`}
              secondaryLine="The tier's own per-guest rate, before any surcharge."
            />
            <PackageKpiCard
              icon={MapPinned}
              iconTone="caramel"
              label="Out-of-Town Coverage"
              value={formatCount(activeCoverageCount)}
              unit="active municipalities"
              subLine={`Surcharge range ${rateRangeLabel}`}
              secondaryLine="The rate is the municipality's, shared by every tier that covers it."
            />
            <PackageKpiCard
              icon={Gauge}
              iconTone="caramel"
              label="Gross Profit Safeguard"
              value={formatPercentage(matrix.baseGrossProfitPercentage, 2)}
              unit="base gross profit"
              subLine={`Logistics-adjusted range ${grossProfitRangeLabel}`}
              secondaryLine={`Food cost is the standard run, the headcount is the tier's floor, and ${formatCount(
                matrix.grossProfitCoverageCount,
              )} covered rows contributed.`}
            />
          </div>

          <div className="grid grid-cols-1 xl:grid-cols-12 gap-gutter items-start">
            <div className="xl:col-span-7 flex flex-col gap-space-md min-w-0">
              <PackageCoverageMatrix
                rows={pendingRows}
                previews={previews}
                pax={effectivePax}
                isRecalculating={isRecalculating}
                pendingRemovalAreaIds={removedAreaIds}
                onToggleActive={handleToggleActive}
                onRemove={handleRemoveRow}
                onRestore={handleRestoreRow}
                onEditMunicipality={(row) => {
                  const catalogueRow = (catalogueQuery.data ?? []).find(
                    (area) => area.areaId === row.areaId,
                  )
                  handleOpenAreaEditor(catalogueRow ?? null)
                }}
                hasUnsavedChanges={isDirty}
              />

              <Card elevation={1} className="flex flex-col gap-space-md">
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <h2 className="font-headline-sm text-headline-sm text-espresso-900">
                    Coverage Set
                  </h2>
                  <span className="font-caption text-caption text-espresso-600">
                    {isDirty ? 'unsaved changes pending' : 'saved'}
                  </span>
                </div>
                <p className="font-body-sm text-body-sm text-espresso-600">
                  Activating, deactivating and removing a coverage row are staged here and
                  applied together, because a coverage list is read as one document.
                </p>
                <div className="flex items-center gap-2 flex-wrap">
                  <Button
                    variant="primary"
                    icon={Save}
                    onClick={handleSaveCoverage}
                    disabled={saveCoverage.isPending}
                  >
                    {saveCoverage.isPending ? 'Applying…' : 'Save coverage set'}
                  </Button>
                  <Button variant="secondary" icon={RotateCcw} onClick={handleRevert}>
                    Revert
                  </Button>
                </div>
              </Card>

              <PackageAreaCataloguePanel
                areas={catalogueQuery.data ?? []}
                coveredAreaIds={coveredAreaIds}
                onAttach={handleAttach}
                onEdit={handleOpenAreaEditor}
                isAttaching={attachArea.isPending}
              />

              <Button
                variant="secondary"
                icon={Plus}
                onClick={() => handleOpenAreaEditor(null)}
                className="self-start"
              >
                Add a municipality
              </Button>

              {isAreaEditorOpen ? (
                <PackageLocationForm
                  area={editingArea}
                  onSubmit={handleSaveArea}
                  onCancel={handleCloseAreaEditor}
                  isSaving={createArea.isPending || updateArea.isPending}
                  serverErrors={areaServerErrors}
                />
              ) : null}
            </div>

            <div className="xl:col-span-5 flex flex-col gap-space-md min-w-0">
              <PackageLocationSimulator
                rows={pendingRows.filter((row) => !removedAreaIds.includes(row.areaId))}
                selectedAreaId={selectedAreaId}
                onSelectedAreaIdChange={setSelectedAreaId}
                pax={pax}
                onPaxChange={setPax}
                paxError={paxError}
                preview={
                  isRecalculating || selectedAreaId === null
                    ? undefined
                    : previews[String(selectedAreaId)]
                }
                isPreviewing={isRecalculating}
                isRecalculating={isRecalculating}
                previewErrors={previewErrors}
                minPax={minPax}
                isUnpriced={isUnpriced}
              />

              <PackageCourseAllocationCard ruleset={rulesetQuery.data} />

              <CapabilityGapNotice
                title="Mobile logistics checklist unavailable"
                missing="No record carries the mobile logistics checklist the reference shows beside the surcharge matrix — the Cambro units, the burners and the water run are not modelled."
                footnote="Deliberately excluded by DEC-084 — no entity carries this data."
              />
            </div>
          </div>
        </>
      ) : null}
    </div>
  )
}
