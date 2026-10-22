import { FileSpreadsheet, Plus, RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { formatPercentage } from '@/utils/currencyFormatter'

export interface MenuBannerProps {
  /** The server's configured benchmark (DEC-040). `null` until the load lands. */
  targetFoodCostPercentage: number | null
  /** `false` when the benchmark falls back to the system default. */
  isBenchmarkConfigured: boolean
  onRecalculate: () => void
  isRecalculating: boolean
  onExport: () => void
  isExporting: boolean
  onCreate: () => void
}

/**
 * The top banner (spec §4.1).
 *
 * The benchmark badge publishes the **server's** target — the client never
 * hardcodes 32 % or 35 %. When the benchmark is not configured the badge says
 * so instead of pretending one is in force.
 */
export function MenuBanner({
  targetFoodCostPercentage,
  isBenchmarkConfigured,
  onRecalculate,
  isRecalculating,
  onExport,
  isExporting,
  onCreate,
}: MenuBannerProps) {
  return (
    <Card elevation={1} className="flex flex-col xl:flex-row xl:items-center xl:justify-between gap-space-md">
      <div className="flex flex-col gap-1 min-w-0">
        <nav
          aria-label="Breadcrumb"
          className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-400"
        >
          <span>Catering Operations</span>
          <span aria-hidden="true">/</span>
          <span>Kitchen Commissary</span>
          <span aria-hidden="true">/</span>
          <span className="font-semibold text-espresso-850">Menu &amp; Food Costing</span>
        </nav>
        <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
          Menu &amp; Food Costing Master
        </h1>
        <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
          Dish catalogue, automated food cost percentage calculations, and catalogue margin
          telemetry.
        </p>
      </div>

      <div className="flex flex-col sm:flex-row items-start sm:items-center gap-2.5">
        {targetFoodCostPercentage !== null ? (
          <div className="flex items-center gap-2 bg-surface-container-high px-3 py-1.5 rounded-lg shadow-elevation-1">
            <span
              className="w-2 h-2 rounded-full bg-culinary-600"
              aria-hidden="true"
            />
            <span className="font-data-sm text-data-sm text-espresso-700 font-medium">
              {isBenchmarkConfigured
                ? `Benchmark Target: ≤ ${formatPercentage(targetFoodCostPercentage)}`
                : 'Default target (not configured)'}
            </span>
          </div>
        ) : null}

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
          <Button
            variant="secondary"
            size="sm"
            icon={FileSpreadsheet}
            onClick={onExport}
            disabled={isExporting}
          >
            {isExporting ? 'Preparing export…' : 'Export Excel'}
          </Button>
          <Button variant="primary" size="sm" icon={Plus} onClick={onCreate}>
            + New Recipe
          </Button>
        </div>
      </div>
    </Card>
  )
}
