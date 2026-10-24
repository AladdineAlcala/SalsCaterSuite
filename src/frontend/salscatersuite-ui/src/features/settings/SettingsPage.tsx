import { Link } from 'react-router-dom'
import { PackagePlus, Settings } from 'lucide-react'
import { Card } from '@/components/ui/Card'

/**
 * The Settings hub (`/settings`).
 *
 * The only implemented settings area in this cycle is the Add-on Master
 * (`/settings/addons`). The other settings surfaces are not built yet, so they
 * are not listed as dead links.
 */
export function SettingsPage() {
  return (
    <div className="flex flex-col gap-space-xl">
      <Card elevation={1} className="flex flex-col gap-1">
        <nav
          aria-label="Breadcrumb"
          className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-400 flex-wrap"
        >
          <span>Catering Operations</span>
          <span aria-hidden="true">/</span>
          <span className="font-semibold text-espresso-850">Settings</span>
        </nav>
        <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight flex items-center gap-2">
          <Settings className="h-6 w-6 text-culinary-700" aria-hidden="true" />
          Settings
        </h1>
        <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
          The configuration surfaces of Sal&apos;s CaterSuite. The Add-on Master is the first
          one delivered.
        </p>
      </Card>

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-gutter">
        <Link
          to="/settings/addons"
          className="block rounded-xl focus:outline-none focus-visible:ring-2 focus-visible:ring-culinary-600"
        >
          <Card
            elevation={1}
            className="h-full flex flex-col gap-space-sm hover:border-culinary-600 transition-colors"
          >
            <span className="w-10 h-10 rounded-lg bg-culinary-100 text-culinary-800 flex items-center justify-center">
              <PackagePlus className="h-5 w-5" aria-hidden="true" />
            </span>
            <h2 className="font-headline-sm text-headline-sm text-espresso-900">Add-ons</h2>
            <p className="font-body-sm text-body-sm text-espresso-600">
              Create, edit and deactivate the add-on master the booking screens draw from.
            </p>
          </Card>
        </Link>
      </div>
    </div>
  )
}
