import { Link } from 'react-router-dom'
import { Bell, Menu, Plus, Search, SquarePen } from 'lucide-react'
import { UserChip } from './UserChip'

export interface TopBarProps {
  /** Opens the off-canvas navigation drawer (mobile only). */
  onOpenNav: () => void
}

/**
 * The sticky top bar.
 *
 * - a global search field that is **deliberately non-functional** — no search
 *   endpoint exists, so it is rendered read-only and clearly labelled rather
 *   than faking results;
 * - "New Payment / OR" and "+ New Booking" affordances that route to the
 *   modules that are not built yet (those routes render an explicit
 *   "not yet implemented" page);
 * - a notification bell that is inert — the solution has no notification hub,
 *   so this build must not pretend to receive alerts;
 * - the user chip with the full name and role from the JWT.
 */
export function TopBar({ onOpenNav }: TopBarProps) {
  return (
    <header className="sticky top-0 z-20 bg-white/95 backdrop-blur-md border-b border-artisan-border">
      <div className="flex items-center gap-3 h-16 px-4 md:px-8">
        <button
          type="button"
          onClick={onOpenNav}
          aria-label="Open navigation"
          className="md:hidden w-10 h-10 rounded-lg flex items-center justify-center text-espresso-700 hover:bg-artisan-subtle"
        >
          <Menu className="h-5 w-5" aria-hidden="true" />
        </button>

        <div className="hidden xl:flex flex-col leading-tight mr-2">
          <span className="font-caption text-caption uppercase tracking-wider text-espresso-400">
            Catering Operations
          </span>
          <span className="font-headline-sm text-headline-sm text-espresso-900">
            Master Booking Telemetry
          </span>
        </div>

        <div className="relative hidden lg:flex items-center flex-1 max-w-md min-w-0">
          <Search
            className="absolute left-3 h-4 w-4 text-espresso-400 pointer-events-none"
            aria-hidden="true"
          />
          <label htmlFor="global-search" className="sr-only">
            Global search
          </label>
          <input
            id="global-search"
            type="search"
            readOnly
            aria-disabled="true"
            title="Global search is not available in this release — no search endpoint exists."
            placeholder="Search bookings, clients, menus… (not available yet)"
            className="h-10 w-full rounded-lg border border-artisan-border bg-surface-container-low pl-9 pr-3 font-body-sm text-body-sm text-espresso-600 placeholder:text-espresso-400"
          />
          <span className="sr-only">
            Global search is not available in this release. No search endpoint exists.
          </span>
        </div>

        <div className="ml-auto flex items-center gap-2">
          <Link
            to="/payments"
            className="hidden sm:inline-flex items-center gap-2 h-10 px-space-md rounded-lg border border-artisan-border bg-artisan-surface text-espresso-850 hover:bg-surface-container-low transition-colors"
            title="Payment capture module — not yet implemented"
          >
            <SquarePen className="h-4 w-4 text-espresso-700" aria-hidden="true" />
            <span className="font-body-sm text-body-sm font-semibold">New Payment / OR</span>
          </Link>

          <Link
            to="/bookings"
            className="inline-flex items-center gap-2 h-10 px-space-md rounded-lg bg-culinary-600 text-white font-semibold hover:bg-culinary-700 transition-colors shadow-elevation-3"
            title="Booking module — not yet implemented"
          >
            <Plus className="h-4 w-4" aria-hidden="true" />
            <span className="font-body-sm text-body-sm">New Booking</span>
          </Link>

          <button
            type="button"
            aria-disabled="true"
            aria-label="Notifications — not available in this release"
            title="No notification hub is configured — this build does not receive alerts."
            onClick={(event) => event.preventDefault()}
            className="w-10 h-10 rounded-lg flex items-center justify-center text-espresso-600 hover:bg-artisan-subtle transition-colors opacity-60"
          >
            <Bell className="h-5 w-5" aria-hidden="true" />
          </button>

          <UserChip />
        </div>
      </div>
    </header>
  )
}
