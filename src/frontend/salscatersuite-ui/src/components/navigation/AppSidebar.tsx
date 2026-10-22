import { Link, NavLink } from 'react-router-dom'
import { Soup } from 'lucide-react'
import { cn } from '@/utils/cn'
import { NAV_ITEMS } from './navItems'

export interface AppSidebarProps {
  /** Called after a navigation item is activated (used to close the mobile drawer). */
  onNavigate?: () => void
  className?: string
  id?: string
}

/**
 * The persistent left navigation.
 *
 * Every item resolves to a real route: the Dashboard is implemented, the other
 * modules render an explicit "not yet implemented" page (AC-F13) — never a
 * dead link.
 */
export function AppSidebar({ onNavigate, className, id }: AppSidebarProps) {
  return (
    <div className={cn('flex h-full flex-col bg-artisan-surface', className)} id={id}>
      <Link
        to="/"
        onClick={onNavigate}
        className="flex items-center gap-3 px-4 py-4 border-b border-artisan-border"
      >
        <span className="w-9 h-9 rounded-lg bg-espresso-900 text-caramel-200 flex items-center justify-center shrink-0">
          <Soup className="h-5 w-5" aria-hidden="true" />
        </span>
        <span className="flex flex-col leading-tight min-w-0">
          <span className="font-headline-sm text-headline-sm text-espresso-900 truncate">
            Sal&apos;s CaterSuite
          </span>
          <span className="font-caption text-caption uppercase tracking-wider text-espresso-400 flex items-center gap-1.5">
            Catering ERP
            <span className="w-1.5 h-1.5 rounded-full bg-culinary-600" aria-hidden="true" />
          </span>
        </span>
      </Link>

      <nav aria-label="Primary" className="flex-1 overflow-y-auto px-3 py-4">
        <ul className="flex flex-col gap-1">
          {NAV_ITEMS.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.to === '/'}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 px-3 py-2 rounded-lg text-body-md transition-colors',
                    isActive
                      ? 'bg-culinary-600 text-white font-semibold'
                      : 'text-espresso-700 hover:bg-artisan-subtle hover:text-espresso-900',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      className={cn('h-4 w-4 shrink-0', isActive ? 'text-white' : 'text-espresso-600')}
                      aria-hidden="true"
                    />
                    <span className="leading-tight">{item.label}</span>
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <div className="px-4 py-3 border-t border-artisan-border">
        <p className="font-caption text-caption text-espresso-400">
          Operations telemetry — figures calculated by the API.
        </p>
      </div>
    </div>
  )
}
