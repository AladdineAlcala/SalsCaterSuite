import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosRequestConfig, AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { MenuCostingMasterPage } from '@/features/menus/MenuCostingMasterPage'
import type { MenuMasterResponse } from '@/types/menu.types'
import {
  emptyMenuCatalogueFixture,
  menuMasterFixture,
  menuNullSummaryFixture,
  noMatchMenuMasterFixture,
} from '@/test/menuFixture'
import { createTestQueryClient } from '@/test/testHelpers'

type GetResolver = (url: string) => unknown

function renderMenuMaster() {
  const queryClient = createTestQueryClient()
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <MenuCostingMasterPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient }
}

/** Routes the mocked GET calls to the fixture that matches the endpoint. */
function mockGet(resolver: GetResolver) {
  const implementation = async (url: string): Promise<AxiosResponse> => ({
    data: resolver(url),
    status: 200,
    statusText: 'OK',
    headers: {},
    config: { headers: {} } as AxiosResponse['config'],
  })

  return vi.spyOn(apiClient, 'get').mockImplementation(implementation)
}

function defaultResolver(url: string): unknown {
  if (url.includes('/master/export')) return new Blob(['xlsx'])
  if (url.includes('/menus/master')) return menuMasterFixture
  return {}
}

describe('MenuCostingMasterPage', () => {
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

  it('renders the KPI cards, the tabs, the card grid and the pagination from the payload', async () => {
    mockGet(defaultResolver)

    renderMenuMaster()

    expect(
      await screen.findByRole('heading', { name: 'Menu & Food Costing Master' }),
    ).toBeInTheDocument()

    // The benchmark badge publishes the server's target (DEC-040).
    expect(await screen.findByText('Benchmark Target: ≤ 32.0%')).toBeInTheDocument()

    // Card 1 — Total Menu Items (KPI-14).
    const totalCard = screen.getByText('Total Menu Items').closest('article') as HTMLElement
    expect(within(totalCard).getByText('181')).toBeInTheDocument()
    expect(within(totalCard).getByText('142 Costed • 39 Uncosted')).toBeInTheDocument()

    // Card 2 — Average Food Cost (KPI-22).
    const averageCard = screen.getByText('Average Food Cost').closest('article') as HTMLElement
    expect(within(averageCard).getByText('28.6%')).toBeInTheDocument()
    expect(within(averageCard).getByText('+3.40 pp headroom')).toBeInTheDocument()
    expect(within(averageCard).getByText('Target ≤ 32.0%')).toBeInTheDocument()

    // Card 3 — Best-Margin Course (KPI-23).
    const bestCard = screen.getByText('Best-Margin Course').closest('article') as HTMLElement
    expect(within(bestCard).getByText('74.2% margin • ₱485.00 avg')).toBeInTheDocument()

    // Card 4 — Margin Alerts (KPI-24). The reference's Cost Volatility card
    // is not implementable and is replaced (DEC-060).
    const alertsCard = screen.getByText('Margin Alerts').closest('article') as HTMLElement
    expect(within(alertsCard).getByText('6')).toBeInTheDocument()
    expect(
      within(alertsCard).getByText('dishes above the 32.0% food-cost target'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/Cost Volatility/i)).not.toBeInTheDocument()

    // The course tabs carry the catalogue's live counts (DEC-061).
    expect(screen.getByRole('button', { name: 'All Dishes (181)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Appetizer (12)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Main Course (42)' })).toBeInTheDocument()

    // The card grid.
    const grid = await screen.findByTestId('menu-dish-grid')
    expect(within(grid).getByText('DS-104')).toBeInTheDocument()
    expect(
      within(grid).getByText('Prime Roast Angus Ribeye with Truffle Demi-Glace'),
    ).toBeInTheDocument()
    expect(within(grid).getByText('₱420.00')).toBeInTheDocument()
    expect(within(grid).getByText('₱281.40')).toBeInTheDocument()
    expect(within(grid).getByText('67.0% GP')).toBeInTheDocument()

    // The pagination footer.
    expect(screen.getByText(/of 181 recipes/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeInTheDocument()
  })

  it('renders the server’s percentage and never one it computed itself', async () => {
    mockGet(defaultResolver)

    renderMenuMaster()

    // The catalogue average is the server's KPI-22. Summing the visible
    // dishes would produce a different figure, so the client cannot have
    // derived it (DEC-002).
    expect(await screen.findByText('28.6%')).toBeInTheDocument()
    expect(screen.queryByText('29.3%')).not.toBeInTheDocument()

    // The dish's own food-cost percentage is the server's too.
    expect(screen.getByText('33.0% · Above target')).toBeInTheDocument()
  })

  it('renders a null summary figure as an em dash, never a zero', async () => {
    mockGet((url) =>
      url.includes('/menus/master') ? menuNullSummaryFixture : defaultResolver(url),
    )

    renderMenuMaster()

    await screen.findByText('Total Menu Items')

    const averageCard = screen.getByText('Average Food Cost').closest('article') as HTMLElement
    expect(within(averageCard).getAllByText('—').length).toBeGreaterThan(0)
    expect(within(averageCard).queryByText('0.0%')).not.toBeInTheDocument()
    expect(within(averageCard).queryByText('0%')).not.toBeInTheDocument()

    const bestCard = screen.getByText('Best-Margin Course').closest('article') as HTMLElement
    expect(within(bestCard).getAllByText('—').length).toBeGreaterThan(0)
    expect(within(bestCard).queryByText('Main Course')).not.toBeInTheDocument()
  })

  it('renders every status chip with its text label, never colour alone', async () => {
    mockGet(defaultResolver)

    renderMenuMaster()

    const chip = await screen.findByText('33.0% · Above target')
    expect(chip).toBeInTheDocument()
    expect(chip.textContent?.trim()).toBe('33.0% · Above target')

    const onTarget = screen.getByText('27.5% · On target')
    expect(onTarget.textContent?.trim()).toBe('27.5% · On target')
  })

  it('loads the selected dish into the inspector', async () => {
    mockGet(defaultResolver)

    renderMenuMaster()

    const grid = await screen.findByTestId('menu-dish-grid')
    expect(within(grid).getByText('DS-104')).toBeInTheDocument()

    const inspector = screen.getByTestId('menu-recipe-inspector')
    expect(
      within(inspector).getByText('Prime Roast Angus Ribeye with Truffle Demi-Glace'),
    ).toBeInTheDocument()
    expect(within(inspector).getByText(/33\.0%/)).toBeInTheDocument()
    expect(within(inspector).getByText('Target ≤ 32.0% (+1.00 pp variance)')).toBeInTheDocument()

    await userEvent.click(
      screen.getByRole('button', { name: /Baked Jumbo Prawns Thermidor/ }),
    )

    const updated = screen.getByTestId('menu-recipe-inspector')
    expect(
      within(updated).getByText('Baked Jumbo Prawns Thermidor with Gruyère Crust'),
    ).toBeInTheDocument()
  })

  it('shows the capability gap where the ingredient breakdown would be and fakes no rows', async () => {
    mockGet(defaultResolver)

    renderMenuMaster()

    const inspector = await screen.findByTestId('menu-recipe-inspector')

    expect(
      within(inspector).getByText('Ingredient & batch breakdown unavailable'),
    ).toBeInTheDocument()
    expect(within(inspector).getByText('Yield calibration unavailable')).toBeInTheDocument()
    expect(within(inspector).getByText('Plating specification unavailable')).toBeInTheDocument()
    expect(within(inspector).getAllByTestId('capability-gap-notice').length).toBeGreaterThan(0)

    // No ingredient table, no ingredient row, no fabricated line item.
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('row')).not.toBeInTheDocument()
    expect(screen.queryByRole('cell')).not.toBeInTheDocument()
  })

  it('renders distinct empty states for an empty catalogue and an empty filter result', async () => {
    mockGet((url) =>
      url.includes('/menus/master') ? emptyMenuCatalogueFixture : defaultResolver(url),
    )

    renderMenuMaster()

    expect(await screen.findByText('The menu catalogue is empty')).toBeInTheDocument()
    expect(screen.queryByText('No dishes match the current filters')).not.toBeInTheDocument()
    expect(screen.queryByTestId('menu-dish-grid')).not.toBeInTheDocument()
  })

  it('names the active filter when the filter matches no dish', async () => {
    mockGet((url) =>
      url.includes('/menus/master') ? noMatchMenuMasterFixture : defaultResolver(url),
    )

    renderMenuMaster()

    expect(await screen.findByText('No dishes match the current filters')).toBeInTheDocument()
    expect(screen.queryByText('The menu catalogue is empty')).not.toBeInTheDocument()
    expect(screen.queryByTestId('menu-dish-grid')).not.toBeInTheDocument()

    // The catalogue holds dishes — the scope is what excludes them.
    await userEvent.click(screen.getByRole('button', { name: 'Appetizer (12)' }))

    expect(
      await screen.findByText('Nothing in the catalogue matches the Appetizer course.'),
    ).toBeInTheDocument()
    expect(screen.queryByText('The menu catalogue is empty')).not.toBeInTheDocument()
  })

  it('keeps the “All Dishes” count on the pre-course count when a course is selected', async () => {
    const getSpy = vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string, config?: AxiosRequestConfig): Promise<AxiosResponse> => {
        if (!url.includes('/menus/master')) {
          return {
            data: defaultResolver(url),
            status: 200,
            statusText: 'OK',
            headers: {},
            config: { headers: {} } as AxiosResponse['config'],
          }
        }

        const courseId = (config?.params as { courseId?: number } | undefined)?.courseId
        // The API publishes the fully filtered summary, so the KPI total
        // follows the course — while `courses.total` stays on the pre-course
        // count of the search- and status-filtered scope.
        const payload: MenuMasterResponse = courseId
          ? {
              ...menuMasterFixture,
              summary: { ...menuMasterFixture.summary, totalDishes: 12 },
            }
          : menuMasterFixture

        return {
          data: payload,
          status: 200,
          statusText: 'OK',
          headers: {},
          config: { headers: {} } as AxiosResponse['config'],
        }
      },
    )

    renderMenuMaster()

    // The tab count is `courses.total`, not `summary.totalDishes`.
    expect(await screen.findByRole('button', { name: 'All Dishes (181)' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Appetizer (12)' }))

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/menus/master',
        expect.objectContaining({ params: expect.objectContaining({ courseId: 1 }) }),
      )
    })

    // The tab count does not move — it is the pre-course count.
    expect(screen.getByRole('button', { name: 'All Dishes (181)' })).toBeInTheDocument()

    // The KPI total follows the fully filtered set.
    const totalCard = screen.getByText('Total Menu Items').closest('article') as HTMLElement
    expect(within(totalCard).getByText('12')).toBeInTheDocument()
  })

  it('renders the error state with a retry and never fabricates content', async () => {
    const getSpy = vi.spyOn(apiClient, 'get')
    getSpy.mockRejectedValue(new ApiError(500, ['The catalogue could not be reconciled.']))

    renderMenuMaster()

    // A 5xx is retried once by the query before it surfaces.
    const alert = await screen.findByRole('alert', undefined, { timeout: 8000 })
    expect(within(alert).getByText('The catalogue could not be reconciled.')).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: /retry/i })).toBeInTheDocument()
    // Nothing is invented while the catalogue is unavailable.
    expect(screen.queryByText('DS-104')).not.toBeInTheDocument()

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

    const grid = await screen.findByTestId('menu-dish-grid')
    expect(within(grid).getByText('DS-104')).toBeInTheDocument()
  })

  it('calls the API with the new page and the new page size', async () => {
    const getSpy = mockGet(defaultResolver)

    renderMenuMaster()

    await screen.findByTestId('menu-dish-grid')

    await userEvent.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/menus/master',
        expect.objectContaining({ params: expect.objectContaining({ page: 2 }) }),
      )
    })

    await userEvent.selectOptions(screen.getByLabelText('Page size:'), '24')

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/menus/master',
        expect.objectContaining({
          params: expect.objectContaining({ page: 1, pageSize: 24 }),
        }),
      )
    })
  })

  it('carries the active filters into the export request', async () => {
    const getSpy = mockGet(defaultResolver)

    renderMenuMaster()

    await screen.findByTestId('menu-dish-grid')

    await userEvent.selectOptions(screen.getByLabelText('Status'), 'MarginAlert')
    await userEvent.selectOptions(screen.getByLabelText('Sort'), 'PriceDesc')

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/menus/master',
        expect.objectContaining({
          params: expect.objectContaining({ status: 'MarginAlert', sort: 'PriceDesc' }),
        }),
      )
    })

    await userEvent.click(screen.getByRole('button', { name: 'Export Excel' }))

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/menus/master/export',
        expect.objectContaining({
          params: expect.objectContaining({ status: 'MarginAlert', sort: 'PriceDesc' }),
        }),
      )
    })
  })

  it('sends the course scope to the API when a course tab is chosen', async () => {
    const getSpy = mockGet(defaultResolver)

    renderMenuMaster()

    await screen.findByTestId('menu-dish-grid')

    await userEvent.click(screen.getByRole('button', { name: 'Appetizer (12)' }))

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/menus/master',
        expect.objectContaining({ params: expect.objectContaining({ courseId: 1 }) }),
      )
    })
  })

  it('omits the elements the approved specification places out of scope', async () => {
    mockGet(defaultResolver)

    renderMenuMaster()

    await screen.findByTestId('menu-dish-grid')

    // The reference's Columns button, Print action, price simulation and
    // volatility card are absent.
    expect(screen.queryByRole('button', { name: /Columns/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Print/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/Simulate/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/VIP Tier/i)).not.toBeInTheDocument()
    // There is no portion attribute on a dish.
    expect(screen.queryByText(/Portion:/i)).not.toBeInTheDocument()
  })
})
