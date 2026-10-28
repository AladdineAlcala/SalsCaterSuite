import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { PackageCourseRulesPage } from '@/features/packages/PackageCourseRulesPage'
import {
  emptyPackageAreaCoverageMatrixFixture,
  emptyPackageCourseRulesetFixture,
  packageAreaCoverageMatrixFixture,
  packageCourseRuleBreachPreviewFixture,
  packageCourseRulePreviewFixture,
  packageCourseRulesetFixture,
  packageCourseTaxonomyFixture,
  unpricedPackageCourseRulesetFixture,
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

function renderRulesPage(route = '/packages/12/rules') {
  const queryClient = createTestQueryClient()
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="/packages" element={<div>Package catalogue</div>} />
          <Route path="/packages/:packageId/edit" element={<div>Tier edit form</div>} />
          <Route path="/packages/:packageId/locations" element={<div>Location workbench</div>} />
          <Route path="/packages/:packageId/rules" element={<PackageCourseRulesPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient }
}

interface MockOptions {
  get?: (url: string) => unknown
  post?: (url: string, body: unknown) => unknown
}

/**
 * A gate over the preview endpoint.
 *
 * It lets a test answer the preview normally, **hold** the next one open, and
 * release it — which is how the pending (recalculating) state is exercised
 * without depending on timing.
 *
 * The handler returns the **payload**, because the api mock wraps whatever it
 * returns in the axios envelope.
 */
function createPreviewGate(payload: unknown = packageCourseRulePreviewFixture) {
  let holding = false
  let count = 0
  const held: Array<(value: unknown) => void> = []

  return {
    /** Hold every preview request instead of answering it. */
    hold() {
      holding = true
    },
    /** Answer the preview requests that were held. */
    release() {
      holding = false
      const resolvers = held.splice(0, held.length)
      resolvers.forEach((resolve) => resolve(payload))
    },
    /** The number of preview requests received. */
    count() {
      return count
    },
    /** The preview request handler for the api mock. */
    handle(url: string): unknown {
      if (!url.includes('/course-rules/preview')) return {}
      count += 1
      if (holding) {
        return new Promise((resolve) => {
          held.push(resolve)
        })
      }
      return payload
    },
  }
}

function defaultGet(url: string): unknown {
  if (url.includes('/course-rules')) return packageCourseRulesetFixture
  if (url.includes('/areas')) return packageAreaCoverageMatrixFixture
  if (url.includes('/menus/courses')) return packageCourseTaxonomyFixture
  return {}
}

function defaultPost(url: string): unknown {
  if (url.includes('/course-rules/preview')) return packageCourseRulePreviewFixture
  return {}
}

function mockApi(options: MockOptions = {}) {
  vi.spyOn(apiClient, 'get').mockImplementation(
    async (url: string): Promise<AxiosResponse> =>
      axiosResponse(options.get ? options.get(url) : defaultGet(url)),
  )
  vi.spyOn(apiClient, 'post').mockImplementation(
    async (url: string, body?: unknown): Promise<AxiosResponse> =>
      axiosResponse(options.post ? options.post(url, body) : defaultPost(url)),
  )
}

describe('PackageCourseRulesPage', () => {
  beforeEach(() => {
    mockApi()
  })

  it('renders the spec rail, the planning forecast, the rules matrix and the compliance verdict from the payloads', async () => {
    renderRulesPage()

    expect(
      await screen.findByRole('heading', { name: 'Course Coverage Rules Workbench' }),
    ).toBeInTheDocument()

    // The specification rail — the tier's identity and its commercial terms.
    const rail = await screen.findByTestId('package-spec-rail')
    expect(within(rail).getByText('Wedding Grand Tier')).toBeInTheDocument()
    expect(within(rail).getByText('Tier key 12 · Wedding')).toBeInTheDocument()
    // The rate is shown twice — as the net pax rate and as the tier term.
    expect(within(rail).getAllByText('₱1,450.00').length).toBeGreaterThan(0)
    expect(within(rail).getByText('₱507.50')).toBeInTheDocument()
    expect(within(rail).getByText('₱217,500.00')).toBeInTheDocument()
    // The rail is read-only; the tier form stays the single editor.
    expect(within(rail).getByRole('button', { name: 'Edit tier terms' })).toBeInTheDocument()

    // The spatial coverage checklist comes from the coverage matrix payload.
    expect(within(rail).getByText('Antipolo City')).toBeInTheDocument()
    expect(within(rail).getByText('2 of 3 active')).toBeInTheDocument()

    // The planning forecast.
    const forecast = screen.getByTestId('package-forecast-panel')
    expect(within(forecast).getByText('35.00%')).toBeInTheDocument()
    expect(within(forecast).getByText('₱329.25')).toBeInTheDocument()
    expect(within(forecast).getByText('77.29%')).toBeInTheDocument()
    expect(
      within(forecast).getByText(/A planning estimate derived from the qualified pools/),
    ).toBeInTheDocument()

    // The rules matrix — the pool size and its costed count.
    const beefCard = screen.getByTestId('package-course-rule-4')
    expect(within(beefCard).getByText('Enforce exactly 2 Beef selections')).toBeInTheDocument()
    expect(within(beefCard).getByText('Quota 2')).toBeInTheDocument()
    expect(within(beefCard).getByText('120 g')).toBeInTheDocument()
    expect(within(beefCard).getByText('5 dishes · 4 costed')).toBeInTheDocument()
    expect(within(beefCard).getByText('₱275.00')).toBeInTheDocument()
    expect(within(beefCard).getByText('Beef Royale')).toBeInTheDocument()

    // The compliance verdict region.
    const compliance = screen.getByTestId('package-quota-compliance')
    expect(
      await within(compliance).findByText('Selection compliant'),
    ).toBeInTheDocument()
    expect(within(compliance).getByText('All course rules satisfied')).toBeInTheDocument()
  })

  it('renders the compliance verdict in a polite live region', async () => {
    renderRulesPage()

    const compliance = await screen.findByTestId('package-quota-compliance')
    await within(compliance).findByText('Selection compliant')

    const live = compliance.querySelector('[aria-live="polite"]')
    expect(live).not.toBeNull()
    expect(live?.textContent).toContain('Selection compliant')
  })

  it('shows the empty ruleset state and never fabricates a rule', async () => {
    mockApi({
      get: (url) =>
        url.includes('/course-rules')
          ? emptyPackageCourseRulesetFixture
          : defaultGet(url),
    })

    renderRulesPage()

    expect(await screen.findByText('This tier holds no course rule')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add course rule' })).toBeInTheDocument()
    expect(screen.queryByTestId('package-course-rule-4')).not.toBeInTheDocument()
  })

  it('says so when a course has no qualified dish', async () => {
    mockApi({
      get: (url) => {
        if (!url.includes('/course-rules')) return defaultGet(url)
        const ruleset = structuredClone(packageCourseRulesetFixture)
        ruleset.rules[1].pool = []
        ruleset.rules[1].poolSize = 0
        ruleset.rules[1].poolCostedCount = 0
        ruleset.rules[1].poolAverageFoodCost = null
        return ruleset
      },
    })

    renderRulesPage()

    const saladCard = await screen.findByTestId('package-course-rule-5')
    expect(
      within(saladCard).getByText(/No dish is qualified for this course yet/),
    ).toBeInTheDocument()
    expect(within(saladCard).getByText('—')).toBeInTheDocument()
  })

  it('shows the empty spatial-coverage state when the tier covers no municipality', async () => {
    mockApi({
      get: (url) =>
        url.includes('/areas') ? emptyPackageAreaCoverageMatrixFixture : defaultGet(url),
    })

    renderRulesPage()

    const rail = await screen.findByTestId('package-spec-rail')
    expect(within(rail).getByText('This tier covers no municipality')).toBeInTheDocument()
  })

  it('renders an unpriced tier as em dashes and says why the simulators cannot run', async () => {
    mockApi({
      get: (url) =>
        url.includes('/course-rules') ? unpricedPackageCourseRulesetFixture : defaultGet(url),
    })

    renderRulesPage()

    const rail = await screen.findByTestId('package-spec-rail')
    expect(within(rail).getAllByText('—').length).toBeGreaterThan(0)
    expect(within(rail).queryByText('₱0.00')).not.toBeInTheDocument()

    const forecast = screen.getByTestId('package-forecast-panel')
    expect(
      within(forecast).getByText('This tier carries no per-guest rate, so no contract figure can be produced.'),
    ).toBeInTheDocument()

    const simulator = screen.getByTestId('package-selection-simulator')
    expect(
      within(simulator).getByText(/This tier carries no per-guest rate, so no cost figure/),
    ).toBeInTheDocument()
  })

  it('sends the operator’s dish selection to the server and renders the preview figures', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderRulesPage()

    const picker = await screen.findByLabelText('Dish for Beef')
    await userEvent.selectOptions(picker, 'DS-203')

    await waitFor(
      () => {
        const calls = postSpy.mock.calls.filter(
          ([url]) => typeof url === 'string' && url.includes('/course-rules/preview'),
        )
        const last = calls[calls.length - 1]
        expect(last).toBeDefined()
        expect(last?.[1]).toEqual({
          selections: [{ courseId: 4, menuId: 'DS-203' }],
          stressPercentage: null,
          pax: null,
        })
      },
      { timeout: 4000 },
    )

    const simulator = screen.getByTestId('package-selection-simulator')
    await waitFor(() => {
      expect(within(simulator).getByText('₱480.00')).toBeInTheDocument()
    })
    expect(within(simulator).getByText('33.10%')).toBeInTheDocument()
    expect(within(simulator).getByText('Within the cost ceiling')).toBeInTheDocument()
  })

  it('warns on a cost-ceiling breach and shows the shortfall and the percentage over the ceiling', async () => {
    mockApi({
      post: (url) =>
        url.includes('/course-rules/preview') ? packageCourseRuleBreachPreviewFixture : {},
    })

    renderRulesPage()

    const simulator = await screen.findByTestId('package-selection-simulator')
    await waitFor(() => {
      expect(within(simulator).getByText('Over the cost ceiling')).toBeInTheDocument()
    })

    // The shortfall in pesos and the percentage over the ceiling.
    expect(within(simulator).getByText('₱232.50')).toBeInTheDocument()
    expect(within(simulator).getByText('45.81%')).toBeInTheDocument()
    expect(
      within(simulator).getByText(/This is a warning: no price is changed/),
    ).toBeInTheDocument()

    // The two verdicts are two different fields: the quota verdict is true and
    // the overall verdict is false (DEC-080).
    const compliance = screen.getByTestId('package-quota-compliance')
    expect(within(compliance).getByText('All course rules satisfied')).toBeInTheDocument()
    expect(within(compliance).getByText('Selection not compliant')).toBeInTheDocument()
    expect(
      within(compliance).getByText(/the selection breaches the cost ceiling/),
    ).toBeInTheDocument()
  })

  it('says the food cost of the selection is unknown when a selected dish is not costed', async () => {
    mockApi({
      post: (url) =>
        url.includes('/course-rules/preview')
          ? {
              ...packageCourseRulePreviewFixture,
              selectedFoodCostPerPax: null,
              foodCostPerPax: null,
              effectiveFoodCostPercentage: null,
              isCostKnown: false,
              isCeilingBreached: false,
              ceilingShortfallPerPax: null,
              ceilingOveragePercentage: null,
            }
          : {},
    })

    renderRulesPage()

    const simulator = await screen.findByTestId('package-selection-simulator')
    await waitFor(() => {
      expect(
        within(simulator).getByText(/The food cost of this selection is unknown/),
      ).toBeInTheDocument()
    })
    expect(within(simulator).queryByText('₱0.00')).not.toBeInTheDocument()
  })

  it('sends the whole ruleset on save and replaces the tier’s rules', async () => {
    const putSpy = vi.spyOn(apiClient, 'put').mockResolvedValue(axiosResponse(packageCourseRulesetFixture))

    renderRulesPage()

    await screen.findByTestId('package-course-rule-4')

    await userEvent.click(screen.getByRole('button', { name: 'Save & apply course rules' }))

    await waitFor(() => {
      expect(putSpy).toHaveBeenCalledWith('/packages/12/course-rules', {
        rules: [
          {
            courseId: 4,
            requiredQuota: 2,
            portionGrams: 120,
            ruleDescription: 'Enforce exactly 2 Beef selections',
          },
          {
            courseId: 5,
            requiredQuota: 1,
            portionGrams: 80,
            ruleDescription: 'One seasonal salad is served',
          },
        ],
      })
      // The retired rule members never reach the API (DEC-172) — the request
      // body carries only the surviving members of contract §13.2.
      const body = (putSpy.mock.calls[putSpy.mock.calls.length - 1]?.[1] ?? null) as {
        rules: Record<string, unknown>[]
      } | null
      expect(body?.rules[0]).toEqual({
        courseId: 4,
        requiredQuota: 2,
        portionGrams: 120,
        ruleDescription: 'Enforce exactly 2 Beef selections',
      })
    })
  })

  it('carries the retired rule members nowhere (DEC-172)', async () => {
    const ruleset = structuredClone(packageCourseRulesetFixture) as unknown as {
      rules: Array<Record<string, unknown>>
    }
    // The frontend contract fixture contains only the fields the server now
    // publishes; assert the retired members are absent from that wire shape.
    expect(Object.keys(ruleset.rules[0])).toEqual(
      expect.not.arrayContaining([
        'inclusionTag',
        'upgradeAddonId',
        'upgradeAddonDescription',
        'upgradeAddonUnitPrice',
        'upgradeAddonUnit',
      ]),
    )

    renderRulesPage()

    // The card shows no inclusion tag and no "Allowed tier-1 upgrade" content.
    const beefCard = await screen.findByTestId('package-course-rule-4')
    expect(within(beefCard).queryByText('Included')).not.toBeInTheDocument()
    expect(within(beefCard).queryByText('Allowed tier-1 upgrade')).not.toBeInTheDocument()
    expect(
      within(beefCard).queryByText('Premium Carving Station'),
    ).not.toBeInTheDocument()

    // The editor shows no inclusion-tag input and no tier-1-upgrade input.
    await userEvent.click(within(beefCard).getByRole('button', { name: 'Edit rule' }))
    await screen.findByTestId('package-course-rule-editor')
    expect(screen.queryByLabelText('Inclusion tag')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Tier-1 upgrade add-on')).not.toBeInTheDocument()
  })

  it('renders the server’s errors verbatim and keeps the operator’s typed input', async () => {
    vi.spyOn(apiClient, 'put').mockRejectedValue(
      new ApiError(400, [
        'Course category 4 is configured more than once.',
        'Required quota must be at least 1.',
      ]),
    )

    renderRulesPage()

    await screen.findByTestId('package-course-rule-4')

    const beefCard = screen.getByTestId('package-course-rule-4')
    await userEvent.click(within(beefCard).getByRole('button', { name: 'Edit rule' }))
    const quotaField = await screen.findByLabelText('Required quota')
    await userEvent.clear(quotaField)
    await userEvent.type(quotaField, '3')
    await userEvent.click(screen.getByRole('button', { name: 'Save & apply course rules' }))

    expect(await screen.findByText('Course category 4 is configured more than once.')).toBeInTheDocument()
    expect(screen.getByText('Required quota must be at least 1.')).toBeInTheDocument()

    // The typed input survived the rejected save (BR-11).
    expect(screen.getByLabelText('Required quota')).toHaveValue(3)
  })

  it('rejects a blank course and a quota below one on the client without calling the API', async () => {
    const putSpy = vi.spyOn(apiClient, 'put')

    renderRulesPage()

    await screen.findByTestId('package-course-rule-4')
    await userEvent.click(screen.getByRole('button', { name: 'Add course rule' }))

    const quotaField = await screen.findByLabelText('Required quota')
    await userEvent.clear(quotaField)
    await userEvent.type(quotaField, '0')
    await userEvent.click(screen.getByRole('button', { name: 'Add to the ruleset' }))

    expect(await screen.findByText('Required quota must be at least 1.')).toBeInTheDocument()
    expect(putSpy).not.toHaveBeenCalled()
  })

  it('renders the load error with a retry and never fabricates content', async () => {
    vi.spyOn(apiClient, 'get').mockRejectedValue(
      new ApiError(404, ['Package not found.']),
    )

    renderRulesPage()

    const alert = await screen.findByRole('alert', undefined, { timeout: 8000 })
    expect(within(alert).getByText('Package not found.')).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: /retry/i })).toBeInTheDocument()
    expect(screen.queryByTestId('package-course-rule-4')).not.toBeInTheDocument()
  })

  it('renders the two capability-gap notices without a requirement footer', async () => {
    renderRulesPage()

    await screen.findByTestId('package-selection-simulator')

    const notices = screen.getAllByTestId('capability-gap-notice')
    expect(notices.length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText('Banquet amenities unavailable')).toBeInTheDocument()
    expect(screen.getByText('Complimentary staples unavailable')).toBeInTheDocument()
    expect(
      screen.getAllByText('Deliberately excluded by DEC-084 — no entity carries this data.').length,
    ).toBeGreaterThanOrEqual(2)
    expect(screen.queryByText(/Blocked under/)).not.toBeInTheDocument()
  })

  it('omits the affordances the approved specification places out of scope', async () => {
    renderRulesPage()

    await screen.findByTestId('package-course-rule-4')

    expect(screen.queryByRole('button', { name: /duplicate/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /publish/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /bulk/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/SignalR/i)).not.toBeInTheDocument()
  })

  it('never presents the previous selection’s figures or verdict while a preview is pending', async () => {
    const gate = createPreviewGate()
    mockApi({ post: gate.handle })

    renderRulesPage()

    // The opening preview answers normally, so the server's figures are shown.
    const simulator = await screen.findByTestId('package-selection-simulator')
    expect(await within(simulator).findByText('₱480.00')).toBeInTheDocument()
    const compliance = screen.getByTestId('package-quota-compliance')
    expect(within(compliance).getByText('Selection compliant')).toBeInTheDocument()
    const forecast = screen.getByTestId('package-forecast-panel')
    expect(within(forecast).getByText('₱217,500.00')).toBeInTheDocument()

    // The operator changes the selection; the next preview is held open.
    gate.hold()
    await userEvent.selectOptions(screen.getByLabelText('Dish for Beef'), 'DS-203')

    // The recalculating state replaces the figures.
    expect((await within(simulator).findAllByText('Recalculating…')).length).toBeGreaterThan(0)
    expect(within(compliance).getAllByText('Recalculating…').length).toBeGreaterThan(0)
    expect(within(forecast).getAllByText('Recalculating…').length).toBeGreaterThan(0)

    // The previous selection's peso figures and its verdict are absent.
    expect(within(simulator).queryByText('₱480.00')).not.toBeInTheDocument()
    expect(within(simulator).queryByText('Within the cost ceiling')).not.toBeInTheDocument()
    expect(within(compliance).queryByText('Selection compliant')).not.toBeInTheDocument()
    expect(within(compliance).queryByText('All course rules satisfied')).not.toBeInTheDocument()
    expect(within(forecast).queryByText('₱217,500.00')).not.toBeInTheDocument()

    // The ruleset, the rule cards, the pools and the planning figures stay.
    const rail = screen.getByTestId('package-spec-rail')
    expect(within(rail).getByText('Wedding Grand Tier')).toBeInTheDocument()
    expect(within(rail).getByText('₱217,500.00')).toBeInTheDocument()
    const beefCard = screen.getByTestId('package-course-rule-4')
    expect(within(beefCard).getByText('Beef Royale')).toBeInTheDocument()
    expect(within(beefCard).getByText('5 dishes · 4 costed')).toBeInTheDocument()
    expect(within(forecast).getByText('₱329.25')).toBeInTheDocument()
    expect(within(forecast).getByText('77.29%')).toBeInTheDocument()

    // The typed selection stays visible — it is the operator's own input.
    expect(within(simulator).getByText('Beef Caldereta')).toBeInTheDocument()

    // When the preview arrives the server's figures take their place.
    gate.release()
    await waitFor(() => {
      expect(within(simulator).queryAllByText('Recalculating…')).toHaveLength(0)
    })
    expect(within(simulator).getByText('₱480.00')).toBeInTheDocument()
    expect(within(compliance).getByText('Selection compliant')).toBeInTheDocument()
  }, 20000)

  it('does not fire a preview request per keystroke on Screen A', async () => {
    const gate = createPreviewGate()
    mockApi({ post: gate.handle })

    renderRulesPage()

    const stressInput = await screen.findByLabelText('Stress preview (percent)')

    // The opening preview is one request.
    await waitFor(() => {
      expect(gate.count()).toBe(1)
    })

    const before = gate.count()
    await userEvent.type(stressInput, '10')

    // One further request — not one per keystroke.
    await waitFor(() => {
      expect(gate.count()).toBe(before + 1)
    })
    await new Promise((resolve) => window.setTimeout(resolve, 700))
    expect(gate.count()).toBe(before + 1)
  }, 20000)

  it('opens the location workbench from the header', async () => {
    renderRulesPage()

    await screen.findByTestId('package-course-rule-4')

    await userEvent.click(screen.getByRole('button', { name: 'Location coverage' }))

    expect(await screen.findByText('Location workbench')).toBeInTheDocument()
  })
})
