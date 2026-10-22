import { MapPin } from 'lucide-react'
import type { ReactNode } from 'react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { ProgressTrack } from '@/components/ui/ProgressTrack'
import { EM_DASH, formatCount, formatCurrency, formatPercentage } from '@/utils/currencyFormatter'
import type { PackageAreaCoverageRow, PackageLocationPreview } from '@/types/package.types'

export interface PackageLocationSimulatorProps {
  /** The municipalities the tier covers — the simulator prices one of them. */
  rows: PackageAreaCoverageRow[]
  /** The municipality the operator is pricing. */
  selectedAreaId: number | null
  onSelectedAreaIdChange: (areaId: number | null) => void
  /** The typed headcount. */
  pax: string
  onPaxChange: (value: string) => void
  /** The client-side rejection of the typed headcount. */
  paxError?: string
  /** The server's breakdown. */
  preview: PackageLocationPreview | undefined
  /** `true` while the breakdown is in flight. */
  isPreviewing: boolean
  /**
   * `true` while the typed headcount has not reached the server yet, or the
   * quote is in flight.
   *
   * While it holds, every breakdown row shows the recalculating caption in
   * place of the figure, so a quote priced for a different headcount is never
   * presented as the current one. A figure the server published as `null`
   * still shows the em dash once the quote has arrived — *unknown* and
   * *pending* are different states.
   */
  isRecalculating: boolean
  /** The server's rejection of the breakdown request, rendered verbatim. */
  previewErrors: string[]
  /** The tier's headcount floor. */
  minPax: number | null
  /** `true` when the tier carries no rate, so no money figure can exist. */
  isUnpriced: boolean
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-data-md text-data-md w-full'

/**
 * The value of one breakdown row.
 *
 * A **pending** figure — the headcount has not reached the server yet, or the
 * quote is in flight — shows the recalculating caption **in place of** the
 * figure, exactly as the course workbench does. It is never the em dash:
 * the em dash means *unknown* (a `null` the server published), while the
 * caption means *pending*, and the two states must not look alike.
 *
 * The markup is the identical caption the location matrix's money column and
 * the three course-workbench areas use, so the affordance is one affordance.
 */
function BreakdownValue({
  isRecalculating,
  children,
}: {
  isRecalculating: boolean
  children: ReactNode
}) {
  if (isRecalculating) {
    return <span className="font-caption text-caption text-espresso-500">Recalculating…</span>
  }
  return <>{children}</>
}

/**
 * The out-of-town pricing simulator (spec §5.3).
 *
 * The breakdown is **server-computed**: the browser sends the municipality and
 * the headcount and renders what comes back. It never multiplies a rate by a
 * headcount and never subtracts a cost from a contract value (DEC-002,
 * AC-P2-28).
 *
 * A municipality with **no logistics cost** omits the logistics band entirely
 * — it is never shown as a zero (DEC-081).
 *
 * **Why a pending quote is never shown as the current one.** The breakdown
 * answers what a given headcount would cost. While the operator's new
 * headcount is on its way to the server, the quote in the browser belongs to
 * the headcount they have **already changed**, so every breakdown row shows
 * the recalculating caption in place of the figure. The em dash keeps its own
 * meaning — *unknown*, a `null` the server published — so the two states never
 * look alike. The course workbench withholds its figures in exactly the same
 * window, in exactly the same shape, for exactly the same reason.
 */
export function PackageLocationSimulator({
  rows,
  selectedAreaId,
  onSelectedAreaIdChange,
  pax,
  onPaxChange,
  paxError,
  preview,
  isPreviewing,
  isRecalculating,
  previewErrors,
  minPax,
  isUnpriced,
}: PackageLocationSimulatorProps) {
  const selectedRow = rows.find((row) => row.areaId === selectedAreaId)

  return (
    <Card
      elevation={1}
      className="flex flex-col gap-space-md"
      data-testid="package-location-simulator"
    >
      <div className="flex items-center justify-between gap-2 pb-1 flex-wrap">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Out-of-Town Pricing Simulator
        </h2>
        <Badge tone="info" shape="pill">
          Server preview
        </Badge>
      </div>

      {rows.length === 0 ? (
        <EmptyState
          icon={MapPin}
          title="No municipality to price"
          description="This tier covers no municipality, so there is no out-of-town quote to simulate. Attach a municipality first."
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="flex flex-col gap-1">
            <label
              htmlFor="package-simulator-municipality"
              className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
            >
              Municipality
            </label>
            <select
              id="package-simulator-municipality"
              name="areaId"
              value={selectedAreaId === null ? '' : String(selectedAreaId)}
              onChange={(event) =>
                onSelectedAreaIdChange(
                  event.target.value.length === 0 ? null : Number(event.target.value),
                )
              }
              aria-describedby="package-simulator-municipality-hint"
              className={INPUT_CLASSES}
            >
              {rows.map((row) => (
                <option key={row.areaId} value={String(row.areaId)}>
                  {row.areaName}
                </option>
              ))}
            </select>
            <p
              id="package-simulator-municipality-hint"
              className="font-caption text-caption text-espresso-600"
            >
              The municipalities this tier covers. The surcharge is the municipality&apos;s own
              rate.
            </p>
          </div>

          <div className="flex flex-col gap-1">
            <label
              htmlFor="package-simulator-pax"
              className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
            >
              Headcount
            </label>
            <input
              id="package-simulator-pax"
              name="pax"
              type="number"
              inputMode="numeric"
              step="1"
              min="1"
              value={pax}
              onChange={(event) => onPaxChange(event.target.value)}
              aria-invalid={paxError ? true : undefined}
              aria-describedby={
                paxError ? 'package-simulator-pax-error' : 'package-simulator-pax-hint'
              }
              className={INPUT_CLASSES}
            />
            <p id="package-simulator-pax-hint" className="font-caption text-caption text-espresso-600">
              {minPax === null
                ? 'The tier has no headcount floor, so a headcount must be entered.'
                : `Leave empty to use the tier’s own floor of ${formatCount(minPax)} heads.`}
            </p>
            {paxError ? (
              <p
                id="package-simulator-pax-error"
                role="alert"
                className="font-caption text-caption text-error"
              >
                {paxError}
              </p>
            ) : null}
          </div>
        </div>
      )}

      {previewErrors.length > 0 ? (
        <div role="alert" className="flex flex-col gap-1">
          {previewErrors.map((message) => (
            <p key={message} className="font-body-sm text-body-sm text-error break-words">
              {message}
            </p>
          ))}
        </div>
      ) : null}

      {/* The contract breakdown — every figure is the server's. */}
      <div className="flex flex-col gap-2 rounded-lg bg-surface-container-low p-3">
        <div className="flex items-center justify-between gap-2">
          <span className="font-body-sm text-body-sm text-espresso-700">Base total</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            <BreakdownValue isRecalculating={isRecalculating}>
              {preview ? formatCurrency(preview.baseTotal) : EM_DASH}
            </BreakdownValue>
          </span>
        </div>
        <div className="flex items-center justify-between gap-2">
          <span className="font-body-sm text-body-sm text-espresso-700">Extended total</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            <BreakdownValue isRecalculating={isRecalculating}>
              {preview ? formatCurrency(preview.extendedTotal) : EM_DASH}
            </BreakdownValue>
          </span>
        </div>
        <div className="flex items-center justify-between gap-2">
          <span className="font-body-sm text-body-sm text-espresso-700">Effective rate per pax</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            <BreakdownValue isRecalculating={isRecalculating}>
              {preview ? formatCurrency(preview.effectiveRatePerPax) : EM_DASH}
            </BreakdownValue>
          </span>
        </div>
        <div className="flex items-center justify-between gap-2">
          <span className="font-body-sm text-body-sm text-espresso-700">Total contract value</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            <BreakdownValue isRecalculating={isRecalculating}>
              {preview ? formatCurrency(preview.totalContractValue) : EM_DASH}
            </BreakdownValue>
          </span>
        </div>
        {selectedRow ? (
          <p className="font-caption text-caption text-espresso-600">
            {selectedRow.additionalRate === null
              ? 'The surcharge applied is not stated per guest — the municipality has stated no rate, so nothing is added to the contract and the contract value stays complete.'
              : `The surcharge applied is ${formatCurrency(selectedRow.additionalRate)} per guest — the municipality's own rate, shared by every tier that covers it.`}
          </p>
        ) : null}
      </div>

      {/* The gross-profit telemetry. */}
      <div className="flex flex-col gap-2 rounded-lg border border-artisan-border p-3">
        <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
          Gross-profit telemetry
        </span>
        <div className="flex items-center justify-between gap-2">
          <span className="font-body-sm text-body-sm text-espresso-700">Gross profit</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            <BreakdownValue isRecalculating={isRecalculating}>
              {preview ? formatCurrency(preview.grossProfit) : EM_DASH}
            </BreakdownValue>
          </span>
        </div>
        <div className="flex items-center justify-between gap-2">
          <span className="font-body-sm text-body-sm text-espresso-700">Food cost</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
            <BreakdownValue isRecalculating={isRecalculating}>
              {preview ? formatCurrency(preview.foodCostTotal) : EM_DASH}
            </BreakdownValue>
          </span>
        </div>
        {/*
          The logistics band is **omitted** when the municipality states no
          logistics cost. It is never shown as a zero (DEC-081). While the quote
          is pending the band is not rendered either — whether the municipality
          has one is itself part of the answer the server has not given yet.
        */}
        {preview && preview.logisticsCost !== null ? (
          <div className="flex items-center justify-between gap-2">
            <span className="font-body-sm text-body-sm text-espresso-700">Logistics burn</span>
            <span className="font-data-md text-data-md text-espresso-900 font-bold break-words">
              {formatCurrency(preview.logisticsCost)}
            </span>
          </div>
        ) : null}
        <div className="flex items-center justify-between gap-2">
          <span className="font-body-sm text-body-sm text-espresso-700">Gross-profit share</span>
          <span className="font-data-md text-data-md text-espresso-900 font-bold">
            <BreakdownValue isRecalculating={isRecalculating}>
              {preview ? formatPercentage(preview.grossProfitPercentage, 2) : EM_DASH}
            </BreakdownValue>
          </span>
        </div>
        <ProgressTrack
          ratio={preview ? preview.grossProfitPercentage : null}
          label="Gross profit share of the contract value"
          tone="culinary"
        />
        <p className="font-caption text-caption text-espresso-600">
          The food cost of the quote is the tier&apos;s standard run — a planning estimate,
          not a commitment. These are planning figures, not revenue and not an accounting
          entry.
        </p>
      </div>

      {isUnpriced ? (
        <p className="font-caption text-caption text-espresso-600">
          This tier carries no per-guest rate, so no contract figure can be produced.
        </p>
      ) : null}

      {isPreviewing ? (
        <p className="font-caption text-caption text-espresso-600">Recalculating the quote…</p>
      ) : null}
    </Card>
  )
}
