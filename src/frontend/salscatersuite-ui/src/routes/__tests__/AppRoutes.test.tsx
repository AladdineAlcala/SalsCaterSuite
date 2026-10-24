import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { AppRoutes } from '@/routes/AppRoutes'
import {
  bookingRegisterPageFixture,
  bookingRegisterSummaryFixture,
} from '@/test/bookingFixture'
import { customerDirectoryFixture } from '@/test/customerFixture'
import { dashboardFixture } from '@/test/dashboardFixture'
import { menuMasterFixture } from '@/test/menuFixture'
import {
  addonAdminFixture,
  addonCategoryLookupFixture,
  departmentLookupFixture,
} from '@/test/addonFixture'
import { createTestToken, renderWithProviders, seedSession } from '@/test/testHelpers'

function mockDashboard() {
  vi.spyOn(apiClient, 'get').mockResolvedValue({ data: dashboardFixture } as AxiosResponse)
}

/** Routes each GET to the fixture that matches its endpoint. */
function mockApiRoutes() {
  vi.spyOn(apiClient, 'get').mockImplementation(
    async (url: string): Promise<AxiosResponse> => ({
      data: url.includes('/menus/master')
        ? menuMasterFixture
        : url.includes('/customers')
          ? customerDirectoryFixture
          : url.includes('/bookings/summary')
            ? bookingRegisterSummaryFixture
            : url.includes('/bookings')
              ? bookingRegisterPageFixture
              : url.includes('/addons/manage')
                ? addonAdminFixture
                : url.includes('/addons/categories')
                  ? addonCategoryLookupFixture
                  : url.includes('/departments')
                    ? departmentLookupFixture
                    : dashboardFixture,
      status: 200,
      statusText: 'OK',
      headers: {},
      config: { headers: {} } as AxiosResponse['config'],
    }),
  )
}

describe('AppRoutes', () => {
  it('redirects an unauthenticated visitor to the login page', async () => {
    renderWithProviders(<AppRoutes />, { route: '/' })

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" }),
    ).not.toBeInTheDocument()
  })

  it('lands the authenticated user on the dashboard', async () => {
    seedSession()
    mockDashboard()

    renderWithProviders(<AppRoutes />, { route: '/' })

    expect(
      await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" }),
    ).toBeInTheDocument()
  })

  it('signs the user in and returns to the dashboard', async () => {
    const token = createTestToken({
      nameid: 'user-1',
      unique_name: 'admin',
      fullName: 'Catering Administrator',
      role: ['Administrator'],
      exp: Math.floor(Date.now() / 1000) + 3600,
    })
    vi.spyOn(apiClient, 'post').mockResolvedValue({
      data: {
        token,
        userId: 'user-1',
        userName: 'admin',
        fullName: 'Catering Administrator',
        roles: ['Administrator'],
      },
    } as AxiosResponse)
    mockDashboard()

    renderWithProviders(<AppRoutes />, { route: '/login' })

    await userEvent.type(await screen.findByLabelText('Username'), 'admin')
    await userEvent.type(screen.getByLabelText('Password'), 'a-secret')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(
      await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" }),
    ).toBeInTheDocument()
  })

  it('shows the API error message inline when the credentials are rejected', async () => {
    vi.spyOn(apiClient, 'post').mockRejectedValue(
      new ApiError(401, ['Invalid credentials.']),
    )

    renderWithProviders(<AppRoutes />, { route: '/login' })

    await userEvent.type(await screen.findByLabelText('Username'), 'admin')
    await userEvent.type(screen.getByLabelText('Password'), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('Invalid credentials.')).toBeInTheDocument()
  })

  it('resolves every navigation item to a real page', async () => {
    seedSession()
    mockApiRoutes()

    renderWithProviders(<AppRoutes />, { route: '/' })

    await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" })

    const navigation = screen.getByRole('navigation', { name: 'Primary' })
    const links = within(navigation).getAllByRole('link')

    // Every navigation entry is a real link — none is a dead "#" anchor.
    expect(links.length).toBeGreaterThan(0)
    for (const link of links) {
      expect(link).toHaveAttribute('href')
      expect(link.getAttribute('href')).not.toBe('#')
    }

    // The Bookings & Events entry now resolves to the **real** register.
    await userEvent.click(within(navigation).getByRole('link', { name: 'Bookings & Events' }))

    expect(
      await screen.findByRole('heading', { name: 'Bookings & Event Operations' }),
    ).toBeInTheDocument()
    expect(screen.queryByText('Not yet implemented')).not.toBeInTheDocument()
  })

  it('keeps every unbuilt navigation entry on a labelled placeholder page', async () => {
    seedSession()
    mockApiRoutes()

    renderWithProviders(<AppRoutes />, { route: '/' })

    await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" })

    const navigation = screen.getByRole('navigation', { name: 'Primary' })

    for (const label of [
      'Payments & Ledger',
      'Accounts Receivable & SOA',
      'Banquet Calendar',
      'Reports & BEO',
    ]) {
      await userEvent.click(within(navigation).getByRole('link', { name: label }))

      expect(
        await screen.findByRole('heading', { name: label }),
      ).toBeInTheDocument()
      expect(screen.getByText('Not yet implemented')).toBeInTheDocument()
    }
  })

  it('opens the Customer Master from the primary navigation', async () => {
    seedSession()
    mockApiRoutes()

    renderWithProviders(<AppRoutes />, { route: '/' })

    await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" })

    const navigation = screen.getByRole('navigation', { name: 'Primary' })
    await userEvent.click(within(navigation).getByRole('link', { name: 'Customers' }))

    expect(
      await screen.findByRole('heading', { name: 'Customer Master & Accounts Ledger' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Tan, Liza')).toBeInTheDocument()
  })

  it('opens the Menu & Food Costing Master from the primary navigation', async () => {
    seedSession()
    mockApiRoutes()

    renderWithProviders(<AppRoutes />, { route: '/' })

    await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" })

    const navigation = screen.getByRole('navigation', { name: 'Primary' })
    await userEvent.click(within(navigation).getByRole('link', { name: 'Menu & Food Costing' }))

    expect(
      await screen.findByRole('heading', { name: 'Menu & Food Costing Master' }),
    ).toBeInTheDocument()
    expect(screen.getAllByText('DS-104').length).toBeGreaterThan(0)
  })

  it('opens the Add-on Master from Settings', async () => {
    seedSession()
    mockApiRoutes()

    renderWithProviders(<AppRoutes />, { route: '/' })

    await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" })

    const navigation = screen.getByRole('navigation', { name: 'Primary' })
    await userEvent.click(within(navigation).getByRole('link', { name: 'Settings' }))

    expect(await screen.findByRole('heading', { name: 'Settings' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('link', { name: /Add-ons/ }))

    expect(
      await screen.findByRole('heading', { name: 'Add-on Master' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Carving station')).toBeInTheDocument()
  })

  it('clears the token on sign out and returns to the login page', async () => {    seedSession()
    mockDashboard()

    renderWithProviders(<AppRoutes />, { route: '/' })

    await screen.findByRole('heading', { name: "Sal's CaterSuite Operations Dashboard" })

    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    expect(window.localStorage.getItem('salscatersuite.session')).toBeNull()
  })
})
