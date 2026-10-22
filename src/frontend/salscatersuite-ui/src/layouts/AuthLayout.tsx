import type { ReactNode } from 'react'
import { Soup } from 'lucide-react'

export interface AuthLayoutProps {
  children: ReactNode
}

/**
 * The unauthenticated shell used by the login screen: the warm artisan canvas
 * with a single centred card.
 */
export function AuthLayout({ children }: AuthLayoutProps) {
  return (
    <div className="min-h-screen bg-artisan-canvas flex flex-col items-center justify-center px-4 py-10">
      <div className="w-full max-w-md">
        <div className="flex items-center gap-3 justify-center mb-6">
          <span className="w-10 h-10 rounded-xl bg-espresso-900 text-caramel-200 flex items-center justify-center shadow-elevation-2">
            <Soup className="h-5 w-5" aria-hidden="true" />
          </span>
          <span className="flex flex-col leading-tight">
            <span className="font-headline-sm text-headline-sm text-espresso-900">
              Sal&apos;s CaterSuite
            </span>
            <span className="font-caption text-caption uppercase tracking-wider text-espresso-400">
              Catering Operations
            </span>
          </span>
        </div>
        {children}
      </div>
    </div>
  )
}
