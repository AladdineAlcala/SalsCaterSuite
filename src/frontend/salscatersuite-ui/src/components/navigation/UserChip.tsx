import { useMemo } from 'react'
import { LogOut } from 'lucide-react'
import { useAuth } from '@/context/AuthContext'

function initialsOf(fullName: string): string {
  const parts = fullName
    .split(/\s+/)
    .filter((part) => part.length > 0)
    .slice(0, 2)
  if (parts.length === 0) return '—'
  return parts.map((part) => part.charAt(0).toUpperCase()).join('')
}

/**
 * The signed-in user chip: the user's full name and primary role, both taken
 * from the session (the role is read from the JWT claims).
 */
export function UserChip() {
  const { user, logout } = useAuth()

  const displayName = user?.fullName?.trim() || user?.userName?.trim() || 'Signed in'
  const role = user?.roles && user.roles.length > 0 ? user.roles.join(', ') : 'No role assigned'
  const initials = useMemo(() => initialsOf(displayName), [displayName])

  return (
    <div className="flex items-center gap-2">
      <span
        className="w-9 h-9 rounded-full bg-espresso-900 text-white flex items-center justify-center font-eyebrow text-eyebrow shrink-0"
        aria-hidden="true"
      >
        {initials}
      </span>
      <span className="hidden lg:flex flex-col leading-tight min-w-0">
        <span className="font-body-sm text-body-sm font-semibold text-espresso-900 truncate">
          {displayName}
        </span>
        <span className="font-caption text-caption text-espresso-600 truncate">{role}</span>
      </span>
      <button
        type="button"
        onClick={logout}
        title="Sign out"
        aria-label="Sign out"
        className="w-9 h-9 rounded-lg flex items-center justify-center text-espresso-600 hover:bg-artisan-subtle hover:text-espresso-900 transition-colors"
      >
        <LogOut className="h-4 w-4" aria-hidden="true" />
      </button>
    </div>
  )
}
