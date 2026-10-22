/**
 * TEST HELPERS — not production data.
 */
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { AuthProvider } from '@/context/AuthContext'
import type { AuthUser } from '@/types/auth.types'

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  })
}

export function renderWithProviders(
  ui: ReactElement,
  options: { route?: string } = {},
): ReturnType<typeof render> {
  const queryClient = createTestQueryClient()
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[options.route ?? '/']}>
        <AuthProvider>{ui}</AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function base64url(value: string): string {
  return Buffer.from(value, 'utf8')
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

/** Builds an unsigned JWT for tests — the API remains the only authority. */
export function createTestToken(payload: Record<string, unknown>): string {
  const header = base64url(JSON.stringify({ alg: 'none', typ: 'JWT' }))
  const body = base64url(JSON.stringify(payload))
  return `${header}.${body}.test-signature`
}

export const testUser: AuthUser = {
  userId: 'user-1',
  userName: 'admin',
  fullName: 'Catering Administrator',
  roles: ['Administrator'],
}

/** Seeds a persisted, non-expired session so the app boots authenticated. */
export function seedSession(
  user: AuthUser = testUser,
  expiresInSeconds = 3600,
): void {
  const token = createTestToken({
    nameid: user.userId,
    unique_name: user.userName,
    fullName: user.fullName,
    role: user.roles,
    exp: Math.floor(Date.now() / 1000) + expiresInSeconds,
  })
  window.localStorage.setItem(
    'salscatersuite.session',
    JSON.stringify({ token, user }),
  )
}
