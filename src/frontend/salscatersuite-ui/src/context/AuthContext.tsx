/**
 * Authentication state for the whole client.
 *
 * - `login` / `logout`
 * - session persistence in `localStorage` and hydration on app start
 * - `user` (fullName, userName, roles[]) and a `hasRole(...)` helper
 * - a 401/403 from any request clears the session and returns the user to the
 *   login page (AC-F12)
 *
 * Authorization is enforced by the API (BR-12). The role list here is used for
 * presentation only — it never gates a capability the API would allow.
 */

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { apiClient } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import {
  clearSession,
  isTokenExpired,
  readSession,
  readTokenFullName,
  readTokenRoles,
  readTokenUserId,
  readTokenUserName,
  setUnauthorizedHandler,
  writeSession,
} from '@/api/tokenStore'
import type { AuthStatus, AuthUser, LoginResponse } from '@/types/auth.types'

export interface AuthContextValue {
  status: AuthStatus
  user: AuthUser | null
  token: string | null
  isAuthenticated: boolean
  login: (username: string, password: string) => Promise<AuthUser>
  logout: () => void
  hasRole: (...roles: string[]) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

interface HydratedSession {
  status: AuthStatus
  user: AuthUser | null
  token: string | null
}

function hydrateSession(): HydratedSession {
  const session = readSession()
  if (!session) {
    return { status: 'anonymous', user: null, token: null }
  }
  if (isTokenExpired(session.token)) {
    clearSession()
    return { status: 'anonymous', user: null, token: null }
  }

  const tokenRoles = readTokenRoles(session.token)
  const user: AuthUser = {
    userId: readTokenUserId(session.token) ?? session.user.userId,
    userName: readTokenUserName(session.token) ?? session.user.userName,
    fullName: readTokenFullName(session.token) ?? session.user.fullName,
    roles: tokenRoles.length > 0 ? tokenRoles : session.user.roles,
  }

  return { status: 'authenticated', user, token: session.token }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<HydratedSession>(hydrateSession)
  const logoutRef = useRef<() => void>(() => undefined)

  const logout = useCallback(() => {
    clearSession()
    setSession({ status: 'anonymous', user: null, token: null })
  }, [])

  logoutRef.current = logout

  // A 401/403 from any request ends the session and the protected route guard
  // sends the user back to the login page (AC-F12).
  useEffect(() => {
    setUnauthorizedHandler(() => {
      logoutRef.current()
    })
    return () => setUnauthorizedHandler(null)
  }, [])

  const login = useCallback(async (username: string, password: string) => {
    const response = await apiClient.post<LoginResponse>(API_ENDPOINTS.auth.login, {
      username,
      password,
    })
    const payload = response.data
    const user: AuthUser = {
      userId: payload.userId,
      userName: payload.userName,
      fullName: payload.fullName,
      roles: Array.isArray(payload.roles) ? payload.roles : [],
    }
    writeSession({ token: payload.token, user })
    setSession({ status: 'authenticated', user, token: payload.token })
    return user
  }, [])

  const hasRole = useCallback(
    (...roles: string[]) => {
      const assigned = session.user?.roles ?? []
      return roles.some((role) => assigned.includes(role))
    },
    [session.user],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      status: session.status,
      user: session.user,
      token: session.token,
      isAuthenticated: session.status === 'authenticated',
      login,
      logout,
      hasRole,
    }),
    [session, login, logout, hasRole],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used inside an <AuthProvider>.')
  }
  return context
}
