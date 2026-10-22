import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ReactNode } from 'react'
import { AuthProvider } from '@/context/AuthContext'
import { RouteErrorBoundary } from '@/components/feedback/RouteErrorBoundary'
import { AppLayout } from '@/layouts/AppLayout'
import { createTestQueryClient, seedSession } from '@/test/testHelpers'

/**
 * A child that throws while `shouldThrow` is true.
 *
 * The flag is mutable so the test can prove that "Try again" re-renders the
 * children rather than leaving the fallback on screen.
 */
let shouldThrow = true

function ExplodingChild() {
  if (shouldThrow) {
    throw new Error('The screen blew up while rendering.')
  }
  return <div>Recovered screen</div>
}

/** A route element that always throws — used for the shell test. */
function ExplodingRoute(): ReactNode {
  throw new Error('The route blew up while rendering.')
}

describe('RouteErrorBoundary', () => {
  beforeEach(() => {
    shouldThrow = true
    // React logs the caught error; keep the test output clean.
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  it('renders the fallback with the error message and a try-again control', async () => {
    render(
      <RouteErrorBoundary area="this screen">
        <ExplodingChild />
      </RouteErrorBoundary>,
    )

    const fallback = await screen.findByTestId('route-error-boundary')

    expect(fallback).toHaveAttribute('role', 'alert')
    expect(fallback).toHaveTextContent('This screen could not be displayed')
    // The server's / the runtime's own message is shown verbatim.
    expect(fallback).toHaveTextContent('The screen blew up while rendering.')
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()

    // The failure is logged so it is diagnosable.
    expect(console.error).toHaveBeenCalled()
  })

  it('re-renders the children when the operator tries again', async () => {
    render(
      <RouteErrorBoundary area="this screen">
        <ExplodingChild />
      </RouteErrorBoundary>,
    )

    await screen.findByTestId('route-error-boundary')

    // The child recovers, then the operator asks the boundary to try again.
    shouldThrow = false
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Recovered screen')).toBeInTheDocument()
    expect(screen.queryByTestId('route-error-boundary')).not.toBeInTheDocument()
  })

  it('keeps the application shell alive when a route throws', async () => {
    seedSession()

    render(
      <QueryClientProvider client={createTestQueryClient()}>
        <MemoryRouter initialEntries={['/boom']}>
          <AuthProvider>
            <Routes>
              <Route element={<AppLayout />}>
                <Route path="/boom" element={<ExplodingRoute />} />
              </Route>
            </Routes>
          </AuthProvider>
        </MemoryRouter>
      </QueryClientProvider>,
    )

    // The error is shown in the content area …
    const fallback = await screen.findByTestId('route-error-boundary')
    expect(fallback).toHaveTextContent('This screen could not be displayed')
    expect(fallback).toHaveTextContent('The route blew up while rendering.')

    // … while the shell survives: the navigation and the top bar are still
    // on screen, so the operator can navigate away.
    expect(screen.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument()
    expect(screen.getByRole('banner')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument()

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
    })
  })
})
