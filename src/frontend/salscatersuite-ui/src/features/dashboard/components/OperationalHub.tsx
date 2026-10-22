import { useId } from 'react'
import { Calculator, ClipboardCheck, Printer, Soup } from 'lucide-react'
import { Button } from '@/components/ui/Button'

const STEPS = [
  {
    eyebrow: 'Step 1 • Intake',
    title: 'Event Booking & Package',
    description: 'Select pax headcount, venue location, buffet tier, and service rules.',
    tone: 'bg-caramel-50/70',
  },
  {
    eyebrow: 'Step 2 • Kitchen',
    title: 'Dish Allocation & Costing',
    description: 'Calculate ingredients, commissary yield losses, and target margins.',
    tone: 'bg-surface-container-low',
  },
  {
    eyebrow: 'Step 3 • Finance',
    title: 'Ledger, OR & BEO Print',
    description:
      'Reconcile the contract ledger, issue official receipts and compile kitchen station sheets.',
    tone: 'bg-surface-container-low',
  },
] as const

/**
 * The elevated operational hub (spec §9.2 item 4).
 *
 * The three process steps describe the catering workflow. The CTA row targets
 * modules that are not built yet, so each button is rendered **disabled with
 * an explanatory tooltip** rather than as a dead link (spec §9.2 item 4).
 */
export function OperationalHub() {
  const hintId = useId()

  return (
    <section className="relative bg-artisan-surface border border-artisan-border rounded-2xl p-space-lg sm:p-margin shadow-elevation-2 overflow-hidden">
      <div
        className="absolute -right-16 -top-16 w-80 h-80 bg-culinary-50 rounded-full blur-3xl pointer-events-none opacity-60"
        aria-hidden="true"
      />
      <div
        className="absolute -left-16 -bottom-16 w-80 h-80 bg-caramel-50 rounded-full blur-3xl pointer-events-none opacity-60"
        aria-hidden="true"
      />

      <div className="relative z-10 flex flex-col items-center text-center max-w-3xl mx-auto gap-space-md">
        <div className="w-14 h-14 rounded-2xl bg-espresso-900 text-caramel-200 flex items-center justify-center shadow-elevation-2">
          <Soup className="h-7 w-7" aria-hidden="true" />
        </div>

        <div className="flex flex-col gap-2">
          <h2 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
            Ready to Dispatch Kitchen BEO or Price Catering Menus?
          </h2>
          <p className="font-body-md text-body-md text-espresso-600 leading-relaxed">
            Sal&apos;s CaterSuite unifies the booking pipeline, the automated food-costing engine,
            customer ledgers and document exports for contracts, statements of account and kitchen
            banquet event orders.
          </p>
        </div>

        <ol className="grid grid-cols-1 md:grid-cols-3 gap-space-sm w-full text-left my-2 list-none">
          {STEPS.map((step) => (
            <li
              key={step.eyebrow}
              className={`p-space-md rounded-xl ${step.tone} shadow-elevation-1 flex flex-col gap-2`}
            >
              <span className="font-eyebrow text-eyebrow uppercase text-espresso-600 tracking-wider">
                {step.eyebrow}
              </span>
              <span className="font-headline-sm text-headline-sm text-espresso-900">{step.title}</span>
              <p className="font-caption text-caption text-espresso-600">{step.description}</p>
            </li>
          ))}
        </ol>

        <div className="flex flex-wrap items-center justify-center gap-space-sm pt-2">
          <Button
            variant="primary"
            icon={ClipboardCheck}
            inactive
            aria-describedby={hintId}
            title="Bookings & Events is not yet implemented."
          >
            + Book New Banquet Event
          </Button>
          <Button
            variant="secondary"
            icon={Printer}
            inactive
            aria-describedby={hintId}
            title="Banquet Event Order printing is not yet implemented."
          >
            Run Kitchen BEO Print
          </Button>
          <Button
            variant="secondary"
            icon={Calculator}
            inactive
            aria-describedby={hintId}
            title="Menu & Food Costing is not yet implemented."
          >
            Calculate Dish Food Costs
          </Button>
        </div>

        <p id={hintId} className="font-caption text-caption text-espresso-600">
          These actions belong to modules that are not implemented in this release. The Operations
          Dashboard is the only live screen.
        </p>
      </div>
    </section>
  )
}
