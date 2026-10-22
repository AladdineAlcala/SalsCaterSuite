import { Construction } from 'lucide-react'
import { Card } from '@/components/ui/Card'
import { Badge } from '@/components/ui/Badge'

export interface NotImplementedPageProps {
  /** The module name exactly as it appears in the navigation. */
  moduleName: string
  /** What the module will cover once it is built. */
  description?: string
}

const MODULE_DESCRIPTIONS: Record<string, string> = {
  'Menu & Food Costing':
    'The menu catalogue, per-dish food costing and contribution margin will live here.',
  'Packages & Buffets':
    'Catering packages, per-pax tiers, minimum pax rules and add-ons will live here.',
  'Payments & Ledger':
    'Payment capture, official receipts and the per-booking payment ledger will live here.',
  'Accounts Receivable & SOA':
    'Customer statements of account, AR aging and collection follow-up will live here.',
  'Banquet Calendar':
    'The full banquet calendar and event detail view will live here.',
  'Reports & BEO':
    'Monthly sales, aging and banquet event order documents will live here.',
  Settings: 'Users, roles, benchmark configuration and system preferences will live here.',
}

/**
 * Placeholder for a module that is not built yet.
 *
 * The navigation never leads to a dead link and never to fabricated data —
 * every unbuilt module resolves here with an explicit statement of its status
 * (AC-F13).
 */
export function NotImplementedPage({ moduleName, description }: NotImplementedPageProps) {
  return (
    <section className="flex flex-col gap-space-md">
      <header className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-caramel-500" aria-hidden="true" />
          <span className="font-eyebrow text-eyebrow uppercase tracking-wider text-caramel-700">
            Not yet implemented
          </span>
        </div>
        <h1 className="font-headline-xl text-headline-xl text-espresso-900">{moduleName}</h1>
        <p className="font-body-md text-body-md text-espresso-600 max-w-2xl">
          {description || MODULE_DESCRIPTIONS[moduleName] || 'This module has not been built yet.'}
        </p>
      </header>

      <Card className="max-w-2xl">
        <div className="flex items-start gap-3">
          <span className="w-10 h-10 rounded-lg bg-caramel-50 text-caramel-700 flex items-center justify-center shrink-0">
            <Construction className="h-5 w-5" aria-hidden="true" />
          </span>
          <div className="flex flex-col items-start gap-2">
            <Badge tone="warning">Planned module</Badge>
            <p className="font-body-sm text-body-sm text-espresso-700">
              This screen is a placeholder. No records, figures or documents are shown because the
              module has no API surface yet — the Dashboard, the Customer Master, the Menu &amp; Food
              Costing Master, the Packages &amp; Buffets catalogue and the Bookings &amp; Events
              register are the implemented screens in this release.
            </p>
          </div>
        </div>
      </Card>
    </section>
  )
}
