import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import type {
  BookingAddonRequestItem,
  BookingMenuSelectionRequestItem,
  BookingQuoteRequest,
  CreateBookingRequest,
} from '@/types/booking.types'
import { WorkbenchAddonPicker } from './components/WorkbenchAddonPicker'
import { WorkbenchClientRail } from './components/WorkbenchClientRail'
import { WorkbenchHeader } from './components/WorkbenchHeader'
import { WorkbenchLedgerRail } from './components/WorkbenchLedgerRail'
import { WorkbenchLogisticsBlock } from './components/WorkbenchLogisticsBlock'
import { WorkbenchMenuDraft } from './components/WorkbenchMenuDraft'
import { WorkbenchScheduleMatrix } from './components/WorkbenchScheduleMatrix'
import { WorkbenchTierEngine } from './components/WorkbenchTierEngine'
import {
  useBookingClientCatalogue,
  useBookingCourseRuleset,
  useBookingMunicipalityCatalogue,
  useBookingQuote,
  useBookingTierCatalogue,
  useCreateBooking,
} from './hooks/useBookingWorkbench'
import { useAddonMaster } from './hooks/useBookingMenu'
import {
  emptyBookingWorkbenchDraft,
  isMenuSelectionComplete,
  parseOptionalNumber,
  supportsExtendedArea,
  toWireTime,
  validateAddonLineAmount,
  validateBookingWorkbenchDraft,
} from './bookingVocabulary'
import type { BookingAddonDraftLine, BookingWorkbenchDraft } from './bookingVocabulary'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

/** Reads a positive integer identifier out of a form field. */
function parseIdentifier(value: string): number | null {
  const parsed = parseOptionalNumber(value)
  return parsed !== null && Number.isInteger(parsed) && parsed > 0 ? parsed : null
}

/**
 * The Reservation Workbench — Screen B (spec §5.3).
 *
 * The screen composes a booking: the client & logistics rail, the package tier
 * & course engine and the live financial & seal rail. It saves a **draft** or
 * **confirms** the booking.
 *
 * **The workbench is read-only on the money.** Every peso, every percentage
 * and every count on the rail comes from `POST /api/bookings/quote` — the
 * server prices the booking and the browser displays the answer (DEC-002,
 * BR-31).
 *
 * Column 2 carries the **dish picker** and the **add-on picker**; the two
 * payloads travel on the quote and on the create, so the quoted total and the
 * created booking cannot diverge (AC-BKG-64, AC-BKG-85).
 *
 * On success the screen **redirects to the menu editor** of the booking it
 * created (AC-BKG-75) — the selection is continued there.
 *
 * The screen renders **inside** the authenticated shell, so the navigation,
 * the session and the route guard are identical on every screen (DEC-110).
 */
export function BookingWorkbenchPage() {
  const navigate = useNavigate()

  const [draft, setDraft] = useState<BookingWorkbenchDraft>(emptyBookingWorkbenchDraft)
  const [clientSearch, setClientSearch] = useState('')
  const [serverErrors, setServerErrors] = useState<string[]>([])

  /*
   * The whole catalogue feeds the **package-type** filter, and the
   * area-filtered catalogue feeds the **tier** picker. The server answers the
   * area filter through `areaId` (DEC-157, AC-BKG-86).
   */
  const catalogueQuery = useBookingTierCatalogue()
  const areaFilterId = parseIdentifier(draft.areaFilterId)
  const tiersQuery = useBookingTierCatalogue(areaFilterId)
  const municipalitiesQuery = useBookingMunicipalityCatalogue()
  const clientsQuery = useBookingClientCatalogue(clientSearch)
  const createBooking = useCreateBooking()

  const handleDraftChange = useCallback((patch: Partial<BookingWorkbenchDraft>) => {
    setDraft((current) => ({ ...current, ...patch }))
  }, [])

  /** The distinct stored types the type filter offers (M7, DEC-070). */
  const packageTypes = useMemo(() => {
    const types = (catalogueQuery.data ?? [])
      .map((tier) => tier.packageType.trim())
      .filter((type) => type.length > 0)
    return Array.from(new Set(types)).sort((left, right) => left.localeCompare(right))
  }, [catalogueQuery.data])

  /*
   * The type filter is applied to the server's list: the type is free text and
   * the list is unpaged, so no request carries it.
   */
  const filteredTiers = useMemo(() => {
    const tiers = tiersQuery.data ?? []
    if (draft.packageType.length === 0) return tiers
    return tiers.filter((tier) => tier.packageType === draft.packageType)
  }, [tiersQuery.data, draft.packageType])

  const selectedTier = useMemo(
    () =>
      (catalogueQuery.data ?? []).find(
        (tier) => String(tier.packageId) === draft.packageId,
      ) ?? null,
    [catalogueQuery.data, draft.packageId],
  )

  /*
   * A tier the current filters no longer offer cannot stay selected: the
   * operator would be composing against a tier the list has hidden.
   */
  useEffect(() => {
    if (!tiersQuery.data || draft.packageId.length === 0) return
    const stillOffered = filteredTiers.some(
      (tier) => String(tier.packageId) === draft.packageId,
    )
    if (!stillOffered) {
      setDraft((current) => ({ ...current, packageId: '' }))
    }
  }, [tiersQuery.data, filteredTiers, draft.packageId])

  /*
   * A tier whose type cannot carry the out-of-town amount must not carry a
   * municipality or the surcharge switch — the create would be refused with a
   * `400` (DEC-141, AC-BKG-72).
   */
  useEffect(() => {
    if (selectedTier === null || supportsExtendedArea(selectedTier.packageType)) return
    if (draft.extendedAreaId.length === 0 && !draft.applyExtendedAmount) return
    setDraft((current) => ({
      ...current,
      extendedAreaId: '',
      applyExtendedAmount: false,
    }))
  }, [selectedTier, draft.extendedAreaId, draft.applyExtendedAmount])

  const packageId = parseIdentifier(draft.packageId)
  const pax = parseIdentifier(draft.noOfPerson)

  const rulesetQuery = useBookingCourseRuleset(packageId)
  const ruleset = rulesetQuery.data ?? null
  const rules = ruleset?.rules ?? []

  const addonMasterQuery = useAddonMaster(pax)

  /** Whether the draft selection satisfies every course quota (BR-15). */
  const selectionComplete = useMemo(
    () => isMenuSelectionComplete(rules, draft.menuSelections),
    [rules, draft.menuSelections],
  )

  /*
   * The quote carries the selection only when it is quota-complete: an
   * incomplete selection is a `400` by design, and a refused quote would leave
   * the rail with no ledger. An empty selection is the standard run and travels
   * as an absent member (DEC-131, AC-BKG-59).
   */
  const selectionPayload = useMemo<BookingMenuSelectionRequestItem[] | undefined>(() => {
    if (!selectionComplete || draft.menuSelections.length === 0) return undefined
    return draft.menuSelections.map((selection) => ({
      courseId: selection.courseId,
      menuId: selection.menuId,
      prepInstruction:
        selection.prepInstruction.trim().length === 0
          ? null
          : selection.prepInstruction.trim(),
    }))
  }, [draft.menuSelections, selectionComplete])

  /** Every add-on line whose stated total can be sent. */
  const addonPayload = useMemo<BookingAddonRequestItem[]>(() => {
    return draft.addonLines.flatMap((line) => {
      const amount = parseOptionalNumber(line.amount)
      if (amount === null || amount < 0) return []
      return [{ addonId: line.addonId, amount }]
    })
  }, [draft.addonLines])

  /** The per-line messages of the add-on editor. */
  const addonLineErrors = useMemo(
    () =>
      draft.addonLines.flatMap((line) => {
        const message = validateAddonLineAmount(line.amount)
        return message ? [message] : []
      }),
    [draft.addonLines],
  )

  /*
   * The quote is requested once the booking holds everything the server needs
   * to price it. Until then the rail shows its empty state rather than a
   * request that would only be refused.
   */
  const quoteRequest = useMemo<BookingQuoteRequest | null>(() => {
    const customerId = parseOptionalNumber(draft.customerId)

    if (
      customerId === null ||
      packageId === null ||
      pax === null ||
      pax < 1 ||
      draft.occasion.trim().length === 0 ||
      draft.venue.trim().length === 0 ||
      draft.eventDate.trim().length === 0 ||
      draft.startTime.trim().length === 0 ||
      draft.endTime.trim().length === 0
    ) {
      return null
    }

    const deposit = parseOptionalNumber(draft.depositAmount)
    const initialPayment = parseOptionalNumber(draft.initialPaymentAmount)
    const areaId = parseIdentifier(draft.extendedAreaId)

    return {
      customerId,
      packageId,
      noOfPerson: Math.trunc(pax),
      occasion: draft.occasion.trim(),
      venue: draft.venue.trim(),
      eventDate: draft.eventDate,
      startTime: toWireTime(draft.startTime) ?? draft.startTime,
      endTime: toWireTime(draft.endTime) ?? draft.endTime,
      callTime: toWireTime(draft.callTime),
      packoutTime: toWireTime(draft.packoutTime),
      extendedAreaId: areaId,
      applyExtendedAmount: draft.applyExtendedAmount,
      discountCode: draft.discountCode.trim().length === 0 ? null : draft.discountCode.trim(),
      depositAmount: deposit,
      initialPaymentAmount: initialPayment,
      selections: selectionPayload,
      addons: addonPayload.length > 0 ? addonPayload : undefined,
    }
  }, [
    draft,
    packageId,
    pax,
    selectionPayload,
    addonPayload,
  ])

  const quoteQuery = useBookingQuote(quoteRequest)

  const quoteErrors = useMemo(() => {
    if (!quoteQuery.error) return []
    return readErrorMessages(quoteQuery.error)
  }, [quoteQuery.error])

  const errors = useMemo(() => validateBookingWorkbenchDraft(draft), [draft])

  /*
   * The step indicator is a **form affordance**: it names the first step
   * whose inputs are still incomplete. It carries no business figure.
   */
  const currentStep = useMemo(() => {
    const clientAndEventComplete =
      draft.customerId.trim().length > 0 &&
      draft.occasion.trim().length > 0 &&
      draft.venue.trim().length > 0 &&
      draft.eventDate.trim().length > 0 &&
      draft.startTime.trim().length > 0 &&
      draft.endTime.trim().length > 0
    if (!clientAndEventComplete) return 1

    const tierComplete = packageId !== null && draft.noOfPerson.trim().length > 0
    if (!tierComplete) return 2

    const financialsComplete = quoteQuery.data !== undefined && quoteErrors.length === 0
    if (!financialsComplete) return 3

    return 4
  }, [draft, packageId, quoteQuery.data, quoteErrors.length])

  const selectionBlocksSave = draft.menuSelections.length > 0 && !selectionComplete

  const canSave =
    currentStep >= 2 &&
    !selectionBlocksSave &&
    addonLineErrors.length === 0 &&
    Object.keys(errors).filter((key) => key !== 'initialPaymentAmount').length === 0

  const canConfirm =
    quoteQuery.data !== undefined &&
    quoteErrors.length === 0 &&
    quoteQuery.data.isTierActive &&
    !selectionBlocksSave &&
    addonLineErrors.length === 0 &&
    Object.keys(errors).length === 0

  /** Builds the create request from the draft. */
  const buildRequest = useCallback(
    (confirm: boolean): CreateBookingRequest | null => {
      const customerId = parseOptionalNumber(draft.customerId)
      const headcount = parseOptionalNumber(draft.noOfPerson)
      const deposit = parseOptionalNumber(draft.depositAmount)
      const initialPayment = parseOptionalNumber(draft.initialPaymentAmount)

      if (customerId === null || headcount === null) return null

      const startTime = toWireTime(draft.startTime)
      const endTime = toWireTime(draft.endTime)
      if (startTime === null || endTime === null) return null

      const request: CreateBookingRequest = {
        customerId,
        bookType: draft.bookType,
        typeOfService: draft.typeOfService,
        noOfPerson: Math.trunc(headcount),
        occasion: draft.occasion.trim(),
        venue: draft.venue.trim(),
        startDate: `${draft.eventDate}T${startTime}Z`,
        endDate: `${draft.eventDate}T${endTime}Z`,
        eventColor: null,
        packageId,
        reference: null,
        extendedAreaId: parseIdentifier(draft.extendedAreaId),
        applyExtendedAmount: draft.applyExtendedAmount,
        specialInstructions: null,
        overridePricePerHead: null,
        callTime: toWireTime(draft.callTime),
        packoutTime: toWireTime(draft.packoutTime),
        discountCode: draft.discountCode.trim().length === 0 ? null : draft.discountCode.trim(),
        depositAmount: deposit,
        confirm,
      }

      /*
       * The selection and the add-ons travel in the **same commit** as the
       * booking (AC-BKG-61). An absent member means none; an explicitly empty
       * array is refused, so an empty draft omits them (contract §15.3).
       */
      if (selectionPayload) {
        request.selections = selectionPayload
      }
      if (addonPayload.length > 0) {
        request.addons = addonPayload
      }

      /*
       * A draft never carries a payment, and a confirmed booking carries one
       * only when the operator stated an amount (spec §3.12).
       */
      if (confirm && initialPayment !== null && initialPayment > 0) {
        request.initialPayment = {
          amountPaid: initialPayment,
          paymentMeans: draft.paymentMeans,
          checkNo: draft.checkNo.trim().length === 0 ? null : draft.checkNo.trim(),
          notes: null,
          particular: draft.particular.trim().length === 0 ? null : draft.particular.trim(),
        }
      }

      return request
    },
    [draft, packageId, selectionPayload, addonPayload],
  )

  const handleSave = useCallback(
    (confirm: boolean) => {
      setServerErrors([])

      const problems = validateBookingWorkbenchDraft(draft)
      /*
       * A draft never carries a payment, so a stated-but-invalid initial
       * payment does not block it — the draft simply omits the payment
       * (spec §2, the server's own rule).
       */
      const blocking = { ...problems }
      if (!confirm) {
        delete blocking.initialPaymentAmount
      }

      const messages = Object.values(blocking).filter(
        (message): message is string => Boolean(message),
      )
      if (selectionBlocksSave) {
        messages.push(
          'Every course must hold exactly its quota before the dish selection can be saved.',
        )
      }
      messages.push(...addonLineErrors)

      if (messages.length > 0) {
        setServerErrors(messages)
        return
      }

      const request = buildRequest(confirm)
      if (request === null) {
        setServerErrors(['The booking could not be read. Check the client, the tier and the times.'])
        return
      }

      createBooking.mutate(request, {
        onSuccess: (response) => {
          /*
           * The owner's ruling: the create flow lands on the menu editor of
           * the booking it just created (AC-BKG-75).
           */
          navigate(`/bookings/${response.transactionId}/menu`)
        },
        onError: (error) => setServerErrors(readErrorMessages(error)),
      })
    },
    [buildRequest, createBooking, draft, navigate, selectionBlocksSave, addonLineErrors],
  )

  const handleAddDish = useCallback((courseId: number, menuId: string) => {
    setDraft((current) => {
      if (
        current.menuSelections.some(
          (selection) => selection.courseId === courseId && selection.menuId === menuId,
        )
      ) {
        return current
      }
      return {
        ...current,
        menuSelections: [
          ...current.menuSelections,
          { courseId, menuId, prepInstruction: '' },
        ],
      }
    })
  }, [])

  const handleRemoveDish = useCallback((courseId: number, menuId: string) => {
    setDraft((current) => ({
      ...current,
      menuSelections: current.menuSelections.filter(
        (selection) =>
          !(selection.courseId === courseId && selection.menuId === menuId),
      ),
    }))
  }, [])

  const handlePrepInstructionChange = useCallback(
    (courseId: number, menuId: string, value: string) => {
      setDraft((current) => ({
        ...current,
        menuSelections: current.menuSelections.map((selection) =>
          selection.courseId === courseId && selection.menuId === menuId
            ? { ...selection, prepInstruction: value }
            : selection,
        ),
      }))
    },
    [],
  )

  const handleResetMenuDraft = useCallback(() => {
    setDraft((current) => ({ ...current, menuSelections: [] }))
  }, [])

  const handleAddAddon = useCallback(
    (addonId: number) => {
      const item = (addonMasterQuery.data ?? []).find((entry) => entry.addonId === addonId)
      setDraft((current) => {
        if (current.addonLines.some((line) => line.addonId === addonId)) return current
        const line: BookingAddonDraftLine = {
          addonId,
          amount: item ? String(item.lineAmountForBooking) : '',
        }
        return { ...current, addonLines: [...current.addonLines, line] }
      })
    },
    [addonMasterQuery.data],
  )

  const handleRemoveAddon = useCallback((addonId: number) => {
    setDraft((current) => ({
      ...current,
      addonLines: current.addonLines.filter((line) => line.addonId !== addonId),
    }))
  }, [])

  const handleAddonAmountChange = useCallback((addonId: number, value: string) => {
    setDraft((current) => ({
      ...current,
      addonLines: current.addonLines.map((line) =>
        line.addonId === addonId ? { ...line, amount: value } : line,
      ),
    }))
  }, [])

  return (
    <div className="flex flex-col gap-space-xl">
      <WorkbenchHeader
        bookingNumber={null}
        currentStep={currentStep}
        onSaveDraft={() => handleSave(false)}
        onConfirm={() => handleSave(true)}
        isSaving={createBooking.isPending}
        canSave={canSave}
        canConfirm={canConfirm}
      />

      {serverErrors.length > 0 ? (
        <AlertBanner
          title="The booking was rejected"
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

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter items-start">
        {/* Column 1 — the client & logistics rail. */}
        <div className="lg:col-span-3 flex flex-col gap-space-md min-w-0">
          <WorkbenchClientRail
            draft={draft}
            errors={errors}
            onChange={handleDraftChange}
            clientSearch={clientSearch}
            onClientSearchChange={setClientSearch}
            clients={clientsQuery.data?.items ?? []}
            isLoadingClients={clientsQuery.isPending}
            clientError={
              clientsQuery.error ? readErrorMessages(clientsQuery.error) : []
            }
          />

          <WorkbenchScheduleMatrix
            draft={draft}
            errors={errors}
            onChange={handleDraftChange}
          />

          <WorkbenchLogisticsBlock
            draft={draft}
            errors={errors}
            onChange={handleDraftChange}
            municipalities={municipalitiesQuery.data ?? []}
            quote={quoteQuery.data ?? null}
            selectedTierType={selectedTier?.packageType ?? null}
          />
        </div>

        {/* Column 2 — the package tier, the dish picker and the add-ons. */}
        <div className="lg:col-span-6 flex flex-col gap-space-md min-w-0">
          <WorkbenchTierEngine
            draft={draft}
            errors={errors}
            onChange={handleDraftChange}
            tiers={filteredTiers}
            isLoadingTiers={tiersQuery.isPending}
            packageTypes={packageTypes}
            isTypeFiltered={draft.packageType.length > 0}
            ruleset={ruleset}
            isLoadingRuleset={rulesetQuery.isPending}
            quote={quoteQuery.data ?? null}
            isRecalculating={quoteQuery.isRecalculating}
          />

          <WorkbenchMenuDraft
            rules={rules}
            selections={draft.menuSelections}
            onAddDish={handleAddDish}
            onRemoveDish={handleRemoveDish}
            onPrepInstructionChange={handlePrepInstructionChange}
            onReset={handleResetMenuDraft}
          />

          <WorkbenchAddonPicker
            master={addonMasterQuery.data ?? []}
            lines={draft.addonLines}
            onAdd={handleAddAddon}
            onRemove={handleRemoveAddon}
            onAmountChange={handleAddonAmountChange}
            isLoading={pax !== null && addonMasterQuery.isPending}
          />
        </div>

        {/* Column 3 — the financial & seal rail. */}
        <div className="lg:col-span-3 flex flex-col gap-space-md min-w-0">
          <WorkbenchLedgerRail
            draft={draft}
            errors={errors}
            onChange={handleDraftChange}
            quote={quoteQuery.data ?? null}
            isRecalculating={quoteQuery.isRecalculating}
            quoteErrors={quoteErrors}
            onSaveDraft={() => handleSave(false)}
            onConfirm={() => handleSave(true)}
            isSaving={createBooking.isPending}
            canSave={canSave}
            canConfirm={canConfirm}
          />
        </div>
      </div>
    </div>
  )
}
