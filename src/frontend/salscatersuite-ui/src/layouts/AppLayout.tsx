import { useCallback, useRef, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { X } from 'lucide-react'
import { RouteErrorBoundary } from '@/components/feedback/RouteErrorBoundary'
import { AppSidebar } from '@/components/navigation/AppSidebar'
import { TopBar } from '@/components/navigation/TopBar'
import { useFocusTrap } from '@/components/navigation/useFocusTrap'

/**
 * The authenticated application shell.
 *
 * - a persistent 16rem (256px) left rail from the `md` breakpoint upwards;
 * - an off-canvas drawer below `md` that traps focus and closes on `Escape`;
 * - a sticky, blurred top bar.
 */
export function AppLayout() {
  const [navOpen, setNavOpen] = useState(false)
  const drawerRef = useRef<HTMLDivElement>(null)

  const closeNav = useCallback(() => setNavOpen(false), [])
  useFocusTrap(drawerRef, navOpen, closeNav)

  return (
    <div className="min-h-screen bg-artisan-canvas">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:top-3 focus:left-3 focus:rounded-lg focus:bg-artisan-surface focus:px-4 focus:py-2 focus:font-body-sm focus:shadow-elevation-2"
      >
        Skip to main content
      </a>

      <aside className="hidden md:flex md:fixed md:inset-y-0 md:left-0 md:w-64 border-r border-artisan-border z-30">
        <AppSidebar className="w-full" />
      </aside>

      {navOpen ? (
        <div className="md:hidden fixed inset-0 z-40">
          <div
            className="absolute inset-0 bg-espresso-900/40"
            onClick={closeNav}
            aria-hidden="true"
          />
          <div
            ref={drawerRef}
            role="dialog"
            aria-modal="true"
            aria-label="Primary navigation"
            className="relative h-full w-64 max-w-[85vw] bg-artisan-surface shadow-elevation-2"
          >
            <button
              type="button"
              onClick={closeNav}
              aria-label="Close navigation"
              className="absolute right-2 top-2 z-10 w-9 h-9 rounded-lg flex items-center justify-center text-espresso-600 hover:bg-artisan-subtle"
            >
              <X className="h-5 w-5" aria-hidden="true" />
            </button>
            <AppSidebar onNavigate={closeNav} className="w-full" />
          </div>
        </div>
      ) : null}

      <div className="md:pl-64 flex flex-col min-h-screen">
        <TopBar onOpenNav={() => setNavOpen(true)} />
        <main
          id="main-content"
          className="w-full max-w-7xl mx-auto px-gutter-mobile md:px-margin py-space-xl flex-1"
        >
          {/*
            The render-error boundary sits **inside** the shell: a screen that
            throws shows its error in the content area while the navigation,
            the top bar and the session survive.
          */}
          <RouteErrorBoundary area="this screen">
            <Outlet />
          </RouteErrorBoundary>
        </main>
      </div>
    </div>
  )
}
