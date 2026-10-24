import {
  act,
  configure,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosRequestConfig, AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { BookingMenuPage } from '@/features/bookings/BookingMenuPage'
import type {
  AddonMasterItemDto,
  BookingAddonSetDto,
  BookingAddonSetRequest,
  BookingMenuSelectionDto,
  BookingMenuSelectionRequest,
} from '@/types/booking.types'
import {
  addonMasterFixture,
  bandBookingMenuSelectionFixture,
  bookingAddonSetFixture,
  bookingDetailsFixture,
  bookingMenuSelectionFixture,
  emptyAddonMasterFixture,
  emptyBookingMenuSelectionFixture,
  lockedBookingMenuSelectionFixture,
  vatBookingMenuSelectionFixture,
} from '@/test/bookingFixture'
import { createTestQueryClient } from '@/test/testHelpers'

/*
 * The page reads three projections at once. A loaded worker can take longer
 * than the one-second default to publish them; waiting longer does not weaken
 * any assertion, it only stops the screen being judged before it painted.
 */
configure({ asyncUtilTimeout: 5000 })

interface MockOptions {
  /** The stored selection the read endpoint publishes. */
  menu?: BookingMenuSelectionDto
  /** The booking's frozen add-on lines. */
  addons?: BookingAddonSetDto
  /** The add-on master. It ships empty in this cycle. */
  master?: AddonMasterItemDto[]
  /** The add-on category master. */
  categories?: { addonCategoryId: number; addonCategoryName: string }[]
  /** Holds the add-on master read open, so a loading state can be asserted. */
  holdMaster?: Promise<void>
  /** Throws once on the add-on master read — a retry then succeeds. */
  masterError?: ApiError
  /** Throws once on the categories read — a retry then succeeds. */
  categoryError?: ApiError
  /** Holds the category-filtered add-on read open, so staleness is assertable. */
  holdFiltered?: Promise<void>
  /** The details read fails — but only after an add-on write has succeeded. */
  detailsErrorAfterWrite?: ApiError
  /**
   * A controllable gate over the details reads made **after** an add-on write
   * succeeded — failing until the test flips it, so a retry's recovery is
   * deterministic rather than racy against the store's own invalidations.
   */
  detailsGate?: { fail: boolean }
  detailsGateError?: ApiError
  /** The details read fails. */
  detailsError?: ApiError
  /** The menu read fails. */
  menuError?: ApiError
  /** The selection write is refused. */
  menuWriteError?: ApiError
  /** The add-on write is refused. */
  addonWriteError?: ApiError
}

interface RecordedRead {
  url: string
  params: Record<string, unknown> | undefined
}

const categoriesFixture = [
  { addonCategoryId: 2, addonCategoryName: 'Live stations' },
  { addonCategoryId: 3, addonCategoryName: 'Dessert' },
]

function axiosResponse(data: unknown, status = 200): AxiosResponse {
  return {
    data,
    status,
    statusText: 'OK',
    headers: {},
    config: { headers: {} } as AxiosResponse['config'],
  }
}

function renderMenu() {
  const queryClient = createTestQueryClient()
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/bookings/101/menu']}>
        <Routes>
          <Route path="/bookings/:transactionId/menu" element={<BookingMenuPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient }
}

/**
 * Builds the saved selection the write endpoint would return.
 *
 * Every field is derived from the request and the stored pool, so the screen
 * reads back what it just sent instead of a hand-written second fixture.
 */
function buildSavedMenu(
  request: BookingMenuSelectionRequest,
  current: BookingMenuSelectionDto,
): BookingMenuSelectionDto {
  const selections = request.selections.map((item) => {
    const course = current.courses.find((entry) => entry.courseId === item.courseId)
    const dish = course?.pool.find((entry) => entry.menuId === item.menuId)
    return {
      courseId: item.courseId,
      courseName: course?.courseName ?? '',
      menuId: item.menuId,
      menuName: dish?.menuName ?? item.menuId,
      note: dish?.note ?? null,
      foodCost: dish?.foodCost ?? null,
      prepInstruction: item.prepInstruction ?? null,
    }
  })

  return {
    ...current,
    selections,
    courses: current.courses.map((course) => ({
      ...course,
      chosenMenuIds: selections
        .filter((selection) => selection.courseId === course.courseId)
        .map((selection) => selection.menuId),
    })),
    selectionState: selections.length === 0 ? 'None' : 'Selected',
  }
}

/** Builds the saved add-on set the write endpoint would return. */
function buildSavedAddons(
  request: BookingAddonSetRequest,
  current: BookingAddonSetDto,
  master: AddonMasterItemDto[],
): BookingAddonSetDto {
  const addons = request.addons.map((item) => {
    const frozen = current.addons.find((entry) => entry.addonId === item.addonId)
    const catalogue = master.find((entry) => entry.addonId === item.addonId)
    return {
      addonId: item.addonId,
      description: frozen?.description ?? catalogue?.description ?? `Add-on ${item.addonId}`,
      amount: item.amount ?? frozen?.amount ?? catalogue?.lineAmountForBooking ?? 0,
      unit: frozen?.unit ?? catalogue?.unit ?? null,
      pricingBasis: frozen?.pricingBasis ?? catalogue?.pricingBasis ?? 'Flat',
      addonCategoryId: frozen?.addonCategoryId ?? catalogue?.addonCategoryId ?? null,
      addonCategoryName:
        frozen?.addonCategoryName ?? catalogue?.addonCategoryName ?? null,
      catalogueAmount: catalogue?.amount ?? frozen?.catalogueAmount ?? null,
      catalogueDescription:
        catalogue?.description ?? frozen?.catalogueDescription ?? null,
    }
  })

  return {
    ...current,
    addons,
    addonsTotal: addons.reduce((total, line) => total + line.amount, 0),
  }
}

/**
 * Routes the mocked calls to the fixture that matches the endpoint and records
 * every write so a test can assert on the exact payload the screen sent.
 *
 * The read handlers answer from mutable state, so a successful write is visible
 * to the invalidation that follows it — exactly as it is against the API.
 */
function mockApi(options: MockOptions = {}) {
  const master = options.master ?? addonMasterFixture
  const categories = options.categories ?? categoriesFixture
  let menuState = options.menu ?? bookingMenuSelectionFixture
  let addonState = options.addons ?? bookingAddonSetFixture
  let detailsState = options.detailsError
    ? null
    : { ...bookingDetailsFixture }
  let masterFailureBudget = options.masterError !== undefined ? 2 : 0
  let categoryFailureBudget = options.categoryError !== undefined ? 2 : 0
  let failDetailsAfterWrite = false

  const menuWrites: BookingMenuSelectionRequest[] = []
  const addonWrites: BookingAddonSetRequest[] = []
  const reads: RecordedRead[] = []

  vi.spyOn(apiClient, 'get').mockImplementation(
    async (
      url: string,
      config?: AxiosRequestConfig,
    ): Promise<AxiosResponse> => {
      const params = config?.params as Record<string, unknown> | undefined
      reads.push({ url, params })

      if (url === '/bookings/101/menus') {
        if (options.menuError) throw options.menuError
        return axiosResponse(menuState)
      }
      if (url === '/bookings/101/addons') return axiosResponse(addonState)
      if (url === '/bookings/101') {
        if (options.detailsError) throw options.detailsError
        if (failDetailsAfterWrite) {
          if (options.detailsGate === undefined) {
            throw options.detailsErrorAfterWrite
          }
          if (options.detailsGate.fail) {
            throw options.detailsGateError ?? options.detailsErrorAfterWrite
          }
        }
        if (detailsState === null) return axiosResponse({})
        return axiosResponse(detailsState)
      }
      if (url === '/addons') {
        if (masterFailureBudget > 0) {
          masterFailureBudget -= 1
          // eslint-disable-next-line @typescript-eslint/only-throw-error
          throw options.masterError
        }
        if (options.holdMaster) await options.holdMaster
        if (options.holdFiltered && params?.categoryId !== undefined) {
          await options.holdFiltered
        }
        const categoryId = params?.categoryId
        return axiosResponse(
          categoryId === undefined
            ? master
            : master.filter((item) => item.addonCategoryId === categoryId),
        )
      }
      if (url === '/addons/categories') {
        if (categoryFailureBudget > 0) {
          categoryFailureBudget -= 1
          // eslint-disable-next-line @typescript-eslint/only-throw-error
          throw options.categoryError
        }
        return axiosResponse(categories)
      }
      if (url === '/bookings/101/beo') return axiosResponse(new Blob(['pdf']))
      return axiosResponse({})
    },
  )

  vi.spyOn(apiClient, 'put').mockImplementation(
    async (url: string, body?: unknown): Promise<AxiosResponse> => {
      if (url === '/bookings/101/menus') {
        if (options.menuWriteError) throw options.menuWriteError
        const request = body as BookingMenuSelectionRequest
        menuWrites.push(request)
        menuState = buildSavedMenu(request, menuState)
        return axiosResponse(menuState)
      }
      if (url === '/bookings/101/addons') {
        if (options.addonWriteError) throw options.addonWriteError
        const request = body as BookingAddonSetRequest
        addonWrites.push(request)
        addonState = buildSavedAddons(request, addonState, master)
        // The add-on replace re-freezes the money; a subsequent details read
        // answers the **freshly frozen** figure, which reconciles with the
        // saved subtotal (this test stands in for the calculator's composition
        // with the saved coefficient and its own constants).
        detailsState = {
          ...bookingDetailsFixture,
          amountDue:
            bookingDetailsFixture.amountDue - 1500 + addonState.addonsTotal,
        }
        failDetailsAfterWrite =
          options.detailsErrorAfterWrite !== undefined || options.detailsGate !== undefined
        return axiosResponse(addonState)
      }
      return axiosResponse({})
    },
  )

  return { menuWrites, addonWrites, reads }
}

/** Finds the one element whose text is exactly `text` (a chip, not its wrapper). */
function exactText(text: string): HTMLElement {
  return screen.getByText(
    (_, element) => element?.tagName === 'SPAN' && element.textContent === text,
  )
}

describe('BookingMenuPage', () => {
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

  it('names the headcount band and its rate from the server, never a hardcoded bound', async () => {
    mockApi({ menu: bandBookingMenuSelectionFixture })

    renderMenu()

    const ledger = await screen.findByTestId('booking-contract-ledger')
    expect(ledger).toHaveTextContent('Automatic headcount band')
    expect(ledger).toHaveTextContent('100 to 999 heads')
    expect(ledger).toHaveTextContent('₱20.00 per head')
  })

  it('renders the event, the tier’s course tabs, the qualified pools and the frozen ledger', async () => {
    mockApi()

    renderMenu()

    // The event identity comes from the booking's own snapshot; the pools and
    // the lock state come from the selection.
    expect(await screen.findByText('Fernandez-Santos Nuptials')).toBeInTheDocument()
    expect(
      screen.getByText('Booking BK-20261120-00101 · the dishes this event is served.'),
    ).toBeInTheDocument()
    expect(screen.getByText('Wedding Grand Tier')).toBeInTheDocument()
    expect(screen.getByText('250 heads covered')).toBeInTheDocument()

    // One tab per course rule, plus "All".
    const tabs = screen.getByTestId('booking-menu-course-tabs')
    expect(tabs).toHaveTextContent('All courses')
    expect(tabs).toHaveTextContent('Beef')
    expect(tabs).toHaveTextContent('Salad')

    // The chosen dishes of the stored selection, course by course.
    const cards = await screen.findAllByTestId('booking-menu-course-card')
    expect(cards).toHaveLength(2)
    expect(cards[0]).toHaveTextContent('Beef Caldereta')
    expect(cards[0]).toHaveTextContent('2 of 2 selected')
    expect(cards[0]).toHaveTextContent('Tomato, bell pepper, potato')
    expect(cards[1]).toHaveTextContent('Caesar Salad')
    expect(cards[1]).toHaveTextContent('1 of 1 selected')

    // The ledger's money is the frozen snapshot, never a client calculation.
    const ledger = screen.getByTestId('booking-contract-ledger')
    expect(ledger).toHaveTextContent('Base catering package')
    expect(ledger).toHaveTextContent('₱285,000.00')
    expect(ledger).toHaveTextContent('Selected add-ons')
    expect(ledger).toHaveTextContent('₱1,500.00')
    expect(ledger).toHaveTextContent('₱36,000.00')
    expect(ledger).toHaveTextContent('Discount code — EARLY-BIRD')
    expect(ledger).toHaveTextContent('Total contract value')
    expect(ledger).toHaveTextContent('₱305,000.00')
    expect(ledger).toHaveTextContent('₱255,000.00')

    // No VAT rate is configured, so the disclosure is absent — never a zero.
    expect(screen.queryByText(/VAT — included/)).not.toBeInTheDocument()

    // The batch mass is the server's derived planning figure.
    const batch = screen.getByTestId('booking-batch-yield-panel')
    expect(batch).toHaveTextContent('120 g / guest')
    expect(batch).toHaveTextContent('30 kg')
  })

  it('renders every unsourced panel as a capability gap instead of inventing data', async () => {
    mockApi()

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')

    // One allergen gap per course card — the notice is never a single claim.
    expect(screen.getAllByText('Allergen declaration unavailable').length).toBeGreaterThan(0)
    expect(screen.getByText('Tasting sheet unavailable')).toBeInTheDocument()
    expect(screen.getByText('Chef sign-off unavailable')).toBeInTheDocument()
    expect(screen.getByText('Quality stamp unavailable')).toBeInTheDocument()
    expect(screen.getByText('Ingredient-level yield unavailable')).toBeInTheDocument()

    // The tasting sheet is the one header action that stays inactive.
    expect(screen.getByRole('button', { name: 'Tasting sheet (PDF)' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
  })

  it('removes a dish, disables the save while the quota is unmet, and restores it by adding another', async () => {
    mockApi()

    renderMenu()

    const board = await screen.findByTestId('booking-menu-course-board')

    await userEvent.click(
      screen.getByRole('button', { name: 'Remove Beef Caldereta from Beef' }),
    )

    // BR-15 — the quota is a form affordance, and the verdict is written out.
    expect(board).toHaveTextContent('1 of 2 selected')
    expect(
      await screen.findByText("The selection does not satisfy the tier's quota"),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save selection' })).toBeDisabled()

    // The course is no longer full, so its picker offers the freed slot.
    const picker = screen.getByLabelText('Change dish for Beef') as HTMLSelectElement
    expect(picker).toBeEnabled()
    await userEvent.selectOptions(picker, 'DS-201')

    expect(board).toHaveTextContent('2 of 2 selected')
    expect(screen.getByRole('button', { name: 'Save selection' })).toBeEnabled()
    expect(
      screen.queryByText("The selection does not satisfy the tier's quota"),
    ).not.toBeInTheDocument()
  })

  it('sends the chef’s prep instruction and the untouched dishes in one full replace', async () => {
    const api = mockApi()

    renderMenu()

    const instructions = await screen.findAllByLabelText('Chef prep instruction — Beef')
    expect(instructions).toHaveLength(2)
    // The stored instruction opens in the field, so it is replaced, not appended.
    expect(instructions[0]).toHaveValue('Sauce on the side')
    await userEvent.clear(instructions[0])
    await userEvent.type(instructions[0], 'Sauce on the side')

    await userEvent.click(screen.getByRole('button', { name: 'Save selection' }))

    await waitFor(() => {
      expect(api.menuWrites).toHaveLength(1)
    })

    // The write is a full replace: an omitted instruction would clear it.
    expect(api.menuWrites[0].selections).toEqual([
      { courseId: 4, menuId: 'DS-203', prepInstruction: 'Sauce on the side' },
      { courseId: 4, menuId: 'DS-204', prepInstruction: null },
      { courseId: 5, menuId: 'DS-301', prepInstruction: null },
    ])

    // The saved response is what the screen reports.
    expect(await screen.findByText('The selection was saved')).toBeInTheDocument()
    expect(screen.getByText(/the server reports it as compliant/)).toBeInTheDocument()
    expect(screen.getByText(/Add-on subtotal ₱1,500.00/)).toBeInTheDocument()
  })

  it('does not request the add-on catalogue or its categories on the initial render', async () => {
    const api = mockApi()

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')

    const addonReads = api.reads.filter(
      (read) => read.url === '/addons' || read.url === '/addons/categories',
    )
    expect(addonReads).toEqual([])
  })

  it('opens the picker lazily, sends the required headcount and filters by category', async () => {
    const api = mockApi()

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')
    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))

    const picker = await screen.findByTestId('booking-addons-picker')
    expect(picker).toHaveAttribute('role', 'dialog')

    await screen.findByText('Line total for this booking')

    // The unfiltered read carries the **required headcount** and no category.
    expect(api.reads.filter((read) => read.url === '/addons/categories')).toHaveLength(1)
    const masterReads = api.reads.filter((read) => read.url === '/addons')
    expect(masterReads.length).toBe(1)
    expect(masterReads[0].params).toEqual({ headcount: 250 })

    const rows = within(picker).getAllByText(/Carving station|Dessert bar|Cocktail welcome/)
    expect(rows.length).toBeGreaterThan(0)

    // The category filter is a label on the select, and the server narrows.
    await userEvent.selectOptions(
      await screen.findByLabelText('Filter by category'),
      'Dessert',
    )

    await waitFor(() => {
      expect(
        api.reads.filter((read) => read.url === '/addons').length,
      ).toBe(2)
    })
    expect(api.reads.filter((read) => read.url === '/addons')[1].params).toEqual({
      headcount: 250,
      categoryId: 3,
    })
    expect(within(picker).getByText('Dessert bar')).toBeInTheDocument()
    expect(within(picker).queryByText('Cocktail welcome')).not.toBeInTheDocument()
  })

  it('suppresses previous-category rows while a filtered add-on read is in flight', async () => {
    let releaseFiltered!: () => void
    const gate = new Promise<void>((resolve) => {
      releaseFiltered = resolve
    })
    mockApi({ holdFiltered: gate })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')
    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    const picker = await screen.findByTestId('booking-addons-picker')

    // The unfiltered view resolves first.
    await screen.findByLabelText('Include Carving station')

    await userEvent.selectOptions(
      await screen.findByLabelText('Filter by category'),
      'Dessert',
    )

    // No prior-category row may be offered while the new read is in flight.
    expect(within(picker).queryByText('Cocktail welcome')).not.toBeInTheDocument()
    expect(within(picker).queryByText('Carving station')).not.toBeInTheDocument()
    expect(screen.getByText(/Loading the add-on master/)).toBeInTheDocument()

    act(() => {
      releaseFiltered()
    })
    await screen.findByLabelText('Include Dessert bar')
    expect(within(picker).queryByText('Cocktail welcome')).not.toBeInTheDocument()
  })

  it('shows a category failure with a retry that recovers', async () => {
    mockApi({ categoryError: new ApiError(500, ['The categories are unreachable.']) })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')
    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    const picker = await screen.findByTestId('booking-addons-picker')

    expect(
      await within(picker).findByText('The categories could not be read'),
    ).toBeInTheDocument()
    expect(
      within(picker).getByText('The categories are unreachable.'),
    ).toBeInTheDocument()

    // The retry re-issues the categories read, which succeeds, and the filter
    // becomes usable.
    await userEvent.click(within(picker).getByRole('button', { name: 'Retry' }))
    expect(
      within(picker).queryByText('The categories could not be read'),
    ).not.toBeInTheDocument()
    expect(
      within(picker).getByRole('option', { name: 'Dessert' }),
    ).toBeInTheDocument()
  })

  it('treats a failed post-save details read as a refresh failure, never a refusal and never a stale total', async () => {
    const api = mockApi({
      detailsErrorAfterWrite: new ApiError(500, ['The booking could not be re-read.']),
    })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')

    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    const picker = await screen.findByTestId('booking-addons-picker')
    await userEvent.click(await within(picker).findByLabelText('Include Dessert bar'))
    await userEvent.click(within(picker).getByRole('button', { name: 'Close' }))

    await userEvent.click(screen.getByRole('button', { name: 'Save selection' }))

    // The write itself went through — the replacement was issued.
    await waitFor(() => {
      expect(api.addonWrites).toHaveLength(1)
    })

    // The refresh failure is distinct from an add-ons refusal…
    expect(
      await screen.findByText(
        'The recalculated contract value could not be loaded',
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        /The add-on write succeeded, but reading the recalculated booking failed/,
      ),
    ).toBeInTheDocument()

    // …no recalculated total is claimed (the cached pre-save figure stays in
    // the query but nothing presents it as current)…
    expect(
      screen.queryByText(/recalculated the total contract value on save/),
    ).not.toBeInTheDocument()
    expect(
      screen.getByText(
        /the recalculated contract value could not be read\./,
      ),
    ).toBeInTheDocument()

    // …and the save is never described as refused.
    expect(screen.queryByText('add-ons were refused')).not.toBeInTheDocument()
    // The recovery affordance is a retry of the read, not a dismissal.
    expect(screen.getByRole('button', { name: 'Retry refresh' })).toBeInTheDocument()
  })

  it('keeps the stale-total warning through a later failing save and recovers via Retry refresh', async () => {
    const gate = { fail: true }
    const api = mockApi({
      detailsGate: gate,
      detailsGateError: new ApiError(500, ['The booking could not be re-read.']),
    })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')

    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    const picker = await screen.findByTestId('booking-addons-picker')
    await userEvent.click(await within(picker).findByLabelText('Include Dessert bar'))
    await userEvent.click(within(picker).getByRole('button', { name: 'Close' }))

    await userEvent.click(screen.getByRole('button', { name: 'Save selection' }))
    await waitFor(() => {
      expect(api.addonWrites).toHaveLength(1)
    })
    expect(
      await screen.findByText(
        'The recalculated contract value could not be loaded',
      ),
    ).toBeInTheDocument()

    // A later save attempt begins and its write is issued — the warning must
    // survive it (it was cleared only after a fresh **successful** read).
    await userEvent.click(screen.getByRole('button', { name: 'Save selection' }))
    await waitFor(() => {
      expect(api.addonWrites).toHaveLength(2)
    })
    expect(
      screen.getByText('The recalculated contract value could not be loaded'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText(/recalculated the total contract value on save/),
    ).not.toBeInTheDocument()

    // Retry refresh: the read now succeeds, so the warning clears and the
    // ledger shows the server's recalculated figure. A retry click can be
    // deduped by the query against one of the failed save-triggered reads,
    // so the recovery polls — the same way an operator would press retry.
    // A retry press while the read still fails keeps the warning.
    await userEvent.click(screen.getByRole('button', { name: 'Retry refresh' }))
    expect(
      screen.getByText('The recalculated contract value could not be loaded'),
    ).toBeInTheDocument()

    // Now the read succeeds: one press is enough — the refresh read is a
    // single round trip that cannot dedupe onto a still-pending failed cycle.
    gate.fail = false
    await userEvent.click(screen.getByRole('button', { name: 'Retry refresh' }))
    await waitFor(() => {
      expect(
        screen.queryByText(
          'The recalculated contract value could not be loaded',
        ),
      ).toBeNull()
    })
    expect(
      screen.queryByText(
        'The recalculated contract value could not be loaded',
      ),
    ).not.toBeInTheDocument()
    await waitFor(() => {
      expect(screen.getByTestId('booking-contract-ledger')).toHaveTextContent(
        '₱342,500.00',
      )
    })
    expect(
      screen.getByText(/The server recalculated the total contract value after refresh: ₱342,500.00\./),
    ).toBeInTheDocument()
  })

  it('keeps the picker selection a draft until save and shows the selected lines only', async () => {
    const api = mockApi()

    renderMenu()

    // The page itself shows only the selected line — the master is not laid out.
    await screen.findByTestId('booking-menu-course-board')
    expect(screen.getByText('Carving station')).toBeInTheDocument()
    expect(screen.getByLabelText('Remove Carving station')).toBeInTheDocument()
    expect(screen.queryByText('Cocktail welcome')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    const picker = await screen.findByTestId('booking-addons-picker')
    await userEvent.click(within(picker).getByLabelText('Include Dessert bar'))
    await userEvent.click(within(picker).getByRole('button', { name: 'Close' }))

    // The closing is the only thing that happens — no write can have fired.
    expect(screen.queryByTestId('booking-addons-picker')).not.toBeInTheDocument()
    expect(api.addonWrites).toHaveLength(0)

    // The page now carries the selected line only, editable.
    const lineTotal = await screen.findByLabelText(
      'Line total for Dessert bar',
    ) as HTMLInputElement
    expect(lineTotal).toHaveValue('37,500.00')

    // Reopening shows the draft retained.
    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    const reopened = await screen.findByTestId('booking-addons-picker')
    expect(
      within(reopened).getByLabelText('Include Dessert bar'),
    ).toBeChecked()

    // The edit on the line amount survives the close/reopen cycle too.
    fireEvent.change(lineTotal, { target: { value: '40000' } })
    await userEvent.click(within(reopened).getByRole('button', { name: 'Close' }))
    expect(screen.getByLabelText('Line total for Dessert bar')).toHaveValue('40,000.00')
  })

  it('replaces the set on save and quotes the server-recalculated contract value from a fresh details read', async () => {
    const api = mockApi()

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')

    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    const picker = await screen.findByTestId('booking-addons-picker')
    await userEvent.click(await within(picker).findByLabelText('Include Dessert bar'))
    await userEvent.click(within(picker).getByRole('button', { name: 'Close' }))

    const lineTotal = await screen.findByLabelText('Line total for Dessert bar')
    fireEvent.change(lineTotal, { target: { value: '40000' } })

    const detailsReadsBefore = api.reads.filter(
      (read) => read.url === '/bookings/101',
    ).length

    await userEvent.click(screen.getByRole('button', { name: 'Save selection' }))

    await waitFor(() => {
      expect(api.addonWrites).toHaveLength(1)
    })
    expect(api.addonWrites[0].addons).toEqual([
      { addonId: 7, amount: 1500 },
      { addonId: 8, amount: 40000 },
    ])

    // The details read is re-issued after the write — the pre-save snapshot
    // is not what the confirmation quotes.
    await waitFor(() => {
      expect(
        api.reads.filter((read) => read.url === '/bookings/101').length,
      ).toBeGreaterThan(detailsReadsBefore)
    })

    expect(
      await screen.findByText(
        /The server recalculated the total contract value on save: .*/,
      ),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/contract value on save: ₱345,000.00/),
    ).toBeInTheDocument()
    const ledger = screen.getByTestId('booking-contract-ledger')
    expect(ledger).toHaveTextContent('₱345,000.00')

    // And the subtotal the confirmation reports is the server's saved answer.
    expect(screen.getByText(/Add-on subtotal ₱41,500.00/)).toBeInTheDocument()
  })

  it('renders the honest empty state in the picker when the add-on master ships empty', async () => {
    mockApi({
      master: emptyAddonMasterFixture,
      addons: { ...bookingAddonSetFixture, addons: [], addonsTotal: 0 },
    })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')

    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))
    expect(
      await screen.findByText('No active add-ons are available'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        /The catalogue holds no active add-on to offer this booking. Add or activate add-ons in Settings → Add-ons./,
      ),
    ).toBeInTheDocument()
    // No fabricated row is offered.
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })

  it('traps focus, closes on Escape and dismisses from the close affordance', async () => {
    mockApi()

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')

    const opener = screen.getByRole('button', { name: 'Choose add-ons' })
    await userEvent.click(opener)

    const picker = await screen.findByTestId('booking-addons-picker')
    expect(picker).toHaveAttribute('aria-modal', 'true')
    expect(within(picker).getByLabelText('Close the add-on picker')).toBeInTheDocument()

    // Escape dismisses and restores focus to the opener. (The Tab containment
    // itself is jsdom-proof only in the hook's own environment — jsdom never
    // computes `offsetParent`, so the trap's focusable enumeration is empty
    // there; the browser behaviour it drives is pinned by the hook unit.)
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByTestId('booking-addons-picker')).not.toBeInTheDocument()
    expect(document.activeElement).toBe(opener)
  })

  it('shows the master loading state while the picker is open', async () => {
    let releaseMaster!: () => void
    const gate = new Promise<void>((resolve) => {
      releaseMaster = resolve
    })
    mockApi({ holdMaster: gate })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')
    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))

    expect(await screen.findByText(/Loading the add-on master/)).toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()

    act(() => {
      releaseMaster()
    })
    await screen.findByLabelText('Include Carving station')
  })

  it('shows the master error state with a retry that recovers', async () => {
    const api = mockApi({ masterError: new ApiError(500, ['The catalogue is unreachable.']) })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')
    await userEvent.click(screen.getByRole('button', { name: 'Choose add-ons' }))

    const picker = await screen.findByTestId('booking-addons-picker')
    expect(
      await within(picker).findByText('The add-on master could not be loaded'),
    ).toBeInTheDocument()
    expect(
      within(picker).getByText('The catalogue is unreachable.'),
    ).toBeInTheDocument()

    // The retry re-issues the same headcount-carrying read, which succeeds.
    await userEvent.click(within(picker).getByRole('button', { name: 'Retry' }))
    await waitFor(() => {
      expect(
        within(picker).getByLabelText('Include Carving station'),
      ).toBeInTheDocument()
    })
    // The count is an implementation detail of the query's own retries; what
    // the contract binds is that **every** add-on read carries the headcount.
    const masterReads = api.reads.filter((read) => read.url === '/addons')
    expect(masterReads.length).toBeGreaterThanOrEqual(2)
    masterReads.forEach((read) => {
      expect(read.params?.headcount).toBe(250)
      expect(read.params?.categoryId).toBeUndefined()
    })
  })

  it('renders the locked selection read-only and says when the window closed', async () => {
    mockApi({ menu: lockedBookingMenuSelectionFixture })

    renderMenu()

    expect(await screen.findByText('This menu is locked')).toBeInTheDocument()
    expect(
      screen.getByText(
        'The menu-change window closed on 19 Oct 2026. The selection and the add-ons are read-only.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('BEO cutoff: 19 Oct 2026')).toBeInTheDocument()

    expect(screen.getByRole('button', { name: 'Save selection' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Reset defaults' })).toBeDisabled()

    const instructions = screen.getAllByLabelText('Chef prep instruction — Beef')
    instructions.forEach((input) => expect(input).toBeDisabled())
    // The locked event keeps the panel read-only: no picker, no edits.
    expect(screen.getByRole('button', { name: 'Choose add-ons' })).toBeDisabled()
    expect(screen.getByLabelText('Remove Carving station')).toBeDisabled()
    expect(screen.getByLabelText('Line total for Carving station')).toBeDisabled()
    expect(screen.getByLabelText('Search the dish pool')).toBeDisabled()
  })

  it('publishes the quota in text for a standard run and for an empty tier', async () => {
    mockApi({ menu: emptyBookingMenuSelectionFixture })

    renderMenu()

    expect(await screen.findByText('This tier states no course rule')).toBeInTheDocument()
    expect(exactText('Course quota 0 / 0 · Standard run')).toBeInTheDocument()

    // The tabs are derived from the tier's rules, so "All" is the only one.
    const tabs = screen.getByTestId('booking-menu-course-tabs')
    expect(tabs).toHaveTextContent('All courses')
    expect(tabs).not.toHaveTextContent('Beef')

    // The batch panel says why no mass can be derived.
    expect(screen.getByText('No batch mass can be derived')).toBeInTheDocument()
  })

  it('renders the VAT disclosure only when the server publishes the rate', async () => {
    mockApi({ menu: vatBookingMenuSelectionFixture })

    renderMenu()

    expect(await screen.findByText('VAT — included')).toBeInTheDocument()
    expect(screen.getByText('Already inside the total at 12.0%')).toBeInTheDocument()
    const ledger = screen.getByTestId('booking-contract-ledger')
    expect(ledger).toHaveTextContent('₱12,889.29')

    // It is a disclosure, never a charge: the total is still the frozen amount.
    expect(ledger).toHaveTextContent('₱305,000.00')
  })

  it('renders the server’s refusal verbatim and keeps the operator’s dish selection', async () => {
    mockApi({
      menuWriteError: new ApiError(400, [
        'The selection names a dish that is not qualified for the course.',
      ]),
    })

    renderMenu()

    await screen.findByTestId('booking-menu-course-board')
    await userEvent.click(screen.getByRole('button', { name: 'Save selection' }))

    expect(
      await screen.findByText('The selection names a dish that is not qualified for the course.'),
    ).toBeInTheDocument()
    // The draft survives the refusal, so nothing the operator chose is lost.
    expect(screen.getByTestId('booking-menu-course-board')).toHaveTextContent(
      'Beef Caldereta',
    )
  })

  it('shows the error state and offers a retry when the selection cannot be read', async () => {
    mockApi({ menuError: new ApiError(500, ['The selection could not be read.']) })

    renderMenu()

    expect(
      await screen.findByText('The menu selection could not be loaded', {}, { timeout: 5000 }),
    ).toBeInTheDocument()
    expect(screen.getByText('The selection could not be read.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })
})
