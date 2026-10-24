import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosRequestConfig, AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { BookingsEventsPage } from '@/features/bookings/BookingsEventsPage'
import {
  bookingRegisterPageFixture,
  bookingRegisterSummaryFixture,
  emptyBookingRegisterPageFixture,
} from '@/test/bookingFixture'
import { createTestQueryClient } from '@/test/testHelpers'

type GetResolver = (url: string) => unknown

interface CapturedRequest {
  url: string
  params: Record<string, unknown> | undefined
}

function renderRegister() {
  const queryClient = createTestQueryClient()
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/bookings']}>
        <Routes>
          <Route path="/bookings" element={<BookingsEventsPage />} />
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

/**
 * Routes the mocked GET calls to the fixture that matches the endpoint and
 * records every request so a test can assert on the query parameters the
 * register sends.
 */
function mockGet(resolver: GetResolver = defaultResolver) {
  const requests: CapturedRequest[] = []

  const implementation = async (
    url: string,
    config?: AxiosRequestConfig,
  ): Promise<AxiosResponse> => {
    requests.push({ url, params: config?.params as Record<string, unknown> | undefined })
    return {
      data: resolver(url),
      status: 200,
      statusText: 'OK',
      headers: {},
      config: { headers: {} } as AxiosResponse['config'],
    }
  }

  vi.spyOn(apiClient, 'get').mockImplementation(implementation)
  return requests
}

function defaultResolver(url: string): unknown {
  if (url.includes('/bookings/summary')) return bookingRegisterSummaryFixture
  if (url.includes('/bookings/export')) return new Blob(['xlsx'])
  if (url.includes('/bookings')) return bookingRegisterPageFixture
  return {}
}

/** The parameters of the last register request. */
function lastRegisterParams(requests: CapturedRequest[]): Record<string, unknown> {
  const register = requests.filter((request) => request.url === '/bookings').at(-1)
  return register?.params ?? {}
}

describe('BookingsEventsPage', () => {
  beforeEach(() => {
    Object.defineProperty(window.URL, 'createObjectURL', {
      configurable: true,
      value: vi.fn(() => 'blob:mock'),
    })
    Object.defineProperty(window.URL, 'revokeObjectURL', {
      configurable: true,
      value: vi.fn(),
    })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
  })

  it('renders the register heading and the server’s dossier figures', async () => {
    mockGet()

    renderRegister()

    expect(await screen.findByText('Bookings & Event Operations')).toBeInTheDocument()

    // The first dossier card — every figure is the one the API published.
    const cards = await screen.findAllByTestId('booking-dossier-card')
    const firstCard = cards[0]
    expect(firstCard).toHaveTextContent('BK-20261120-00101')
    expect(firstCard).toHaveTextContent('₱285,000.00')
    expect(firstCard).toHaveTextContent('Paid: ₱285,000.00')
    expect(firstCard).toHaveTextContent('Outstanding: ₱0.00')

    // The lifecycle status and the payment state are two separate labels.
    expect(within(firstCard).getByText('Confirmed')).toBeInTheDocument()
    expect(within(firstCard).getByText('Fully paid')).toBeInTheDocument()

    // The countdown is the server's `daysUntilEvent`, never the browser's clock.
    expect(firstCard).toHaveTextContent('In 29 days')

    // The third card is overdue — a dimension of its own, not a payment state.
    const thirdCard = cards[2]
    expect(within(thirdCard).getByText('Overdue')).toBeInTheDocument()
    expect(within(thirdCard).getByText('Awaiting initial deposit')).toBeInTheDocument()
  })

  it('renders the totals strip and the four KPI cards from the server’s aggregate', async () => {
    mockGet()

    renderRegister()

    expect(await screen.findByText('Table totals:')).toBeInTheDocument()
    // The net contracted value appears in the totals strip and in the KPI card.
    expect(screen.getAllByText('₱628,000.00').length).toBeGreaterThan(0)
    expect(screen.getAllByText('₱335,000.00').length).toBeGreaterThan(0)
    expect(screen.getAllByText('₱293,000.00').length).toBeGreaterThan(0)
    expect(screen.getByText(/of 3 bookings/)).toBeInTheDocument()

    expect(screen.getByText('Active Pipeline')).toBeInTheDocument()
    expect(screen.getByText('Leading Tier')).toBeInTheDocument()
    expect(screen.getByText('Contract Value')).toBeInTheDocument()
    expect(screen.getByText('Committed Headcount')).toBeInTheDocument()
    expect(screen.getAllByText('Wedding Grand Tier').length).toBeGreaterThan(0)
  })

  it('renders the tier pills with the server’s facet counts', async () => {
    mockGet()

    renderRegister()

    const pills = await screen.findByTestId('booking-tier-pills')
    const weddingPill = within(pills).getByText('Wedding').closest('button')
    expect(weddingPill).not.toBeNull()
    expect(weddingPill).toHaveTextContent('Wedding')
    expect(weddingPill).toHaveTextContent('1')
  })

  it('sends every filter to the API and asks for the chronological order in the timeline view', async () => {
    const requests = mockGet()

    renderRegister()

    await screen.findByText('Table totals:')

    fireEvent.change(screen.getByLabelText('Search bookings'), { target: { value: 'gala' } })
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Confirmed' } })
    fireEvent.change(screen.getByLabelText('Payment'), { target: { value: 'Settled' } })
    await userEvent.click(screen.getByText('Overdue only'))
    await userEvent.click(screen.getByText('Timeline'))

    await waitFor(() => {
      const params = lastRegisterParams(requests)
      expect(params.search).toBe('gala')
      expect(params.status).toBe('Confirmed')
      expect(params.paymentState).toBe('Settled')
      expect(params.overdue).toBe(true)
      expect(params.sort).toBe('EventDateAsc')
    })
  })

  it('shows the empty state of a register that holds no booking at all', async () => {
    mockGet((url) => {
      if (url.includes('/bookings/summary')) return bookingRegisterSummaryFixture
      return emptyBookingRegisterPageFixture
    })

    renderRegister()

    expect(await screen.findByText('The bookings register is empty')).toBeInTheDocument()
    expect(screen.queryByText('No booking matches the current filters')).not.toBeInTheDocument()
  })

  it('names the filters in the empty state of a filtered register', async () => {
    mockGet((url) => {
      if (url.includes('/bookings/summary')) return bookingRegisterSummaryFixture
      return emptyBookingRegisterPageFixture
    })

    renderRegister()

    await screen.findByText('Table totals:')

    fireEvent.change(screen.getByLabelText('Search bookings'), { target: { value: 'gala' } })

    expect(
      await screen.findByText('No booking matches the current filters'),
    ).toBeInTheDocument()
    expect(screen.getByText(/the search “gala”/)).toBeInTheDocument()
  })

  it('shows the error state and offers a retry when the register cannot be loaded', async () => {
    vi.spyOn(apiClient, 'get').mockRejectedValue(new ApiError(500, ['The register failed.']))

    renderRegister()

    expect(
      await screen.findByText('The bookings register could not be loaded', {}, { timeout: 5000 }),
    ).toBeInTheDocument()
    expect(screen.getByText('The register failed.')).toBeInTheDocument()
    expect(screen.getByText('Retry')).toBeInTheDocument()
  })

  it('renders the selection chip, the add-on lines and the menu-selection action of each dossier', async () => {
    mockGet()

    renderRegister()

    const cards = await screen.findAllByTestId('booking-dossier-card')

    // M2 — the three selection states are carried by words, never colour alone.
    expect(within(cards[0]).getByText('Menu: locked')).toBeInTheDocument()
    expect(within(cards[1]).getByText('Menu: not chosen')).toBeInTheDocument()
    expect(within(cards[2]).getByText('Menu: chosen')).toBeInTheDocument()

    // The add-on count and its frozen total are the server's.
    expect(cards[0]).toHaveTextContent('0 lines · ₱0.00')
    expect(cards[1]).toHaveTextContent('1 line · ₱1,500.00')

    // The reference's "Manage Event" action is not part of this delivery.
    expect(screen.queryByText('Manage Event')).not.toBeInTheDocument()
    expect(screen.getAllByText('Menu selection')).toHaveLength(3)
  })

  it('opens the menu editor of the dossier whose action was used', async () => {
    mockGet()

    renderRegister()

    const cards = await screen.findAllByTestId('booking-dossier-card')

    await userEvent.click(
      within(cards[1]).getByRole('button', {
        name: 'Menu selection for BK-20261121-00102',
      }),
    )

    expect(await screen.findByText('Menu editor of the booking')).toBeInTheDocument()
  })

  it('keeps the Batch BEOs action inactive and renders the BEO capability gap', async () => {
    mockGet()

    renderRegister()

    const beoButton = await screen.findByText('Batch BEOs')
    expect(beoButton).toHaveAttribute('aria-disabled', 'true')

    const gaps = await screen.findAllByTestId('capability-gap-notice')
    expect(gaps.length).toBeGreaterThan(0)
    expect(screen.getAllByText(/BEO state unavailable/).length).toBeGreaterThan(0)
  })

  it('renders the operational dock with the capacity load and the AR telemetry', async () => {
    mockGet()

    renderRegister()

    expect(await screen.findByText('Commissary Load')).toBeInTheDocument()
    // The capacity is not configured, so the gauge reads "not stated".
    expect(screen.getAllByText(/not stated/i).length).toBeGreaterThan(0)
    expect(screen.getByText('AR & SOA Telemetry')).toBeInTheDocument()
    expect(screen.getAllByText('₱293,000.00').length).toBeGreaterThan(0)
    expect(screen.getByText(/1 overdue/)).toBeInTheDocument()
  })
})
