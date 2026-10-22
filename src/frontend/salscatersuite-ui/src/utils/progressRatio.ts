/**
 * Progress-track ratio helpers.
 *
 * A progress track is a *display bound*, not a business figure: the value is
 * always clamped to 0–100 so the bar can never overflow its track. The
 * underlying numbers are either supplied by the API (coverage, margin,
 * category shares) or are counts/amounts already published in the payload
 * (committed share of the period population, overdue share of total AR).
 *
 * The frontend never computes a financial **total** (DEC-002) — only ratios
 * between figures the server has already published.
 */

import { EM_DASH } from './currencyFormatter'

/**
 * Clamps a percentage to the 0–100 range.
 * `null` / `undefined` / non-finite input yields `null` so the caller can
 * render an em dash instead of a misleading `0%`.
 */
export function clampRatio(value: number | null | undefined): number | null {
  if (typeof value !== 'number' || !Number.isFinite(value)) return null
  if (value < 0) return 0
  if (value > 100) return 100
  return value
}

/**
 * Percentage of `numerator` over `denominator`, clamped to 0–100.
 * A zero or missing denominator yields `null` (undefined ratio → em dash).
 */
export function ratioPercentage(
  numerator: number | null | undefined,
  denominator: number | null | undefined,
): number | null {
  if (typeof numerator !== 'number' || !Number.isFinite(numerator)) return null
  if (typeof denominator !== 'number' || !Number.isFinite(denominator)) return null
  if (denominator === 0) return null
  return clampRatio((numerator / denominator) * 100)
}

/**
 * Percentage of `value` against `benchmark`, clamped to 0–100.
 * Used for "margin ÷ benchmark" progress tracks. A missing benchmark yields `null`.
 */
export function benchmarkPercentage(
  value: number | null | undefined,
  benchmark: number | null | undefined,
): number | null {
  return ratioPercentage(value, benchmark)
}

/** Formats a ratio for a `role="progressbar"` `aria-valuenow` attribute. */
export function progressAriaValue(value: number | null | undefined): number | null {
  const clamped = clampRatio(value)
  if (clamped === null) return null
  return Math.round(clamped * 10) / 10
}

/** The textual form of a ratio — an em dash when the ratio is undefined. */
export function formatRatio(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value)
    ? EM_DASH
    : `${Math.round(value * 10) / 10}%`
}
