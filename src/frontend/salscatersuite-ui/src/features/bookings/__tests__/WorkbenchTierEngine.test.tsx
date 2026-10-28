import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { WorkbenchTierEngine } from '../components/WorkbenchTierEngine'
import type { BookingQuoteHeldRate, BookingWorkbenchDraft } from '../bookingVocabulary'
import {
  RATE_DIVERGENCE_NOTE,
  emptyBookingWorkbenchDraft,
} from '../bookingVocabulary'
import { areaCatalogueFixture, packageCourseRulesetFixture } from '@/test/packageWorkbenchFixture'
import { bookingQuoteFixture } from '@/test/bookingFixture'
import { packageTiersFixture } from '@/test/packageFixture'
import type { PackageDto } from '@/types/package.types'
import type { BookingQuote } from '@/types/booking.types'

/**
 * The WorkbenchTierEngine's readouts and gate states (spec §5.2,
 * AC-BKG-89..93, AC-BKG-95..98).
 *
 * The component is presentational: the tiers it shows are the page's
 * server-filtered list, and every cascade choice is one draft patch it
 * emits — the tests below assert both.
 */
function quoteWith(pricePerHead: number | null): BookingQuote {
  return { ...bookingQuoteFixture, pricePerHead }
}

describe('WorkbenchTierEngine', () => {
  const tierWith = (overrides: Partial<PackageDto>): PackageDto => ({
    packageId: 31,
    packageCode: 'PKG-REG-01',
    packageType: 'Regular',
    description: 'Regular Feast Buffet',
    amountPax: 950,
    minPax: 80,
    numPaxId: 2,
    extendedLocationAmount: null,
    isActive: true,
    ...overrides,
  })

  function renderEngine(overrides: {
    draft?: Partial<BookingWorkbenchDraft>
    tiers?: PackageDto[]
    quote?: BookingQuote | null
    lastResolvedQuote?: BookingQuoteHeldRate | null
    isLoadingTiers?: boolean
    isRecalculating?: boolean
  } = {}) {
    const patches: Partial<BookingWorkbenchDraft>[] = []
    const props = {
      draft: { ...emptyBookingWorkbenchDraft(), ...overrides.draft },
      errors: {},
      onChange: vi.fn((patch: Partial<BookingWorkbenchDraft>) => patches.push(patch)),
      tiers: overrides.tiers ?? packageTiersFixture,
      isLoadingTiers: overrides.isLoadingTiers ?? false,
      municipalities: areaCatalogueFixture,
      packageTypes: ['Premier', 'Pm', 'Regular', 'Sd', 'Vip', 'Wedding'],
      ruleset: packageCourseRulesetFixture,
      isLoadingRuleset: false,
      quote: overrides.quote ?? null,
      lastResolvedQuote: overrides.lastResolvedQuote ?? null,
      isRecalculating: overrides.isRecalculating ?? false,
    }
    render(<WorkbenchTierEngine {...props} />)
    return { patches }
  }

  describe('Rate per head', () => {
    it('shows the catalogue rate before any quote exists (AC-BKG-89)', () => {
      renderEngine({ draft: { packageId: '31' }, tiers: [tierWith({})] })

      expect(screen.getByText('Regular Feast Buffet')).toBeInTheDocument()
      expect(screen.getByText('₱950.00')).toBeInTheDocument()
    })

    it('lets the current quote for the selected tier win (AC-BKG-98)', () => {
      renderEngine({
        draft: { packageId: '31' },
        tiers: [tierWith({})],
        quote: quoteWith(1140),
        lastResolvedQuote: { packageId: 31, rate: 1140 },
      })

      expect(screen.getByText('₱1,140.00')).toBeInTheDocument()
      expect(screen.getByTestId('booking-rate-divergence')).toHaveTextContent(
        RATE_DIVERGENCE_NOTE,
      )
    })

    it('keeps the last resolved rate while re-pricing the same tier (AC-BKG-90)', () => {
      renderEngine({
        draft: { packageId: '31' },
        tiers: [tierWith({})],
        lastResolvedQuote: { packageId: 31, rate: 1140 },
        isRecalculating: true,
      })

      expect(screen.getByText('₱1,140.00')).toBeInTheDocument()
      expect(screen.getByTestId('booking-rate-recalculating')).toHaveTextContent('Re-pricing…')
    })

    it('falls back to the new tier catalogue rate immediately after a package change (AC-BKG-90)', () => {
      renderEngine({
        draft: { packageId: '31' },
        tiers: [tierWith({})],
        // The client still holds tier 12's quote — it may not speak for 31.
        quote: quoteWith(1140),
        lastResolvedQuote: { packageId: 12, rate: 1140 },
      })

      expect(screen.getByText('₱950.00')).toBeInTheDocument()
      expect(screen.queryByText('₱1,140.00')).not.toBeInTheDocument()
      expect(screen.queryByTestId('booking-rate-divergence')).not.toBeInTheDocument()
    })

    it('shows "not stated" for an unpriced tier, never zero — including from the quote (AC-BKG-91)', () => {
      renderEngine({
        draft: { packageId: '63' },
        tiers: [tierWith({ packageId: 63, description: 'Morning Briefing Tier', amountPax: null })],
        quote: quoteWith(0),
        lastResolvedQuote: { packageId: 63, rate: 0 },
      })

      expect(screen.getByText('not stated')).toBeInTheDocument()
      expect(screen.queryByText('₱0.00')).not.toBeInTheDocument()
    })

    it('renders an actual zero rate as currency zero (AC-BKG-91)', () => {
      renderEngine({
        draft: { packageId: '91' },
        tiers: [tierWith({ packageId: 91, description: 'Zero Rate Tier', amountPax: 0 })],
      })

      expect(screen.getByText('₱0.00')).toBeInTheDocument()
    })

    it('keeps only the rate cell alive while re-pricing — the other quote-derived display rests', () => {
      renderEngine({
        draft: { packageId: '31' },
        tiers: [tierWith({})],
        quote: quoteWith(1140),
        lastResolvedQuote: { packageId: 31, rate: 1140 },
        isRecalculating: true,
      })

      expect(screen.getByText('₱1,140.00')).toBeInTheDocument()
      expect(screen.queryByText(/The tier states a minimum/)).not.toBeInTheDocument()
      expect(screen.queryByText('Tier planning estimate')).not.toBeInTheDocument()
      // A verdict from the previous request is not repeated either.
      expect(screen.queryByText('The tier is deactivated')).not.toBeInTheDocument()
    })

    it('shows the quote-derived display again once the current quote stands', () => {
      renderEngine({
        draft: { packageId: '31' },
        tiers: [tierWith({})],
        quote: quoteWith(1140),
        lastResolvedQuote: { packageId: 31, rate: 1140 },
      })

      expect(screen.getByText(/The tier states a minimum/)).toBeInTheDocument()
      expect(screen.getByText('Tier planning estimate')).toBeInTheDocument()
    })
  })

  describe('the cascade gate', () => {
    it('opens closed: the tier picker waits for the package type (AC-BKG-92)', () => {
      const { patches } = renderEngine({})

      const tier = screen.getByLabelText('Buffet tier') as HTMLSelectElement
      expect(tier).toBeDisabled()
      expect(tier).toHaveValue('')
      expect(screen.getByText('Select a package type first')).toBeInTheDocument()
      expect(screen.getByText('Choose the package type to load its tiers')).toBeInTheDocument()

      fireEvent.change(screen.getByLabelText('Package type'), {
        target: { value: 'Wedding' },
      })
      expect(patches.at(-1)).toEqual({
        packageType: 'Wedding',
        packageId: '',
        areaFilterId: '',
      })
    })

    it('requires the event area for an eligible type above the picker (AC-BKG-93)', () => {
      renderEngine({ draft: { packageType: 'Regular' } })

      expect(screen.getByLabelText('Area filter')).toBeInTheDocument()
      const area = screen.getByLabelText('Area filter') as HTMLSelectElement
      expect(area.required).toBe(true)
      const tier = screen.getByLabelText('Buffet tier') as HTMLSelectElement
      expect(tier).toBeDisabled()
      expect(screen.getByText('Choose the event area to load its tiers')).toBeInTheDocument()
    })

    it('names the loading area on the picker while the placeholder data stands in (AC-BKG-95)', () => {
      renderEngine({
        draft: { packageType: 'Regular', areaFilterId: '4' },
        isLoadingTiers: true,
      })

      const tier = screen.getByLabelText('Buffet tier') as HTMLSelectElement
      expect(tier).toBeDisabled()
      expect(tier.textContent).toContain('Loading the tiers for Antipolo City…')
    })

    it('explains an empty result and offers clear actions (AC-BKG-95)', () => {
      const { patches } = renderEngine({
        draft: { packageType: 'Regular', areaFilterId: '6' },
        tiers: [],
      })

      expect(
        screen.getByText(/No active tier of type Regular covers Lucena City/),
      ).toBeInTheDocument()
      expect(screen.getByText('Clear the area')).toBeInTheDocument()
      expect(screen.getByText('Clear the type')).toBeInTheDocument()

      fireEvent.click(screen.getByText('Clear the area'))
      expect(patches.at(-1)).toEqual({ areaFilterId: '', packageId: '' })
      fireEvent.click(screen.getByText('Clear the type'))
      expect(patches.at(-1)).toEqual({
        packageType: '',
        packageId: '',
        areaFilterId: '',
      })
    })

    it('keeps a chosen area across the two eligible types and clears it on a non-eligible type (AC-BKG-94)', () => {
      const { patches } = renderEngine({
        draft: { packageType: 'Regular', areaFilterId: '4' },
      })

      fireEvent.change(screen.getByLabelText('Package type'), {
        target: { value: 'Premier' },
      })
      expect(patches.at(-1)).toEqual({ packageType: 'Premier', packageId: '' })

      fireEvent.change(screen.getByLabelText('Package type'), {
        target: { value: 'Vip' },
      })
      expect(patches.at(-1)).toEqual({
        packageType: 'Vip',
        packageId: '',
        areaFilterId: '',
      })
    })

    it('copies a non-empty area into the event municipality in one patch and never reverses it (DEC-171)', () => {
      const { patches } = renderEngine({
        draft: { packageType: 'Regular', areaFilterId: '4' },
      })

      fireEvent.change(screen.getByLabelText('Area filter'), {
        target: { value: '9' },
      })
      expect(patches.at(-1)).toEqual({
        areaFilterId: '9',
        packageId: '',
        extendedAreaId: '9',
      })

      fireEvent.change(screen.getByLabelText('Area filter'), {
        target: { value: '' },
      })
      expect(patches.at(-1)).toEqual({ areaFilterId: '', packageId: '' })
    })
  })
})
