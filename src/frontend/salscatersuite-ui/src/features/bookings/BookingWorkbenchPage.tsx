import { useCallback, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ArrowLeft, RefreshCw } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import type { BookingQuoteRequest, CreateBookingRequest } from '@/types/booking.types'
import { WorkbenchClientRail } from './components/WorkbenchClientRail'
import { WorkbenchHeader } from './components/WorkbenchHeader'
import { WorkbenchLedgerRail } from './components/WorkbenchLedgerRail'
import { WorkbenchLogisticsBlock } from './components/WorkbenchLogisticsBlock'
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
import {
  emptyBookingWorkbenchDraft,
  parseOptionalNumber,
  toWireTime,
  validateBookingWorkbenchDraft,
} from './bookingVocabulary'
import type { BookingWorkbenchDraft } from './bookingVocabulary'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
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
 * The screen renders **inside** the authenticated shell, so the navigation,
 * the session and the route guard are identical on every screen (DEC-110).
 */
export function BookingWorkbenchPage() {
  const navigate = useNavigate()

  const [draft, setDraft] = useState<BookingWorkbenchDraft>(emptyBookingWorkbenchDraft)
  const [clientSearch, setClientSearch] = useState('')
  const [serverErrors, setServerErrors] = useState<string[]>([])
  const [saveNotice, setSaveNotice] = useState<string | null>(null)
  const [createdBookingNumber, setCreatedBookingNumber] = useState<string | null>(null)

  const tiersQuery = useBookingTierCatalogue()
  const municipalitiesQuery = useBookingMunicipalityCatalogue()
  const clientsQuery = useBookingClientCatalogue(clientSearch)
  const createBooking = useCreateBooking()

  const handleDraftChange = useCallback((patch: Partial<BookingWorkbenchDraft>) => {
    setDraft((current) => ({ ...current, ...patch }))
  }, [])

  const packageId = useMemo(() => {
    const parsed = parseOptionalNumber(draft.packageId)
    return parsed !== null && Number.isInteger(parsed) && parsed > 0 ? parsed : null
  }, [draft.packageId])

  const rulesetQuery = useBookingCourseRuleset(packageId)

  /*
   * The quote is requested once the booking holds everything the server needs
   * to price it. Until then the rail shows its empty state rather than a
   * request that would only be refused.
   */
  const quoteRequest = useMemo<BookingQuoteRequest | null>(() => {
    const customerId = parseOptionalNumber(draft.customerId)
    const pax = parseOptionalNumber(draft.noOfPerson)
    const areaId = parseOptionalNumber(draft.extendedAreaId)

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
      extendedAreaId: areaId !== null && Number.isInteger(areaId) && areaId > 0 ? areaId : null,
      applyExtendedAmount: draft.applyExtendedAmount,
      discountCode: draft.discountCode.trim().length === 0 ? null : draft.discountCode.trim(),
      depositAmount: deposit,
      initialPaymentAmount: initialPayment,
    }
  }, [draft, packageId])

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

  const canSave =
    currentStep >= 2 &&
    Object.keys(errors).filter((key) => key !== 'initialPaymentAmount').length === 0

  const canConfirm =
    quoteQuery.data !== undefined &&
    quoteErrors.length === 0 &&
    quoteQuery.data.isTierActive &&
    Object.keys(errors).length === 0

  /** Builds the create request from the draft. */
  const buildRequest = useCallback(
    (confirm: boolean): CreateBookingRequest | null => {
      const customerId = parseOptionalNumber(draft.customerId)
      const pax = parseOptionalNumber(draft.noOfPerson)
      const areaId = parseOptionalNumber(draft.extendedAreaId)
      const deposit = parseOptionalNumber(draft.depositAmount)
      const initialPayment = parseOptionalNumber(draft.initialPaymentAmount)

      if (customerId === null || pax === null) return null

      const startTime = toWireTime(draft.startTime)
      const endTime = toWireTime(draft.endTime)
      if (startTime === null || endTime === null) return null

      const request: CreateBookingRequest = {
        customerId,
        bookType: draft.bookType,
        typeOfService: draft.typeOfService,
        noOfPerson: Math.trunc(pax),
        occasion: draft.occasion.trim(),
        venue: draft.venue.trim(),
        startDate: `${draft.eventDate}T${startTime}Z`,
        endDate: `${draft.eventDate}T${endTime}Z`,
        eventColor: null,
        packageId,
        reference: null,
        extendedAreaId: areaId !== null && Number.isInteger(areaId) && areaId > 0 ? areaId : null,
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
    [draft, packageId],
  )

  const handleSave = useCallback(
    (confirm: boolean) => {
      setServerErrors([])
      setSaveNotice(null)

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
      if (Object.keys(blocking).length > 0) {
        setServerErrors(
          Object.values(blocking).filter((message): message is string => Boolean(message)),
        )
        return
      }

      const request = buildRequest(confirm)
      if (request === null) {
        setServerErrors(['The booking could not be read. Check the client, the tier and the times.'])
        return
      }

      createBooking.mutate(request, {
        onSuccess: (response) => {
          setCreatedBookingNumber(response.bookingNumber)
          setSaveNotice(
            confirm
              ? `The booking ${response.bookingNumber} was confirmed in the ${response.status} status.`
              : `The draft ${response.bookingNumber} was saved in the ${response.status} status.`,
          )
        },
        onError: (error) => setServerErrors(readErrorMessages(error)),
      })
    },
    [buildRequest, createBooking, draft],
  )

  const handleBack = useCallback(() => navigate('/bookings'), [navigate])

  return (
    <div className="flex flex-col gap-space-xl">
      <WorkbenchHeader
        bookingNumber={createdBookingNumber}
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

      {saveNotice ? (
        <AlertBanner
          title="The booking was saved"
          errors={[saveNotice]}
          variant="info"
          action={
            <Button variant="secondary" size="sm" icon={ArrowLeft} onClick={handleBack}>
              Back to the register
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
          />
        </div>

        {/* Column 2 — the package tier & course engine. */}
        <div className="lg:col-span-6 flex flex-col gap-space-md min-w-0">
          <WorkbenchTierEngine
            draft={draft}
            errors={errors}
            onChange={handleDraftChange}
            tiers={tiersQuery.data ?? []}
            isLoadingTiers={tiersQuery.isPending}
            ruleset={rulesetQuery.data ?? null}
            isLoadingRuleset={rulesetQuery.isPending}
            quote={quoteQuery.data ?? null}
            isRecalculating={quoteQuery.isRecalculating}
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
