import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient } from '@/api/apiClient'
import { AddonMasterPage } from '@/features/settings/addons/AddonMasterPage'
import {
  addonAdminFixture,
  addonCategoryLookupFixture,
  departmentLookupFixture,
} from '@/test/addonFixture'
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

function renderPage() {
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={['/settings/addons']}>
        <AddonMasterPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function mockReads() {
  vi.spyOn(apiClient, 'get').mockImplementation(
    async (url: string): Promise<AxiosResponse> => {
      if (url.includes('/addons/categories')) return axiosResponse(addonCategoryLookupFixture)
      if (url.includes('/departments')) return axiosResponse(departmentLookupFixture)
      if (url.includes('/addons/manage')) return axiosResponse(addonAdminFixture)
      return axiosResponse([])
    },
  )
}

describe('AddonMasterPage', () => {
  beforeEach(() => {
    mockReads()
    vi.spyOn(apiClient, 'post').mockResolvedValue(axiosResponse({ addonId: 11 }, 201))
    vi.spyOn(apiClient, 'put').mockResolvedValue(axiosResponse(null, 204))
    vi.spyOn(apiClient, 'delete').mockResolvedValue(axiosResponse(null, 204))
  })

  it('lists the active and inactive add-ons with their category, department and basis', async () => {
    renderPage()

    expect(await screen.findByText('Carving station')).toBeInTheDocument()
    expect(screen.getByText('Dessert bar')).toBeInTheDocument()
    expect(screen.getByText('Cocktail welcome')).toBeInTheDocument()
    expect(screen.getAllByText('Live stations').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Kitchen').length).toBeGreaterThan(0)
    expect(screen.getByText('₱1,500.00')).toBeInTheDocument()
    expect(screen.getAllByText('Per guest').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Not stated (Flat)').length).toBeGreaterThan(0)
    expect(screen.getByText('Inactive')).toBeInTheDocument()
  })

  it('creates an add-on with the server payload', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')
    renderPage()

    await screen.findByText('Carving station')
    await userEvent.click(screen.getByRole('button', { name: /new add-on/i }))

    await userEvent.type(screen.getByLabelText('Description'), 'Pasta station')
    await userEvent.selectOptions(screen.getByLabelText('Category'), '2')
    await userEvent.selectOptions(screen.getByLabelText('Department'), '1')
    await userEvent.selectOptions(screen.getByLabelText('Pricing basis'), 'Flat')
    await userEvent.type(screen.getByLabelText('Unit price'), '1500')
    await userEvent.type(screen.getByLabelText('Unit of measure'), 'event')
    await userEvent.click(screen.getByRole('button', { name: 'Create add-on' }))

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith('/addons', {
        addonCategoryId: 2,
        deptId: 1,
        pricingBasis: 'Flat',
        description: 'Pasta station',
        unitPrice: 1500,
        unit: 'event',
        isActive: true,
      })
    })

    expect(await screen.findByText('The add-on was created.')).toBeInTheDocument()
  })

  it('rejects a blank description and a missing unit price on the client', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')
    renderPage()

    await screen.findByText('Carving station')
    await userEvent.click(screen.getByRole('button', { name: /new add-on/i }))
    await userEvent.click(screen.getByRole('button', { name: 'Create add-on' }))

    expect(await screen.findByText('Add-on description is required.')).toBeInTheDocument()
    expect(screen.getByText('Unit price is required.')).toBeInTheDocument()
    expect(postSpy).not.toHaveBeenCalled()
  })

  it('edits an add-on and sends the route id', async () => {
    const putSpy = vi.spyOn(apiClient, 'put')
    renderPage()

    await screen.findByText('Carving station')
    await userEvent.click(screen.getByRole('button', { name: 'Edit Carving station' }))

    const description = screen.getByLabelText('Description')
    await userEvent.clear(description)
    await userEvent.type(description, 'Carving station deluxe')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => {
      expect(putSpy).toHaveBeenCalledWith('/addons/7', {
        addonCategoryId: 2,
        deptId: 1,
        pricingBasis: 'Flat',
        description: 'Carving station deluxe',
        unitPrice: 1500,
        unit: 'event',
        isActive: true,
      })
    })

    expect(await screen.findByText('The add-on was updated.')).toBeInTheDocument()
  })

  it('deletes an add-on after the inline confirmation', async () => {
    const deleteSpy = vi.spyOn(apiClient, 'delete')
    renderPage()

    await screen.findByText('Carving station')
    await userEvent.click(screen.getByRole('button', { name: 'Delete Carving station' }))

    const confirmation = screen.getByRole('alert')
    expect(within(confirmation).getByText('Delete this add-on?')).toBeInTheDocument()

    await userEvent.click(within(confirmation).getByRole('button', { name: 'Delete add-on' }))

    await waitFor(() => {
      expect(deleteSpy).toHaveBeenCalledWith('/addons/7')
    })
    expect(await screen.findByText('The add-on was deleted.')).toBeInTheDocument()
  })

  it('filters the list by description, category or department', async () => {
    renderPage()

    await screen.findByText('Carving station')
    await userEvent.type(screen.getByLabelText('Search'), 'Pastry')

    expect(screen.getByText('Dessert bar')).toBeInTheDocument()
    expect(screen.queryByText('Carving station')).not.toBeInTheDocument()
    expect(screen.queryByText('Cocktail welcome')).not.toBeInTheDocument()
  })
})
