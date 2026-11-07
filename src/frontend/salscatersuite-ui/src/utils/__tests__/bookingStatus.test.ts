import { describe, expect, it } from 'vitest'
import { BOOKING_STATUS_META, getBookingStatusMeta } from '@/utils/bookingStatus'
import type { BookingStatus } from '@/types/dashboard.types'

const ALL_STATUSES: BookingStatus[] = [
  'Pending',
  'Confirmed',
  'InProgress',
  'Completed',
  'Cancelled',
  'NoShow',
  'Rescheduled',
  'Expired',
]

describe('BOOKING_STATUS_META', () => {
  it('covers every BR-10 status', () => {
    expect(Object.keys(BOOKING_STATUS_META).sort()).toEqual([...ALL_STATUSES].sort())
  })

  it('maps each status to a human label', () => {
    expect(BOOKING_STATUS_META.Pending.label).toBe('Pending')
    expect(BOOKING_STATUS_META.Confirmed.label).toBe('Confirmed')
    expect(BOOKING_STATUS_META.InProgress.label).toBe('In Progress')
    expect(BOOKING_STATUS_META.Completed.label).toBe('Completed')
    expect(BOOKING_STATUS_META.Cancelled.label).toBe('Cancelled')
    expect(BOOKING_STATUS_META.NoShow.label).toBe('No Show')
    expect(BOOKING_STATUS_META.Rescheduled.label).toBe('Rescheduled')
    expect(BOOKING_STATUS_META.Expired.label).toBe('Expired')
  })

  it('never renders a BEO production state', () => {
    const labels = ALL_STATUSES.map((status) => BOOKING_STATUS_META[status].label)
    expect(labels).not.toContain('BEO Ready')
    expect(labels).not.toContain('Prep Pending')
    expect(labels).not.toContain('Finalized')
  })

  it('always pairs the status with an icon so colour is never the only signal', () => {
    for (const status of ALL_STATUSES) {
      expect(BOOKING_STATUS_META[status].icon).toBeDefined()
    }
  })
})

describe('getBookingStatusMeta', () => {
  it('resolves a known status', () => {
    expect(getBookingStatusMeta('Confirmed').label).toBe('Confirmed')
  })

  it('degrades gracefully for an unknown status', () => {
    const meta = getBookingStatusMeta('SomethingNew')
    expect(meta.label).toBe('SomethingNew')
    expect(meta.tone).toBe('neutral')
  })

  it('degrades gracefully for a missing status', () => {
    expect(getBookingStatusMeta(null).label).toBe('Unknown')
  })
})
