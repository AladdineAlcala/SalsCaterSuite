import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@/context/AuthContext'
import { SpinnerLoader } from '@/components/feedback/SpinnerLoader'

export interface ProtectedRouteProps {
  children: ReactNode
}

/**
 * Route guard.
 *
 * An unauthenticated visitor to any protected route is redirected to the
 * login page (AC-F11). The session is hydrated synchronously from
 * `localStorage` on app start, so the guard never flickers between states.
 */
export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'initialising') {
    return (
      <div className="min-h-screen bg-artisan-canvas flex items-center justify-center">
        <SpinnerLoader label="Restoring session…" />
      </div>
    )
  }

  if (status !== 'authenticated') {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <>{children}</>
}
