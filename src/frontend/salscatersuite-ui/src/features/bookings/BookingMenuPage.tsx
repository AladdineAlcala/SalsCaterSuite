import { useCallback, useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { formatCurrency } from '@/utils/currencyFormatter'
import { formatEventDate } from '@/utils/dateFormatter'
import type {
  BookingAddonRequestItem,
  BookingDetails,
} from '@/types/booking.types'
import { BookingAddonsPanel } from './components/BookingAddonsPanel'
import { BookingBatchYieldPanel } from './components/BookingBatchYieldPanel'
import { BookingContractLedger } from './components/BookingContractLedger'
import { BookingMenuCourseBoard } from './components/BookingMenuCourseBoard'
import { BookingMenuCourseTabs } from './components/BookingMenuCourseTabs'
import { BookingMenuEventBanner } from './components/BookingMenuEventBanner'
import { BookingMenuFilterPanel } from './components/BookingMenuFilterPanel'
import { BookingMenuHeader } from './components/BookingMenuHeader'
import {
  readBookingDetailsIntoCache,
  useAddonCategories,
  useAddonMaster,
  useBookingAddonSet,
  useBookingDetails,
  useBookingMenu,
  useKitchenBeoDownload,
  useReplaceBookingAddons,
  useReplaceBookingMenu,
} from './hooks/useBookingMenu'
import {
  assessMenuQuota,
  isMenuSelectionComplete,
  parseOptionalNumber,
  validateAddonLineAmount,
} from './bookingVocabulary'
import type { BookingAddonDraftLine, BookingMenuDraftItem } from './bookingVocabulary'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/** Reads the route's surrogate identifier. */
function parseTransactionId(value: string | undefined): number | null {
  if (!value) return null
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null
}

/**
 * The menu editor — `/bookings/:transactionId/menu` (spec §5.3).
 *
 * A two-column workbench: the **course selection list** on the left and the
 * **add-ons, the contract ledger and the batch yield** on the right.
 *
 * The screen reads three projections and derives nothing authoritative:
 * - `GET /api/bookings/{id}` — the event identity and the **frozen** money chain;
 * - `GET /api/bookings/{id}/menus` — the selection, the qualified pools, the
 *   lock state and the planning figures;
 * - `GET /api/bookings/{id}/addons` — the frozen add-on lines.
 *
 * The write is a **full replace** of both sets, issued in sequence from the one
 * save action. The response is the **saved** selection, and the draft is
 * re-read from it, so the screen always shows what the server stored
 * (AC-BKG-51, AC-BKG-62).
 *
 * When the menu-change window has closed the whole editor is read-only and
 * says when it locked (AC-BKG-65).
 */
export function BookingMenuPage() {
  const params = useParams<{ transactionId: string }>()
  const transactionId = parseTransactionId(params.transactionId)
  const queryClient = useQueryClient()

  const menuQuery = useBookingMenu(transactionId)
  const addonSetQuery = useBookingAddonSet(transactionId)
  const detailsQuery = useBookingDetails(transactionId)

  const details = detailsQuery.data ?? null
  const pax = details?.numberOfHeads ?? null

  /*
   * The add-on master and its categories are read **only while the picker
   * modal is open** (spec §5.4, DEC-170): an initial page render never
   * requests the catalogue, and the cache is kept so reopening is instant.
   */
  const [isPickerOpen, setIsPickerOpen] = useState(false)
  const [activeCategoryId, setActiveCategoryId] = useState<number | null>(null)

  const addonCategoriesQuery = useAddonCategories(isPickerOpen)
  const addonMasterQuery = useAddonMaster(pax, activeCategoryId, isPickerOpen)
  const replaceMenu = useReplaceBookingMenu(transactionId ?? 0)
  const replaceAddons = useReplaceBookingAddons(transactionId ?? 0)
  const beoDownload = useKitchenBeoDownload()

  const [selections, setSelections] = useState<BookingMenuDraftItem[]>([])
  const [addonLines, setAddonLines] = useState<BookingAddonDraftLine[]>([])
  const [search, setSearch] = useState('')
  const [activeCourseId, setActiveCourseId] = useState<number | null>(null)
  /** Stable so the modal's focus trap does not re-arm on every render. */
  const closePicker = useCallback(() => setIsPickerOpen(false), [])
  const [saveErrors, setSaveErrors] = useState<string[]>([])
  const [saveNotice, setSaveNotice] = useState<string | null>(null)
  /**
   * `true` when the details read after a **successful** add-on write failed —
   * the total contract value could not be refreshed, so no figure may claim
   * to be recalculated until the ledger is re-read (DEC-002).
   */
  const [isDetailsRefreshFailed, setIsDetailsRefreshFailed] = useState(false)

  /*
   * The draft is seeded from the server's stored selection and re-seeded after
   * every successful save, because the write is a full replace and its response
   * is the truth (AC-BKG-51).
   */
  useEffect(() => {
    if (!menuQuery.data) return
    setSelections(
      menuQuery.data.selections.map((selection) => ({
        courseId: selection.courseId,
        menuId: selection.menuId,
        prepInstruction: selection.prepInstruction ?? '',
      })),
    )
  }, [menuQuery.data])

  useEffect(() => {
    if (!addonSetQuery.data) return
    setAddonLines(
      addonSetQuery.data.addons.map((line) => ({
        addonId: line.addonId,
        amount: String(line.amount),
      })),
    )
  }, [addonSetQuery.data])

  const menu = menuQuery.data ?? null
  const addonSet = addonSetQuery.data ?? null

  const courses = menu?.courses ?? []
  const isLocked = menu?.isLocked ?? false

  /** The quota rules the form's own affordance reads (BR-15). */
  const quotaRules = useMemo(
    () =>
      courses.map((course) => ({
        courseId: course.courseId,
        courseName: course.courseName,
        requiredQuota: course.requiredQuota,
      })),
    [courses],
  )

  const quotaStatuses = useMemo(
    () => assessMenuQuota(quotaRules, selections),
    [quotaRules, selections],
  )

  const isQuotaComplete = useMemo(
    () => isMenuSelectionComplete(quotaRules, selections),
    [quotaRules, selections],
  )

  const selectedTotal = quotaStatuses.reduce(
    (total, status) => total + status.selectedCount,
    0,
  )
  const requiredTotal = quotaStatuses.reduce(
    (total, status) => total + status.requiredQuota,
    0,
  )

  const quotaMode: 'standard-run' | 'complete' | 'incomplete' =
    selections.length === 0 ? 'standard-run' : isQuotaComplete ? 'complete' : 'incomplete'

  const poolSize = useMemo(
    () => courses.reduce((total, course) => total + course.pool.length, 0),
    [courses],
  )

  const matchedCount = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (term.length === 0) return poolSize
    return courses.reduce(
      (total, course) =>
        total + course.pool.filter((dish) => dish.menuName.toLowerCase().includes(term)).length,
      0,
    )
  }, [courses, poolSize, search])

  /** The saved add-on key — the comparison that says whether the draft differs. */
  const savedAddonKey = useMemo(
    () =>
      (addonSet?.addons ?? [])
        .map((line) => `${line.addonId}:${line.amount}`)
        .sort()
        .join('|'),
    [addonSet],
  )

  const draftAddonKey = useMemo(
    () =>
      addonLines
        .map((line) => `${line.addonId}:${line.amount}`)
        .sort()
        .join('|'),
    [addonLines],
  )

  const isAddonDirty = savedAddonKey !== draftAddonKey

  const addonLineErrors = useMemo(
    () =>
      addonLines.flatMap((line) => {
        const message = validateAddonLineAmount(line.amount)
        return message ? [message] : []
      }),
    [addonLines],
  )

  const selectionBlocksSave = selections.length > 0 && !isQuotaComplete

  const isSaving = replaceMenu.isPending || replaceAddons.isPending
  const canSave =
    menu !== null && !isLocked && !isSaving && !selectionBlocksSave && addonLineErrors.length === 0

  const handleAddDish = useCallback((courseId: number, menuId: string) => {
    setSaveNotice(null)
    setSelections((current) => {
      if (
        current.some(
          (selection) => selection.courseId === courseId && selection.menuId === menuId,
        )
      ) {
        return current
      }
      return [...current, { courseId, menuId, prepInstruction: '' }]
    })
  }, [])

  const handleRemoveDish = useCallback((courseId: number, menuId: string) => {
    setSaveNotice(null)
    setSelections((current) =>
      current.filter(
        (selection) =>
          !(selection.courseId === courseId && selection.menuId === menuId),
      ),
    )
  }, [])

  const handlePrepInstructionChange = useCallback(
    (courseId: number, menuId: string, value: string) => {
      setSaveNotice(null)
      setSelections((current) =>
        current.map((selection) =>
          selection.courseId === courseId && selection.menuId === menuId
            ? { ...selection, prepInstruction: value }
            : selection,
        ),
      )
    },
    [],
  )

  const handleReset = useCallback(() => {
    setSaveNotice(null)
    setSaveErrors([])
    setSelections([])
  }, [])

  const handleToggleAddon = useCallback(
    (addonId: number, isSelected: boolean) => {
      setSaveNotice(null)
      setAddonLines((current) => {
        if (!isSelected) {
          return current.filter((line) => line.addonId !== addonId)
        }
        if (current.some((line) => line.addonId === addonId)) return current
        const item = (addonMasterQuery.data ?? []).find(
          (entry) => entry.addonId === addonId,
        )
        const frozen = (addonSet?.addons ?? []).find((line) => line.addonId === addonId)
        return [
          ...current,
          { addonId, amount: String(item?.lineAmountForBooking ?? frozen?.amount ?? 0) },
        ]
      })
    },
    [addonMasterQuery.data, addonSet],
  )

  const handleAddonAmountChange = useCallback((addonId: number, value: string) => {
    setSaveNotice(null)
    setAddonLines((current) =>
      current.map((line) => (line.addonId === addonId ? { ...line, amount: value } : line)),
    )
  }, [])

  const handleSave = useCallback(async () => {
    if (menu === null || transactionId === null) return

    setSaveErrors([])
    setSaveNotice(null)

    if (selections.length > 0 && !isQuotaComplete) {
      setSaveErrors([
        'Every course must hold exactly its quota before the dish selection can be saved.',
      ])
      return
    }

    if (addonLineErrors.length > 0) {
      setSaveErrors(addonLineErrors)
      return
    }

    const addons: BookingAddonRequestItem[] = addonLines.map((line) => {
      const amount = parseOptionalNumber(line.amount)
      return amount === null ? { addonId: line.addonId } : { addonId: line.addonId, amount }
    })

    let savedMenu
    try {
      savedMenu = await replaceMenu.mutateAsync({
        selections: selections.map((selection) => ({
          courseId: selection.courseId,
          menuId: selection.menuId,
          prepInstruction:
            selection.prepInstruction.trim().length === 0
              ? null
              : selection.prepInstruction.trim(),
        })),
      })
    } catch (error) {
      setSaveErrors(readErrorMessages(error))
      return
    }

    try {
      const savedAddons = await replaceAddons.mutateAsync({ addons })

      /*
       * The total contract value is the **server's** (DEC-002, DEC-152). The
       * add-on replace re-freezes the money, so the pre-save details are
       * stale — a fresh details read is issued and the confirmation quotes
       * the **returned** figure, never a client-summed estimate. The single
       * round trip (`readBookingDetailsIntoCache`) cannot resolve with a
       * previous cycle's stale failure, and a throw here cannot be answered
       * with the cached pre-save `amountDue`.
       */
      let freshDetails: BookingDetails | null
      try {
        freshDetails = await readBookingDetailsIntoCache(transactionId, queryClient)
      } catch {
        freshDetails = null
        // A failed read often keeps the cached pre-save snapshot in the
        // query — it must never be presented as the recalculated figure
        // (DEC-002). The write itself succeeded; only the refresh failed.
        setIsDetailsRefreshFailed(true)
      }

      const notice = [
        savedMenu.isCompliant
          ? 'The selection was saved and the server reports it as compliant.'
          : `The selection was saved, but the server reports a breach: ${
              savedMenu.complianceMessage ?? 'the selection is not compliant.'
            }`,
        `Add-on subtotal ${formatCurrency(savedAddons.addonsTotal)}.`,
      ]
      if (freshDetails !== null) {
        setIsDetailsRefreshFailed(false)
        notice.push(
          `The server recalculated the total contract value on save: ${formatCurrency(
            freshDetails.amountDue,
          )}.`,
        )
      } else {
        notice.push(
          'The add-on subtotal was saved, but the recalculated contract value could not be read.',
        )
      }
      setSaveNotice(notice.join(' '))
    } catch (error) {
      setSaveErrors([
        'The dish selection was saved, but the add-ons were refused.',
        ...readErrorMessages(error),
      ])
    }
  }, [
    menu,
    transactionId,
    selections,
    isQuotaComplete,
    addonLineErrors,
    addonLines,
    replaceMenu,
    replaceAddons,
    queryClient,
  ])

  const handleDownloadBeo = useCallback(() => {
    if (transactionId === null) return
    setSaveErrors([])
    beoDownload.mutate(transactionId, {
      onError: (error) => setSaveErrors(readErrorMessages(error)),
    })
  }, [beoDownload, transactionId])

  /**
   * Re-reads the booking and clears the stale-total warning **only** if the
   * read itself succeeded; a failed attempt keeps the warning and never
   * quotes the cached pre-save figure (DEC-002).
   *
   * The refresh read is deliberately a single round trip through the
   * existing endpoint (`readBookingDetailsIntoCache`), so a wedged retry
   * cycle can never hand an older failure's answer to a fresh user retry.
   */
  const handleRetryDetailsRefresh = useCallback(async () => {
    try {
      const refreshed = await readBookingDetailsIntoCache(transactionId ?? 0, queryClient)
      setIsDetailsRefreshFailed(false)
      setSaveNotice((current) =>
        current?.replace(
          'The add-on subtotal was saved, but the recalculated contract value could not be read.',
          `The server recalculated the total contract value after refresh: ${formatCurrency(
            refreshed.amountDue,
          )}.`,
        ) ?? null,
      )
    } catch {
      setIsDetailsRefreshFailed(true)
    }
  }, [queryClient, transactionId])

  const readErrors = useMemo(() => {
    const messages = [
      ...(detailsQuery.error ? readErrorMessages(detailsQuery.error) : []),
      ...(addonSetQuery.error ? readErrorMessages(addonSetQuery.error) : []),
    ]
    return Array.from(new Set(messages))
  }, [detailsQuery.error, addonSetQuery.error])

  if (transactionId === null) {
    return (
      <AlertBanner
        title="No booking is selected"
        errors={['Open the menu editor from a booking in the register.']}
      />
    )
  }

  if (menuQuery.isPending) {
    return (
      <div className="flex flex-col gap-space-xl">
        <BookingMenuHeader
          bookingNumber={null}
          onReset={handleReset}
          onSave={() => undefined}
          isSaving={false}
          canSave={false}
          isLocked={false}
        />
        <p role="status" className="font-body-sm text-body-sm text-espresso-600">
          Reading the booking&apos;s menu selection…
        </p>
      </div>
    )
  }

  if (menuQuery.error || menu === null) {
    return (
      <div className="flex flex-col gap-space-xl">
        <AlertBanner
          title="The menu selection could not be loaded"
          errors={
            menuQuery.error
              ? readErrorMessages(menuQuery.error)
              : ['The booking published no selection.']
          }
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => void menuQuery.refetch()}
            >
              Retry
            </Button>
          }
        />
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-space-xl">
      <BookingMenuHeader
        bookingNumber={menu.bookingNumber}
        onReset={handleReset}
        onSave={() => void handleSave()}
        isSaving={isSaving}
        canSave={canSave}
        isLocked={isLocked}
      />

      {isLocked ? (
        <AlertBanner
          variant="warning"
          title="This menu is locked"
          errors={[
            `The menu-change window closed on ${formatEventDate(
              menu.menuSelectionLockDate,
            )}. The selection and the add-ons are read-only.`,
          ]}
        />
      ) : null}

      {selectionBlocksSave ? (
        <AlertBanner
          variant="warning"
          title="The selection does not satisfy the tier's quota"
          errors={[
            'Every course must hold exactly its quota, and no dish may come from a course the tier holds no rule for. The save stays disabled until it does.',
          ]}
        />
      ) : null}

      {readErrors.length > 0 ? (
        <AlertBanner
          title="Part of the booking could not be read"
          errors={readErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => {
                if (detailsQuery.error) void detailsQuery.refetch()
                if (addonSetQuery.error) void addonSetQuery.refetch()
              }}
            >
              Retry
            </Button>
          }
        />
      ) : null}

      {saveErrors.length > 0 ? (
        <AlertBanner
          title="The selection could not be saved"
          errors={saveErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setSaveErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {saveNotice ? (
        <AlertBanner
          variant="info"
          title="The selection was saved"
          errors={[saveNotice]}
          action={
            <Button variant="secondary" size="sm" onClick={() => setSaveNotice(null)}>
              Dismiss
            </Button>
          }
        />
      ) : null}

      {isDetailsRefreshFailed ? (
        <AlertBanner
          variant="warning"
          title="The recalculated contract value could not be loaded"
          errors={[
            'The add-on write succeeded, but reading the recalculated booking failed. The ledger below still shows the amount due before this save.',
          ]}
          action={
            <Button
              variant="secondary"
              size="sm"
              onClick={() => void handleRetryDetailsRefresh()}
            >
              Retry refresh
            </Button>
          }
        />
      ) : null}

      <BookingMenuEventBanner
        menu={menu}
        details={details}
        quota={{
          selected: selectedTotal,
          required: requiredTotal,
          mode: quotaMode,
        }}
      />

      <div
        aria-disabled={isLocked || undefined}
        className="grid grid-cols-1 lg:grid-cols-12 gap-gutter items-start"
      >
        {/* The course selection list — two thirds. */}
        <div className="lg:col-span-8 flex flex-col gap-space-md min-w-0">
          <BookingMenuFilterPanel
            search={search}
            onSearchChange={setSearch}
            lockDate={menu.menuSelectionLockDate}
            pax={pax}
            poolSize={poolSize}
            matchedCount={matchedCount}
            isLocked={isLocked}
          />

          <BookingMenuCourseTabs
            courses={courses}
            activeCourseId={activeCourseId}
            onChange={setActiveCourseId}
          />

          {selections.length === 0 && courses.length > 0 ? (
            <p className="font-body-sm text-body-sm text-espresso-600 rounded-lg bg-surface-container-low border border-artisan-border p-space-sm">
              This event is currently served the tier&apos;s standard run. Choose the dishes to
              override it.
            </p>
          ) : null}

          <BookingMenuCourseBoard
            courses={courses}
            activeCourseId={activeCourseId}
            search={search}
            selections={selections}
            isLocked={isLocked}
            onAddDish={handleAddDish}
            onRemoveDish={handleRemoveDish}
            onPrepInstructionChange={handlePrepInstructionChange}
          />
        </div>

        {/* The add-ons, the ledger and the batch yield — one third. */}
        <div className="lg:col-span-4 flex flex-col gap-space-md min-w-0">
          <BookingAddonsPanel
            master={addonMasterQuery.data ?? []}
            savedLines={addonSet?.addons ?? []}
            lines={addonLines}
            savedAddonsTotal={addonSet?.addonsTotal ?? menu.addonsTotal}
            isDirty={isAddonDirty}
            isLoading={addonMasterQuery.isPending}
            isCategoryLoading={addonCategoriesQuery.isPending}
            categoryErrors={
              addonCategoriesQuery.error ? readErrorMessages(addonCategoriesQuery.error) : []
            }
            masterErrors={
              addonMasterQuery.error ? readErrorMessages(addonMasterQuery.error) : []
            }
            activeCategoryId={activeCategoryId}
            categories={addonCategoriesQuery.data ?? []}
            isPickerOpen={isPickerOpen}
            isLocked={isLocked}
            onOpenPicker={() => {
              setSaveNotice(null)
              setIsPickerOpen(true)
            }}
            onClosePicker={closePicker}
            onCategoryChange={setActiveCategoryId}
            onCategoryRetry={() => void addonCategoriesQuery.refetch()}
            onMasterRetry={() => void addonMasterQuery.refetch()}
            onToggle={handleToggleAddon}
            onAmountChange={handleAddonAmountChange}
          />

          <BookingContractLedger details={details} menu={menu} />

          <BookingBatchYieldPanel
            courses={courses}
            onDownloadBeo={handleDownloadBeo}
            isDownloading={beoDownload.isPending}
          />
        </div>
      </div>
    </div>
  )
}
