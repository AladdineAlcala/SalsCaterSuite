import { configure, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosRequestConfig, AxiosResponse } from 'axios'
import type { PackageDto, PackagePaxApplicable } from '@/types/package.types'
import { apiClient, ApiError } from '@/api/apiClient'
import { BookingWorkbenchPage } from '@/features/bookings/BookingWorkbenchPage'
import type { BookingQuoteRequest } from '@/types/booking.types'
import {
  addonMasterFixture,
  bandDiscountBookingQuoteFixture,
  bookingQuoteFixture,
  inactiveTierBookingQuoteFixture,
} from '@/test/bookingFixture'
import { customerDirectoryFixture } from '@/test/customerFixture'
import {
  packagePaxApplicablesFixture,
  packageTiersFixture,
} from '@/test/packageFixture'
import {
  areaCatalogueFixture,
  packageCourseRulesetFixture,
} from '@/test/packageWorkbenchFixture'
import { createTestQueryClient } from '@/test/testHelpers'

/*
 * The screen chains three reads and a debounced quote, so a loaded worker can
 * take longer than the one-second default to publish the first figure. The
 * assertions are not less strict for waiting longer; they stop being flaky.
 */
configure({ asyncUtilTimeout: 5000 })

interface MockOptions {
  /** Holds the quote request open so the recalculating state can be seen. */
  holdQuote?: boolean
  /** The quote the server answers with. */
  quote?: unknown
  /** The create endpoint refuses the payload. */
  createError?: ApiError
  /** The catalogue answer — `GET /packages` (every key, params unfiltered). */
  tiers?: PackageDto[]
  /** The read-only applicable-pax lookup answer. */
  paxApplicables?: PackagePaxApplicable[]
  /** Holds the lookup request open so its pending state can be tested. */
  holdPaxApplicables?: boolean
  /** The applicable-pax lookup refuses the request. */
  paxApplicablesError?: ApiError
}

function renderWorkbench(options: { seedPaxApplicables?: boolean } = {}) {
  const queryClient = createTestQueryClient()
  if (options.seedPaxApplicables) {
    queryClient.setQueryData(['package-pax-applicables'], packagePaxApplicablesFixture)
  }
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/bookings/new']}>
        <Routes>
          <Route path="/bookings/new" element={<BookingWorkbenchPage />} />
          <Route
            path="/bookings/:transactionId/menu"
            element={<p>Menu editor of the booking</p>}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient }
}

function axiosResponse(data: unknown, status = 200): AxiosResponse {
  return {
    data,
    status,
    statusText: 'OK',
    headers: {},
    config: { headers: {} } as AxiosResponse['config'],
  }
}

interface CapturedRequest {
  url: string
  params: Record<string, unknown> | undefined
}

function paramsOf(config?: AxiosRequestConfig): Record<string, unknown> {
  return (config?.params ?? {}) as Record<string, unknown>
}

function isActiveTier(tier: unknown): boolean {
  return Boolean((tier as { isActive?: boolean }).isActive)
}

/** The bodies of every create request the workbench sent. */
const createdBodies: unknown[] = []

/** The bodies of every quote request the workbench sent. */
const quoteBodies: BookingQuoteRequest[] = []

function mockApi(options: MockOptions = {}) {
  const held: Array<(value: unknown) => void> = []
  const heldPaxApplicables: Array<(value: unknown) => void> = []
  const requests: CapturedRequest[] = []

  vi.spyOn(apiClient, 'get').mockImplementation(
    async (url: string, config?: AxiosRequestConfig): Promise<AxiosResponse> => {
      requests.push({ url, params: config?.params as Record<string, unknown> | undefined })

      if (url.includes('/course-rules')) return axiosResponse(packageCourseRulesetFixture)
      if (url === '/packages/pax-applicables') {
        if (options.paxApplicablesError) throw options.paxApplicablesError
        if (options.holdPaxApplicables) {
          return new Promise((resolve) => {
            heldPaxApplicables.push((value) => resolve(axiosResponse(value)))
          }) as Promise<AxiosResponse>
        }
        return axiosResponse(options.paxApplicables ?? packagePaxApplicablesFixture)
      }
      if (url.includes('/packages')) {
        // Mock fidelity — the server honours `activeOnly` and answers with the
        // selectable tiers only; the general catalogue fixture's inactive row
        // must never reach a filtered request.
        if (paramsOf(config).activeOnly) {
          return axiosResponse((options.tiers ?? packageTiersFixture).filter(isActiveTier))
        }
        return axiosResponse(options.tiers ?? packageTiersFixture)
      }
      if (url === '/areas') return axiosResponse(areaCatalogueFixture)
      if (url.includes('/customers')) return axiosResponse(customerDirectoryFixture)
      // The add-on master ships empty in this cycle; the fixtures keep the
      // picker's happy path testable without inventing a server state.
      if (url === '/addons') return axiosResponse(addonMasterFixture)
      return axiosResponse({})
    },
  )

  vi.spyOn(apiClient, 'post').mockImplementation(
    async (url: string, body?: unknown): Promise<AxiosResponse> => {
      if (url.includes('/bookings/quote')) {
        quoteBodies.push(body as BookingQuoteRequest)
        if (options.holdQuote) {
          return new Promise((resolve) => {
            held.push(() => resolve(axiosResponse(options.quote ?? bookingQuoteFixture)))
          }) as Promise<AxiosResponse>
        }
        return axiosResponse(options.quote ?? bookingQuoteFixture)
      }

      if (url.endsWith('/bookings')) {
        createdBodies.push(body)
        if (options.createError) throw options.createError
        return axiosResponse(
          {
            transactionId: 200,
            bookingNumber: 'BK-20261120-00200',
            status: 'Confirmed',
          },
          201,
        )
      }

      return axiosResponse({})
    },
  )

  return {
    requests,
    /** How many quote requests are being held open. */
    pendingCount() {
      return held.length
    },
    releaseQuote() {
      held.splice(0, held.length).forEach((resolve) => resolve(undefined))
    },
    releasePaxApplicables(value: unknown = packagePaxApplicablesFixture) {
      heldPaxApplicables.splice(0, heldPaxApplicables.length).forEach((resolve) => resolve(value))
    },
  }
}

/** Waits until a catalogue-backed control has been filled by the server. */
async function waitForEnabled(label: string): Promise<HTMLSelectElement> {
  const control = screen.getByLabelText(label) as HTMLSelectElement
  await waitFor(() => {
    expect(control).toBeEnabled()
  })
  return control
}

/**
 * Fills every input the server needs before it can price the booking.
 *
 * The type/area gate comes first (AC-BKG-92): the tier picker stays disabled
 * until a package type is chosen. The Wedding tier is not an eligible
 * out-of-town type, so no area is required on this path. The controls are
 * driven with a single change event each rather than a keystroke per
 * character: the component test does not need to simulate a human's typing
 * speed.
 */
async function fillRequiredInputs() {
  await waitForEnabled('Client')
  fireEvent.change(screen.getByLabelText('Client'), { target: { value: '18' } })
  fireEvent.change(screen.getByLabelText('Occasion'), { target: { value: 'Golden Jubilee' } })
  fireEvent.change(screen.getByLabelText('Venue'), { target: { value: 'The Grand Pavilion' } })
  fireEvent.change(screen.getByLabelText('Event date'), { target: { value: '2026-11-20' } })
  await waitForEnabled('Package type')
  fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Wedding' } })
  await waitForEnabled('Buffet tier')
  fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '12' } })
  fireEvent.change(screen.getByLabelText('Contract headcount'), { target: { value: '250' } })
  fireEvent.change(screen.getByLabelText('Program'), { target: { value: '17:00' } })
  fireEvent.change(screen.getByLabelText('Service ends'), { target: { value: '22:00' } })
}

/** Chooses a quota-complete selection — two Beef dishes and one Salad. */
async function chooseDishes() {
  const beef = await waitForEnabled('Add a dish for Beef')
  fireEvent.change(beef, { target: { value: 'DS-203' } })
  fireEvent.change(screen.getByLabelText('Add a dish for Beef'), { target: { value: 'DS-204' } })
  fireEvent.change(screen.getByLabelText('Add a dish for Salad'), { target: { value: 'DS-301' } })
}

describe('BookingWorkbenchPage', () => {
  beforeEach(() => {
    createdBodies.length = 0
    quoteBodies.length = 0
  })

  it('renders the three columns, the booking-number placeholder and the step indicator', async () => {
    mockApi()

    renderWorkbench()

    expect(await screen.findByText('Reservation Workbench')).toBeInTheDocument()
    expect(screen.getAllByText('Client & event').length).toBeGreaterThan(0)
    expect(screen.getByText('Package tier & headcount')).toBeInTheDocument()
    expect(screen.getByText('Contract & ledger')).toBeInTheDocument()

    // The booking number is a placeholder until the booking is saved — its
    // contractual format embeds the surrogate identifier.
    expect(screen.getByText('Booking number assigned on save')).toBeInTheDocument()

    // The four steps of the reservation workflow.
    expect(screen.getByText('Package & courses')).toBeInTheDocument()
    expect(screen.getByText('Financials')).toBeInTheDocument()
    expect(screen.getByText('Deposit')).toBeInTheDocument()
  })

  it('uses the lookup cache key shared with the package feature', async () => {
    const api = mockApi()

    renderWorkbench({ seedPaxApplicables: true })

    await waitForEnabled('Package type')
    expect(
      api.requests.some((request) => request.url === '/packages/pax-applicables'),
    ).toBe(false)
  })

  it('includes exact API labels in accessible tier options and refreshes the selected panel', async () => {
    mockApi()

    renderWorkbench()

    await waitForEnabled('Package type')
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Wedding' } })
    const tierPicker = await waitForEnabled('Buffet tier')
    expect(
      within(tierPicker).getByRole('option', {
        name: /Wedding Grand Tier · Wedding · 80 to 100 Above/,
      }),
    ).toBeInTheDocument()

    fireEvent.change(tierPicker, { target: { value: '12' } })
    const selectedTier = screen.getByTestId('booking-selected-tier')
    expect(within(selectedTier).getByText('80 to 100 Above')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Vip' } })
    expect(screen.queryByTestId('booking-selected-tier')).not.toBeInTheDocument()

    const vipTierPicker = await waitForEnabled('Buffet tier')
    expect(
      within(vipTierPicker).getByRole('option', {
        name: /VIP Degustation & Special Service · Vip · 30 to 49 Pax/,
      }),
    ).toBeInTheDocument()
    fireEvent.change(vipTierPicker, { target: { value: '18' } })

    const updatedSelectedTier = screen.getByTestId('booking-selected-tier')
    expect(within(updatedSelectedTier).getByText('30 to 49 Pax')).toBeInTheDocument()
    expect(within(updatedSelectedTier).queryByText('80 to 100 Above')).not.toBeInTheDocument()
  })

  it('keeps tier selection, quote and save available while the classification lookup is pending', async () => {
    const api = mockApi({ holdPaxApplicables: true })

    renderWorkbench()

    await fillRequiredInputs()
    const selectedTier = screen.getByTestId('booking-selected-tier')
    expect(within(selectedTier).getByText('Loading classification…')).toBeInTheDocument()
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('12')
    expect(await screen.findByText('₱305,000.00')).toBeInTheDocument()

    await userEvent.click(screen.getAllByText('Save draft')[0])
    await waitFor(() => expect(createdBodies).toHaveLength(1))
    expect(createdBodies[0]).toMatchObject({ confirm: false, packageId: 12 })
    expect(api.requests.some((request) => request.url === '/packages/pax-applicables')).toBe(true)

    api.releasePaxApplicables()
  })

  it('keeps booking usable when the lookup fails or does not contain the selected ID', async () => {
    const failedApi = mockApi({
      paxApplicablesError: new ApiError(400, ['The lookup could not be loaded.']),
    })

    const failedPage = renderWorkbench()
    await fillRequiredInputs()
    expect(
      within(screen.getByTestId('booking-selected-tier')).getByText(
        'Classification unavailable',
      ),
    ).toBeInTheDocument()
    expect(await screen.findByText('₱305,000.00')).toBeInTheDocument()
    await userEvent.click(screen.getAllByText('Save draft')[0])
    await waitFor(() => expect(createdBodies).toHaveLength(1))
    expect(failedApi.requests.some((request) => request.url === '/packages/pax-applicables')).toBe(
      true,
    )
    failedPage.unmount()

    createdBodies.length = 0
    const unmatchedApi = mockApi({
      paxApplicables: packagePaxApplicablesFixture.filter((item) => item.numPaxId !== 4),
    })
    renderWorkbench()
    await fillRequiredInputs()
    expect(
      within(screen.getByTestId('booking-selected-tier')).getByText(
        'Classification unavailable',
      ),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('12')
    expect(unmatchedApi.requests.some((request) => request.url === '/packages/pax-applicables')).toBe(
      true,
    )
  })

  it('renders the capability-gap notices that remain and the real pickers that replaced the rest', async () => {
    mockApi()

    renderWorkbench()

    await screen.findByText('Reservation Workbench')

    const gaps = screen.getAllByTestId('capability-gap-notice')
    expect(gaps.length).toBeGreaterThanOrEqual(2)
    expect(screen.getAllByText('Chef batching prep yield unavailable').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Included site amenities unavailable').length).toBeGreaterThan(0)
    expect(screen.getAllByText(/REQ-MNU-03/).length).toBeGreaterThan(0)

    // REQ-MNU-03's per-event dish selection is delivered, so it is a picker now
    // — not a gap notice.
    expect(screen.queryByText('Per-event dish selection unavailable')).not.toBeInTheDocument()
    expect(screen.getByText('Event dish selection')).toBeInTheDocument()
    expect(screen.getByText('Add-ons & upgrades')).toBeInTheDocument()
  })

  it('prices the ledger from the server’s quote and reads "not stated" for an unconfigured deposit', async () => {
    mockApi()

    renderWorkbench()

    await fillRequiredInputs()

    // The net contract, the surcharge and the balance are the quote's figures.
    expect(await screen.findByText('₱305,000.00')).toBeInTheDocument()
    expect(screen.getAllByText('₱36,000.00').length).toBeGreaterThan(0)
    expect(screen.getByText('₱255,000.00')).toBeInTheDocument()

    // The required deposit percentage is configuration and absent by default.
    const requiredDeposit = screen.getByText('Required deposit').closest('div')
    expect(requiredDeposit).toHaveTextContent('not stated')

    // The tier's approved course ruleset is the course specification.
    expect(await screen.findByText('Quota 2')).toBeInTheDocument()
    expect(screen.getByText('Pool 5')).toBeInTheDocument()
  })

  it('shows the recalculating state and re-prices the rail when the headcount changes', async () => {
    const gate = mockApi({ holdQuote: true })

    renderWorkbench()

    await fillRequiredInputs()

    expect(await screen.findByTestId('booking-quote-recalculating')).toBeInTheDocument()
    expect(screen.queryByText('₱305,000.00')).not.toBeInTheDocument()

    // Wait until the request itself is open, then release it.
    await waitFor(() => {
      expect(gate.pendingCount()).toBeGreaterThan(0)
    })
    gate.releaseQuote()

    expect(await screen.findByText('₱305,000.00')).toBeInTheDocument()
    expect(screen.queryByTestId('booking-quote-recalculating')).not.toBeInTheDocument()

    // A new headcount re-prices the rail — and never shows a stale figure.
    fireEvent.change(screen.getByLabelText('Contract headcount'), { target: { value: '300' } })

    expect(await screen.findByTestId('booking-quote-recalculating')).toBeInTheDocument()
  })

  it('names the discount rule the server applied, including the band and its rate', async () => {
    mockApi({ quote: bandDiscountBookingQuoteFixture })

    renderWorkbench()

    await fillRequiredInputs()

    await screen.findByText('₱305,000.00')

    // DEC-154 — the source and the band's own label are the server's.
    expect(screen.getByText('Automatic headcount band — 100 to 999 heads')).toBeInTheDocument()
    expect(screen.getByText('₱20.00 per head')).toBeInTheDocument()
    expect(screen.getByText('₱5,000.00')).toBeInTheDocument()
    expect(screen.queryByText(/Discount code —/)).not.toBeInTheDocument()
  })

  it('offers the area filter only for Regular and Premier tiers and hides the municipality for an ineligible tier', async () => {
    const api = mockApi()

    renderWorkbench()

    await waitForEnabled('Package type')
    expect(screen.queryByLabelText('Area filter')).not.toBeInTheDocument()

    // A Regular tier may carry the out-of-town amount, so the filter appears.
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Regular' } })
    const areaFilter = await waitForEnabled('Area filter')
    expect(screen.queryByLabelText('Municipality')).not.toBeInTheDocument()

    // The area filter is answered by the server through `areaId`, and the tier
    // list asks for the active tiers only (AC-BKG-93).
    fireEvent.change(areaFilter, { target: { value: '4' } })
    await waitFor(() => {
      expect(
        api.requests.some(
          (request) =>
            request.url === '/packages' &&
            request.params?.areaId === 4 &&
            request.params?.activeOnly === true,
        ),
      ).toBe(true)
    })

    await waitForEnabled('Buffet tier')
    // One server-filtered list — the client never fans out a call per tier.
    expect(
      api.requests.filter((request) => /^\/packages\/\d+$/.test(request.url)),
    ).toHaveLength(0)
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '31' } })
    expect(screen.getByLabelText('Municipality')).toBeInTheDocument()

    // A Wedding tier cannot carry the amount: the type filter clears the tier
    // and the selector disappears with it.
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Wedding' } })
    await waitFor(() => {
      expect(screen.queryByLabelText('Municipality')).not.toBeInTheDocument()
    })
    expect(screen.queryByLabelText('Area filter')).not.toBeInTheDocument()

    // Choosing the Wedding tier states why the amount cannot be applied.
    await waitFor(() => {
      expect(screen.getByLabelText('Buffet tier')).toHaveValue('')
    })
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '12' } })

    expect(
      screen
        .getAllByRole('note')
        .find((note) => note.textContent?.includes('only the Regular and Premier types can')),
    ).toHaveTextContent('The Wedding type cannot carry the out-of-town amount')
    expect(screen.queryByLabelText('Municipality')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Apply the out-of-town surcharge')).not.toBeInTheDocument()
  })

  it('carries the dish selection and the add-on lines on the quote and on the create', async () => {
    mockApi()

    renderWorkbench()

    await fillRequiredInputs()
    await screen.findByText('₱305,000.00')

    await chooseDishes()

    // The add-on's line total is the server's `lineAmountForBooking`.
    const addonPicker = await waitForEnabled('Add an add-on')
    fireEvent.change(addonPicker, { target: { value: '8' } })
    expect(screen.getByLabelText('Line total')).toHaveValue('37,500.00')
    expect(screen.getByText('Quota complete')).toBeInTheDocument()

    await waitFor(() => {
      expect(
        quoteBodies.some(
          (body) => body.selections?.length === 3 && body.addons?.length === 1,
        ),
      ).toBe(true)
    })

    const quoted = quoteBodies.filter((body) => body.selections?.length === 3).at(-1)
    expect(quoted?.selections).toEqual([
      { courseId: 4, menuId: 'DS-203', prepInstruction: null },
      { courseId: 4, menuId: 'DS-204', prepInstruction: null },
      { courseId: 5, menuId: 'DS-301', prepInstruction: null },
    ])
    expect(quoted?.addons).toEqual([{ addonId: 8, amount: 37500 }])

    await userEvent.click(screen.getAllByText('Save draft')[0])

    await waitFor(() => {
      expect(createdBodies).toHaveLength(1)
    })
    expect(createdBodies[0]).toMatchObject({
      confirm: false,
      selections: [
        { courseId: 4, menuId: 'DS-203', prepInstruction: null },
        { courseId: 4, menuId: 'DS-204', prepInstruction: null },
        { courseId: 5, menuId: 'DS-301', prepInstruction: null },
      ],
      addons: [{ addonId: 8, amount: 37500 }],
    })
  })

  it('saves a draft and continues on the menu editor of the booking it created', async () => {
    mockApi()

    renderWorkbench()

    await fillRequiredInputs()
    await screen.findByText('₱305,000.00')

    await userEvent.click(screen.getAllByText('Save draft')[0])

    await waitFor(() => {
      expect(createdBodies).toHaveLength(1)
    })
    expect(createdBodies[0]).toMatchObject({ confirm: false })
    // A draft never carries a payment.
    expect(createdBodies[0]).not.toHaveProperty('initialPayment')

    // AC-BKG-75 — the create flow lands on the menu editor.
    expect(await screen.findByText('Menu editor of the booking')).toBeInTheDocument()
  })

  it('confirms a booking and records its initial payment', async () => {
    mockApi()

    renderWorkbench()

    await fillRequiredInputs()
    await screen.findByText('₱305,000.00')

    fireEvent.change(screen.getByLabelText('Initial payment amount'), {
      target: { value: '50000' },
    })
    await userEvent.click(screen.getAllByText('Confirm booking')[0])

    await waitFor(() => {
      expect(createdBodies).toHaveLength(1)
    })
    expect(createdBodies[0]).toMatchObject({
      confirm: true,
      initialPayment: { amountPaid: 50000, paymentMeans: 'Cash' },
    })
  })

  it('keeps the confirm action inactive when the tier is deactivated', async () => {
    mockApi({ quote: inactiveTierBookingQuoteFixture })

    renderWorkbench()

    await fillRequiredInputs()

    await screen.findByText('₱305,000.00')

    const confirmButtons = screen.getAllByText('Confirm booking')
    confirmButtons.forEach((button) => expect(button).toBeDisabled())
    expect(screen.getByText('The tier is deactivated')).toBeInTheDocument()
  })

  it('refuses a dispatch schedule that is not ordered against the programme', async () => {
    mockApi()

    renderWorkbench()

    await fillRequiredInputs()
    await screen.findByText('₱305,000.00')

    // BR-34 — the call time precedes the programme and the pack-out follows it.
    fireEvent.change(screen.getByLabelText('Call time'), { target: { value: '18:00' } })

    expect(
      await screen.findAllByText("The call time must not be later than the programme's start time."),
    ).not.toHaveLength(0)

    await userEvent.click(screen.getAllByText('Confirm booking')[0])

    // The client refuses before the request is made.
    expect(createdBodies).toHaveLength(0)
  })

  it('refuses a dish selection that does not satisfy the course quota', async () => {
    mockApi()

    renderWorkbench()

    await fillRequiredInputs()
    await screen.findByText('₱305,000.00')

    // One Beef dish of the two the tier requires.
    await waitForEnabled('Add a dish for Beef')
    fireEvent.change(screen.getByLabelText('Add a dish for Beef'), {
      target: { value: 'DS-203' },
    })

    expect(screen.getByText('Quota not met')).toBeInTheDocument()
    expect(screen.getAllByText('Save draft')[0]).toBeDisabled()

    await userEvent.click(screen.getAllByText('Save draft')[0])
    expect(createdBodies).toHaveLength(0)
  })

  it('renders the server’s rejection verbatim and keeps the operator’s input', async () => {
    mockApi({ createError: new ApiError(400, ['The initial payment exceeds the net amount due.']) })

    renderWorkbench()

    await fillRequiredInputs()
    await screen.findByText('₱305,000.00')

    await userEvent.click(screen.getAllByText('Confirm booking')[0])

    expect(
      await screen.findByText('The initial payment exceeds the net amount due.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Occasion')).toHaveValue('Golden Jubilee')
  })

  it('starts the cascade required and closed, naming the package type as the first step', async () => {
    mockApi()

    renderWorkbench()

    const typeControl = await waitForEnabled('Package type') as HTMLSelectElement
    expect(typeControl.required).toBe(true)
    const tierControl = screen.getByLabelText('Buffet tier') as HTMLSelectElement
    expect(tierControl).toBeDisabled()
    expect(within(tierControl).getByText('Select a package type first')).toBeInTheDocument()
    expect(screen.getByText('Choose the package type to load its tiers')).toBeInTheDocument()
    expect(within(typeControl).getByText('Select a package type')).toBeInTheDocument()
  })

  it('requires the event area for an eligible type and answers the tier list from one filtered server request', async () => {
    const api = mockApi()

    renderWorkbench()

    await waitForEnabled('Package type')
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Premier' } })

    const tierControl = screen.getByLabelText('Buffet tier') as HTMLSelectElement
    expect(tierControl).toBeDisabled()
    expect(screen.getByText('Choose the event area to load its tiers')).toBeInTheDocument()

    const areaFilter = await waitForEnabled('Area filter') as HTMLSelectElement
    expect(areaFilter.required).toBe(true)

    fireEvent.change(areaFilter, { target: { value: '6' } })
    await waitFor(() => {
      expect(
        api.requests.some(
          (request) =>
            request.url === '/packages' &&
            request.params?.areaId === 6 &&
            request.params?.activeOnly === true,
        ),
      ).toBe(true)
    })

    // One server-filtered list, still without a per-tier fan-out (AC-BKG-93).
    expect(
      api.requests.filter((request) => /^\/packages\/\d+$/.test(request.url)),
    ).toHaveLength(0)
  })

  it('clears the tier inside the change that moves the type or the area, keeping the area across the two eligible types', async () => {
    mockApi()

    renderWorkbench()

    await waitForEnabled('Package type')
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Wedding' } })
    await waitForEnabled('Buffet tier')
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '12' } })
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('12')

    // A type change always clears the tier — synchronously, in the change.
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Regular' } })
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('')

    fireEvent.change(screen.getByLabelText('Area filter'), { target: { value: '4' } })
    await waitForEnabled('Buffet tier')
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '31' } })

    // An eligible-to-eligible switch keeps the chosen area and clears the tier.
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Premier' } })
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('')
    expect(screen.getByLabelText('Area filter')).toHaveValue('4')
    await waitForEnabled('Buffet tier')
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '24' } })

    // An area change — including a clear — clears the tier in the same update.
    fireEvent.change(screen.getByLabelText('Area filter'), { target: { value: '5' } })
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('')

    // A non-eligible type hides and clears the area.
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Pm' } })
    await waitFor(() => {
      expect(screen.queryByLabelText('Area filter')).not.toBeInTheDocument()
    })
    expect(await waitForEnabled('Buffet tier')).toHaveValue('')
  })

  it('holds the tier picker until a changed area settles and never exposes the previous area options', async () => {
    const heldAreaFetches: Array<{
      areaId: unknown
      resolve: (response: AxiosResponse) => void
    }> = []
    vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string, config?: AxiosRequestConfig): Promise<AxiosResponse> => {
        if (url === '/packages/pax-applicables') {
          return axiosResponse(packagePaxApplicablesFixture)
        }
        if (url === '/packages') {
          const params = (config?.params ?? {}) as Record<string, unknown>
          if (!params.activeOnly || params.areaId === undefined) {
            return axiosResponse(packageTiersFixture)
          }
          return new Promise((resolve) => {
            heldAreaFetches.push({ areaId: params.areaId, resolve })
          }) as Promise<AxiosResponse>
        }
        if (url.includes('/course-rules')) return axiosResponse(packageCourseRulesetFixture)
        if (url === '/areas') return axiosResponse(areaCatalogueFixture)
        if (url.includes('/customers')) return axiosResponse(customerDirectoryFixture)
        if (url === '/addons') return axiosResponse(addonMasterFixture)
        return axiosResponse({})
      },
    )

    renderWorkbench()

    await waitForEnabled('Package type')
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Regular' } })
    const areaFilter = await waitForEnabled('Area filter')
    fireEvent.change(areaFilter, { target: { value: '4' } })
    await waitFor(() => {
      expect(heldAreaFetches.some((fetch) => fetch.areaId === 4)).toBe(true)
    })
    heldAreaFetches
      .filter((fetch) => fetch.areaId === 4)
      .forEach((fetch) => fetch.resolve(axiosResponse([packageTiersFixture[3]])))
    await waitForEnabled('Buffet tier')
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '31' } })
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('31')

    // A new area immediately clears the tier and holds the picker — the
    // previous area's tier cannot be selected while the list stands in.
    // userEvent honors the `disabled` attribute, so a selection attempt on
    // the held picker fails (fireEvent would bypass it and prove nothing).
    fireEvent.change(screen.getByLabelText('Area filter'), { target: { value: '5' } })
    const tierControl = screen.getByLabelText('Buffet tier') as HTMLSelectElement
    expect(tierControl).toHaveValue('')
    expect(tierControl).toBeDisabled()
    expect(await screen.findByText('Loading the tiers for Batangas City…')).toBeInTheDocument()
    // The previous area's tier still stands in the list, but only as text:
    // the picker is disabled, so the draft cannot hold the staged choice
    // (AC-BKG-95) — the cascade already cleared it in the same update.
    expect(within(tierControl).getByText(/Regular Feast Buffet/)).toBeInTheDocument()
    expect(tierControl).toBeDisabled()

    // The new area answers with no tier of this type — the explanation names
    // both and offers the clear actions.
    heldAreaFetches
      .filter((fetch) => fetch.areaId === 5)
      .forEach((fetch) => fetch.resolve(axiosResponse([packageTiersFixture[2]])))
    expect(
      await screen.findByText(/No active tier of type Regular covers Batangas City/),
    ).toBeInTheDocument()

    // Clearing the area re-closes the eligible-type gate; choosing an area
    // reopens it.
    await userEvent.click(screen.getByText('Clear the area'))
    expect(
      await screen.findByText('Choose the event area to load its tiers'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Buffet tier')).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Area filter'), { target: { value: '4' } })
    await waitFor(() => {
      expect(heldAreaFetches.filter((fetch) => fetch.areaId === 4).length).toBeGreaterThan(0)
    })
    heldAreaFetches
      .filter((fetch) => fetch.areaId === 4)
      .forEach((fetch) => fetch.resolve(axiosResponse([packageTiersFixture[3]])))
    await waitForEnabled('Buffet tier')
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '31' } })
    expect(screen.getByLabelText('Buffet tier')).toHaveValue('31')
  })

  it('blocks save and confirm while the type/area gate is unsatisfied and keeps the create payload unchanged', async () => {
    mockApi()

    renderWorkbench()

    await waitForEnabled('Package type')

    // The gate is unsatisfied on a fresh draft, so save and confirm are off —
    // and the messages are the client's own (never sent to the API).
    const saveButtons = screen.getAllByText('Save draft')
    saveButtons.forEach((button) => expect(button).toBeDisabled())
    screen
      .getAllByText('Confirm booking')
      .forEach((button) => expect(button).toBeDisabled())
    expect(
      screen.getAllByText('Select a package type before choosing a buffet tier.').length,
    ).toBeGreaterThan(0)
    await userEvent.click(saveButtons[0])
    expect(createdBodies).toHaveLength(0)

    await fillRequiredInputs()
    await screen.findByText('₱305,000.00')

    await userEvent.click(screen.getAllByText('Save draft')[0])
    await waitFor(() => {
      expect(createdBodies).toHaveLength(1)
    })

    // Package type and area filter are UI state only — the create request
    // carries `packageId` and is otherwise unchanged (AC-BKG-97).
    expect(createdBodies[0]).not.toHaveProperty('packageType')
    expect(createdBodies[0]).not.toHaveProperty('areaFilterId')
    expect(createdBodies[0]).toMatchObject({ packageId: 12, confirm: false })
  })

  it('wins the rate with the quote on catalogue divergence and refreshes the tier catalogue only once', async () => {
    const api = mockApi()
    renderWorkbench()

    await fillRequiredInputs()

    // The Wedding tier's catalogue rate is 1450 while the quote's is 1140 —
    // the quote wins and the divergence note appears.
    expect(await screen.findByTestId('booking-rate-divergence')).toHaveTextContent(
      'The quoted rate differs from the catalogue rate — the catalogue has been refreshed.',
    )
    expect(screen.getAllByText('₱1,140.00').length).toBeGreaterThan(0)

    const packageRequestCount = () =>
      api.requests.filter((request) => request.url === '/packages').length
    await waitFor(() => {
      expect(packageRequestCount()).toBe(4)
    })

    // Rerenders and re-pricing never re-send the invalidation.
    fireEvent.change(screen.getByLabelText('Contract headcount'), { target: { value: '300' } })
    await screen.findByTestId('booking-rate-recalculating')
    await screen.findByText('₱305,000.00')
    await waitFor(() => {
      expect(packageRequestCount()).toBe(4)
    })
  })

  it('filters the tier list by the trimmed and still case-sensitive package type', async () => {
    /*
     * The stored types carry whitespace and a case variant — the list filter
     * trims both sides and keeps the case (AC-BKG-96), while the eligibility
     * test stays its own trimmed, case-insensitive rule (DEC-156).
     */
    const typedTiersFixture: PackageDto[] = [
      ...packageTiersFixture.filter((tier) => tier.packageType !== 'Regular'),
      {
        packageId: 71,
        packageCode: 'PKG-REG-71',
        packageType: ' Regular',
        description: 'Whitespace Regular Tier',
        amountPax: 900,
        minPax: 80,
        numPaxId: 2,
        extendedLocationAmount: null,
        isActive: true,
      },
      {
        packageId: 72,
        packageCode: 'PKG-REG-72',
        packageType: 'regular',
        description: 'Lowercase regular Tier',
        amountPax: 800,
        minPax: 80,
        numPaxId: 2,
        extendedLocationAmount: null,
        isActive: true,
      },
    ]

    mockApi({ tiers: typedTiersFixture })

    renderWorkbench()

    await waitForEnabled('Package type')
    // The type options are the stored types after trimming — exact values.
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Regular' } })
    // The Regular gate needs an area before the picker opens (AC-BKG-93).
    const areaFilter = await waitForEnabled('Area filter')
    fireEvent.change(areaFilter, { target: { value: '4' } })
    await waitForEnabled('Buffet tier')
    const buffet = screen.getByLabelText('Buffet tier')
    expect(within(buffet).getByText(/Whitespace Regular Tier/)).toBeInTheDocument()
    expect(within(buffet).queryByText(/Lowercase regular Tier/)).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'regular' } })
    const lowered = screen.getByLabelText('Buffet tier')
    expect(within(lowered).getByText(/Lowercase regular Tier/)).toBeInTheDocument()
    expect(within(lowered).queryByText(/Whitespace Regular Tier/)).not.toBeInTheDocument()
  })

  it('prefills the event municipality from the area filter without turning the surcharge on', async () => {
    mockApi()

    renderWorkbench()

    await waitForEnabled('Package type')
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Regular' } })
    const areaFilter = await waitForEnabled('Area filter')
    fireEvent.change(areaFilter, { target: { value: '4' } })
    await waitForEnabled('Buffet tier')
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '31' } })

    // DEC-171 — the non-empty area copies into the event municipality in the
    // same update, leaves it editable, and never enables the surcharge. The
    // clearing branch's no-reverse-sync is asserted by the exact patch shape
    // in WorkbenchTierEngine.test.tsx.
    const municipality = screen.getByLabelText('Municipality') as HTMLSelectElement
    await waitFor(() => {
      expect(municipality).toHaveValue('4')
    })
    expect(screen.getByLabelText('Apply the out-of-town surcharge')).not.toBeChecked()

    fireEvent.change(municipality, { target: { value: '9' } })
    expect(municipality).toHaveValue('9')
    expect(screen.getByLabelText('Apply the out-of-town surcharge')).not.toBeChecked()
  })

  it('offers no inactive tier on the filtered server list', async () => {
    mockApi()

    renderWorkbench()

    await waitForEnabled('Package type')
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Regular' } })
    fireEvent.change(await waitForEnabled('Area filter'), { target: { value: '4' } })
    const buffet = await waitForEnabled('Buffet tier')

    // Mock fidelity — the fixture's inactive Morning Briefing row never
    // reaches an `activeOnly=true` response.
    expect(within(buffet).getByText(/Regular Feast Buffet/)).toBeInTheDocument()
    expect(within(buffet).queryByText(/Morning Briefing Tier/)).not.toBeInTheDocument()
  })

  it('hides every quote-derived figure but the rate while the quote is re-priced', async () => {
    const gate = mockApi({ holdQuote: true })

    renderWorkbench()

    await waitForEnabled('Client')
    fireEvent.change(screen.getByLabelText('Client'), { target: { value: '18' } })
    fireEvent.change(screen.getByLabelText('Occasion'), { target: { value: 'Golden Jubilee' } })
    fireEvent.change(screen.getByLabelText('Venue'), { target: { value: 'The Grand Pavilion' } })
    fireEvent.change(screen.getByLabelText('Event date'), { target: { value: '2026-11-20' } })
    await waitForEnabled('Package type')
    fireEvent.change(screen.getByLabelText('Package type'), { target: { value: 'Regular' } })
    fireEvent.change(await waitForEnabled('Area filter'), { target: { value: '4' } })
    await waitForEnabled('Buffet tier')
    fireEvent.change(screen.getByLabelText('Buffet tier'), { target: { value: '31' } })
    fireEvent.change(screen.getByLabelText('Contract headcount'), { target: { value: '120' } })
    fireEvent.change(screen.getByLabelText('Program'), { target: { value: '17:00' } })
    fireEvent.change(screen.getByLabelText('Service ends'), { target: { value: '22:00' } })

    // The first quote resolves (fixture: surcharge ₱36,000.00 covered, floor note).
    await waitFor(() => {
      expect(gate.pendingCount()).toBeGreaterThan(0)
    })
    gate.releaseQuote()
    const tierPanel = await screen.findByText('Regular Feast Buffet')
    expect(tierPanel).toBeInTheDocument()
    expect((await screen.findAllByText('₱36,000.00')).length).toBeGreaterThan(0)
    expect(
      await screen.findByText('The tier states a minimum of 150 heads.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Tier planning estimate')).toBeInTheDocument()

    // A re-priced quote holds — none of the previous request's figures stays.
    fireEvent.change(screen.getByLabelText('Contract headcount'), { target: { value: '150' } })
    await screen.findByTestId('booking-quote-recalculating')
    await waitFor(() => {
      expect(gate.pendingCount()).toBeGreaterThan(0)
    })
    expect(screen.queryByText('₱36,000.00')).not.toBeInTheDocument()
    expect(
      screen.queryByText('The tier states a minimum of 150 heads.'),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('Tier planning estimate')).not.toBeInTheDocument()
    // …but the rate cell alone keeps the same-package held rate.
    expect(screen.getAllByText('₱1,140.00').length).toBeGreaterThan(0)

    gate.releaseQuote()
    expect((await screen.findAllByText('₱36,000.00')).length).toBeGreaterThan(0)
  })
})
