/**
 * Date & period formatting.
 *
 * Every format is fixed and explicit — `toLocaleDateString()` is deliberately
 * not used for display output, because it drifts with the browser locale and
 * would make the same payload render differently per machine. Month names come
 * from the tables below and dates are read with the UTC accessors, so a
 * `yyyy-MM-dd` event date never shifts a day because of the client time zone.
 *
 * The server is authoritative for the reporting period boundaries
 * (contract §9.1) — the client only formats the label it receives.
 */

import { EM_DASH } from './currencyFormatter'

/** How many months the telemetry pill offers: the current month plus history. */
export const DEFAULT_PERIOD_OPTION_COUNT = 12

const MONTHS_SHORT = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
] as const

const MONTHS_LONG = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
] as const

/** `2026-10-28` → `28 Oct 2026`. */
export function formatEventDate(value: string | null | undefined): string {
  const date = parseDateOnly(value)
  if (!date) return EM_DASH
  return `${date.getUTCDate()} ${MONTHS_SHORT[date.getUTCMonth()]} ${date.getUTCFullYear()}`
}

/** `2026-10-28` → `28 Oct 2026, 17:00`. */
export function formatEventDateTime(value: string | null | undefined): string {
  const date = parseDateOnly(value)
  if (!date) return EM_DASH
  const hours = String(date.getUTCHours()).padStart(2, '0')
  const minutes = String(date.getUTCMinutes()).padStart(2, '0')
  return `${date.getUTCDate()} ${MONTHS_SHORT[date.getUTCMonth()]} ${date.getUTCFullYear()}, ${hours}:${minutes}`
}

/** `2026-10-19T10:00:00Z` → `19 Oct 2026, 10:00 UTC`. */
export function formatTimestamp(value: string | null | undefined): string {
  if (!value) return EM_DASH
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return EM_DASH
  const hours = String(date.getUTCHours()).padStart(2, '0')
  const minutes = String(date.getUTCMinutes()).padStart(2, '0')
  return `${date.getUTCDate()} ${MONTHS_SHORT[date.getUTCMonth()]} ${date.getUTCFullYear()}, ${hours}:${minutes} UTC`
}

/** `2026` + `10` → `October 2026` (1-based month). */
export function formatMonthYear(year: number, month: number): string {
  const monthName = MONTHS_LONG[month - 1]
  if (!monthName) return EM_DASH
  return `${monthName} ${year}`
}

/** The `YYYY-MM` key of the current UTC calendar month. */
export function currentPeriodKey(now: Date = new Date()): string {
  return periodKeyFromUtc(now.getUTCFullYear(), now.getUTCMonth() + 1)
}

/** Builds the `YYYY-MM` key for a 1-based month. */
export function periodKeyFromUtc(year: number, month: number): string {
  return `${year}-${String(month).padStart(2, '0')}`
}

export interface PeriodOption {
  /** `YYYY-MM` — the value sent to the API as `?period=`. */
  value: string
  /** Human label, e.g. `October 2026`. */
  label: string
}

/**
 * The selectable reporting periods offered in the telemetry pill: the current
 * UTC calendar month and the preceding months. Generated from the clock — the
 * API remains authoritative for the boundaries it resolves.
 */
export function buildPeriodOptions(
  count = DEFAULT_PERIOD_OPTION_COUNT,
  now: Date = new Date(),
): PeriodOption[] {
  const options: PeriodOption[] = []
  const year = now.getUTCFullYear()
  const month = now.getUTCMonth() + 1

  let cursorYear = year
  let cursorMonth = month
  for (let index = 0; index < count; index += 1) {
    options.push({
      value: periodKeyFromUtc(cursorYear, cursorMonth),
      label: formatMonthYear(cursorYear, cursorMonth),
    })
    cursorMonth -= 1
    if (cursorMonth === 0) {
      cursorMonth = 12
      cursorYear -= 1
    }
  }
  return options
}

function parseDateOnly(value: string | null | undefined): Date | null {
  if (!value) return null
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (dateOnly) {
    const parsed = new Date(
      Date.UTC(Number(dateOnly[1]), Number(dateOnly[2]) - 1, Number(dateOnly[3])),
    )
    return Number.isNaN(parsed.getTime()) ? null : parsed
  }
  // A date-time **without** an offset ("2026-03-15T00:00:00") carries a
  // calendar date, not an instant. Parsing it as a local time would shift the
  // rendered day in every zone east of UTC, so its parts are read verbatim.
  const naiveDateTime = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::\d{2})?$/.exec(value)
  if (naiveDateTime) {
    const parsed = new Date(
      Date.UTC(
        Number(naiveDateTime[1]),
        Number(naiveDateTime[2]) - 1,
        Number(naiveDateTime[3]),
        Number(naiveDateTime[4]),
        Number(naiveDateTime[5]),
      ),
    )
    return Number.isNaN(parsed.getTime()) ? null : parsed
  }
  const fallback = new Date(value)
  return Number.isNaN(fallback.getTime()) ? null : fallback
}
