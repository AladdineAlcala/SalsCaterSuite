import { FileSpreadsheet, Printer, UserPlus } from 'lucide-react'
import { Button } from '@/components/ui/Button'

export interface CustomerBannerProps {
  onExport: () => void
  isExporting: boolean
  onCreate: () => void
}

/**
 * The top banner (spec §4.1).
 *
 * The batch-Statement-of-Account action of the reference renders **inactive**
 * with an explanatory tooltip — batch generation is out of scope (spec §1.2).
 */
export function CustomerBanner({ onExport, isExporting, onCreate }: CustomerBannerProps) {
  return (
    <section className="flex flex-col md:flex-row md:items-center justify-between gap-space-md bg-artisan-surface p-space-lg rounded-xl shadow-elevation-1 border border-artisan-border">
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-culinary-50 text-culinary-800 font-caption text-caption uppercase tracking-wider font-bold">
            <span className="w-1.5 h-1.5 rounded-full bg-culinary-600" aria-hidden="true" />
            EF Core 10 &bull; SalsCaterSuiteDb.Customer
          </span>
        </div>
        <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
          Customer Master &amp; Accounts Ledger
        </h1>
        <p className="font-body-md text-body-md text-espresso-600">
          Client directory, banquet booking histories, and Statement of Account (SOA) receivables
          pipeline.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-2.5">
        <Button
          variant="secondary"
          icon={FileSpreadsheet}
          onClick={onExport}
          disabled={isExporting}
        >
          {isExporting ? 'Preparing export…' : 'Export (ClosedXML)'}
        </Button>
        <Button
          variant="secondary"
          icon={Printer}
          inactive
          title="Batch Statement of Account generation is not available in this release."
        >
          Batch SOAs (QuestPDF)
        </Button>
        <Button variant="primary" icon={UserPlus} onClick={onCreate}>
          + Add New Customer
        </Button>
      </div>
    </section>
  )
}
