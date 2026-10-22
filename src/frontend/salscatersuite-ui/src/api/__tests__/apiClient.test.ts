import { AxiosError } from 'axios'
import type { AxiosResponse, InternalAxiosRequestConfig } from 'axios'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiClient, extractErrorPayload, toApiError } from '@/api/apiClient'
import { setUnauthorizedHandler } from '@/api/tokenStore'

function installAdapter(status: number, body: unknown) {
  apiClient.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    const response = {
      data: body,
      status,
      statusText: '',
      headers: {},
      config,
    } as AxiosResponse
    if (status >= 200 && status < 300) {
      return response
    }
    throw new AxiosError('Request failed with status code ' + status, String(status), config, {}, response)
  }
}

afterEach(() => {
  apiClient.defaults.adapter = undefined
  setUnauthorizedHandler(null)
})

describe('extractErrorPayload', () => {
  it('reads the standard errors array', () => {
    const result = extractErrorPayload({ errors: ['Booking transaction not found.'] })
    expect(result.errors).toEqual(['Booking transaction not found.'])
    expect(result.detail).toBeNull()
  })

  it('reads the login message envelope', () => {
    const result = extractErrorPayload({ message: 'Invalid credentials.' })
    expect(result.errors).toEqual(['Invalid credentials.'])
  })

  it('reads the ProblemDetails detail', () => {
    const result = extractErrorPayload({
      type: 'https://example.invalid/error',
      title: 'An error occurred.',
      status: 500,
      detail: 'The ledger could not be reconciled.',
    })
    expect(result.errors).toEqual(['The ledger could not be reconciled.'])
    expect(result.detail).toBe('The ledger could not be reconciled.')
  })

  it('returns an empty message list for an unknown body', () => {
    expect(extractErrorPayload('boom')).toEqual({ errors: [], detail: null })
  })
})

describe('toApiError', () => {
  it('normalises a 400 errors array', async () => {
    installAdapter(400, { errors: ['Period must be in the format YYYY-MM.'] })

    const error = await apiClient.get('/dashboard').catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(400)
    expect(apiError.errors).toEqual(['Period must be in the format YYYY-MM.'])
  })

  it('normalises a 401 and notifies the session layer', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    installAdapter(401, { errors: ['Authentication is required.'] })

    const error = await apiClient.get('/dashboard').catch((caught: unknown) => caught)

    expect((error as ApiError).status).toBe(401)
    expect((error as ApiError).errors).toEqual(['Authentication is required.'])
    expect(handler).toHaveBeenCalledWith(401)
  })

  it('normalises a 500 ProblemDetails body', async () => {
    installAdapter(500, { title: 'An error occurred.', detail: 'The ledger could not be reconciled.' })

    const error = await apiClient.get('/dashboard').catch((caught: unknown) => caught)

    expect((error as ApiError).status).toBe(500)
    expect((error as ApiError).detail).toBe('The ledger could not be reconciled.')
    expect((error as ApiError).errors).toEqual(['The ledger could not be reconciled.'])
  })

  it('normalises the login envelope that answers with a message', async () => {
    installAdapter(401, { message: 'Invalid credentials.' })

    const error = await apiClient
      .post('/Auth/login', { username: 'admin', password: 'wrong' })
      .catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    const apiError = error as ApiError
    expect(apiError.status).toBe(401)
    expect(apiError.errors).toEqual(['Invalid credentials.'])
  })

  it('falls back to a readable message for an empty body', () => {
    const error = toApiError(
      new AxiosError('failed', '404', undefined, undefined, {
        data: {},
        status: 404,
        statusText: '',
        headers: {},
        config: {} as InternalAxiosRequestConfig,
      }),
    )
    expect(error.status).toBe(404)
    expect(error.errors.length).toBe(1)
  })
})
