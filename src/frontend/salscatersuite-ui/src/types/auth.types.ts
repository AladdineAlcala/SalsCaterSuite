/**
 * Authentication contracts.
 *
 * `POST /api/Auth/login` (contract §2 / `AuthController`) answers
 * `{ token, userId, userName, fullName, roles[] }` on success and
 * `{ message }` on a 400/401 — the *other* error envelope the client must
 * normalise alongside the standard `{ errors: string[] }` shape (contract §7).
 */

export interface LoginRequest {
  username: string
  password: string
}

export interface LoginResponse {
  token: string
  userId: string
  userName: string
  fullName: string
  roles: string[]
}

export interface AuthUser {
  userId: string
  userName: string
  fullName: string
  roles: string[]
}

export type AuthStatus = 'initialising' | 'authenticated' | 'anonymous'
