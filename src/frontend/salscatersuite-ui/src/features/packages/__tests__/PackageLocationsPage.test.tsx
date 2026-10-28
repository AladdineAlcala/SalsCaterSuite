import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { PackageLocationsPage } from '@/features/packages/PackageLocationsPage'
import {
  areaCatalogueFixture,
  emptyPackageAreaCoverageMatrixFixture,
  packageAreaCoverageMatrixFixture,
  packageCourseRulesetFixture,
  packageLocationPreviewFixture,
  packageLocationPreviewNoLogisticsFixture,
  packageLocationPreviewNoRateFixture,
} from '@/test/packageWorkbenchFixture'
import { createTestQueryClient } from '@/test/testHelpers'

function axiosResponse(data: unknown, status = 200): AxiosResponse {
  return {
    data,
    status,
    statusText: 'OK',
    headers: {},
    config: { headers: {} } as AxiosResponse['config'],
  }
}

function renderLocationsPage(route = '/packages/12/locations') {
  const queryClient = createTestQueryClient()
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="/packages" element={<div>Package catalogue</div>} />
          <Route path="/packages/:packageId/rules" element={<div>Course rules workbench</div>} />
          <Route path="/packages/:packageId/locations" element={<PackageLocationsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient }
}

function defaultGet(url: string): unknown {
  if (url === '/areas') return areaCatalogueFixture
  if (url.includes('/course-rules')) return packageCourseRulesetFixture
  if (url.includes('/spec-sheet')) return new Blob(['pdf'], { type: 'application/pdf' })
  if (url.includes('/areas')) return packageAreaCoverageMatrixFixture
  return {}
}

function defaultPost(url: string, body: unknown): unknown {
  if (url.includes('/areas/preview')) {
    const areaId = (body as { areaId: number | null }).areaId
    if (areaId === 6) return packageLocationPreviewNoRateFixture
    if (areaId === 5) return packageLocationPreviewNoLogisticsFixture
    return packageLocationPreviewFixture
  }
  return { packageAreaCoverageId: 900 }
}

interface MockOptions {
  get?: (url: string) => unknown
  post?: (url: string, body: unknown) => unknown
}

function mockApi(options: MockOptions = {}) {
  vi.spyOn(apiClient, 'get').mockImplementation(
    async (url: string): Promise<AxiosResponse> =>
      axiosResponse(options.get ? options.get(url) : defaultGet(url)),
  )
  vi.spyOn(apiClient, 'post').mockImplementation(
    async (url: string, body?: unknown): Promise<AxiosResponse> =>
      axiosResponse(options.post ? options.post(url, body) : defaultPost(url, body)),
  )
}

/**
 * A gate over the location preview endpoint.
 *
 * It lets a test answer the quote normally, **hold** the next batch open, and
 * release it — which is how the pending (recalculating) state is exercised
 * without depending on timing.
 *
 * The handler returns the **payload**, because the api mock wraps whatever it
 * returns in the axios envelope.
 */
function createLocationPreviewGate() {
  let holding = false
  const held: Array<{ areaId: number | null; resolve: (value: unknown) => void }> = []

  const payloadFor = (areaId: number | null): unknown => {
    if (areaId === 6) return packageLocationPreviewNoRateFixture
    if (areaId === 5) return packageLocationPreviewNoLogisticsFixture
    return packageLocationPreviewFixture
  }

  return {
    /** Hold every quote instead of answering it. */
    hold() {
      holding = true
    },
    /** Answer the quote requests that were held. */
    release() {
      holding = false
      held.splice(0, held.length).forEach((entry) => entry.resolve(payloadFor(entry.areaId)))
    },
    /** The quote request handler for the api mock. */
    handle(url: string, body: unknown): unknown {
      if (!url.includes('/areas/preview')) return {}
      const areaId = (body as { areaId: number | null }).areaId
      if (holding) {
        return new Promise((resolve) => {
          held.push({ areaId, resolve })
        })
      }
      return payloadFor(areaId)
    },
  }
}

describe('PackageLocationsPage', () => {
  beforeEach(() => {
    mockApi()
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:spec-sheet'),
      configurable: true,
    })
    Object.defineProperty(URL, 'revokeObjectURL', {
      value: vi.fn(),
      configurable: true,
    })
    // The download anchor is clicked for real; jsdom cannot navigate to a blob,
    // so the click is stubbed to keep the run free of a jsdom notice.
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
  })

  it('renders the four-card strip, the matrix, the catalogue and the simulator from the payloads', async () => {
    renderLocationsPage()

    expect(
      await screen.findByRole('heading', { name: 'Location & Out-of-Town Add-On Matrix' }),
    ).toBeInTheDocument()
    expect(
      await screen.findByText('Wedding Grand Tier — 3 covered municipalities, 2 of them active.'),
    ).toBeInTheDocument()

    // Card 1 — Minimum.
    const minimumCard = screen.getByText('Minimum').closest('article') as HTMLElement
    expect(within(minimumCard).getByText('150')).toBeInTheDocument()
    expect(within(minimumCard).getByText('heads and above')).toBeInTheDocument()
    expect(within(minimumCard).getByText('Minimum contract value ₱217,500.00')).toBeInTheDocument()

    // Card 2 — Base tariff.
    const tariffCard = screen.getByText('Base Tariff').closest('article') as HTMLElement
    expect(within(tariffCard).getByText('₱1,450.00')).toBeInTheDocument()
    expect(within(tariffCard).getByText('per guest')).toBeInTheDocument()

    // Card 3 — Out-of-town coverage.
    const coverageCard = screen.getByText('Out-of-Town Coverage').closest('article') as HTMLElement
    expect(within(coverageCard).getByText('2')).toBeInTheDocument()
    expect(within(coverageCard).getByText('Surcharge range ₱120.00 to ₱350.00')).toBeInTheDocument()

    // Card 4 — the gross-profit safeguard, with its inputs and its contributor count.
    const safeguardCard = screen.getByText('Gross Profit Safeguard').closest('article') as HTMLElement
    expect(within(safeguardCard).getByText('77.29%')).toBeInTheDocument()
    expect(
      within(safeguardCard).getByText('Logistics-adjusted range 58.40% to 71.90%'),
    ).toBeInTheDocument()
    expect(
      within(safeguardCard).getByText(
        "Food cost is the standard run, the headcount is the tier's floor, and 2 covered rows contributed.",
      ),
    ).toBeInTheDocument()

    // The catalogue panel.
    const catalogue = await screen.findByTestId('package-area-catalogue')
    expect(within(catalogue).getByText('Antipolo City')).toBeInTheDocument()
    expect(within(catalogue).getByText('Tagaytay City')).toBeInTheDocument()

    // The course-allocation card.
    const allocation = screen.getByTestId('package-course-allocation')
    expect(within(allocation).getByText('Beef')).toBeInTheDocument()
    expect(within(allocation).getByText('Quota 2')).toBeInTheDocument()
  })

  it('shows each covered municipality’s rate, distance, dispatch note and coverage state, and says the rate is the municipality’s', async () => {
    renderLocationsPage()

    const matrix = await screen.findByTestId('package-coverage-matrix')
    expect(within(matrix).getByText('Antipolo City')).toBeInTheDocument()
    expect(within(matrix).getByText('₱120.00')).toBeInTheDocument()
    expect(within(matrix).getByText('18 km')).toBeInTheDocument()
    expect(within(matrix).getByText('Reefer Truck')).toBeInTheDocument()
    expect(within(matrix).getAllByText('Coverage active').length).toBeGreaterThan(0)
    expect(within(matrix).getAllByText('Coverage inactive').length).toBeGreaterThan(0)
    expect(
      within(matrix).getByText(/shared by every tier that covers it/),
    ).toBeInTheDocument()

    // The table carries a caption and column headers.
    expect(within(matrix).getByRole('table')).toBeInTheDocument()
    expect(within(matrix).getAllByRole('columnheader').length).toBeGreaterThan(0)
  })

  it('gives every row action an accessible name that includes the municipality', async () => {
    renderLocationsPage()

    const matrix = await screen.findByTestId('package-coverage-matrix')
    expect(
      within(matrix).getByRole('button', { name: 'Deactivate the coverage of Antipolo City' }),
    ).toBeInTheDocument()
    expect(
      within(matrix).getByRole('button', { name: 'Remove Antipolo City from the coverage' }),
    ).toBeInTheDocument()
    expect(
      within(matrix).getByRole('button', { name: 'Edit the municipality Antipolo City' }),
    ).toBeInTheDocument()
  })

  it('shows the server’s breakdown at the tier’s headcount floor', async () => {
    renderLocationsPage()

    const simulator = await screen.findByTestId('package-location-simulator')
    await waitFor(() => {
      expect(within(simulator).getByText('₱217,500.00')).toBeInTheDocument()
    })
    expect(within(simulator).getByText('₱18,000.00')).toBeInTheDocument()
    expect(within(simulator).getByText('₱1,570.00')).toBeInTheDocument()
    expect(within(simulator).getByText('₱235,500.00')).toBeInTheDocument()
    expect(within(simulator).getByText('₱182,912.50')).toBeInTheDocument()
    expect(within(simulator).getByText('77.67%')).toBeInTheDocument()
    // The logistics band is present because the municipality states one.
    expect(within(simulator).getByText('₱3,200.00')).toBeInTheDocument()
  })

  it('omits the logistics band when the municipality states no logistics cost', async () => {
    renderLocationsPage()

    const simulator = await screen.findByTestId('package-location-simulator')
    await screen.findByTestId('package-location-simulator')

    await userEvent.selectOptions(screen.getByLabelText('Municipality'), '5')

    await waitFor(() => {
      expect(within(simulator).getByText('₱270,000.00')).toBeInTheDocument()
    })
    expect(within(simulator).queryByText('Logistics burn')).not.toBeInTheDocument()
    // The gross profit is unknown because an operand is unknown.
    expect(within(simulator).queryByText('₱0.00')).not.toBeInTheDocument()
  })

  it('shows a municipality with no stated rate as “not stated” while the contract value stays complete', async () => {
    renderLocationsPage()

    const simulator = await screen.findByTestId('package-location-simulator')
    await userEvent.selectOptions(screen.getByLabelText('Municipality'), '6')

    await waitFor(() => {
      expect(within(simulator).getAllByText('₱217,500.00').length).toBeGreaterThan(0)
    })
    expect(within(simulator).getByText(/not stated per guest/)).toBeInTheDocument()
    expect(within(simulator).getByText('₱1,450.00')).toBeInTheDocument()

    /*
     * The em-dash-**as-unknown** rule is untouched: the gross profit and the
     * gross-profit share are `null` for this municipality (it states no
     * logistics cost), so once the quote has arrived they render the em dash —
     * *unknown* — and never the recalculating caption.
     */
    expect(within(simulator).queryAllByText('Recalculating…')).toHaveLength(0)
    expect(within(simulator).getAllByText('—').length).toBeGreaterThan(0)
  })

  it('renders the empty matrix state and never fabricates a municipality', async () => {
    mockApi({
      get: (url) => {
        if (url === '/areas') return areaCatalogueFixture
        if (url === '/packages/12/areas') return emptyPackageAreaCoverageMatrixFixture
        return defaultGet(url)
      },
    })

    renderLocationsPage()

    const matrix = await screen.findByTestId('package-coverage-matrix')
    expect(within(matrix).getByText('This tier covers no municipality')).toBeInTheDocument()
    expect(within(matrix).queryByRole('table')).not.toBeInTheDocument()
  })

  it('sends the whole coverage set on save and applies it', async () => {
    const putSpy = vi
      .spyOn(apiClient, 'put')
      .mockResolvedValue(axiosResponse(packageAreaCoverageMatrixFixture))

    renderLocationsPage()

    const matrix = await screen.findByTestId('package-coverage-matrix')
    await userEvent.click(
      within(matrix).getByRole('button', { name: 'Deactivate the coverage of Antipolo City' }),
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save coverage set' }))

    await waitFor(() => {
      expect(putSpy).toHaveBeenCalledWith('/packages/12/areas', {
        coverages: [
          { areaId: 4, isActive: false },
          { areaId: 5, isActive: true },
          { areaId: 6, isActive: false },
        ],
      })
    })
    expect(await screen.findByText('The coverage set was applied to this tier.')).toBeInTheDocument()
  })

  it('stages a removal and restores it with the revert action', async () => {
    const putSpy = vi.spyOn(apiClient, 'put')

    renderLocationsPage()

    const matrix = await screen.findByTestId('package-coverage-matrix')
    await userEvent.click(
      within(matrix).getByRole('button', { name: 'Remove Antipolo City from the coverage' }),
    )

    expect(within(matrix).getByText('Pending removal')).toBeInTheDocument()
    expect(
      within(matrix).getByRole('button', { name: 'Restore Antipolo City to the coverage' }),
    ).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Revert' }))

    expect(within(matrix).queryByText('Pending removal')).not.toBeInTheDocument()
    expect(putSpy).not.toHaveBeenCalled()
  })

  it('attaches a municipality to the tier', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderLocationsPage()

    const catalogue = await screen.findByTestId('package-area-catalogue')
    await userEvent.click(
      within(catalogue).getByRole('button', { name: 'Attach Tagaytay City to this tier' }),
    )

    await waitFor(() => {
      const call = postSpy.mock.calls.find(
        ([url]) => typeof url === 'string' && url === '/packages/12/areas',
      )
      expect(call).toBeDefined()
      expect(call?.[1]).toEqual({ areaId: 9, isActive: true })
    })
  })

  it('renders the server’s errors verbatim on a rejected coverage save', async () => {
    vi.spyOn(apiClient, 'put').mockRejectedValue(
      new ApiError(400, ['Area 4 is covered more than once.']),
    )

    renderLocationsPage()

    await screen.findByTestId('package-coverage-matrix')
    await userEvent.click(screen.getByRole('button', { name: 'Save coverage set' }))

    expect(await screen.findByText('Area 4 is covered more than once.')).toBeInTheDocument()
  })

  it('creates a municipality and warns that the rate belongs to the municipality', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderLocationsPage()

    await screen.findByTestId('package-area-catalogue')
    await userEvent.click(screen.getByRole('button', { name: 'Add a municipality' }))

    const form = await screen.findByTestId('package-location-form')
    expect(within(form).getByText(/belongs to the/)).toBeInTheDocument()
    expect(
      within(form).getByText(/the extended-area fee is not charged by the as-built code/),
    ).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Municipality name'), 'Cavite City')
    await userEvent.type(screen.getByLabelText('Out-of-town rate per pax'), '150')
    await userEvent.click(screen.getByRole('button', { name: 'Add municipality' }))

    await waitFor(() => {
      const call = postSpy.mock.calls.find(
        ([url]) => typeof url === 'string' && url === '/areas',
      )
      expect(call).toBeDefined()
      expect(call?.[1]).toEqual({
        areaDetails: 'Cavite City',
        additionalRate: 150,
        distanceKm: null,
        logisticsNote: null,
        logisticsCost: null,
      })
    })
  })

  it('rejects a blank municipality name on the client without calling the API', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderLocationsPage()

    await screen.findByTestId('package-area-catalogue')
    await userEvent.click(screen.getByRole('button', { name: 'Add a municipality' }))
    await userEvent.click(await screen.findByRole('button', { name: 'Add municipality' }))

    expect(await screen.findByText('Area name is required.')).toBeInTheDocument()
    expect(
      postSpy.mock.calls.some(([url]) => typeof url === 'string' && url === '/areas'),
    ).toBe(false)
  })

  it('downloads the specification sheet as a PDF', async () => {
    const getSpy = vi.spyOn(apiClient, 'get')

    renderLocationsPage()

    await screen.findByTestId('package-coverage-matrix')
    await userEvent.click(screen.getByRole('button', { name: 'Specification sheet' }))

    await waitFor(() => {
      expect(
        getSpy.mock.calls.some(
          ([url]) => typeof url === 'string' && url.includes('/spec-sheet'),
        ),
      ).toBe(true)
    })
    expect(URL.createObjectURL).toHaveBeenCalled()
  })

  it('renders the capability-gap notice without a requirement footer', async () => {
    renderLocationsPage()

    await screen.findByTestId('package-location-simulator')

    expect(screen.getByText('Mobile logistics checklist unavailable')).toBeInTheDocument()
    expect(screen.getByText('Complimentary staples unavailable')).toBeInTheDocument()
    expect(screen.getByText(/course rules do not link add-ons \(DEC-084, DEC-172\)/)).toBeInTheDocument()
    expect(screen.queryByText(/no add-on is configured against a tier/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Blocked under/)).not.toBeInTheDocument()
  })

  it('omits the affordances the approved specification places out of scope', async () => {
    renderLocationsPage()

    await screen.findByTestId('package-coverage-matrix')

    expect(screen.queryByRole('button', { name: /duplicate/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /publish/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /bulk/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/SignalR/i)).not.toBeInTheDocument()
  })

  it('debounces the headcount so a typed sequence costs one batch of preview requests', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderLocationsPage()

    const previewCalls = () =>
      postSpy.mock.calls.filter(
        ([url]) => typeof url === 'string' && url.includes('/areas/preview'),
      ).length

    // The opening batch prices the three covered municipalities once.
    await waitFor(() => {
      expect(previewCalls()).toBe(3)
    })

    const before = previewCalls()

    const headcount = await screen.findByLabelText('Headcount')
    await userEvent.type(headcount, '250')

    // One batch — one request per covered municipality — not one per keystroke.
    await waitFor(() => {
      expect(previewCalls()).toBe(before + 3)
    })

    // The batch carries the **final** headcount, not an intermediate one.
    const lastCall = postSpy.mock.calls
      .filter(([url]) => typeof url === 'string' && url.includes('/areas/preview'))
      .at(-1)
    expect(lastCall?.[1]).toMatchObject({ pax: 250 })

    // No further batch arrives for the intermediate keystrokes.
    await new Promise((resolve) => window.setTimeout(resolve, 700))
    expect(previewCalls()).toBe(before + 3)
  }, 20000)

  it('shows a recalculating state instead of a stale or zero extended total while the headcount is pending', async () => {
    renderLocationsPage()

    const matrix = await screen.findByTestId('package-coverage-matrix')

    // Every covered row shows the explicit state — one per covered
    // municipality — while the quote is pending.
    expect(await within(matrix).findAllByText('Recalculating…')).toHaveLength(3)

    // No figure priced for a different headcount is presented as the current
    // one, and no zero is shown in its place.
    expect(within(matrix).queryByText('₱18,000.00')).not.toBeInTheDocument()

    // Once the quote arrives the server's figures take their place.
    await waitFor(() => {
      expect(within(matrix).queryByText('Recalculating…')).not.toBeInTheDocument()
    })
    expect(within(matrix).getByText('₱18,000.00')).toBeInTheDocument()
  }, 20000)

  it('shows the recalculating caption in place of each pending breakdown figure', async () => {
    const gate = createLocationPreviewGate()
    mockApi({ post: gate.handle })

    renderLocationsPage()

    const simulator = await screen.findByTestId('package-location-simulator')

    // The opening quote answers normally, so the server's figures are shown.
    await waitFor(() => {
      expect(within(simulator).getByText('₱235,500.00')).toBeInTheDocument()
    })

    // The operator changes the headcount; the next quote is held open.
    gate.hold()
    const headcount = await screen.findByLabelText('Headcount')
    await userEvent.type(headcount, '2')

    // Every breakdown row shows the caption in place of the figure.
    await waitFor(() => {
      expect(within(simulator).getAllByText('Recalculating…').length).toBeGreaterThan(0)
    })
    expect(within(simulator).getAllByText('Recalculating…').length).toBeGreaterThanOrEqual(7)

    // The pending state is the caption — **not** the em dash, which means
    // *unknown* — and no figure priced for the previous headcount is shown.
    expect(within(simulator).queryByText('—')).not.toBeInTheDocument()
    expect(within(simulator).queryByText('₱235,500.00')).not.toBeInTheDocument()
    expect(within(simulator).queryByText('₱217,500.00')).not.toBeInTheDocument()

    // When the quote arrives the server's figures replace the caption.
    gate.release()
    await waitFor(() => {
      expect(within(simulator).queryAllByText('Recalculating…')).toHaveLength(0)
    })
    expect(within(simulator).getByText('₱235,500.00')).toBeInTheDocument()
    expect(within(simulator).getByText('₱217,500.00')).toBeInTheDocument()
  }, 20000)

  it('opens the course-rules workbench from the header', async () => {
    renderLocationsPage()

    await screen.findByTestId('package-coverage-matrix')

    await userEvent.click(screen.getByRole('button', { name: 'Course rules' }))

    expect(await screen.findByText('Course rules workbench')).toBeInTheDocument()
  })
})
