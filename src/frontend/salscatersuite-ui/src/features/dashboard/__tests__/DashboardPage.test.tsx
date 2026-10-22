import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { dashboardFixture, emptyDashboardFixture } from '@/test/dashboardFixture'
import { createTestQueryClient } from '@/test/testHelpers'

function renderDashboard() {
  const queryClient = createTestQueryClient()
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function mockResolved(data: unknown) {
  vi.spyOn(apiClient, 'get').mockResolvedValue({ data } as AxiosResponse)
}

describe('DashboardPage', () => {
  it('renders skeleton placeholders while the aggregate is loading', () => {
    vi.spyOn(apiClient, 'get').mockReturnValue(new Promise<AxiosResponse>(() => undefined))

    renderDashboard()

    expect(screen.getByRole('status')).toHaveTextContent(
      'Loading the operations dashboard…',
    )
    expect(screen.getAllByTestId('skeleton').length).toBeGreaterThan(0)
    expect(screen.queryByRole('heading', { name: /Operations Dashboard/ })).not.toBeInTheDocument()
  })

  it('renders the API error messages verbatim and offers a retry', async () => {
    const refetch = vi.fn()
    vi.spyOn(apiClient, 'get').mockRejectedValue(
      new ApiError(400, ['Period must be in the format YYYY-MM.']),
    )

    renderDashboard()

    const alert = await screen.findByRole('alert')
    expect(within(alert).getByText('Period must be in the format YYYY-MM.')).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: /retry/i })).toBeInTheDocument()
    expect(refetch).not.toHaveBeenCalled()
    // The connection indicator reflects the failed fetch (AC-F10).
    expect(screen.getByText('API unreachable')).toBeInTheDocument()
    expect(screen.queryByText(/v2\.5\.0/)).not.toBeInTheDocument()
  })

  it('renders the ProblemDetails detail of a server fault', async () => {
    vi.spyOn(apiClient, 'get').mockRejectedValue(
      new ApiError(500, ['The ledger could not be reconciled.'], 'The ledger could not be reconciled.'),
    )

    renderDashboard()

    // A 5xx is retried once by the query before it surfaces.
    const alert = await screen.findByRole('alert', undefined, { timeout: 8000 })
    expect(
      within(alert).getByText('The ledger could not be reconciled.'),
    ).toBeInTheDocument()
  })

  it('renders explicit empty states instead of zeros', async () => {
    mockResolved(emptyDashboardFixture)

    renderDashboard()

    expect(await screen.findByText('No upcoming events')).toBeInTheDocument()
    expect(screen.getByText('No menus costed yet')).toBeInTheDocument()
    expect(screen.getByText('No ledger activity')).toBeInTheDocument()
    // An undefined ratio is an em dash, never 0%.
    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2)
  })

  it('renders every panel from the API payload', async () => {
    mockResolved(dashboardFixture)

    renderDashboard()

    // Header + telemetry pill
    expect(
      await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" }),
    ).toBeInTheDocument()
    expect(screen.getByText('October 2026')).toBeInTheDocument()
    expect(screen.getByText('35.0%')).toBeInTheDocument()
    expect(screen.getByText('70.6%')).toBeInTheDocument()

    // KPI card 1 — Active Bookings
    expect(screen.getByText('3 Committed, 1 Tentative')).toBeInTheDocument()
    expect(screen.getByText('₱425,000.00')).toBeInTheDocument()

    // KPI card 2 — Dish Costing Coverage
    expect(screen.getByText('75.0%')).toBeInTheDocument()
    expect(screen.getByText('15 of 20 Live Dish Costs')).toBeInTheDocument()
    expect(screen.getByText('5 need costing')).toBeInTheDocument()

    // KPI card 3 — Catalogue Margin
    expect(screen.getByText('56.0%')).toBeInTheDocument()
    expect(screen.getByText('Catalogue Margin')).toBeInTheDocument()
    expect(screen.getByText('Benchmark Target: 65.0%')).toBeInTheDocument()
    expect(screen.getByText('−9.00 pp vs benchmark')).toBeInTheDocument()

    // KPI card 4 — Outstanding Receivables
    expect(screen.getByText('₱125,000.00')).toBeInTheDocument()
    expect(screen.getByText('Across 3 Event Ledgers')).toBeInTheDocument()
    expect(screen.getByText('1 Overdue (>30 days)')).toBeInTheDocument()
    // The overdue amount appears twice: on the receivables card and in the
    // receivables-aging breakdown of the ledger panel.
    expect(screen.getAllByText('₱40,000.00')).toHaveLength(2)

    // Upcoming schedule
    expect(screen.getByText('Santos, Maria')).toBeInTheDocument()
    expect(screen.getByText('Silver Wedding Anniversary')).toBeInTheDocument()
    expect(screen.getByText('28 Oct 2026 • 250 pax')).toBeInTheDocument()
    expect(screen.getByText('Confirmed')).toBeInTheDocument()
    expect(screen.getByText('Premier Wedding Package')).toBeInTheDocument()
    expect(screen.getByText('₱285,000.00')).toBeInTheDocument()
    expect(screen.getByText('₱85,000.00')).toBeInTheDocument()

    // Food cost by category
    expect(screen.getByText('Beef Entree')).toBeInTheDocument()
    expect(screen.getByText('60.0%')).toBeInTheDocument()
    expect(screen.getByText('Uncategorised')).toBeInTheDocument()
    expect(screen.getByText('40.0%')).toBeInTheDocument()
    expect(screen.getByText('₱132,000.00')).toBeInTheDocument()

    // Payment ledger
    expect(screen.getByText('Collected (Past 7 Days)')).toBeInTheDocument()
    expect(screen.getByText('₱180,000.00')).toBeInTheDocument()
    expect(screen.getByText('5 receipts')).toBeInTheDocument()
    expect(screen.getByText('Bookings Awaiting Deposit')).toBeInTheDocument()
    expect(screen.getByText('₱75,000.00')).toBeInTheDocument()
    expect(screen.getByText('2 bookings')).toBeInTheDocument()
    expect(screen.getByText('Official Receipts Issued')).toBeInTheDocument()
    expect(screen.getByText('6 vouchers')).toBeInTheDocument()

    // Status footer
    expect(screen.getByText('Data as of 19 Oct 2026, 10:00 UTC')).toBeInTheDocument()
    expect(screen.getByText('API connected')).toBeInTheDocument()
  })

  it('exposes bounded progress tracks with the correct aria values', async () => {
    mockResolved(dashboardFixture)

    renderDashboard()

    const committed = await screen.findByRole('progressbar', {
      name: 'Committed share of the period booking population',
    })
    expect(committed).toHaveAttribute('aria-valuenow', '75')
    expect(committed).toHaveAttribute('aria-valuemin', '0')
    expect(committed).toHaveAttribute('aria-valuemax', '100')

    const coverage = screen.getByRole('progressbar', {
      name: 'Share of the menu catalogue that carries a food cost',
    })
    expect(coverage).toHaveAttribute('aria-valuenow', '75')

    const margin = screen.getByRole('progressbar', {
      name: 'Catalogue margin measured against the configured benchmark',
    })
    expect(margin).toHaveAttribute('aria-valuenow', '86.2')

    const overdue = screen.getByRole('progressbar', {
      name: 'Overdue share of total accounts receivable',
    })
    expect(overdue).toHaveAttribute('aria-valuenow', '32')
  })

  it('does not render the mockup figures that the API does not publish', async () => {
    mockResolved(dashboardFixture)

    renderDashboard()

    await screen.findByText('Collected (Past 7 Days)')

    expect(screen.queryByText(/BEO Ready/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Prep Pending/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Finalized/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/vs last month/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/downpayment/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Compliant/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/v2\.5\.0/)).not.toBeInTheDocument()
    expect(screen.queryByText(/SignalR/i)).not.toBeInTheDocument()
    // The mockup's "Price Volatility" chip described data the API does not
    // publish; the panel states what it actually reports (DEC-044).
    expect(screen.queryByText(/Price Volatility/i)).not.toBeInTheDocument()
    expect(screen.getByText('Catalogue Cost Mix')).toBeInTheDocument()
  })

  it('renders the receivables aging breakdown from the server buckets', async () => {
    mockResolved(dashboardFixture)

    renderDashboard()

    expect(await screen.findByText('Receivables Aging')).toBeInTheDocument()
    // Every bucket is rendered with the server's own label, count and amount.
    expect(screen.getByText('Not yet due')).toBeInTheDocument()
    expect(screen.getByText('1-30 days')).toBeInTheDocument()
    expect(screen.getByText('31-60 days')).toBeInTheDocument()
    expect(screen.getByText('61-90 days')).toBeInTheDocument()
    expect(screen.getByText('90+ days')).toBeInTheDocument()
    expect(screen.getByText('(2)')).toBeInTheDocument()
    expect(screen.getByText('(1)')).toBeInTheDocument()
    // The overdue bucket carries the action tone; the current one does not.
    const aging = within(screen.getByRole('list', { name: 'Receivables aging buckets' }))
    expect(aging.getByText('₱65,000.00')).toHaveClass('text-espresso-900')
    expect(aging.getByText('₱40,000.00')).toHaveClass('text-caramel-700')
  })

  it('renders a margin below the benchmark in the action tone, not the success tone', async () => {
    mockResolved(dashboardFixture)

    renderDashboard()

    // The fixture's variance is -9 pp: under-performing the benchmark.
    const variance = await screen.findByText('−9.00 pp vs benchmark')
    expect(variance).toHaveClass('text-caramel-700')
    expect(variance).not.toHaveClass('text-culinary-700')
  })

  it('renders a margin at or above the benchmark in the success tone', async () => {
    mockResolved({
      ...dashboardFixture,
      margin: { ...dashboardFixture.margin, varianceToBenchmarkPercentage: 4.25 },
    })

    renderDashboard()

    const variance = await screen.findByText('+4.25 pp vs benchmark')
    expect(variance).toHaveClass('text-culinary-700')
  })
})
