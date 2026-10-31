import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { PackageCataloguePage } from '@/features/packages/PackageCataloguePage'
import {
  emptyPackageSummaryFixture,
  emptyPackageTiersFixture,
  packageSummaryFixture,
  packagePaxApplicablesFixture,
  packageTiersFixture,
  unpricedPackageSummaryFixture,
  unpricedPackageTiersFixture,
} from '@/test/packageFixture'
import { createTestQueryClient } from '@/test/testHelpers'

type GetResolver = (url: string) => unknown | Promise<unknown>

function renderCatalogue() {
  const queryClient = createTestQueryClient()
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/packages']}>
        <Routes>
          <Route path="/packages" element={<PackageCataloguePage />} />
          <Route path="/packages/new" element={<div>New tier form</div>} />
          <Route path="/packages/:packageId/edit" element={<div>Tier edit form</div>} />
          <Route path="/packages/:packageId/rules" element={<div>Course rules workbench</div>} />
          <Route
            path="/packages/:packageId/locations"
            element={<div>Location matrix workbench</div>}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient }
}

/** Routes the mocked GET calls to the fixture that matches the endpoint. */
function mockGet(resolver: GetResolver) {
  const implementation = async (url: string): Promise<AxiosResponse> => ({
    data: await resolver(url),
    status: 200,
    statusText: 'OK',
    headers: {},
    config: { headers: {} } as AxiosResponse['config'],
  })

  return vi.spyOn(apiClient, 'get').mockImplementation(implementation)
}

function defaultResolver(url: string): unknown {
  if (url.includes('/packages/summary')) return packageSummaryFixture
  if (url === '/packages/pax-applicables') return packagePaxApplicablesFixture
  if (url.includes('/packages')) return packageTiersFixture
  return {}
}

describe('PackageCataloguePage', () => {
  beforeEach(() => {
    vi.spyOn(apiClient, 'patch').mockResolvedValue({
      data: { isActive: false },
      status: 200,
      statusText: 'OK',
      headers: {},
      config: { headers: {} } as AxiosResponse['config'],
    })
  })

  it('renders the banner, the four KPI cards, the type tabs and the tier cards from the payloads', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    expect(
      await screen.findByRole('heading', { name: 'Packages & Buffets Catalogue' }),
    ).toBeInTheDocument()

    // Card 1 — Configured Tiers (KPI-25).
    const configuredCard = (await screen.findByText('Configured Tiers')).closest(
      'article',
    ) as HTMLElement
    expect(within(configuredCard).getByText('7')).toBeInTheDocument()
    expect(within(configuredCard).getByText('6 active / 1 inactive')).toBeInTheDocument()
    expect(
      within(configuredCard).getByRole('progressbar', { name: 'Active share' }),
    ).toBeInTheDocument()

    // Card 2 — Average Per-Pax Rate (KPI-26).
    const averageCard = screen.getByText('Average Per-Pax Rate').closest('article') as HTMLElement
    expect(within(averageCard).getByText('₱1,225.00')).toBeInTheDocument()
    expect(within(averageCard).getByText('Across 6 priced tiers')).toBeInTheDocument()

    // Card 3 — Per-Pax Rate Range (KPI-27).
    const rangeCard = screen.getByText('Per-Pax Rate Range').closest('article') as HTMLElement
    expect(within(rangeCard).getByText('₱500.00 to ₱1,850.00')).toBeInTheDocument()

    // Card 4 — Minimum Headcount (KPI-28).
    const headcountCard = screen.getByText('Minimum Headcount').closest('article') as HTMLElement
    expect(within(headcountCard).getByText('40')).toBeInTheDocument()
    expect(within(headcountCard).getByText('Highest floor: 150 heads')).toBeInTheDocument()

    // The type tabs carry the catalogue's live counts (AC-P03).
    expect(screen.getByRole('button', { name: 'All Tiers (7)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Wedding (1)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Regular (2)' })).toBeInTheDocument()

    // The tier cards carry the server's rate.
    const grid = await screen.findByTestId('package-tier-grid')
    expect(within(grid).getByText('Wedding Grand Tier')).toBeInTheDocument()
    expect(within(grid).getByText('₱1,450.00')).toBeInTheDocument()
    expect(within(grid).getByText('₱1,850.00')).toBeInTheDocument()
    // The unpriced tier shows an em dash, never a zero.
    expect(within(grid).getByText('Morning Briefing Tier')).toBeInTheDocument()
  })

  it('renders a null summary figure as an em dash, never a zero', async () => {
    mockGet((url) =>
      url.includes('/packages/summary')
        ? unpricedPackageSummaryFixture
        : unpricedPackageTiersFixture,
    )

    renderCatalogue()

    await screen.findByText('Average Per-Pax Rate')

    const averageCard = screen.getByText('Average Per-Pax Rate').closest('article') as HTMLElement
    expect(within(averageCard).getAllByText('—').length).toBeGreaterThan(0)
    expect(within(averageCard).queryByText('₱0.00')).not.toBeInTheDocument()
    expect(within(averageCard).queryByText('0.00')).not.toBeInTheDocument()

    const rangeCard = screen.getByText('Per-Pax Rate Range').closest('article') as HTMLElement
    expect(within(rangeCard).getByText('—')).toBeInTheDocument()
    expect(within(rangeCard).queryByText('₱0.00 to ₱0.00')).not.toBeInTheDocument()

    const headcountCard = screen.getByText('Minimum Headcount').closest('article') as HTMLElement
    expect(within(headcountCard).getAllByText('—').length).toBeGreaterThan(0)
    expect(within(headcountCard).queryByText('0')).not.toBeInTheDocument()
  })

  it('renders every state chip with its text label, never colour alone', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    const grid = await screen.findByTestId('package-tier-grid')
    const chips = within(grid).getAllByText(/^(Active|Inactive)$/)
    expect(chips.length).toBeGreaterThan(0)
    expect(chips.some((chip) => chip.textContent?.trim() === 'Active')).toBe(true)
    expect(chips.some((chip) => chip.textContent?.trim() === 'Inactive')).toBe(true)
  })

  it('loads the selected tier into the inspector', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    const grid = await screen.findByTestId('package-tier-grid')
    expect(within(grid).getByText('Wedding Grand Tier')).toBeInTheDocument()

    // The inspector falls back to the first card of the current ordering.
    const inspector = screen.getByTestId('package-tier-inspector')
    expect(within(inspector).getByText('VIP Degustation & Special Service')).toBeInTheDocument()
    expect(within(inspector).getByText('₱1,850.00')).toBeInTheDocument()
    expect(within(inspector).getByText('30 to 49 Pax')).toBeInTheDocument()
    // The identity is the human-readable code generated when the tier was created.
    expect(within(inspector).getByText('PKG-VIP-01')).toBeInTheDocument()

    await userEvent.click(
      screen.getByRole('button', { name: /PM Merienda Buffet, rate ₱500\.00 per guest/ }),
    )

    const updated = screen.getByTestId('package-tier-inspector')
    expect(within(updated).getByText('PM Merienda Buffet')).toBeInTheDocument()
    expect(within(updated).getByText('₱500.00')).toBeInTheDocument()
    expect(within(updated).getByText('15 to 19 Pax')).toBeInTheDocument()
    expect(within(updated).queryByText('30 to 49 Pax')).not.toBeInTheDocument()
    expect(within(updated).getByText('PKG-PM-01')).toBeInTheDocument()
  })

  it('renders the exact API classification in card content and accessible names', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    const weddingCard = await screen.findByRole('button', {
      name: /Wedding Grand Tier.*applicable pax classification: 80 to 100 Above/,
    })
    expect(within(weddingCard).getByText('80 to 100 Above')).toBeInTheDocument()
  })

  it('shows a pending classification state without blocking catalogue interaction', async () => {
    let resolveLookup!: (value: unknown) => void
    const pendingLookup = new Promise<unknown>((resolve) => {
      resolveLookup = resolve
    })
    mockGet((url) =>
      url === '/packages/pax-applicables' ? pendingLookup : defaultResolver(url),
    )

    renderCatalogue()

    const grid = await screen.findByTestId('package-tier-grid')
    const weddingCard = within(grid).getByRole('button', { name: /Wedding Grand Tier/ })
    expect(within(weddingCard).getByText('Loading classification…')).toBeInTheDocument()
    expect(weddingCard).toBeEnabled()
    expect(
      within(screen.getByTestId('package-tier-inspector')).getByText(
        'Loading classification…',
      ),
    ).toBeInTheDocument()

    resolveLookup(packagePaxApplicablesFixture)
    expect(await within(weddingCard).findByText('80 to 100 Above')).toBeInTheDocument()
  })

  it('shows an explicit unavailable state for an unmatched ID without blocking selection', async () => {
    mockGet((url) =>
      url === '/packages/pax-applicables'
        ? packagePaxApplicablesFixture.filter((item) => item.numPaxId !== 4)
        : defaultResolver(url),
    )

    renderCatalogue()

    const grid = await screen.findByTestId('package-tier-grid')
    const weddingCard = within(grid).getByRole('button', { name: /Wedding Grand Tier/ })
    expect(within(weddingCard).getByText('Classification unavailable')).toBeInTheDocument()
    await userEvent.click(weddingCard)
    expect(
      within(screen.getByTestId('package-tier-inspector')).getByText(
        'Classification unavailable',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Edit Tier' })).toBeEnabled()
  })

  it('shows lookup failure as unavailable while keeping the catalogue usable', async () => {
    mockGet((url) => {
      if (url === '/packages/pax-applicables') {
        throw new ApiError(400, ['The lookup could not be loaded.'])
      }
      return defaultResolver(url)
    })

    renderCatalogue()

    const grid = await screen.findByTestId('package-tier-grid')
    const weddingCard = within(grid).getByRole('button', { name: /Wedding Grand Tier/ })
    expect(within(weddingCard).getByText('Classification unavailable')).toBeInTheDocument()
    expect(screen.queryByText('The packages catalogue could not be loaded')).not.toBeInTheDocument()
    await userEvent.click(weddingCard)
    expect(screen.getByRole('button', { name: 'Edit Tier' })).toBeEnabled()
  })

  it('shows the remaining capability gaps and fabricates no course, venue or margin content', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    const inspector = await screen.findByTestId('package-tier-inspector')

    // The course-allocation and venue-coverage gaps are gone: REQ-PKG-02
    // publishes both, so they are workbench actions now (DEC-084).
    expect(within(inspector).queryByText('Course allocations unavailable')).not.toBeInTheDocument()
    expect(within(inspector).queryByText('Venue coverage unavailable')).not.toBeInTheDocument()
    expect(within(inspector).getByRole('button', { name: 'Course Rules' })).toBeInTheDocument()
    expect(
      within(inspector).getByRole('button', { name: 'Location Coverage' }),
    ).toBeInTheDocument()

    // The food-cost gap is narrowed to the figure that is still unavailable.
    expect(
      within(inspector).getByText('Stored per-tier food cost unavailable'),
    ).toBeInTheDocument()
    expect(within(inspector).getByText('Event inclusions unavailable')).toBeInTheDocument()
    expect(within(inspector).getAllByTestId('capability-gap-notice')).toHaveLength(2)

    // Nothing is faked: no course matrix, no venue list, no margin figure.
    expect(screen.queryByText(/Course Allocations Matrix/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Venue Coverage:/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Gross Margin:/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Food Cost:/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/GP Target/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('row')).not.toBeInTheDocument()
    expect(screen.queryByRole('cell')).not.toBeInTheDocument()
  })

  it('opens the course-rules workbench from the inspector', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    const inspector = await screen.findByTestId('package-tier-inspector')

    await userEvent.click(within(inspector).getByRole('button', { name: 'Course Rules' }))

    expect(await screen.findByText('Course rules workbench')).toBeInTheDocument()
  })

  it('opens the location matrix from the inspector', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    const inspector = await screen.findByTestId('package-tier-inspector')

    await userEvent.click(
      within(inspector).getByRole('button', { name: 'Location Coverage' }),
    )

    expect(await screen.findByText('Location matrix workbench')).toBeInTheDocument()
  })

  it('renders distinct empty states for an empty catalogue and an empty filter result', async () => {
    mockGet((url) =>
      url.includes('/packages/summary')
        ? emptyPackageSummaryFixture
        : emptyPackageTiersFixture,
    )

    renderCatalogue()

    expect(await screen.findByText('The packages catalogue is empty')).toBeInTheDocument()
    expect(screen.queryByText('No tiers match the current filters')).not.toBeInTheDocument()
    expect(screen.queryByTestId('package-tier-grid')).not.toBeInTheDocument()
  })

  it('names the active filter when the filter matches no tier', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    await screen.findByTestId('package-tier-grid')

    await userEvent.type(screen.getByLabelText('Search packages'), 'no-such-tier')

    expect(await screen.findByText('No tiers match the current filters')).toBeInTheDocument()
    expect(screen.queryByText('The packages catalogue is empty')).not.toBeInTheDocument()
    expect(screen.queryByTestId('package-tier-grid')).not.toBeInTheDocument()
    expect(
      screen.getByText('Nothing in the catalogue matches the search “no-such-tier”.'),
    ).toBeInTheDocument()
  })

  it('narrows the catalogue by tier type', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    await screen.findByTestId('package-tier-grid')

    await userEvent.click(screen.getByRole('button', { name: 'Wedding (1)' }))

    const grid = screen.getByTestId('package-tier-grid')
    expect(within(grid).getByText('Wedding Grand Tier')).toBeInTheDocument()
    expect(within(grid).queryByText('PM Merienda Buffet')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Wedding (1)' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

  it('renders the error state with a retry and never fabricates content', async () => {
    const getSpy = vi.spyOn(apiClient, 'get')
    getSpy.mockRejectedValue(new ApiError(500, ['The catalogue could not be reconciled.']))

    renderCatalogue()

    // A 5xx is retried once by the query before it surfaces.
    const alert = await screen.findByRole('alert', undefined, { timeout: 8000 })
    expect(within(alert).getByText('The catalogue could not be reconciled.')).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: /retry/i })).toBeInTheDocument()
    // Nothing is invented while the catalogue is unavailable.
    expect(screen.queryByText('Wedding Grand Tier')).not.toBeInTheDocument()
    expect(screen.queryByTestId('package-tier-grid')).not.toBeInTheDocument()

    getSpy.mockImplementation(
      async (url: string): Promise<AxiosResponse> => ({
        data: defaultResolver(url),
        status: 200,
        statusText: 'OK',
        headers: {},
        config: { headers: {} } as AxiosResponse['config'],
      }),
    )

    await userEvent.click(within(alert).getByRole('button', { name: /retry/i }))

    const grid = await screen.findByTestId('package-tier-grid')
    expect(within(grid).getByText('Wedding Grand Tier')).toBeInTheDocument()
  })

  it('toggles the active state through the endpoint and refreshes the catalogue', async () => {
    const patchSpy = vi.spyOn(apiClient, 'patch')
    mockGet(defaultResolver)

    renderCatalogue()

    await screen.findByTestId('package-tier-inspector')

    await userEvent.click(
      screen.getByRole('button', { name: /Wedding Grand Tier, rate ₱1,450\.00 per guest/ }),
    )

    await userEvent.click(screen.getByRole('button', { name: 'Deactivate Tier' }))

    await waitFor(() => {
      expect(patchSpy).toHaveBeenCalledWith('/packages/12/toggle-active')
    })
  })

  it('labels the toggle action after the tier state and opens the edit route', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    await screen.findByTestId('package-tier-inspector')

    // The selected tier is active, so the action offers to deactivate it.
    expect(screen.getByRole('button', { name: 'Deactivate Tier' })).toBeInTheDocument()

    // An inactive tier offers to be activated instead.
    await userEvent.click(
      screen.getByRole('button', { name: /Morning Briefing Tier, no per-guest rate set/ }),
    )
    expect(screen.getByRole('button', { name: 'Activate Tier' })).toBeInTheDocument()

    await userEvent.click(
      screen.getByRole('button', { name: /Wedding Grand Tier, rate ₱1,450\.00 per guest/ }),
    )
    await userEvent.click(screen.getByRole('button', { name: 'Edit Tier' }))

    expect(await screen.findByText('Tier edit form')).toBeInTheDocument()
  })

  it('opens the create route from the banner action', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    await screen.findByTestId('package-tier-grid')

    await userEvent.click(screen.getAllByRole('button', { name: '+ New Package' })[0])

    expect(await screen.findByText('New tier form')).toBeInTheDocument()
  })

  it('omits the affordances the approved specification places out of scope', async () => {
    mockGet(defaultResolver)

    renderCatalogue()

    await screen.findByTestId('package-tier-grid')

    // No packages export, no duplicate action, no SignalR panel and no
    // QuestPDF telemetry (DEC-071).
    expect(screen.queryByRole('button', { name: /export/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /duplicate/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/SignalR/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/QuestPDF/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /archive/i })).not.toBeInTheDocument()
  })
})
