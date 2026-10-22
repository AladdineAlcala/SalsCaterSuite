/**
 * TEST FIXTURE — not production data.
 *
 * A fixed, hand-written set of Packages & Buffets payloads used by the
 * component tests. Every value is chosen so the assertions can check the exact
 * rendering of the formatters, the server's null semantics and the error
 * contract.
 *
 * This file lives under `src/test/` and is never imported by application code.
 */
import type { PackageCatalogueSummary, PackageDto } from '@/types/package.types'

/**
 * The catalogue the server published.
 *
 * Six priced, active tiers across six distinct tier types, plus one tier that
 * carries neither a rate nor a headcount floor and is not selectable. The
 * unpriced tier exercises the em dash and the "unpriced sorts last" rule.
 */
export const packageTiersFixture: PackageDto[] = [
  {
    packageId: 12,
    packageType: 'Wedding',
    description: 'Wedding Grand Tier',
    amountPax: 1450,
    minPax: 150,
    isActive: true,
  },
  {
    packageId: 18,
    packageType: 'Vip',
    description: 'VIP Degustation & Special Service',
    amountPax: 1850,
    minPax: 50,
    isActive: true,
  },
  {
    packageId: 24,
    packageType: 'Premier',
    description: 'Premier Corporate Buffet',
    amountPax: 1250,
    minPax: 100,
    isActive: true,
  },
  {
    packageId: 31,
    packageType: 'Regular',
    description: 'Regular Feast Buffet',
    amountPax: 950,
    minPax: 80,
    isActive: true,
  },
  {
    packageId: 42,
    packageType: 'Pm',
    description: 'PM Merienda Buffet',
    amountPax: 500,
    minPax: 50,
    isActive: true,
  },
  {
    packageId: 57,
    packageType: 'Sd',
    description: 'Special Diet / SD Tailored',
    amountPax: 1350,
    minPax: 40,
    isActive: true,
  },
  {
    packageId: 63,
    packageType: 'Regular',
    description: 'Morning Briefing Tier',
    amountPax: null,
    minPax: null,
    isActive: false,
  },
]

/**
 * The aggregate the server computed over {@link packageTiersFixture}.
 *
 * The average is the mean of the six priced rates and the range spans the
 * lowest and the highest of them.
 */
export const packageSummaryFixture: PackageCatalogueSummary = {
  totalPackages: 7,
  activePackages: 6,
  inactivePackages: 1,
  pricedPackages: 6,
  averageAmountPax: 1225,
  lowestAmountPax: 500,
  highestAmountPax: 1850,
  lowestMinPax: 40,
  highestMinPax: 150,
  generatedAt: '2026-10-21T09:15:00.1234567Z',
}

/** A catalogue that holds no tier at all. */
export const emptyPackageTiersFixture: PackageDto[] = []

/** The aggregate of a catalogue that holds no tier at all. */
export const emptyPackageSummaryFixture: PackageCatalogueSummary = {
  totalPackages: 0,
  activePackages: 0,
  inactivePackages: 0,
  pricedPackages: 0,
  averageAmountPax: null,
  lowestAmountPax: null,
  highestAmountPax: null,
  lowestMinPax: null,
  highestMinPax: null,
  generatedAt: '2026-10-21T09:15:00.1234567Z',
}

/** A single tier that carries neither a rate nor a headcount floor. */
export const unpricedPackageTiersFixture: PackageDto[] = [
  {
    packageId: 63,
    packageType: 'Regular',
    description: 'Morning Briefing Tier',
    amountPax: null,
    minPax: null,
    isActive: false,
  },
]

/**
 * The aggregate of a catalogue in which **no tier is priced**.
 *
 * Every monetary figure and every headcount figure is `null` — unknown, not
 * zero — so the client renders an em dash (contract rule 6).
 */
export const unpricedPackageSummaryFixture: PackageCatalogueSummary = {
  totalPackages: 1,
  activePackages: 0,
  inactivePackages: 1,
  pricedPackages: 0,
  averageAmountPax: null,
  lowestAmountPax: null,
  highestAmountPax: null,
  lowestMinPax: null,
  highestMinPax: null,
  generatedAt: '2026-10-21T09:15:00.1234567Z',
}

/** One tier, used by the tier-form tests. */
export const packageTierFixture: PackageDto = packageTiersFixture[0]
