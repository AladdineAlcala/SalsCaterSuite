/**
 * Philippine Peso formatting for every monetary readout on the dashboard.
 *
 * Design rule (spec §9.1): currency is Philippine Peso, `en-PH` / `PHP` locale,
 * peso sign, **full precision** (`₱1,425,800.00`). Compact forms such as `₱1.43M`
 * are not used in KPI readouts.
 *
 * A `null` / `undefined` / non-finite value renders as an em dash — never `0`,
 * never `₱0.00` (spec §9.3 "Empty" and contract §9.2 rule 6 "No null-as-zero").
 */

export const EM_DASH = '—'

const pesoFormatter = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const countFormatter = new Intl.NumberFormat('en-PH', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
})

const decimalFormatter = new Intl.NumberFormat('en-PH', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})

function isDisplayable(value: number | null | undefined): value is number {
  return typeof value === 'number' && Number.isFinite(value)
}

/** `₱1,425,800.00` — full precision, thousands separators, peso sign. */
export function formatCurrency(value: number | null | undefined): string {
  if (!isDisplayable(value)) return EM_DASH
  return pesoFormatter.format(value)
}

/** `1,425,800` — plain integer grouping, no currency symbol. */
export function formatCount(value: number | null | undefined): string {
  if (!isDisplayable(value)) return EM_DASH
  return countFormatter.format(value)
}

/** `1,425,800.5` — grouped decimal, at most two fraction digits. */
export function formatDecimal(value: number | null | undefined): string {
  if (!isDisplayable(value)) return EM_DASH
  return decimalFormatter.format(value)
}

/**
 * `78.4%` — a percentage that the API already computed.
 * `null` renders as an em dash, never `0%`.
 */
export function formatPercentage(
  value: number | null | undefined,
  fractionDigits = 1,
): string {
  if (!isDisplayable(value)) return EM_DASH
  return `${value.toFixed(fractionDigits)}%`
}

/**
 * `+8.90 pp` / `−8.90 pp` — a difference expressed in **percentage points**.
 * Used for the catalogue-margin variance against the configured benchmark
 * (spec §6.9). Never rendered as a relative percentage change.
 */
export function formatPercentagePoints(
  value: number | null | undefined,
  fractionDigits = 2,
): string {
  if (!isDisplayable(value)) return EM_DASH
  const magnitude = Math.abs(value).toFixed(fractionDigits)
  const sign = Number(magnitude) === 0 ? '' : value > 0 ? '+' : '−'
  return `${sign}${magnitude} pp`
}
