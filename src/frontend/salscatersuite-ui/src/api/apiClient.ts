/**
 * The single axios instance used by the whole client.
 *
 * Responsibilities:
 *  1. attach the bearer token to every outbound request (interceptor);
 *  2. normalise the **two** error envelopes the API actually returns into one
 *     typed `ApiError`:
 *       - `{ "errors": ["..."] }`            — contract §7 (validation, 401, 403, 404…)
 *       - `{ "message": "..." }`             — `AuthController` (login 400/401)
 *     plus the `ProblemDetails` `detail` for a 500 (contract §7);
 *  3. signal the session layer on a 401/403 so the user is returned to the
 *     login page (AC-F12).
 */

import axios from 'axios'
import type { AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios'
import { getStoredToken, notifyUnauthorized } from './tokenStore'

export class ApiError extends Error {
  /** HTTP status of the failed call (`0` for a network failure). */
  readonly status: number
  /** Verbatim messages from the API — `errors[]`, `message`, or the ProblemDetails title. */
  readonly errors: string[]
  /** `ProblemDetails.detail` when the server answered with a 500. */
  readonly detail: string | null

  constructor(status: number, errors: string[], detail: string | null = null) {
    super(errors[0] ?? 'An unexpected error occurred.')
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
    this.detail = detail
  }
}

interface ProblemDetailsBody {
  title?: unknown
  detail?: unknown
  status?: unknown
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function readString(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 ? value : null
}

/**
 * Extracts the message list and the ProblemDetails detail from any response
 * body the API may produce.
 */
export function extractErrorPayload(
  body: unknown,
): { errors: string[]; detail: string | null } {
  if (!isRecord(body)) {
    return { errors: [], detail: null }
  }

  const errorsField = body.errors
  if (Array.isArray(errorsField)) {
    const messages = errorsField
      .map((entry) => readString(entry))
      .filter((entry): entry is string => entry !== null)
    if (messages.length > 0) {
      return { errors: messages, detail: null }
    }
  }

  const problem = body as ProblemDetailsBody
  const detail = readString(problem.detail)
  const title = readString(problem.title)
  const message = readString(body.message)

  if (detail) {
    // A ProblemDetails `detail` is the most specific message the server gives.
    return { errors: [detail], detail }
  }
  if (message) {
    return { errors: [message], detail: null }
  }
  if (title) {
    return { errors: [title], detail: null }
  }
  return { errors: [], detail: null }
}

const FALLBACK_MESSAGES: Record<number, string> = {
  400: 'The request was rejected by the server.',
  401: 'Authentication is required.',
  403: 'You are not authorized to perform this action.',
  404: 'The requested resource was not found.',
  409: 'The request conflicts with existing data.',
  500: 'The server could not complete the request.',
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error

  if (axios.isAxiosError(error)) {
    const status = error.response?.status ?? 0
    const { errors, detail } = extractErrorPayload(error.response?.data)

    if (status === 401 || status === 403) {
      notifyUnauthorized(status)
    }

    if (errors.length > 0) {
      return new ApiError(status, errors, detail)
    }
    if (status === 0) {
      return new ApiError(status, ['The API could not be reached. Is the backend running?'])
    }
    return new ApiError(status, [FALLBACK_MESSAGES[status] ?? 'The request failed.'])
  }

  return new ApiError(0, ['An unexpected error occurred.'])
}

export const apiClient: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 30_000,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = getStoredToken()
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  return config
})

apiClient.interceptors.response.use(
  (response: AxiosResponse) => response,
  (error: unknown) => Promise.reject(toApiError(error)),
)
