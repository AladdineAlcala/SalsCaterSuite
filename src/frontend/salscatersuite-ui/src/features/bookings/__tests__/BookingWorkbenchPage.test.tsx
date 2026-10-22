import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { BookingWorkbenchPage } from '@/features/bookings/BookingWorkbenchPage'
import {
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
      <MemoryRouter>
        <BookingWorkbenchPage />
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

/** The bodies of every create request the workbench sent. */
const createdBodies: unknown[] = []

function mockApi(options: MockOptions = {}) {
  const held: Array<(value: unknown) => void> = []

  vi.spyOn(apiClient, 'get').mockImplementation(async (url: string): Promise<AxiosResponse> => {
    if (url.includes('/course-rules')) return axiosResponse(packageCourseRulesetFixture)
    if (url.includes('/packages')) return axiosResponse(packageTiersFixture)
    if (url.includes('/areas')) return axiosResponse(areaCatalogueFixture)
    if (url.includes('/customers')) return axiosResponse(customerDirectoryFixture)
    return axiosResponse({})
  })

  vi.spyOn(apiClient, 'post').mockImplementation(
    async (url: string, body?: unknown): Promise<AxiosResponse> => {
      if (url.includes('/bookings/quote')) {
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

describe('BookingWorkbenchPage', () => {
  beforeEach(() => {
    createdBodies.length = 0
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

  it('renders the capability-gap notices instead of fabricating the panels', async () => {
    mockApi()

    renderWorkbench()

    await screen.findByText('Reservation Workbench')

    const gaps = screen.getAllByTestId('capability-gap-notice')
    expect(gaps.length).toBeGreaterThanOrEqual(3)
    expect(screen.getAllByText('Per-event dish selection unavailable').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Chef batching prep yield unavailable').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Included site amenities unavailable').length).toBeGreaterThan(0)
    expect(screen.getAllByText(/REQ-BKG-02/).length).toBeGreaterThan(0)
    expect(screen.getAllByText(/REQ-MNU-03/).length).toBeGreaterThan(0)
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

  it('saves a draft, confirms a booking and records its initial payment', async () => {
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

    fireEvent.change(screen.getByLabelText('Initial payment amount'), {
      target: { value: '50000' },
    })
    await userEvent.click(screen.getAllByText('Confirm booking')[0])

    await waitFor(() => {
      expect(createdBodies).toHaveLength(2)
    })
    expect(createdBodies[1]).toMatchObject({
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
