import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AxiosResponse } from 'axios'
import { apiClient, ApiError } from '@/api/apiClient'
import { PackageTierFormPage } from '@/features/packages/PackageTierFormPage'
import {
  PACKAGE_LIST_QUERY_KEY,
  PACKAGE_SUMMARY_QUERY_KEY,
} from '@/features/packages/hooks/usePackageCatalogue'
import { packagePaxApplicablesFixture, packageTierFixture } from '@/test/packageFixture'
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

function renderTierForm(route: string) {
  const queryClient = createTestQueryClient()
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')
  const utils = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="/packages" element={<div>Package catalogue</div>} />
          <Route path="/packages/new" element={<PackageTierFormPage />} />
          <Route path="/packages/:packageId/edit" element={<PackageTierFormPage />} />
          <Route path="/packages/:packageId/rules" element={<div>Course rules workbench</div>} />
          <Route
            path="/packages/:packageId/locations"
            element={<div>Location matrix workbench</div>}
          />
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
      // The literal segment must be routed before the `/{id}` capture.
      if (url.includes('/packages/pax-applicables')) {
        return axiosResponse(packagePaxApplicablesFixture)
      }
      if (url.includes('/packages/')) return axiosResponse(packageTierFixture)
      return axiosResponse({})
    },
  )
  vi.spyOn(apiClient, 'post').mockResolvedValue(axiosResponse({ packageId: 91 }))
  vi.spyOn(apiClient, 'put').mockResolvedValue(axiosResponse(null, 204))
}

describe('PackageTierFormPage', () => {
  beforeEach(() => {
    mockApi()
  })

  it('seeds the edit form from the tier the API published', async () => {
    renderTierForm('/packages/12/edit')

    await waitFor(() => {
      expect(screen.getByLabelText('Tier type')).toHaveValue('Wedding')
    })
    expect(screen.getByLabelText('Tier name')).toHaveValue('Wedding Grand Tier')
    expect(screen.getByLabelText('Rate per guest')).toHaveValue('1,450.00')
    expect(screen.getByLabelText('Minimum pax')).toHaveValue(150)
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeInTheDocument()
  })

  it('loads the five classifications from the lookup endpoint in ID order', async () => {
    renderTierForm('/packages/new')

    // The options arrive with the lookup query; wait for the first of them.
    await screen.findByRole('option', { name: '15 to 19 Pax' })

    const select = screen.getByLabelText('Applicable pax classification')
    const options = Array.from(select.querySelectorAll('option')).filter(
      (option) => option.value !== '',
    )

    expect(options.map((option) => option.textContent)).toEqual([
      '15 to 19 Pax',
      '20 to 29 Pax',
      '30 to 49 Pax',
      '80 to 100 Above',
      'Not Applicable to PackMeals',
    ])
    expect(options.map((option) => option.value)).toEqual(['1', '2', '3', '4', '5'])
    expect(apiClient.get).toHaveBeenCalledWith('/packages/pax-applicables')
  })

  it('preselects the saved classification in edit mode', async () => {
    renderTierForm('/packages/12/edit')

    // The tier fixture carries numPaxId 4 — "80 to 100 Above".
    await waitFor(() => {
      expect(screen.getByLabelText('Applicable pax classification')).toHaveValue('4')
    })
  })

  it('disables the selector and the save while the classifications load', async () => {
    let resolveLookup: ((response: AxiosResponse) => void) | undefined
    vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/packages/pax-applicables')) {
          return new Promise<AxiosResponse>((resolve) => {
            resolveLookup = resolve
          })
        }
        if (url.includes('/packages/')) return axiosResponse(packageTierFixture)
        return axiosResponse({})
      },
    )

    renderTierForm('/packages/new')

    const select = await screen.findByLabelText('Applicable pax classification')
    expect(select).toBeDisabled()
    expect(
      screen.getByText('Loading the applicable-pax classifications…'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save Package' })).toBeDisabled()

    resolveLookup?.(axiosResponse(packagePaxApplicablesFixture))

    await waitFor(() => {
      expect(screen.getByLabelText('Applicable pax classification')).toBeEnabled()
    })
    expect(screen.getByRole('button', { name: 'Save Package' })).toBeEnabled()
  })

  it('renders an explicit lookup failure with a retry that recovers', async () => {
    vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/packages/pax-applicables')) {
          throw new ApiError(500, ['The applicable-pax lookup could not be loaded.'])
        }
        if (url.includes('/packages/')) return axiosResponse(packageTierFixture)
        return axiosResponse({})
      },
    )

    renderTierForm('/packages/new')

    const select = await screen.findByLabelText('Applicable pax classification')
    expect(select).toBeDisabled()
    expect(
      await screen.findByText('The applicable-pax lookup could not be loaded.', undefined, {
        timeout: 8000,
      }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save Package' })).toBeDisabled()

    // The select's description references the rendered lookup-error block,
    // never a hint node the failure removed (a11y review correction).
    expect(select).toHaveAttribute(
      'aria-describedby',
      'package-num-pax-id-lookup-error',
    )

    // The next lookup attempt succeeds and unblocks the form.
    vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/packages/pax-applicables')) {
          return axiosResponse(packagePaxApplicablesFixture)
        }
        if (url.includes('/packages/')) return axiosResponse(packageTierFixture)
        return axiosResponse({})
      },
    )

    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))

    await waitFor(() => {
      expect(screen.getByLabelText('Applicable pax classification')).toBeEnabled()
    })
    expect(screen.getByRole('button', { name: 'Save Package' })).toBeEnabled()

    // Recovered, the description points back at the rendered hint.
    expect(select).toHaveAttribute('aria-describedby', 'package-num-pax-id-hint')
  })

  it('explains an empty classification list and keeps the save blocked', async () => {
    vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/packages/pax-applicables')) return axiosResponse([])
        if (url.includes('/packages/')) return axiosResponse(packageTierFixture)
        return axiosResponse({})
      },
    )

    renderTierForm('/packages/new')

    const select = await screen.findByLabelText('Applicable pax classification')
    expect(
      await screen.findByText(
        'No applicable-pax classifications are defined. The tier cannot be saved without one.',
      ),
    ).toBeInTheDocument()
    expect(select).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Save Package' })).toBeDisabled()
  })

  it('blocks the save with the required message when no classification is chosen', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderTierForm('/packages/new')

    await screen.findByLabelText('Applicable pax classification')
    await userEvent.type(screen.getByLabelText('Tier type'), 'Wedding')
    await userEvent.type(screen.getByLabelText('Tier name'), 'Wedding Grand Tier')
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    expect(
      await screen.findByText('Applicable pax classification is required.'),
    ).toBeInTheDocument()
    expect(postSpy).not.toHaveBeenCalled()
  })

  it('offers the known tier names as suggestions without imposing them', async () => {
    renderTierForm('/packages/new')

    const options = screen
      .getByLabelText('Tier type')
      .closest('div')
      ?.querySelectorAll('datalist option')

    expect(options).toHaveLength(6)
    expect(Array.from(options ?? []).map((option) => option.getAttribute('value'))).toEqual([
      'Regular',
      'Wedding',
      'Vip',
      'Premier',
      'Pm',
      'Sd',
    ])

    // A value outside the suggestion list is accepted and never rewritten.
    await userEvent.type(screen.getByLabelText('Tier type'), 'Merienda')
    expect(screen.getByLabelText('Tier type')).toHaveValue('Merienda')
  })

  it('rejects a blank tier type on the client with the server wording', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier name'), 'Wedding Grand Tier')
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    expect(await screen.findByText('Package type is required.')).toBeInTheDocument()
    expect(postSpy).not.toHaveBeenCalled()
  })

  it('caps the tier type at the column length', async () => {
    renderTierForm('/packages/new')

    // The column is nvarchar(50), so the browser must not accept more than the
    // server would store (DEC-075).
    expect(screen.getByLabelText('Tier type')).toHaveAttribute('maxlength', '50')
    expect(screen.getByLabelText('Tier name')).toHaveAttribute('maxlength', '250')
  })

  it('rejects a blank tier name on the client and never calls the API', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier type'), 'Wedding')
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    // The client message is the server's own wording, so the operator reads one
    // message wherever the request is rejected (DEC-075).
    expect(await screen.findByText('Package description is required.')).toBeInTheDocument()
    expect(postSpy).not.toHaveBeenCalled()

    // The message sits under the field it belongs to.
    const nameField = screen.getByLabelText('Tier name').closest('div') as HTMLElement
    expect(within(nameField).getByText('Package description is required.')).toBeInTheDocument()
  })

  it('rejects a negative rate and a minimum pax below one on the client', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier type'), 'Wedding')
    await userEvent.type(screen.getByLabelText('Tier name'), 'Wedding Grand Tier')
    await userEvent.type(screen.getByLabelText('Rate per guest'), '-10')
    await userEvent.type(screen.getByLabelText('Minimum pax'), '0')
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    expect(await screen.findByText('Rate per guest must be zero or greater.')).toBeInTheDocument()
    expect(screen.getByText('Minimum pax must be at least 1.')).toBeInTheDocument()
    expect(postSpy).not.toHaveBeenCalled()
  })

  it('renders the server’s errors verbatim and keeps the typed input', async () => {
    vi.spyOn(apiClient, 'post').mockRejectedValue(
      new ApiError(400, [
        'Package description is required.',
        'Rate per guest must be zero or greater.',
      ]),
    )

    renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier type'), 'Wedding')
    await userEvent.type(screen.getByLabelText('Tier name'), 'Wedding Grand Tier')
    await userEvent.type(screen.getByLabelText('Rate per guest'), '1450')
    await userEvent.selectOptions(
      screen.getByLabelText('Applicable pax classification'),
      '4',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    expect(await screen.findByText('Package description is required.')).toBeInTheDocument()
    expect(screen.getByText('Rate per guest must be zero or greater.')).toBeInTheDocument()

    // The operator's input survived the rejected save (AC-P11).
    expect(screen.getByLabelText('Tier type')).toHaveValue('Wedding')
    expect(screen.getByLabelText('Tier name')).toHaveValue('Wedding Grand Tier')
    expect(screen.getByLabelText('Rate per guest')).toHaveValue('1,450.00')
  })

  it('creates the tier and returns to the catalogue', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')
    const { invalidateSpy } = renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier type'), 'Wedding')
    await userEvent.type(screen.getByLabelText('Tier name'), 'Wedding Grand Tier')
    await userEvent.type(screen.getByLabelText('Rate per guest'), '1450')
    await userEvent.type(screen.getByLabelText('Minimum pax'), '150')
    await userEvent.selectOptions(
      screen.getByLabelText('Applicable pax classification'),
      '4',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith('/packages', {
        packageType: 'Wedding',
        description: 'Wedding Grand Tier',
        amountPax: 1450,
        minPax: 150,
        numPaxId: 4,
        extendedLocationAmount: null,
      })
    })

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [PACKAGE_LIST_QUERY_KEY] })
    })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [PACKAGE_SUMMARY_QUERY_KEY] })
    expect(await screen.findByText('Package catalogue')).toBeInTheDocument()
  })

  it('updates the tier and returns to the catalogue after a successful save', async () => {
    const putSpy = vi.spyOn(apiClient, 'put')
    const { invalidateSpy } = renderTierForm('/packages/12/edit')

    await waitFor(() => {
      expect(screen.getByLabelText('Tier name')).toHaveValue('Wedding Grand Tier')
    })

    await userEvent.click(screen.getByRole('button', { name: 'Save Changes' }))

    await waitFor(() => {
      expect(putSpy).toHaveBeenCalledWith('/packages/12', {
        packageType: 'Wedding',
        description: 'Wedding Grand Tier',
        amountPax: 1450,
        minPax: 150,
        numPaxId: 4,
        extendedLocationAmount: null,
      })
    })

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [PACKAGE_LIST_QUERY_KEY] })
    })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: [PACKAGE_SUMMARY_QUERY_KEY] })
    expect(await screen.findByText('Package catalogue')).toBeInTheDocument()
  })

  it('sends a null rate and a null headcount when the operator leaves them empty', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')

    renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier type'), 'Sd')
    await userEvent.type(screen.getByLabelText('Tier name'), 'Special Diet Tailored')
    await userEvent.selectOptions(
      screen.getByLabelText('Applicable pax classification'),
      '2',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith('/packages', {
        packageType: 'Sd',
        description: 'Special Diet Tailored',
        amountPax: null,
        minPax: null,
        numPaxId: 2,
        extendedLocationAmount: null,
      })
    })
  })

  it('cancels back to the catalogue without a write', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')
    const putSpy = vi.spyOn(apiClient, 'put')

    renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier type'), 'Wedding')
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(await screen.findByText('Package catalogue')).toBeInTheDocument()
    expect(postSpy).not.toHaveBeenCalled()
    expect(putSpy).not.toHaveBeenCalled()
  })

  it('renders the error state when the tier cannot be loaded', async () => {
    // Only the tier load fails here; the lookup endpoint is beside the point
    // of this test and keeps serving its rows.
    vi.spyOn(apiClient, 'get').mockImplementation(
      async (url: string): Promise<AxiosResponse> => {
        if (url.includes('/packages/pax-applicables')) {
          return axiosResponse(packagePaxApplicablesFixture)
        }
        throw new ApiError(404, ['No package tier was found for that reference.'])
      },
    )

    renderTierForm('/packages/404/edit')

    const alert = await screen.findByRole('alert', undefined, { timeout: 8000 })
    expect(
      within(alert).getByText('No package tier was found for that reference.'),
    ).toBeInTheDocument()
    expect(within(alert).getByRole('button', { name: /retry/i })).toBeInTheDocument()
  })

  it('offers the course-rules link for a saved tier and opens it', async () => {
    renderTierForm('/packages/12/edit')

    await waitFor(() => {
      expect(screen.getByLabelText('Tier name')).toHaveValue('Wedding Grand Tier')
    })

    await userEvent.click(screen.getByRole('button', { name: 'Course Rules' }))

    expect(await screen.findByText('Course rules workbench')).toBeInTheDocument()
  })

  it('offers the location-coverage link for a saved tier and opens it', async () => {
    renderTierForm('/packages/12/edit')

    await waitFor(() => {
      expect(screen.getByLabelText('Tier name')).toHaveValue('Wedding Grand Tier')
    })

    await userEvent.click(screen.getByRole('button', { name: 'Location Coverage' }))

    expect(await screen.findByText('Location matrix workbench')).toBeInTheDocument()
  })

  it('keeps the workbench links inactive until the tier is saved', async () => {
    renderTierForm('/packages/new')

    await screen.findByLabelText('Tier name')

    expect(
      screen.getByText('Save the tier first to open its course rules and its location coverage.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Course Rules' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
  })

  it('replaces the course-allocation and venue-coverage gaps with the workbench links', async () => {
    renderTierForm('/packages/12/edit')

    await waitFor(() => {
      expect(screen.getByLabelText('Tier name')).toHaveValue('Wedding Grand Tier')
    })

    expect(screen.queryByText('Course allocations unavailable')).not.toBeInTheDocument()
    expect(screen.queryByText('Venue coverage unavailable')).not.toBeInTheDocument()
    expect(screen.getByText('Stored per-tier food cost unavailable')).toBeInTheDocument()
    expect(screen.getByText('Event inclusions unavailable')).toBeInTheDocument()
    expect(screen.getAllByTestId('capability-gap-notice')).toHaveLength(2)
  })

  it('shows the extended location amount only for Regular and Premier', async () => {
    renderTierForm('/packages/new')

    expect(
      screen.queryByLabelText('Extended location amount (per guest)'),
    ).not.toBeInTheDocument()

    const type = screen.getByLabelText('Tier type')
    await userEvent.type(type, 'Regular')
    expect(
      screen.getByLabelText('Extended location amount (per guest)'),
    ).toBeInTheDocument()

    await userEvent.clear(type)
    await userEvent.type(type, 'Wedding')
    expect(
      screen.queryByLabelText('Extended location amount (per guest)'),
    ).not.toBeInTheDocument()
  })

  it('sends the extended location amount for an eligible tier', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')
    renderTierForm('/packages/new')

    await userEvent.type(screen.getByLabelText('Tier type'), 'Regular')
    await userEvent.type(screen.getByLabelText('Tier name'), 'Regular Feast')
    await userEvent.type(screen.getByLabelText('Rate per guest'), '1000')
    await userEvent.type(screen.getByLabelText('Minimum pax'), '50')
    await userEvent.type(
      screen.getByLabelText('Extended location amount (per guest)'),
      '125',
    )
    await userEvent.selectOptions(
      screen.getByLabelText('Applicable pax classification'),
      '4',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith('/packages', {
        packageType: 'Regular',
        description: 'Regular Feast',
        amountPax: 1000,
        minPax: 50,
        numPaxId: 4,
        extendedLocationAmount: 125,
      })
    })
  })

  it('clears the extended amount when the type leaves Regular and Premier', async () => {
    const postSpy = vi.spyOn(apiClient, 'post')
    renderTierForm('/packages/new')

    const type = screen.getByLabelText('Tier type')
    await userEvent.type(type, 'Regular')
    await userEvent.type(screen.getByLabelText('Tier name'), 'Regular Feast')
    await userEvent.type(
      screen.getByLabelText('Extended location amount (per guest)'),
      '125',
    )
    await userEvent.selectOptions(
      screen.getByLabelText('Applicable pax classification'),
      '2',
    )

    await userEvent.clear(type)
    await userEvent.type(type, 'Wedding')

    await userEvent.click(screen.getByRole('button', { name: 'Save Package' }))

    await waitFor(() => {
      expect(postSpy).toHaveBeenCalledWith('/packages', {
        packageType: 'Wedding',
        description: 'Regular Feast',
        amountPax: null,
        minPax: null,
        numPaxId: 2,
        extendedLocationAmount: null,
      })
    })
  })
})
