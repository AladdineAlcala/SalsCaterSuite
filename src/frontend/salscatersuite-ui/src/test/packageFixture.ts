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
import type {
  PackageCatalogueSummary,
  PackageDto,
  PackagePaxApplicable,
} from '@/types/package.types'

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
    packageCode: 'PKG-WED-01',
    packageType: 'Wedding',
    description: 'Wedding Grand Tier',
    amountPax: 1450,
    minPax: 150,
    numPaxId: 4,
    extendedLocationAmount: null,
    isActive: true,
  },
  {
    packageId: 18,
    packageCode: 'PKG-VIP-01',
    packageType: 'Vip',
    description: 'VIP Degustation & Special Service',
    amountPax: 1850,
    minPax: 50,
    numPaxId: 3,
    extendedLocationAmount: null,
    isActive: true,
  },
  {
    packageId: 24,
    packageCode: 'PKG-PRE-01',
    packageType: 'Premier',
    description: 'Premier Corporate Buffet',
    amountPax: 1250,
    minPax: 100,
    numPaxId: 4,
    extendedLocationAmount: 150,
    isActive: true,
  },
  {
    packageId: 31,
    packageCode: 'PKG-REG-01',
    packageType: 'Regular',
    description: 'Regular Feast Buffet',
    amountPax: 950,
    minPax: 80,
    numPaxId: 2,
    extendedLocationAmount: 100,
    isActive: true,
  },
  {
    packageId: 42,
    packageCode: 'PKG-PM-01',
    packageType: 'Pm',
    description: 'PM Merienda Buffet',
    amountPax: 500,
    minPax: 50,
    numPaxId: 1,
    extendedLocationAmount: null,
    isActive: true,
  },
  {
    packageId: 57,
    packageCode: 'PKG-SD-01',
    packageType: 'Sd',
    description: 'Special Diet / SD Tailored',
    amountPax: 1350,
    minPax: 40,
    numPaxId: 2,
    extendedLocationAmount: null,
    isActive: true,
  },
  {
    packageId: 63,
    packageCode: 'PKG-REG-02',
    packageType: 'Regular',
    description: 'Morning Briefing Tier',
    amountPax: null,
    minPax: null,
    numPaxId: 5,
    extendedLocationAmount: null,
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
    packageCode: 'PKG-REG-02',
    packageType: 'Regular',
    description: 'Morning Briefing Tier',
    amountPax: null,
    minPax: null,
    numPaxId: 5,
    extendedLocationAmount: null,
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

/**
 * The read-only applicable-pax lookup the server publishes (contract §12.8,
 * DEC-173) — the five fixed rows in ID order, labels verbatim.
 *
 * The tier form's options come from the endpoint alone; this fixture exists so
 * the component tests can assert against exactly what the API would return.
 * It is test data, never a production option source.
 */
export const packagePaxApplicablesFixture: PackagePaxApplicable[] = [
  { numPaxId: 1, packageApplicablePax: '15 to 19 Pax' },
  { numPaxId: 2, packageApplicablePax: '20 to 29 Pax' },
  { numPaxId: 3, packageApplicablePax: '30 to 49 Pax' },
  { numPaxId: 4, packageApplicablePax: '80 to 100 Above' },
  { numPaxId: 5, packageApplicablePax: 'Not Applicable to PackMeals' },
]
