import { describe, expect, it } from 'vitest'
import {
  EM_DASH,
  formatCount,
  formatCurrency,
  formatDecimal,
  formatPercentage,
  formatPercentagePoints,
} from '@/utils/currencyFormatter'

describe('formatCurrency', () => {
  it('renders the peso sign with full precision', () => {
    expect(formatCurrency(1425800)).toBe('₱1,425,800.00')
  })

  it('groups thousands and keeps two decimals', () => {
    expect(formatCurrency(342500)).toBe('₱342,500.00')
    expect(formatCurrency(0)).toBe('₱0.00')
    expect(formatCurrency(1234.5)).toBe('₱1,234.50')
  })

  it('never uses a compact form', () => {
    expect(formatCurrency(1425800)).not.toMatch(/[MK]$/)
  })

  it('renders an em dash for null, undefined and non-finite values', () => {
    expect(formatCurrency(null)).toBe(EM_DASH)
    expect(formatCurrency(undefined)).toBe(EM_DASH)
    expect(formatCurrency(Number.NaN)).toBe(EM_DASH)
    expect(formatCurrency(Number.POSITIVE_INFINITY)).toBe(EM_DASH)
  })
})

describe('formatCount', () => {
  it('groups thousands', () => {
    expect(formatCount(1425800)).toBe('1,425,800')
  })

  it('renders an em dash for null', () => {
    expect(formatCount(null)).toBe(EM_DASH)
  })
})

describe('formatDecimal', () => {
  it('keeps at most two fraction digits', () => {
    expect(formatDecimal(1234.567)).toBe('1,234.57')
  })
})

describe('formatPercentage', () => {
  it('renders one fraction digit by default', () => {
    expect(formatPercentage(78.4)).toBe('78.4%')
  })

  it('honours the requested fraction digits', () => {
    expect(formatPercentage(78.45, 2)).toBe('78.45%')
  })

  it('renders an em dash for null instead of 0%', () => {
    expect(formatPercentage(null)).toBe(EM_DASH)
    expect(formatPercentage(undefined)).toBe(EM_DASH)
  })
})

describe('formatPercentagePoints', () => {
  it('expresses a difference in percentage points', () => {
    expect(formatPercentagePoints(-8.9)).toBe('−8.90 pp')
    expect(formatPercentagePoints(3.8)).toBe('+3.80 pp')
  })

  it('drops the sign for a zero variance', () => {
    expect(formatPercentagePoints(0)).toBe('0.00 pp')
  })

  it('renders an em dash for null', () => {
    expect(formatPercentagePoints(null)).toBe(EM_DASH)
  })
})
