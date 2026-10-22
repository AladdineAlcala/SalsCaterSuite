import { describe, expect, it } from 'vitest'
import {
  benchmarkPercentage,
  clampRatio,
  formatRatio,
  progressAriaValue,
  ratioPercentage,
} from '@/utils/progressRatio'

describe('clampRatio', () => {
  it('keeps a value inside the track', () => {
    expect(clampRatio(42.5)).toBe(42.5)
  })

  it('clamps below zero to zero', () => {
    expect(clampRatio(-12)).toBe(0)
  })

  it('clamps above one hundred to one hundred', () => {
    expect(clampRatio(180)).toBe(100)
  })

  it('returns null for an undefined ratio', () => {
    expect(clampRatio(null)).toBeNull()
    expect(clampRatio(undefined)).toBeNull()
    expect(clampRatio(Number.NaN)).toBeNull()
  })
})

describe('ratioPercentage', () => {
  it('computes a bounded percentage', () => {
    expect(ratioPercentage(3, 4)).toBe(75)
  })

  it('clamps an out-of-range ratio', () => {
    expect(ratioPercentage(9, 4)).toBe(100)
  })

  it('returns null for a zero denominator', () => {
    expect(ratioPercentage(0, 0)).toBeNull()
    expect(ratioPercentage(10, 0)).toBeNull()
  })

  it('returns null for missing operands', () => {
    expect(ratioPercentage(null, 10)).toBeNull()
    expect(ratioPercentage(10, undefined)).toBeNull()
  })
})

describe('benchmarkPercentage', () => {
  it('measures a margin against its benchmark', () => {
    expect(benchmarkPercentage(56, 65)).toBeCloseTo(86.1538, 3)
  })

  it('returns null when the benchmark is not configured', () => {
    expect(benchmarkPercentage(56, null)).toBeNull()
  })
})

describe('progressAriaValue', () => {
  it('rounds to one fraction digit', () => {
    expect(progressAriaValue(78.449)).toBe(78.4)
  })

  it('returns null for an undefined ratio', () => {
    expect(progressAriaValue(null)).toBeNull()
  })
})

describe('formatRatio', () => {
  it('renders a percentage', () => {
    expect(formatRatio(75)).toBe('75%')
  })

  it('renders an em dash instead of 0% for an undefined ratio', () => {
    expect(formatRatio(null)).toBe('—')
  })
})
