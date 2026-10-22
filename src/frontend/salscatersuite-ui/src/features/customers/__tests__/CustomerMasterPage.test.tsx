import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { CustomerMasterPage } from '@/features/customers/CustomerMasterPage'
import { CUSTOMER_DIRECTORY_QUERY_KEY } from '@/features/customers/hooks/useCustomerDirectory'
import type { CustomerStatement } from '@/types/customer.types'
import {
  customerDetailFixture,
  customerDirectoryFixture,
  customerStatementFixture,
  emptyCustomerDirectoryFixture,
  sparseCustomerDetailFixture,
} from '@/test/customerFixture'
import { createTestQueryClient } from '@/test/testHelpers'

type GetResolver = (url: string) => unknown

function renderCustomerMaster() {
  const queryClient = createTestQueryClient()
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <CustomerMasterPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient, invalidateSpy }
}

/**
 * Routes the mocked GET calls to the fixture that matches the endpoint, so a
 * test can drive the directory, the profile and the statement independently.
 */
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
  if (url.includes('/directory/export')) return new Blob(['xlsx'])
  if (url.includes('/directory')) return customerDirectoryFixture
  if (url.includes('/statement')) return customerStatementFixture
  if (url.includes('/customers/')) return customerDetailFixture
  return {}
}

/** Opens the drawer on the given customer and waits for its profile. */
async function openCustomer(fullName: string) {
  await userEvent.click(await screen.findByRole('button', { name: `Edit ${fullName}` }))
  await screen.findByLabelText('Last name')
}

describe('CustomerMasterPage', () => {
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

  it('renders the KPI cards, the table and the pagination from the API payload', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    // Banner
    expect(
      await screen.findByRole('heading', { name: 'Customer Master & Accounts Ledger' }),
    ).toBeInTheDocument()

    // The four KPI cards, straight from the summary.
    expect(await screen.findByText('Total Registered Clients')).toBeInTheDocument()
    expect(screen.getByText('248')).toBeInTheDocument()
    expect(screen.getByText('Profiles Active')).toBeInTheDocument()

    expect(screen.getByText('Active AR Exposure')).toBeInTheDocument()
    expect(screen.getByText('₱684,200.00')).toBeInTheDocument()
    expect(screen.getByText('14 with open balance')).toBeInTheDocument()

    expect(screen.getByText('Total Contracted Value')).toBeInTheDocument()
    expect(screen.getByText('₱18,420,000.00')).toBeInTheDocument()
    expect(screen.getByText('Avg ₱74,274.19 per client')).toBeInTheDocument()

    expect(screen.getByText('Overdue AR (>30 days)')).toBeInTheDocument()
    expect(screen.getByText('₱142,000.00')).toBeInTheDocument()
    expect(screen.getByText('3 clients overdue')).toBeInTheDocument()

    // The directory table.
    expect(screen.getByText('Tan, Liza')).toBeInTheDocument()
    expect(screen.getByText('Apex Global Technologies Inc.')).toBeInTheDocument()
    expect(screen.getByText('+63 920 441 9090')).toBeInTheDocument()
    expect(screen.getByText('6 past • 1 upcoming')).toBeInTheDocument()
    expect(screen.getByText('Total: ₱1,180,000.00')).toBeInTheDocument()

    // The pagination footer.
    expect(screen.getByText('1–10')).toBeInTheDocument()
    expect(screen.getByText(/of 248 records/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeInTheDocument()
  })

  it('renders the aging bucket label exactly as the server sent it', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    // The server's own strings — the client never maps a key to a label and
    // never counts days (AC-C3).
    expect((await screen.findAllByText('Paid in full')).length).toBeGreaterThan(0)
    expect((await screen.findAllByText('31-60 days overdue')).length).toBeGreaterThan(0)
    expect((await screen.findAllByText('Escalated to Collections')).length).toBeGreaterThan(0)

    // No key-derived wording is invented for the bucket the client can see.
    expect(screen.queryByText('90+ days')).not.toBeInTheDocument()
    expect(screen.queryByText('Over 90 days')).not.toBeInTheDocument()
    expect(screen.queryByText('31-60 days')).not.toBeInTheDocument()
  })

  it('shows the classification in the ID column and the aging state only in the AR column', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await screen.findByText('Tan, Liza')

    expect(
      screen.getByRole('columnheader', { name: 'Customer ID & Type' }),
    ).toBeInTheDocument()

    const row = screen.getByText('Tan, Liza').closest('tr') as HTMLElement

    // The derived classification is the chip in the ID column.
    expect(within(row).getByText('Corporate')).toBeInTheDocument()
    // The aging state appears exactly once per row — in the AR Ledger column.
    expect(within(row).getAllByText('Paid in full')).toHaveLength(1)

    const overdueRow = screen.getByText('Fernandez, Ramon Santos').closest('tr') as HTMLElement
    expect(within(overdueRow).getByText('Private')).toBeInTheDocument()
    expect(within(overdueRow).getAllByText('31-60 days overdue')).toHaveLength(1)
  })

  it('gives the directory the full width until a customer is selected', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await screen.findByText('Tan, Liza')

    // No selection — the directory owns the whole row and a compact hint
    // replaces the empty detail card.
    expect(screen.getByTestId('customer-directory-column')).toHaveClass('xl:col-span-12')
    expect(screen.queryByTestId('customer-drawer-column')).not.toBeInTheDocument()
    expect(screen.getByText('Select a client to open its profile')).toBeInTheDocument()
    expect(screen.queryByText('Select a customer')).not.toBeInTheDocument()

    await openCustomer('Tan, Liza')

    // A selection — the reference's 7/5 split applies.
    expect(screen.getByTestId('customer-directory-column')).toHaveClass('xl:col-span-7')
    expect(screen.getByTestId('customer-drawer-column')).toHaveClass('xl:col-span-5')
    expect(screen.queryByText('Select a client to open its profile')).not.toBeInTheDocument()
  })

  it('renders every status chip with its text label, never colour alone', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    const chip = (await screen.findAllByText('Paid in full'))[0]
    expect(chip).toBeInTheDocument()
    expect(chip).toHaveTextContent('Paid in full')
    // The chip is a text node inside a bordered chip — the state is legible
    // without any colour perception.
    expect(chip.textContent?.trim()).toBe('Paid in full')
  })

  it('opens the drawer with the profile the API published', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')

    const drawer = screen.getByRole('dialog')
    expect(within(drawer).getByRole('heading', { name: 'Apex Global Technologies Inc.' })).toBeInTheDocument()
    expect(within(drawer).getByText('Update Mode')).toBeInTheDocument()
    expect(screen.getByLabelText('Last name')).toHaveValue('Tan')
    expect(screen.getByLabelText('First name')).toHaveValue('Liza')
    expect(screen.getByLabelText('Company')).toHaveValue('Apex Global Technologies Inc.')
    // The derived classification chip and the upcoming-booking ribbon.
    expect(within(drawer).getByText('Corporate')).toBeInTheDocument()
    expect(screen.getByText('Upcoming Booking')).toBeInTheDocument()
    expect(screen.getByText('BK-20260315-00601 • Q1 Gala Dinner • 15 Mar 2026')).toBeInTheDocument()
  })

  it('renders the statement lines with the server’s figures and aging label', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('tab', { name: 'Ledger History' }))

    expect(await screen.findByText(/BK-20260315-00601 • 15 Mar 2026/)).toBeInTheDocument()
    expect(screen.getByText(/BK-20251204-00588 • 4 Dec 2025/)).toBeInTheDocument()
    expect(screen.getByText('₱245,000.00')).toBeInTheDocument()
    expect(screen.getByText('₱150,000.00')).toBeInTheDocument()
    expect(screen.getByText('₱95,000.00')).toBeInTheDocument()
    // The aging label is the server's string.
    expect(screen.getByText('Over 90 days overdue')).toBeInTheDocument()
  })

  it('carries every data point of a statement line in its card', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('tab', { name: 'Ledger History' }))

    const card = (await screen.findByText(/BK-20251204-00588 • 4 Dec 2025/)).closest(
      'li',
    ) as HTMLElement

    // The booking number, the occasion, the event date, the amount, the paid
    // sum and the balance — nothing is dropped from the card.
    expect(within(card).getByText(/BK-20251204-00588 • 4 Dec 2025/)).toBeInTheDocument()
    expect(within(card).getByText('Leadership Summit')).toBeInTheDocument()
    expect(within(card).getByText('₱245,000.00')).toBeInTheDocument()
    expect(within(card).getByText('₱150,000.00')).toBeInTheDocument()
    expect(within(card).getByText('₱95,000.00')).toBeInTheDocument()
    expect(within(card).getByText('Amount')).toBeInTheDocument()
    expect(within(card).getByText('Paid')).toBeInTheDocument()
    expect(within(card).getByText('Balance')).toBeInTheDocument()
  })

  it('shows the server’s status and aging label on the statement line', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('tab', { name: 'Ledger History' }))

    const agedCard = (await screen.findByText(/BK-20251204-00588 • 4 Dec 2025/)).closest(
      'li',
    ) as HTMLElement

    // The BR-10 lifecycle name, humanised through the shared helper — the
    // same label the Operations Dashboard shows.
    expect(within(agedCard).getByText('Deposit Paid')).toBeInTheDocument()
    // The aging label is the server's own display string, rendered verbatim.
    expect(within(agedCard).getByText('Over 90 days overdue')).toBeInTheDocument()

    // A line with no aging bucket carries no aging chip at all.
    const settledCard = screen.getByText(/BK-20260315-00601 • 15 Mar 2026/).closest(
      'li',
    ) as HTMLElement
    expect(within(settledCard).getByText('Completed')).toBeInTheDocument()
    expect(within(settledCard).queryByText('Over 90 days overdue')).not.toBeInTheDocument()
    expect(within(settledCard).queryByText('Not yet due')).not.toBeInTheDocument()
  })

  it('humanises the booking status the same way the dashboard does', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('tab', { name: 'Ledger History' }))

    // The server's wire name `FullyPaid` is rendered as "Fully Paid" …
    const paidCard = (await screen.findByText(/BK-20260410-00544 • 10 Apr 2026/)).closest(
      'li',
    ) as HTMLElement
    expect(within(paidCard).getByText('Fully Paid')).toBeInTheDocument()
    expect(within(paidCard).queryByText('FullyPaid')).not.toBeInTheDocument()

    // … and `DepositPaid` as "Deposit Paid".
    const depositCard = screen.getByText(/BK-20251204-00588 • 4 Dec 2025/).closest(
      'li',
    ) as HTMLElement
    expect(within(depositCard).getByText('Deposit Paid')).toBeInTheDocument()
    expect(within(depositCard).queryByText('DepositPaid')).not.toBeInTheDocument()
  })

  it('keeps the raw text of a booking status the client does not know', async () => {
    const statement: CustomerStatement = {
      ...customerStatementFixture,
      transactions: [
        {
          ...customerStatementFixture.transactions[0],
          status: 'AwaitingApproval',
        },
      ],
    }
    mockGet((url) => (url.includes('/statement') ? statement : defaultResolver(url)))

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('tab', { name: 'Ledger History' }))

    const card = (await screen.findByText(/BK-20260315-00601 • 15 Mar 2026/)).closest(
      'li',
    ) as HTMLElement

    // A status added server-side after this client shipped still shows its
    // raw text, on a neutral chip.
    expect(within(card).getByText('AwaitingApproval')).toBeInTheDocument()
  })

  it('names a statement line by its booking number when it carries no occasion', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('tab', { name: 'Ledger History' }))

    const card = (await screen.findByText(/BK-20251106-00577 • —/)).closest('li') as HTMLElement

    // The empty occasion falls back to the booking number as the card title.
    const title = card.querySelector('span') as HTMLElement
    expect(title).toHaveTextContent('BK-20251106-00577')
  })

  it('keeps the statement totals strip above the lines', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('tab', { name: 'Ledger History' }))

    expect(await screen.findByText('Total Billed')).toBeInTheDocument()
    expect(screen.getByText('Total Paid')).toBeInTheDocument()
    expect(screen.getByText('Outstanding Balance')).toBeInTheDocument()
    // The billed and paid totals are both the statement's ₱1,180,000.00.
    expect(screen.getAllByText('₱1,180,000.00')).toHaveLength(2)
    // The outstanding balance belongs to the totals strip.
    const outstandingColumn = screen.getByText('Outstanding Balance').parentElement as HTMLElement
    expect(within(outstandingColumn).getByText('₱0.00')).toBeInTheDocument()
    // The customer's own aging state still rides in the totals strip.
    const drawer = screen.getByRole('dialog')
    expect(within(drawer).getByText('Paid in full')).toBeInTheDocument()
  })

  it('renders the server’s validation errors verbatim and keeps the typed input', async () => {
    mockGet(defaultResolver)
    vi.spyOn(apiClient, 'put').mockRejectedValue(
      new ApiError(400, ['Last name is required.', 'First name must not be empty.']),
    )

    renderCustomerMaster()

    await openCustomer('Tan, Liza')

    const lastName = screen.getByLabelText('Last name')
    await userEvent.clear(lastName)
    await userEvent.type(lastName, 'Ocampo')

    await userEvent.click(screen.getByRole('button', { name: 'Save & Update' }))

    expect(await screen.findByText('Last name is required.')).toBeInTheDocument()
    expect(screen.getByText('First name must not be empty.')).toBeInTheDocument()
    // The operator's input survived the rejected save (AC-C12).
    expect(screen.getByLabelText('Last name')).toHaveValue('Ocampo')
  })

  it('renders every quick-view pill with the count the server published', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await screen.findByText('Tan, Liza')

    // Every pill carries a live server count — none is suppressed.
    expect(screen.getByRole('button', { name: 'All Clients (248)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Corporate (142)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Private (106)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'With Open Balance (14)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Overdue > 30 days (3)' })).toBeInTheDocument()
  })

  it('keeps the quick-view counts on the population when a filter is applied', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await screen.findByText('Tan, Liza')

    await userEvent.selectOptions(screen.getByLabelText('Type'), 'Corporate')

    // The counts describe the population, not the filtered page, so they do
    // not move when a scope is applied (spec §4.3).
    expect(await screen.findByRole('button', { name: 'All Clients (248)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Corporate (142)' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Private (106)' })).toBeInTheDocument()
  })

  it('shows the server generation timestamp in the status footer', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    expect(
      await screen.findByText('Data as of 20 Oct 2026, 09:15 UTC'),
    ).toBeInTheDocument()
  })

  it('renders a customer whose record carries no optional facts without throwing', async () => {
    mockGet((url) =>
      url.includes('/directory') ? customerDirectoryFixture : sparseCustomerDetailFixture,
    )

    renderCustomerMaster()

    await openCustomer('Tan, Liza')

    // The editable attributes still render — the empty ones are simply empty.
    expect(screen.getByLabelText('Last name')).toHaveValue('Santos')
    expect(screen.getByLabelText('Company')).toHaveValue('')

    // A missing registration date and a missing booking population are em
    // dashes — never a zero and never a crash.
    const registeredBlock = screen.getByText('Registered').parentElement as HTMLElement
    const bookingsBlock = screen.getByText('Bookings').parentElement as HTMLElement
    expect(within(registeredBlock).getByText('—')).toBeInTheDocument()
    expect(within(bookingsBlock).getByText('—')).toBeInTheDocument()

    // A ledger state the API did not publish is an em dash, not a guess.
    const drawer = screen.getByRole('dialog')
    expect(within(drawer).getAllByText('—').length).toBeGreaterThanOrEqual(2)

    // No upcoming booking — the ribbon is simply not rendered.
    expect(screen.queryByText('Upcoming Booking')).not.toBeInTheDocument()
  })

  it('closes the drawer on Escape', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await openCustomer('Tan, Liza')
    expect(screen.getByRole('dialog')).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  it('prevents a duplicate submission while a save is in flight', async () => {
    mockGet(defaultResolver)
    let resolveSave: (value: AxiosResponse) => void = () => undefined
    vi.spyOn(apiClient, 'put').mockReturnValue(
      new Promise<AxiosResponse>((resolve) => {
        resolveSave = resolve
      }),
    )

    renderCustomerMaster()

    await openCustomer('Tan, Liza')

    await userEvent.click(screen.getByRole('button', { name: 'Save & Update' }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Saving…' })).toBeDisabled()
    })

    resolveSave({ data: null, status: 204 } as AxiosResponse)

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  it('omits the elements the specification places out of scope', async () => {
    mockGet(defaultResolver)

    renderCustomerMaster()

    await screen.findByText('Tan, Liza')

    // The batch Statement-of-Account action is rendered but unavailable.
    const batch = screen.getByRole('button', { name: 'Batch SOAs (QuestPDF)' })
    expect(batch).toHaveAttribute('aria-disabled', 'true')
    expect(batch).toHaveAttribute('title')

    // The reference's out-of-scope filters, columns and figures are absent.
    expect(screen.queryByLabelText('Metro Area')).not.toBeInTheDocument()
    expect(screen.queryByText(/TIN/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Lifetime Value|LTV/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/Repeat Rate/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/QuestPDF SOA Compliance/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/SignalR/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/BIR/i)).not.toBeInTheDocument()
  })

  it('invalidates the directory query after a successful save', async () => {
    mockGet(defaultResolver)
    vi.spyOn(apiClient, 'put').mockResolvedValue({ data: null, status: 204 } as AxiosResponse)

    const { invalidateSpy } = renderCustomerMaster()

    await openCustomer('Tan, Liza')
    await userEvent.click(screen.getByRole('button', { name: 'Save & Update' }))

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [CUSTOMER_DIRECTORY_QUERY_KEY] })
    })
    // A successful save closes the drawer (AC-C13).
    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  it('renders the empty state instead of a fabricated row', async () => {
    mockGet((url) =>
      url.includes('/directory') ? emptyCustomerDirectoryFixture : defaultResolver(url),
    )

    renderCustomerMaster()

    expect(
      await screen.findByText('No customers match the current filters'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Tan, Liza')).not.toBeInTheDocument()
    expect(screen.queryByRole('row', { name: /Tan/ })).not.toBeInTheDocument()
    // A null average is an em dash, never a zero (contract rule 6).
    expect(screen.getByText('Avg — per client')).toBeInTheDocument()
    expect(screen.queryByText('₱0.00 per client')).not.toBeInTheDocument()
  })

  it('renders the error state with a retry and never fabricates content', async () => {
    const getSpy = vi.spyOn(apiClient, 'get')
    getSpy.mockRejectedValue(new ApiError(500, ['The ledger could not be reconciled.']))

    renderCustomerMaster()

    // A 5xx is retried once by the query before it surfaces.
    const alert = await screen.findByRole('alert', undefined, { timeout: 8000 })
    expect(within(alert).getByText('The ledger could not be reconciled.')).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: /retry/i })).toBeInTheDocument()
    // Nothing is invented while the directory is unavailable.
    expect(screen.queryByText('Tan, Liza')).not.toBeInTheDocument()

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

    expect(await screen.findByText('Tan, Liza')).toBeInTheDocument()
  })

  it('calls the API with the new page and the new page size', async () => {
    const getSpy = mockGet(defaultResolver)

    renderCustomerMaster()

    await screen.findByText('Tan, Liza')

    await userEvent.click(screen.getByRole('button', { name: 'Next' }))

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/customers/directory',
        expect.objectContaining({ params: expect.objectContaining({ page: 2 }) }),
      )
    })

    await userEvent.selectOptions(screen.getByLabelText('Page size:'), '25')

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/customers/directory',
        expect.objectContaining({
          params: expect.objectContaining({ page: 1, pageSize: 25 }),
        }),
      )
    })
  })

  it('carries the active filters into the export request', async () => {
    const getSpy = mockGet(defaultResolver)

    renderCustomerMaster()

    await screen.findByText('Tan, Liza')

    await userEvent.selectOptions(screen.getByLabelText('Type'), 'Corporate')
    await userEvent.selectOptions(screen.getByLabelText('AR State'), 'Overdue30')

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/customers/directory',
        expect.objectContaining({
          params: expect.objectContaining({ customerType: 'Corporate', arState: 'Overdue30' }),
        }),
      )
    })

    await userEvent.click(screen.getByRole('button', { name: 'Export (ClosedXML)' }))

    await waitFor(() => {
      expect(getSpy).toHaveBeenCalledWith(
        '/customers/directory/export',
        expect.objectContaining({
          params: expect.objectContaining({ customerType: 'Corporate', arState: 'Overdue30' }),
        }),
      )
    })
  })
})
