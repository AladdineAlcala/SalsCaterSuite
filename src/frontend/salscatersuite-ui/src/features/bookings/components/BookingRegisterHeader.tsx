import { FileDown, FileText, Plus } from 'lucide-react'
import { Button } from '@/components/ui/Button'

export interface BookingRegisterHeaderProps {
  onExport: () => void
  onCreate: () => void
  isExporting: boolean
}

/**
 * The register's header row (spec §5.2 item 1).
 *
 * The reference's **Live Calendar Sync** badge is **not** rendered: the
 * calendar feed exists, but there is no hub and no push channel — SignalR is
 * registered and `NotificationHub` is not yet built — so a "live" badge would
 * assert a capability that does not exist (spec §1.3).
 *
 * The reference's **Batch QuestPDF BEOs** action is rendered as an **inactive**
 * button: there is no BEO entity and no per-booking dish selection to print,
 * so no queue is fabricated (DEC-104).
 */
export function BookingRegisterHeader({
  onExport,
  onCreate,
  isExporting,
}: BookingRegisterHeaderProps) {
  return (
    <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-space-md">
      <div className="flex flex-col gap-1 min-w-0">
        <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
          Bookings &amp; Event Operations
        </h1>
        <p className="font-body-sm text-body-sm text-espresso-600">
          Banquet schedule, package tier allocations, pax counts and ledger payment telemetry.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button
          variant="secondary"
          size="sm"
          icon={FileDown}
          onClick={onExport}
          disabled={isExporting}
        >
          {isExporting ? 'Exporting…' : 'Export (XLSX)'}
        </Button>

        <Button
          variant="secondary"
          size="sm"
          icon={FileText}
          inactive
          title="There is no banquet event order entity yet, so no batch can be produced."
        >
          Batch BEOs
        </Button>

        <Button variant="primary" icon={Plus} onClick={onCreate}>
          New booking
        </Button>
      </div>
    </div>
  )
}
