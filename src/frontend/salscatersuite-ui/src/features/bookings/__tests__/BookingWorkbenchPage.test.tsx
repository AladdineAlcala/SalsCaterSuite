import { configure, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosRequestConfig, AxiosResponse } from 'axios'
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
import { packageTiersFixture } from '@/test/packageFixture'
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
}

function renderWorkbench() {
  const queryClient = createTestQueryClient()
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

/** The bodies of every create request the workbench sent. */
const createdBodies: unknown[] = []

/** The bodies of every quote request the workbench sent. */
const quoteBodies: BookingQuoteRequest[] = []

function mockApi(options: MockOptions = {}) {
  const held: Array<(value: unknown) => void> = []
  const requests: CapturedRequest[] = []

  vi.spyOn(apiClient, 'get').mockImplementation(
    async (url: string, config?: AxiosRequestConfig): Promise<AxiosResponse> => {
      requests.push({ url, params: config?.params as Record<string, unknown> | undefined })

      if (url.includes('/course-rules')) return axiosResponse(packageCourseRulesetFixture)
      if (url.includes('/packages')) return axiosResponse(packageTiersFixture)
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
 * The controls are driven with a single change event each rather than a
 * keystroke per character: the component test does not need to simulate a
 * human's typing speed.
 */
async function fillRequiredInputs() {
  await waitForEnabled('Client')
  fireEvent.change(screen.getByLabelText('Client'), { target: { value: '18' } })
  fireEvent.change(screen.getByLabelText('Occasion'), { target: { value: 'Golden Jubilee' } })
  fireEvent.change(screen.getByLabelText('Venue'), { target: { value: 'The Grand Pavilion' } })
  fireEvent.change(screen.getByLabelText('Event date'), { target: { value: '2026-11-20' } })
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

    // The area filter is answered by the server through `areaId`.
    fireEvent.change(areaFilter, { target: { value: '4' } })
    await waitFor(() => {
      expect(
        api.requests.some(
          (request) => request.url.includes('/packages') && request.params?.areaId === 4,
        ),
      ).toBe(true)
    })

    await waitForEnabled('Buffet tier')
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
})
