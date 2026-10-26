import { describe, expect, it } from 'vitest'
import { bookingQuoteFixture } from '@/test/bookingFixture'
import type { BookingQuote } from '@/types/booking.types'
import {
  emptyBookingWorkbenchDraft,
  isSameTierType,
  resolveTierQuoteRate,
  supportsExtendedArea,
  validateBookingWorkbenchDraft,
} from '../bookingVocabulary'
import type { BookingQuoteHeldRate, BookingWorkbenchDraft } from '../bookingVocabulary'

/**
 * The package type → Area filter → Buffet tier cascade's client gate, the
 * trimmed type list filter and the Rate-per-head resolver (spec §5.2,
 * AC-BKG-89..98, DEC-171).
 *
 * The gate is a form affordance only — nothing here reaches the API.
 */
describe('bookingVocabulary — the tier cascade gate', () => {
  function withDraft(overrides: Partial<BookingWorkbenchDraft>): BookingWorkbenchDraft {
    return { ...emptyBookingWorkbenchDraft(), ...overrides }
  }

  it('demands a package type before a tier (AC-BKG-92)', () => {
    const errors = validateBookingWorkbenchDraft(
      withDraft({ customerId: '18', occasion: 'Gala', venue: 'Pavilion' }),
    )

    expect(errors.packageType).toBe('Select a package type before choosing a buffet tier.')
  })

  it('demands the event area for the eligible types (AC-BKG-93)', () => {
    const regular = validateBookingWorkbenchDraft(withDraft({ packageType: ' Regular ' }))
    expect(regular.areaFilterId).toBe('Select the event area before choosing a buffet tier.')
    expect(regular.packageType).toBeUndefined()

    const premier = validateBookingWorkbenchDraft(withDraft({ packageType: 'PREMIER' }))
    expect(premier.areaFilterId).toBe('Select the event area before choosing a buffet tier.')
  })

  it('does not demand an area for a non-eligible stored type (AC-BKG-93)', () => {
    const errors = validateBookingWorkbenchDraft(
      withDraft({ packageType: 'Wedding', areaFilterId: '' }),
    )

    expect(errors.packageType).toBeUndefined()
    expect(errors.areaFilterId).toBeUndefined()
  })

  it('refuses a tier chosen while the gate is unsatisfied (AC-BKG-97)', () => {
    const withoutType = validateBookingWorkbenchDraft(withDraft({ packageId: '12' }))
    expect(withoutType.packageId).toBe(
      'The selected buffet tier does not match the package type and area.',
    )

    const withoutArea = validateBookingWorkbenchDraft(
      withDraft({ packageType: 'Regular', packageId: '31' }),
    )
    expect(withoutArea.packageId).toBe(
      'The selected buffet tier does not match the package type and area.',
    )
  })

  it('passes a tier whose gate is satisfied without a complaint', () => {
    const ineligible = validateBookingWorkbenchDraft(
      withDraft({ packageType: 'Wedding', packageId: '12' }),
    )
    expect(ineligible.packageType).toBeUndefined()
    expect(ineligible.packageId).toBeUndefined()

    const eligible = validateBookingWorkbenchDraft(
      withDraft({ packageType: 'Regular', areaFilterId: '4', packageId: '31' }),
    )
    expect(eligible.packageType).toBeUndefined()
    expect(eligible.areaFilterId).toBeUndefined()
    expect(eligible.packageId).toBeUndefined()
  })

  it('keeps supportsExtendedArea the single trimmed, case-insensitive eligibility helper (DEC-156)', () => {
    expect(supportsExtendedArea(' Regular ')).toBe(true)
    expect(supportsExtendedArea('PREMIER')).toBe(true)
    expect(supportsExtendedArea('Premier Buffet')).toBe(false)
    expect(supportsExtendedArea(null)).toBe(false)
  })
})

describe('isSameTierType — the trimmed, case-sensitive list filter (AC-BKG-96)', () => {
  it('matches after trimming both sides', () => {
    expect(isSameTierType(' Regular', 'Regular ')).toBe(true)
    expect(isSameTierType('Wedding', 'Wedding')).toBe(true)
  })

  it('keeps the case significant after trimming', () => {
    expect(isSameTierType('regular', 'Regular')).toBe(false)
    expect(isSameTierType('Premier ', 'premier')).toBe(false)
  })
})

describe('resolveTierQuoteRate — the Rate-per-head readout (AC-BKG-89..91, 98)', () => {
  function quoteWith(pricePerHead: number | null): BookingQuote {
    return { ...bookingQuoteFixture, pricePerHead }
  }

  const held = (packageId: number, rate: number | null): BookingQuoteHeldRate => ({
    packageId,
    rate,
  })

  it('shows the catalogue rate before any quote exists (AC-BKG-89)', () => {
    const resolution = resolveTierQuoteRate({
      selectedPackageId: 31,
      catalogueRate: 950,
      quote: null,
      lastResolvedQuote: null,
    })

    expect(resolution.statedRate).toBe(950)
    expect(resolution.isQuoteShown).toBe(false)
    expect(resolution.isDivergent).toBe(false)
  })

  it('lets the quote for the selected tier win (AC-BKG-98)', () => {
    const resolution = resolveTierQuoteRate({
      selectedPackageId: 12,
      catalogueRate: 1450,
      quote: quoteWith(1140),
      lastResolvedQuote: held(12, 1140),
    })

    expect(resolution.statedRate).toBe(1140)
    expect(resolution.isQuoteShown).toBe(true)
    expect(resolution.isDivergent).toBe(true)
  })

  it('keeps the last resolved rate while the same tier re-prices (AC-BKG-90)', () => {
    // The quote is in flight: no data, but the held rate belongs to tier 31.
    const resolution = resolveTierQuoteRate({
      selectedPackageId: 31,
      catalogueRate: 950,
      quote: null,
      lastResolvedQuote: held(31, 1140),
    })

    expect(resolution.statedRate).toBe(1140)
    expect(resolution.isQuoteShown).toBe(false)
    expect(resolution.isDivergent).toBe(false)
  })

  it('never carries the previous tier rate across a package change (AC-BKG-90)', () => {
    const withQuote = resolveTierQuoteRate({
      selectedPackageId: 12,
      catalogueRate: 1450,
      quote: quoteWith(1140),
      lastResolvedQuote: held(31, 950),
    })
    expect(withQuote.statedRate).toBe(1450)

    const withoutQuote = resolveTierQuoteRate({
      selectedPackageId: 12,
      catalogueRate: 1450,
      quote: null,
      lastResolvedQuote: held(31, 950),
    })
    expect(withoutQuote.statedRate).toBe(1450)
  })

  it('never renders a catalogue null as the quote zero, and keeps an actual zero (AC-BKG-91)', () => {
    const unpriced = resolveTierQuoteRate({
      selectedPackageId: 63,
      catalogueRate: null,
      quote: quoteWith(0),
      lastResolvedQuote: held(63, 0),
    })
    // The quote's zero is not money here — but the rates still differ
    // strictly, so the divergence verdict fires (AC-BKG-98).
    expect(unpriced.statedRate).toBeNull()
    expect(unpriced.isDivergent).toBe(true)

    const zeroCatalogue = resolveTierQuoteRate({
      selectedPackageId: 91,
      catalogueRate: 0,
      quote: quoteWith(0),
      lastResolvedQuote: held(91, 0),
    })
    expect(zeroCatalogue.statedRate).toBe(0)
    expect(zeroCatalogue.isDivergent).toBe(false)
  })

  it('treats a null quoted rate as authoritative "not stated", never as catalogue fallback', () => {
    const resolution = resolveTierQuoteRate({
      selectedPackageId: 31,
      catalogueRate: 950,
      quote: quoteWith(null),
      lastResolvedQuote: held(31, null),
    })

    expect(resolution.statedRate).toBeNull()
    // Strict comparison — null differs from 950, so the note fires.
    expect(resolution.isDivergent).toBe(true)
  })

  it('keeps a held null rate a resolved "not stated" while re-pricing, never the catalogue', () => {
    const resolution = resolveTierQuoteRate({
      selectedPackageId: 31,
      catalogueRate: 950,
      quote: null,
      lastResolvedQuote: held(31, null),
    })

    expect(resolution.statedRate).toBeNull()
    expect(resolution.isDivergent).toBe(false)
  })

  it('does not call a differing held rate a divergence (only a current quote is)', () => {
    const resolution = resolveTierQuoteRate({
      selectedPackageId: 31,
      catalogueRate: 950,
      quote: null,
      lastResolvedQuote: held(31, 1140),
    })

    expect(resolution.isDivergent).toBe(false)
  })
})
