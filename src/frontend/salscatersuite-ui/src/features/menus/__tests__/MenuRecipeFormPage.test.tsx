import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { MenuRecipeFormPage } from '@/features/menus/MenuRecipeFormPage'
import { MENU_MASTER_QUERY_KEY } from '@/features/menus/hooks/useMenuMaster'
import {
  menuCoursesFixture,
  menuDetailFixture,
  menuPreviewFixture,
} from '@/test/menuFixture'
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

function renderRecipeForm(route: string) {
  const queryClient = createTestQueryClient()
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="/menus" element={<div>Menu catalogue</div>} />
          <Route path="/menus/new" element={<MenuRecipeFormPage />} />
          <Route path="/menus/:menuId/edit" element={<MenuRecipeFormPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { ...utils, queryClient, invalidateSpy }
}

/** Routes each call to the payload that matches its endpoint. */
function mockApi() {
  vi.spyOn(apiClient, 'get').mockImplementation(
    async (url: string): Promise<AxiosResponse> => {
      if (url.includes('/menus/courses')) return axiosResponse(menuCoursesFixture)
      if (url.includes('/menus/')) return axiosResponse(menuDetailFixture)
      return axiosResponse({})
    },
  )
  vi.spyOn(apiClient, 'post').mockImplementation(
    async (url: string): Promise<AxiosResponse> => {
      if (url.includes('/costing/preview')) return axiosResponse(menuPreviewFixture)
      return axiosResponse({ menuId: 'DS-900' })
    },
  )
  vi.spyOn(apiClient, 'put').mockResolvedValue(axiosResponse(null, 204))
}

async function fillTheForm() {
  await userEvent.type(screen.getByLabelText('Recipe ID'), 'DS-900')
  await userEvent.type(
    screen.getByLabelText('Official Banquet Menu Name'),
    'Pan-Roasted Herb-Crusted Salmon Fillet',
  )
  await userEvent.selectOptions(screen.getByLabelText('Course Category'), '4')
  await userEvent.type(screen.getByLabelText('Selling Price per Pax'), '380')
  await userEvent.type(screen.getByLabelText('Food Cost'), '104.2')
}

describe('MenuRecipeFormPage', () => {
  beforeEach(() => {
    mockApi()
  })

  it('fetches the margin preview from the server and renders it verbatim', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderRecipeForm('/menus/new')

    await fillTheForm()

    // The preview travels to the server — the browser never derives it.
    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith('/menus/costing/preview', {
        sellingPrice: 380,
        foodCost: 104.2,
      })
    })

    // The server's own figure, at the precision it published.
    const preview = await screen.findAllByText('27.42%', undefined, { timeout: 5000 })
    expect(preview.length).toBeGreaterThan(0)
    // A locally rounded figure would read 27.4%.
    expect(screen.queryByText('27.4%')).not.toBeInTheDocument()

    // The gross profit and the gross margin are the server's too.
    expect(screen.getAllByText('₱275.80').length).toBeGreaterThan(0)
    expect(screen.getAllByText('72.58%').length).toBeGreaterThan(0)
  })

  it('renders the server’s validation errors verbatim and keeps the typed input', async () => {
    vi.spyOn(apiClient, 'post').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/costing/preview')) return axiosResponse(menuPreviewFixture)
        throw new ApiError(400, [
          'Official Banquet Menu Name is required.',
          'Price must not be negative.',
        ])
      },
    )

    renderRecipeForm('/menus/new')

    await fillTheForm()
    await userEvent.click(screen.getByRole('button', { name: 'Save Recipe' }))

    expect(await screen.findByText('Official Banquet Menu Name is required.')).toBeInTheDocument()
    expect(screen.getByText('Price must not be negative.')).toBeInTheDocument()

    // The operator's input survived the rejected save (AC-M16).
    expect(screen.getByLabelText('Recipe ID')).toHaveValue('DS-900')
    expect(screen.getByLabelText('Official Banquet Menu Name')).toHaveValue(
      'Pan-Roasted Herb-Crusted Salmon Fillet',
    )
    expect(screen.getByLabelText('Selling Price per Pax')).toHaveValue('380.00')
  })

  it('shows the duplicate-id message on the recipe-id field', async () => {
    vi.spyOn(apiClient, 'post').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/costing/preview')) return axiosResponse(menuPreviewFixture)
        throw new ApiError(409, ['A dish with the recipe id DS-900 already exists.'])
      },
    )

    renderRecipeForm('/menus/new')

    await fillTheForm()
    await userEvent.click(screen.getByRole('button', { name: 'Save Recipe' }))

    expect(
      await screen.findByText('A dish with the recipe id DS-900 already exists.'),
    ).toBeInTheDocument()
  })

  it('invalidates the master query and returns to the catalogue after a save', async () => {
    const { invalidateSpy } = renderRecipeForm('/menus/new')

    await fillTheForm()
    await userEvent.click(screen.getByRole('button', { name: 'Save Recipe' }))

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [MENU_MASTER_QUERY_KEY] })
    })
    expect(await screen.findByText('Menu catalogue')).toBeInTheDocument()
  })

  it('prevents a duplicate submission while a save is in flight', async () => {
    let resolveSave: (value: AxiosResponse) => void = () => undefined
    vi.spyOn(apiClient, 'post').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/costing/preview')) return axiosResponse(menuPreviewFixture)
        return new Promise<AxiosResponse>((resolve) => {
          resolveSave = resolve
        })
      },
    )

    renderRecipeForm('/menus/new')

    await fillTheForm()
    await userEvent.click(screen.getByRole('button', { name: 'Save Recipe' }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Saving…' })).toBeDisabled()
    })

    resolveSave(axiosResponse({ menuId: 'DS-900' }))

    await waitFor(() => {
      expect(screen.getByText('Menu catalogue')).toBeInTheDocument()
    })
  })

  it('seeds the edit form from the dish the API published', async () => {
    renderRecipeForm('/menus/DS-104/edit')

    await waitFor(() => {
      expect(screen.getByLabelText('Recipe ID')).toHaveValue('DS-104')
    })
    expect(screen.getByLabelText('Official Banquet Menu Name')).toHaveValue(
      'Prime Roast Angus Ribeye with Truffle Demi-Glace',
    )
    expect(screen.getByLabelText('Selling Price per Pax')).toHaveValue('420.00')
    expect(screen.getByLabelText('Food Cost')).toHaveValue('138.60')
    expect(screen.getByLabelText('Course Category')).toHaveValue('4')
    // The recipe code identifies the record and is not re-keyed.
    expect(screen.getByLabelText('Recipe ID')).toHaveAttribute('readonly')
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeInTheDocument()
  })

  it('renders the capability gaps where the reference shows the recipe engine', async () => {
    renderRecipeForm('/menus/new')

    expect(await screen.findByText('Ingredient formulation table unavailable')).toBeInTheDocument()
    expect(screen.getByText('Yield & shrinkage factors unavailable')).toBeInTheDocument()
    expect(screen.getByText('Plating & prep directives unavailable')).toBeInTheDocument()
    expect(screen.getByText('Allergen safety tags unavailable')).toBeInTheDocument()
    expect(screen.getByText('Package tier suitability unavailable')).toBeInTheDocument()

    // Nothing is faked: there is no ingredient table and no ingredient row.
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('row')).not.toBeInTheDocument()
  })

  it('renders the error state when the dish cannot be loaded', async () => {
    vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/menus/courses')) return axiosResponse(menuCoursesFixture)
        throw new ApiError(404, ['No dish was found for that recipe id.'])
      },
    )

    renderRecipeForm('/menus/DS-404/edit')

    const alert = await screen.findByRole('alert', undefined, { timeout: 8000 })
    expect(within(alert).getByText('No dish was found for that recipe id.')).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: /retry/i })).toBeInTheDocument()
  })
})
