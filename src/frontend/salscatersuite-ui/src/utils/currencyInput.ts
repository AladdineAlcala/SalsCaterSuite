/**
 * The input-side currency helpers (owner request 2026-10-24).
 *
 * They format a **field** value the way the operator types it — two decimals and
 * thousands separators, no peso symbol (`1,425.00`) — and parse it back to a
 * number. They are deliberately separate from `formatCurrency`, which renders a
 * **readout** with the peso symbol (`₱1,425.00`): an input is not a readout and
 * must not put a symbol inside the editable text.
 */

const inputFormatter = new Intl.NumberFormat('en-PH', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

/** `1425` → `1,425.00`; an empty or unreadable value stays empty. */
export function formatCurrencyInput(value: string): string {
  const trimmed = value.trim().replace(/,/g, '')
  if (trimmed.length === 0) return ''

  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? inputFormatter.format(parsed) : ''
}

/**
 * Keeps what the operator may type: digits and at most one decimal point, with at
 * most two fraction digits. Commas and any other character are removed.
 */
export function normalizeCurrencyInput(value: string): string {
  // A leading minus is preserved so a negative entry still reaches the shared validator (the
  // server rejects it); it is not silently turned into a positive amount.
  const negative = value.trimStart().startsWith('-')
  const cleaned = value.replace(/[^\d.]/g, '')
  const firstDot = cleaned.indexOf('.')

  const normalized = firstDot === -1
    ? cleaned
    : `${cleaned.slice(0, firstDot)}.${cleaned
        .slice(firstDot + 1)
        .replace(/\./g, '')
        .slice(0, 2)}`

  return negative ? (normalized.length > 0 ? `-${normalized}` : '-') : normalized
}

/** `1,425.00` → `1425`; an empty or unreadable value is `null`, never zero. */
export function parseCurrencyInput(value: string): number | null {
  const trimmed = value.trim().replace(/,/g, '')
  if (trimmed.length === 0) return null

  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}
