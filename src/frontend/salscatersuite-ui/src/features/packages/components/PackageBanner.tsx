import { Plus, RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'

export interface PackageBannerProps {
  /** Refetches the catalogue list and the server-computed aggregate. */
  onRecalculate: () => void
  /** `true` while a refetch is in flight. */
  isRecalculating: boolean
  /** Opens the tier form in create mode. */
  onCreate: () => void
}

/**
 * The top banner (spec §5.2 item 1).
 *
 * The reference's **Export Specs (ClosedXML)** action is not rendered: there
 * is no packages export endpoint and a workbook of tier rows would be a new
 * document generator outside this cycle (DEC-071).
 */
export function PackageBanner({
  onRecalculate,
  isRecalculating,
  onCreate,
}: PackageBannerProps) {
  return (
    <Card
      elevation={1}
      className="flex flex-col xl:flex-row xl:items-center xl:justify-between gap-space-md"
    >
      <div className="flex flex-col gap-1 min-w-0">
        <nav
          aria-label="Breadcrumb"
          className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-400 flex-wrap"
        >
          <span>Catering Operations</span>
          <span aria-hidden="true">/</span>
          <span className="font-semibold text-espresso-850">Packages &amp; Buffets</span>
        </nav>
        <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
          Packages &amp; Buffets Catalogue
        </h1>
        <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
          Buffet tier catalogue with the per-guest rate, the minimum headcount and the
          selectable state of every configured tier.
        </p>
      </div>

      <div className="flex flex-col sm:flex-row items-start sm:items-center gap-2.5 shrink-0">
        <div className="flex items-center gap-2 flex-wrap">
          <Button
            variant="secondary"
            size="sm"
            icon={RefreshCw}
            onClick={onRecalculate}
            disabled={isRecalculating}
          >
            {isRecalculating ? 'Recalculating…' : 'Recalculate'}
          </Button>
          <Button variant="primary" size="sm" icon={Plus} onClick={onCreate}>
            + New Package
          </Button>
        </div>
      </div>
    </Card>
  )
}
