import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, MapPinned, Plus, RefreshCw, Save } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { EmptyState } from '@/components/feedback/EmptyState'
import { SpinnerLoader } from '@/components/feedback/SpinnerLoader'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import type {
  PackageCourseRuleRequest,
  PackageCourseSelection,
} from '@/types/package.types'
import { PackageCourseRuleCard } from './components/PackageCourseRuleCard'
import { PackageCourseRuleEditor } from './components/PackageCourseRuleEditor'
import { PackageForecastPanel } from './components/PackageForecastPanel'
import { PackageQuotaComplianceList } from './components/PackageQuotaComplianceList'
import { PackageSelectionSimulator } from './components/PackageSelectionSimulator'
import { PackageSpecRail } from './components/PackageSpecRail'
import {
  usePackageCourseRulePreview,
  usePackageCourseRuleset,
  usePackageCourseTaxonomy,
  useSavePackageCourseRules,
} from './hooks/usePackageCourseRules'
import { usePackageAreaCoverage } from './hooks/usePackageAreaCoverage'
import {
  courseRuleDraftFromRule,
  courseRuleView,
  emptyCourseRuleDraft,
  parseOptionalNumber,
  toCourseRuleRequest,
  validateCourseRuleDraft,
} from './packageTypes'
import type {
  PackageCourseRuleDraft,
  PackageCourseRuleDraftErrors,
} from './packageTypes'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/**
 * The Course Coverage Rules Workbench — Screen A (spec §5.2).
 *
 * The screen attaches an enforced course-category rule to each course a tier
 * must satisfy and then simulates a client's dish selection against those
 * rules.
 *
 * **Every figure is the server's.** The ruleset, the qualified pools, the
 * standard run, the cost ceiling, the compliance verdicts and the headcount
 * figures all come from the API; the browser computes no money and no
 * percentage (DEC-002).
 *
 * The screen holds the **whole** unsaved ruleset and the save applies all of
 * it at once, because a ruleset is read as one document (DEC-085).
 */
export function PackageCourseRulesPage() {
  const navigate = useNavigate()
  const params = useParams<{ packageId?: string }>()

  const rawId = params.packageId ?? null
  const parsedId = rawId === null ? Number.NaN : Number(rawId)
  const packageId = Number.isFinite(parsedId) ? parsedId : null

  const rulesetQuery = usePackageCourseRuleset(packageId)
  const coverageQuery = usePackageAreaCoverage(packageId)
  const taxonomyQuery = usePackageCourseTaxonomy()
  const saveRules = useSavePackageCourseRules()

  /* The pending ruleset — the whole document, saved in one write. */
  const [drafts, setDrafts] = useState<PackageCourseRuleDraft[]>([])
  const [editorDraft, setEditorDraft] = useState<PackageCourseRuleDraft | null>(null)
  const [editingIndex, setEditingIndex] = useState<number | null>(null)
  const [draftErrors, setDraftErrors] = useState<PackageCourseRuleDraftErrors>({})
  const [serverErrors, setServerErrors] = useState<string[]>([])
  const [saveNotice, setSaveNotice] = useState<string | null>(null)

  /* The simulator's inputs. */
  const [selections, setSelections] = useState<PackageCourseSelection[]>([])
  const [stressPercentage, setStressPercentage] = useState('')
  const [pax, setPax] = useState('')

  // The pending ruleset is seeded once from the ruleset the API published; a
  // rejected save never discards what the operator typed (BR-11).
  const seededId = useRef<number | null>(null)
  useEffect(() => {
    const data = rulesetQuery.data
    if (data && seededId.current !== data.packageId) {
      seededId.current = data.packageId
      setDrafts(data.rules.map(courseRuleDraftFromRule))
    }
  }, [rulesetQuery.data])

  const serverRules = useMemo(() => rulesetQuery.data?.rules ?? [], [rulesetQuery.data])

  const ruleEntries = useMemo(
    () =>
      drafts.map((draft, index) => {
        const serverRule = serverRules.find((rule) => String(rule.courseId) === draft.courseId)
        return {
          index,
          view: courseRuleView(serverRule, draft),
          isNew: serverRule === undefined,
        }
      }),
    [drafts, serverRules],
  )

  const usedCourseIds = useMemo(
    () =>
      drafts
        .filter((_, index) => index !== editingIndex)
        .map((draft) => Number(draft.courseId))
        .filter((courseId) => Number.isFinite(courseId)),
    [drafts, editingIndex],
  )

  const handleAddRule = useCallback(() => {
    setEditorDraft(emptyCourseRuleDraft())
    setEditingIndex(null)
    setDraftErrors({})
    setServerErrors([])
    setSaveNotice(null)
  }, [])

  const handleEditRule = useCallback(
    (index: number) => {
      setEditorDraft(drafts[index])
      setEditingIndex(index)
      setDraftErrors({})
      setServerErrors([])
    },
    [drafts],
  )

  const handleRemoveRule = useCallback(
    (index: number) => {
      setDrafts((current) => current.filter((_, position) => position !== index))
      if (editingIndex === index) {
        setEditorDraft(null)
        setEditingIndex(null)
        setDraftErrors({})
      }
    },
    [editingIndex],
  )

  const handleEditorSubmit = useCallback(() => {
    if (editorDraft === null) return

    const problems = validateCourseRuleDraft(editorDraft)
    if (Object.keys(problems).length > 0) {
      setDraftErrors(problems)
      return
    }

    setDrafts((current) => {
      if (editingIndex === null) return [...current, editorDraft]
      const next = [...current]
      next[editingIndex] = editorDraft
      return next
    })
    setEditorDraft(null)
    setEditingIndex(null)
    setDraftErrors({})
    setServerErrors([])
  }, [editorDraft, editingIndex])

  const handleEditorCancel = useCallback(() => {
    setEditorDraft(null)
    setEditingIndex(null)
    setDraftErrors({})
  }, [])

  const handleSave = useCallback(() => {
    if (packageId === null) {
      setServerErrors(['The tier being configured could not be identified.'])
      return
    }

    for (let index = 0; index < drafts.length; index += 1) {
      const problems = validateCourseRuleDraft(drafts[index])
      if (Object.keys(problems).length > 0) {
        setEditorDraft(drafts[index])
        setEditingIndex(index)
        setDraftErrors(problems)
        setServerErrors([])
        return
      }
    }

    const seen = new Set<number>()
    const requests: PackageCourseRuleRequest[] = []
    for (const draft of drafts) {
      const courseId = Number(draft.courseId)
      if (seen.has(courseId)) {
        setServerErrors([`Course category ${courseId} is configured more than once.`])
        return
      }
      seen.add(courseId)

      const request = toCourseRuleRequest(draft)
      if (request === null) {
        setServerErrors(['A course rule could not be read. Check the quota and the portion.'])
        return
      }
      requests.push(request)
    }

    setDraftErrors({})
    setServerErrors([])
    setSaveNotice(null)

    saveRules.mutate(
      { packageId, request: { rules: requests } },
      {
        onSuccess: () => setSaveNotice('The course rules were applied to this tier.'),
        onError: (error) => setServerErrors(readErrorMessages(error)),
      },
    )
  }, [drafts, packageId, saveRules])

  const handleAddDish = useCallback((courseId: number, menuId: string) => {
    setSelections((current) =>
      current.some((selection) => selection.courseId === courseId && selection.menuId === menuId)
        ? current
        : [...current, { courseId, menuId }],
    )
  }, [])

  const handleRemoveDish = useCallback((courseId: number, menuId: string) => {
    setSelections((current) =>
      current.filter(
        (selection) => !(selection.courseId === courseId && selection.menuId === menuId),
      ),
    )
  }, [])

  const handleResetSelection = useCallback(() => setSelections([]), [])

  /* The preview inputs — the browser only parses what the operator typed. */
  const stressNumber = useMemo(() => parseOptionalNumber(stressPercentage), [stressPercentage])
  const stressError = useMemo(() => {
    if (stressPercentage.trim().length === 0) return undefined
    if (stressNumber === null) return 'Stress percentage must be a number.'
    if (stressNumber < 0) return 'Stress percentage must be zero or greater.'
    return undefined
  }, [stressPercentage, stressNumber])

  const paxNumber = useMemo(() => parseOptionalNumber(pax), [pax])
  const paxError = useMemo(() => {
    if (pax.trim().length === 0) return undefined
    if (paxNumber === null) return 'Pax must be a number.'
    if (paxNumber < 1) return 'Pax must be at least 1.'
    return undefined
  }, [pax, paxNumber])

  const previewQuery = usePackageCourseRulePreview(
    packageId,
    selections,
    stressError === undefined ? stressNumber : null,
    paxError === undefined ? paxNumber : null,
  )

  const previewErrors = useMemo(() => {
    if (!previewQuery.error) return []
    return readErrorMessages(previewQuery.error)
  }, [previewQuery.error])

  /*
   * While the typed selection has not reached the server yet — or the preview
   * is in flight — no preview-derived figure is presented. The screen shows
   * the explicit "recalculating" state instead, so a verdict or a peso figure
   * priced for the **previous** selection is never read as the current one.
   */
  const isRecalculating = previewQuery.isRecalculating

  const ruleset = rulesetQuery.data
  const isUnpriced = ruleset !== undefined && ruleset.amountPax === null

  const handleRetry = useCallback(() => {
    void rulesetQuery.refetch()
    void coverageQuery.refetch()
  }, [rulesetQuery, coverageQuery])

  const handleEditTierTerms = useCallback(() => {
    if (packageId === null) return
    navigate(`/packages/${encodeURIComponent(String(packageId))}/edit`)
  }, [navigate, packageId])

  const handleOpenLocations = useCallback(() => {
    if (packageId === null) return
    navigate(`/packages/${encodeURIComponent(String(packageId))}/locations`)
  }, [navigate, packageId])

  const handleBack = useCallback(() => navigate('/packages'), [navigate])

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
            <span className="font-semibold text-espresso-850">Course Rules</span>
          </nav>
          <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
            Course Coverage Rules Workbench
          </h1>
          <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
            The enforced course rules of the tier, their qualified dish pools and the
            selection simulator that judges a client&apos;s choice against them.
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0 flex-wrap">
          <Button variant="secondary" icon={ArrowLeft} onClick={handleBack}>
            Back to the catalogue
          </Button>
          <Button variant="secondary" icon={MapPinned} onClick={handleOpenLocations}>
            Location coverage
          </Button>
        </div>
      </Card>

      {serverErrors.length > 0 ? (
        <AlertBanner
          title="The course rules were rejected"
          errors={serverErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setServerErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {saveNotice ? (
        <AlertBanner
          title="The ruleset was applied"
          errors={[saveNotice]}
          variant="info"
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setSaveNotice(null)}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {rulesetQuery.isError ? (
        <AlertBanner
          title="The course ruleset could not be loaded"
          errors={readErrorMessages(rulesetQuery.error)}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={handleRetry}>
              Retry
            </Button>
          }
        />
      ) : null}

      {rulesetQuery.isPending ? <SpinnerLoader label="Loading the course ruleset" /> : null}

      {ruleset ? (
        <div className="grid grid-cols-1 xl:grid-cols-12 gap-gutter items-start">
          {/* Column 1 — the specification rail and the planning forecast. */}
          <div className="xl:col-span-3 flex flex-col gap-space-md min-w-0">
            <PackageSpecRail
              ruleset={ruleset}
              coverage={coverageQuery.data}
              onEditTierTerms={handleEditTierTerms}
            />
            <PackageForecastPanel
              ruleset={ruleset}
              preview={previewQuery.data}
              isPreviewing={previewQuery.isFetching}
              isRecalculating={isRecalculating}
              pax={pax}
              onPaxChange={setPax}
              paxError={paxError}
              isUnpriced={isUnpriced}
            />
          </div>

          {/* Column 2 — the course-category rules and quota matrix. */}
          <div className="xl:col-span-5 flex flex-col gap-space-md min-w-0">
            <Card elevation={1} className="flex flex-col gap-space-md">
              <div className="flex items-center justify-between gap-2 flex-wrap">
                <h2 className="font-headline-sm text-headline-sm text-espresso-900">
                  Course Rules
                </h2>
                <span className="font-caption text-caption text-espresso-600">
                  {ruleEntries.length} rules
                </span>
              </div>

              {ruleEntries.length === 0 ? (
                <EmptyState
                  icon={Plus}
                  title="This tier holds no course rule"
                  description="Add the first course rule to give the client a course to choose from. No rule is ever invented here."
                />
              ) : (
                <div className="flex flex-col gap-space-md">
                  {ruleEntries.map((entry) => (
                    <PackageCourseRuleCard
                      key={`${entry.view.courseId}-${entry.index}`}
                      rule={entry.view}
                      isEditing={editingIndex === entry.index}
                      isNew={entry.isNew}
                      onEdit={() => handleEditRule(entry.index)}
                      onRemove={() => handleRemoveRule(entry.index)}
                    />
                  ))}
                </div>
              )}

              <div className="flex items-center gap-2 flex-wrap">
                <Button variant="secondary" icon={Plus} onClick={handleAddRule}>
                  Add course rule
                </Button>
                <Button
                  variant="primary"
                  icon={Save}
                  onClick={handleSave}
                  disabled={saveRules.isPending}
                >
                  {saveRules.isPending ? 'Applying…' : 'Save & apply course rules'}
                </Button>
              </div>

              {taxonomyQuery.isError ? (
                <AlertBanner
                  title="The course categories could not be loaded"
                  errors={readErrorMessages(taxonomyQuery.error)}
                  variant="warning"
                />
              ) : null}
            </Card>

            {editorDraft ? (
              <PackageCourseRuleEditor
                draft={editorDraft}
                onChange={setEditorDraft}
                courses={taxonomyQuery.data ?? []}
                usedCourseIds={usedCourseIds}
                errors={draftErrors}
                onSubmit={handleEditorSubmit}
                onCancel={handleEditorCancel}
                isSaving={saveRules.isPending}
                isNew={editingIndex === null}
              />
            ) : null}
          </div>

          {/* Column 3 — the selection simulator and the compliance verdict. */}
          <div className="xl:col-span-4 flex flex-col gap-space-md min-w-0">
            <PackageSelectionSimulator
              rules={serverRules}
              selections={selections}
              onAddDish={handleAddDish}
              onRemoveDish={handleRemoveDish}
              onReset={handleResetSelection}
              preview={previewQuery.data}
              isPreviewing={previewQuery.isFetching}
              isRecalculating={isRecalculating}
              previewErrors={previewErrors}
              stressPercentage={stressPercentage}
              onStressPercentageChange={setStressPercentage}
              stressError={stressError}
              isUnpriced={isUnpriced}
            />
            <PackageQuotaComplianceList
              preview={previewQuery.data}
              isPreviewing={previewQuery.isFetching}
              isRecalculating={isRecalculating}
            />

            <CapabilityGapNotice
              title="Banquet amenities unavailable"
              missing="No seating, linen, hardware or waitstaff-ratio record is stored against a tier, so the amenities panel cannot be rendered."
              footnote="Deliberately excluded by DEC-084 — no entity carries this data."
            />
            <CapabilityGapNotice
              title="Complimentary staples unavailable"
              missing="No record carries the complimentary staples the reference shows beside the course rules."
              footnote="Deliberately excluded by DEC-084 — no entity carries this data."
            />
          </div>
        </div>
      ) : null}
    </div>
  )
}
