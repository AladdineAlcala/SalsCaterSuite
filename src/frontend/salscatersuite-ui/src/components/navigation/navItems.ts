import type { LucideIcon } from 'lucide-react'
import {
  CalendarDays,
  CalendarRange,
  CreditCard,
  HandCoins,
  LayoutDashboard,
  NotebookPen,
  Package,
  Settings,
  Users,
  UtensilsCrossed,
} from 'lucide-react'

export interface NavItem {
  /** Label exactly as published in the design reference. */
  label: string
  /** Route the item resolves to. Every route resolves to a real page. */
  to: string
  icon: LucideIcon
  /**
   * `false` for every module that is not built yet. Those routes render an
   * explicit "not yet implemented" page (AC-F13) — never a dead link and never
   * fabricated data.
   */
  implemented: boolean
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', to: '/', icon: LayoutDashboard, implemented: true },
  { label: 'Bookings & Events', to: '/bookings', icon: CalendarDays, implemented: true },
  { label: 'Menu & Food Costing', to: '/menus', icon: UtensilsCrossed, implemented: true },
  { label: 'Packages & Buffets', to: '/packages', icon: Package, implemented: true },
  { label: 'Payments & Ledger', to: '/payments', icon: CreditCard, implemented: false },
  { label: 'Accounts Receivable & SOA', to: '/receivables', icon: HandCoins, implemented: false },
  { label: 'Banquet Calendar', to: '/calendar', icon: CalendarRange, implemented: false },
  { label: 'Customers', to: '/customers', icon: Users, implemented: true },
  { label: 'Reports & BEO', to: '/reports', icon: NotebookPen, implemented: false },
  { label: 'Settings', to: '/settings', icon: Settings, implemented: false },
]
