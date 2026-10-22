/**
 * Session token storage.
 *
 * The bearer token is persisted in `localStorage` so a page reload keeps the
 * session (spec B5 / AC-F11). The stored payload is the token plus the user
 * profile the login endpoint returned — the profile is re-read from the JWT
 * claims when the session is hydrated, so a tampered local entry cannot grant
 * a different identity.
 */

import type { AuthUser } from '@/types/auth.types'

const SESSION_STORAGE_KEY = 'salscatersuite.session'

export interface StoredSession {
  token: string
  user: AuthUser
}

/** Registers the callback fired when the API answers 401/403. */
type UnauthorizedHandler = (status: number) => void

let unauthorizedHandler: UnauthorizedHandler | null = null

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

export function notifyUnauthorized(status: number): void {
  unauthorizedHandler?.(status)
}

export function readSession(): StoredSession | null {
  if (typeof window === 'undefined') return null
  try {
    const raw = window.localStorage.getItem(SESSION_STORAGE_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<StoredSession>
    if (typeof parsed.token !== 'string' || parsed.token.length === 0) return null
    return {
      token: parsed.token,
      user: {
        userId: parsed.user?.userId ?? '',
        userName: parsed.user?.userName ?? '',
        fullName: parsed.user?.fullName ?? '',
        roles: Array.isArray(parsed.user?.roles) ? parsed.user.roles : [],
      },
    }
  } catch {
    return null
  }
}

export function writeSession(session: StoredSession): void {
  if (typeof window === 'undefined') return
  try {
    window.localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session))
  } catch {
    /* Storage unavailable (private mode / quota) — the session stays in memory. */
  }
}

export function clearSession(): void {
  if (typeof window === 'undefined') return
  try {
    window.localStorage.removeItem(SESSION_STORAGE_KEY)
  } catch {
    /* ignore */
  }
}

export function getStoredToken(): string | null {
  return readSession()?.token ?? null
}

export type JwtClaims = Record<string, unknown>

/**
 * Decodes the JWT payload (no signature verification — the API is the only
 * authority) so the session can be hydrated and its expiry checked without a
 * round-trip.
 */
export function decodeJwtClaims(token: string): JwtClaims | null {
  const parts = token.split('.')
  if (parts.length !== 3) return null
  try {
    const normalised = parts[1].replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalised.padEnd(normalised.length + ((4 - (normalised.length % 4)) % 4), '=')
    const json = decodeURIComponent(
      atob(padded)
        .split('')
        .map((character) => `%${character.charCodeAt(0).toString(16).padStart(2, '0')}`)
        .join(''),
    )
    return JSON.parse(json) as JwtClaims
  } catch {
    return null
  }
}

function readClaim(claims: JwtClaims | null, keys: string[]): string | null {
  if (!claims) return null
  for (const key of keys) {
    const value = claims[key]
    if (typeof value === 'string' && value.trim().length > 0) return value
  }
  return null
}

/** `true` when the token carries an `exp` claim that has already passed. */
export function isTokenExpired(token: string, now: number = Date.now()): boolean {
  const claims = decodeJwtClaims(token)
  const exp = claims?.exp
  if (typeof exp !== 'number') return false
  return exp * 1000 <= now
}

/**
 * Reads the role claims out of a bearer token.
 * .NET writes `ClaimTypes.Role` as the `role` claim (an array when the user
 * holds more than one role); the `roles` key is accepted as a fallback.
 */
export function readTokenRoles(token: string): string[] {
  const claims = decodeJwtClaims(token)
  if (!claims) return []
  const candidates = [claims.role, claims.roles]
  for (const candidate of candidates) {
    if (Array.isArray(candidate)) {
      const roles = candidate.filter(
        (role): role is string => typeof role === 'string' && role.length > 0,
      )
      if (roles.length > 0) return roles
    }
    if (typeof candidate === 'string' && candidate.length > 0) {
      return [candidate]
    }
  }
  return []
}

/** Reads the full name claim out of a bearer token. */
export function readTokenFullName(token: string): string | null {
  return readClaim(decodeJwtClaims(token), ['fullName'])
}

/** Reads the user name claim out of a bearer token. */
export function readTokenUserName(token: string): string | null {
  return readClaim(decodeJwtClaims(token), ['unique_name', 'name'])
}

/** Reads the user id claim out of a bearer token. */
export function readTokenUserId(token: string): string | null {
  return readClaim(decodeJwtClaims(token), ['nameid', 'sub'])
}
