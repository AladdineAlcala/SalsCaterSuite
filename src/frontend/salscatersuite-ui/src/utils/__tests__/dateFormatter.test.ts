import { describe, expect, it } from 'vitest'
import {
  buildPeriodOptions,
  currentPeriodKey,
  formatEventDate,
  formatEventDateTime,
  formatMonthYear,
  formatTimestamp,
  periodKeyFromUtc,
} from '@/utils/dateFormatter'

describe('formatEventDate', () => {
  it('renders a fixed day-month-year format', () => {
    expect(formatEventDate('2026-10-28')).toBe('28 Oct 2026')
  })

  it('does not drift with the browser locale', () => {
    // The same input always produces the same output.
    expect(formatEventDate('2026-01-05')).toBe('5 Jan 2026')
    expect(formatEventDate('2026-12-31')).toBe('31 Dec 2026')
  })

  it('renders an em dash for a missing or invalid value', () => {
    expect(formatEventDate(null)).toBe('—')
    expect(formatEventDate(undefined)).toBe('—')
    expect(formatEventDate('not-a-date')).toBe('—')
  })
})

describe('formatEventDateTime', () => {
  it('renders the UTC time', () => {
    expect(formatEventDateTime('2026-10-28T17:00:00Z')).toBe('28 Oct 2026, 17:00')
  })
})

describe('formatTimestamp', () => {
  it('renders the server timestamp in UTC', () => {
    expect(formatTimestamp('2026-10-19T10:00:00Z')).toBe('19 Oct 2026, 10:00 UTC')
  })

  it('renders an em dash for a missing timestamp', () => {
    expect(formatTimestamp(undefined)).toBe('—')
  })
})

describe('formatMonthYear', () => {
  it('renders the full month name', () => {
    expect(formatMonthYear(2026, 10)).toBe('October 2026')
  })

  it('renders an em dash for an invalid month', () => {
    expect(formatMonthYear(2026, 13)).toBe('—')
  })
})

describe('period helpers', () => {
  it('builds a zero padded YYYY-MM key', () => {
    expect(periodKeyFromUtc(2026, 3)).toBe('2026-03')
  })

  it('derives the current UTC month', () => {
    const now = new Date(Date.UTC(2026, 9, 19, 12, 0, 0))
    expect(currentPeriodKey(now)).toBe('2026-10')
  })

  it('offers the current month first and walks backwards', () => {
    const now = new Date(Date.UTC(2026, 1, 15, 0, 0, 0))
    const options = buildPeriodOptions(3, now)
    expect(options.map((option) => option.value)).toEqual(['2026-02', '2026-01', '2025-12'])
    expect(options[0].label).toBe('February 2026')
    expect(options[2].label).toBe('December 2025')
  })
})
