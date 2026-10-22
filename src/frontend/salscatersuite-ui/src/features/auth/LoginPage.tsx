import { useState } from 'react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { LogIn } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { useAuth } from '@/context/AuthContext'

interface FieldErrors {
  username?: string
  password?: string
}

/**
 * Sign-in screen.
 *
 * Posts to `POST /api/Auth/login` and stores the returned bearer token.
 * The API answers a failure with `{ message }` (not `{ errors }`), so the
 * client normalises both envelopes and shows the server's message inline.
 *
 * No credential is pre-filled, hinted at or displayed: the bootstrap
 * administrator credential is a deployment secret (DEC-018 / DEC-030).
 */
export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formErrors, setFormErrors] = useState<string[]>([])
  const [submitting, setSubmitting] = useState(false)

  function validate(): FieldErrors {
    const errors: FieldErrors = {}
    if (username.trim().length === 0) {
      errors.username = 'Enter your username.'
    }
    if (password.length === 0) {
      errors.password = 'Enter your password.'
    }
    return errors
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const errors = validate()
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) {
      return
    }

    setSubmitting(true)
    setFormErrors([])
    try {
      await login(username.trim(), password)
      const state = location.state as { from?: string } | null
      const destination = state?.from && state.from !== '/login' ? state.from : '/'
      navigate(destination, { replace: true })
    } catch (error) {
      if (error instanceof ApiError) {
        setFormErrors(error.errors)
      } else {
        setFormErrors(['Sign-in failed. Please try again.'])
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Card elevation={2} className="w-full">
      <div className="flex flex-col gap-1 mb-space-md">
        <h1 className="font-headline-lg text-headline-lg text-espresso-900">Sign in</h1>
        <p className="font-body-sm text-body-sm text-espresso-600">
          Use your Sal&apos;s CaterSuite operations account to continue.
        </p>
      </div>

      {formErrors.length > 0 ? (
        <AlertBanner
          title="Sign-in failed"
          errors={formErrors}
          className="mb-space-md"
          action={
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setFormErrors([])}
              aria-label="Dismiss sign-in error"
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-space-md">
        <div className="flex flex-col gap-1.5">
          <label
            htmlFor="username"
            className="font-body-sm text-body-sm font-semibold text-espresso-700"
          >
            Username
          </label>
          <input
            id="username"
            name="username"
            type="text"
            autoComplete="username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            aria-invalid={fieldErrors.username ? true : undefined}
            aria-describedby={fieldErrors.username ? 'username-error' : undefined}
            className="h-10 rounded-lg border border-artisan-border bg-artisan-surface px-3 font-body-sm text-body-sm text-espresso-900 placeholder:text-espresso-400"
          />
          {fieldErrors.username ? (
            <p id="username-error" className="font-caption text-caption text-error">
              {fieldErrors.username}
            </p>
          ) : null}
        </div>

        <div className="flex flex-col gap-1.5">
          <label
            htmlFor="password"
            className="font-body-sm text-body-sm font-semibold text-espresso-700"
          >
            Password
          </label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            aria-invalid={fieldErrors.password ? true : undefined}
            aria-describedby={fieldErrors.password ? 'password-error' : undefined}
            className="h-10 rounded-lg border border-artisan-border bg-artisan-surface px-3 font-body-sm text-body-sm text-espresso-900"
          />
          {fieldErrors.password ? (
            <p id="password-error" className="font-caption text-caption text-error">
              {fieldErrors.password}
            </p>
          ) : null}
        </div>

        <Button
          type="submit"
          variant="primary"
          icon={LogIn}
          disabled={submitting}
          className="w-full"
        >
          {submitting ? 'Signing in…' : 'Sign in'}
        </Button>
      </form>
    </Card>
  )
}
