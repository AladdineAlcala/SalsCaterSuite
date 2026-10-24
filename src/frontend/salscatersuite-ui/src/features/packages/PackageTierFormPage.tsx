import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, MapPinned, RefreshCw, UtensilsCrossed } from 'lucide-react'
import { ApiError } from '@/api/apiClient'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { SpinnerLoader } from '@/components/feedback/SpinnerLoader'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { CurrencyInput } from '@/components/ui/CurrencyInput'
import type { PackageDto, PackageUpsertRequest } from '@/types/package.types'
import { parseCurrencyInput } from '@/utils/currencyInput'
import { usePackageDetail } from './hooks/usePackageCatalogue'
import { useCreatePackage, useUpdatePackage } from './hooks/usePackageMutations'
import { isExtendedLocationEligible, KNOWN_PACKAGE_TYPES } from './packageTypes'

/** The editable attributes of a buffet tier (spec §5.3). */
export interface PackageFormValues {
  /** `Package.PackageType` — free text. */
  packageType: string
  /** `Package.Description` — the tier's display name. */
  description: string
  /** `Package.AmountPax` — the per-guest rate, typed as a plain number. */
  amountPax: string
  /** `Package.MinPax` — the minimum headcount, typed as a plain number. */
  minPax: string
  /** `Package.ExtendedLocationAmount` — the tier's per-guest extended charge. */
  extendedLocationAmount: string
}

export const EMPTY_PACKAGE_FORM_VALUES: PackageFormValues = {
  packageType: '',
  description: '',
  amountPax: '',
  minPax: '',
  extendedLocationAmount: '',
}

/** The per-field client validation messages. */
export interface PackageFormErrors {
  packageType?: string
  description?: string
  amountPax?: string
  minPax?: string
  extendedLocationAmount?: string
}

/** Seeds the form from the tier the API published. */
export function packageFormValuesFromTier(tier: PackageDto): PackageFormValues {
  return {
    packageType: tier.packageType,
    description: tier.description,
    amountPax: tier.amountPax === null ? '' : String(tier.amountPax),
    minPax: tier.minPax === null ? '' : String(tier.minPax),
    extendedLocationAmount:
      tier.extendedLocationAmount == null ? '' : String(tier.extendedLocationAmount),
  }
}

/**
 * Reads a typed number.
 *
 * The browser only parses what the operator typed — it never derives a rate
 * from another figure.
 */
export function parsePackageNumber(value: string): number | null {
  const trimmed = value.trim().replace(/,/g, '')
  if (trimmed.length === 0) return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

/**
 * The presence and sign checks the form makes before it calls the API
 * (spec §5.3, AC-P11).
 *
 * The length limits are the server's to rule, and its `errors[]` are rendered
 * verbatim.
 */
export function validatePackageForm(values: PackageFormValues): PackageFormErrors {
  const errors: PackageFormErrors = {}

  if (values.packageType.trim().length === 0) {
    errors.packageType = 'Package type is required.'
  }
  if (values.description.trim().length === 0) {
    errors.description = 'Package description is required.'
  }

  if (values.amountPax.trim().length > 0) {
    const amountPax = parsePackageNumber(values.amountPax)
    if (amountPax === null) {
      errors.amountPax = 'Rate per guest must be a number.'
    } else if (amountPax < 0) {
      errors.amountPax = 'Rate per guest must be zero or greater.'
    }
  }

  if (values.minPax.trim().length > 0) {
    const minPax = parsePackageNumber(values.minPax)
    if (minPax === null) {
      errors.minPax = 'Minimum pax must be a whole number.'
    } else if (minPax < 1) {
      errors.minPax = 'Minimum pax must be at least 1.'
    }
  }

  if (values.extendedLocationAmount.trim().length > 0) {
    if (!isExtendedLocationEligible(values.packageType)) {
      errors.extendedLocationAmount =
        'The extended location amount applies only to the Regular and Premier tier types.'
    } else {
      const extendedLocationAmount = parseCurrencyInput(values.extendedLocationAmount)
      if (extendedLocationAmount === null) {
        errors.extendedLocationAmount = 'Extended location amount must be a number.'
      } else if (extendedLocationAmount < 0) {
        errors.extendedLocationAmount = 'Extended location amount must be zero or greater.'
      }
    }
  }

  return errors
}

/**
 * Maps the form onto the API request body (spec §5.3).
 *
 * `null` when a typed number could not be read — the API requires a rate that
 * is a number and a headcount that is a whole number.
 */
export function toPackageRequest(values: PackageFormValues): PackageUpsertRequest | null {
  const amountPax = parsePackageNumber(values.amountPax)
  const minPax = parsePackageNumber(values.minPax)
  const extendedLocationAmount = parseCurrencyInput(values.extendedLocationAmount)

  if (values.amountPax.trim().length > 0 && amountPax === null) return null
  if (values.minPax.trim().length > 0 && minPax === null) return null
  if (
    values.extendedLocationAmount.trim().length > 0 &&
    extendedLocationAmount === null
  ) {
    return null
  }

  return {
    packageType: values.packageType.trim(),
    description: values.description.trim(),
    amountPax,
    minPax,
    extendedLocationAmount: isExtendedLocationEligible(values.packageType)
      ? extendedLocationAmount
      : null,
  }
}

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-md text-body-md w-full'

/**
 * The Create / Edit Tier screen (spec §5.3).
 *
 * The screen holds the tier's own record: its type, its display name, its
 * per-guest rate and its minimum headcount. The rate is a plain number in the
 * field — it is never a peso-formatted string — and the browser never
 * derives a figure from it.
 *
 * The server's `errors[]` are rendered verbatim and the operator's typed
 * input survives a rejected save (AC-P11).
 */
export function PackageTierFormPage() {
  const navigate = useNavigate()
  const params = useParams<{ packageId?: string }>()

  const rawId = params.packageId ?? null
  const parsedId = rawId === null ? Number.NaN : Number(rawId)
  const packageId = Number.isFinite(parsedId) ? parsedId : null
  const isEdit = rawId !== null

  const [values, setValues] = useState<PackageFormValues>(EMPTY_PACKAGE_FORM_VALUES)
  const [fieldErrors, setFieldErrors] = useState<PackageFormErrors>({})
  const [serverErrors, setServerErrors] = useState<string[]>([])

  const { data: tier, isPending: isLoadingTier, isError: isTierError, error: tierError, refetch } =
    usePackageDetail(isEdit ? packageId : null)

  const createPackage = useCreatePackage()
  const updatePackage = useUpdatePackage()

  // The form is seeded once from the tier the API published; a rejected save
  // never resets what the operator typed (AC-P11).
  const seededId = useRef<number | null>(null)
  useEffect(() => {
    if (tier && seededId.current !== tier.packageId) {
      seededId.current = tier.packageId
      setValues(packageFormValuesFromTier(tier))
    }
  }, [tier])

  const handleChange = useCallback(
    (field: keyof PackageFormValues, value: string) => {
      const clearsExtendedAmount =
        field === 'packageType' && !isExtendedLocationEligible(value)

      setValues((current) => ({
        ...current,
        [field]: value,
        ...(clearsExtendedAmount ? { extendedLocationAmount: '' } : {}),
      }))
      setFieldErrors((current) => {
        const next = { ...current }
        delete next[field]
        if (clearsExtendedAmount) delete next.extendedLocationAmount
        return next
      })
    },
    [],
  )

  const handleCancel = useCallback(() => {
    navigate('/packages')
  }, [navigate])

  /*
   * The workbench links are reachable once the tier is saved: an unsaved tier
   * has no ruleset and no coverage set to open (DEC-084).
   */
  const isSavedTier = isEdit && packageId !== null

  const handleOpenWorkbench = useCallback(
    (workbench: 'rules' | 'locations') => {
      if (packageId === null) return
      navigate(`/packages/${encodeURIComponent(String(packageId))}/${workbench}`)
    },
    [navigate, packageId],
  )

  const handleSave = useCallback(() => {
    if (isEdit && packageId === null) {
      setFieldErrors({})
      setServerErrors(['The tier being edited could not be identified.'])
      return
    }

    const problems = validatePackageForm(values)
    if (Object.keys(problems).length > 0) {
      setFieldErrors(problems)
      setServerErrors([])
      return
    }

    const request = toPackageRequest(values)
    if (request === null) {
      setFieldErrors({
        amountPax:
          values.amountPax.trim().length > 0
            ? 'Rate per guest must be a number.'
            : undefined,
        minPax:
          values.minPax.trim().length > 0 ? 'Minimum pax must be a whole number.' : undefined,
        extendedLocationAmount:
          values.extendedLocationAmount.trim().length > 0
            ? 'Extended location amount must be a number.'
            : undefined,
      })
      setServerErrors([])
      return
    }

    setFieldErrors({})
    setServerErrors([])

    const onError = (error: unknown) => {
      setServerErrors(readErrorMessages(error))
    }

    if (isEdit && packageId !== null) {
      updatePackage.mutate(
        { packageId, request },
        {
          onSuccess: () => navigate('/packages'),
          onError,
        },
      )
      return
    }

    createPackage.mutate(request, {
      onSuccess: () => navigate('/packages'),
      onError,
    })
  }, [values, isEdit, packageId, updatePackage, createPackage, navigate])

  const isSaving = createPackage.isPending || updatePackage.isPending

  return (
    <div className="flex flex-col gap-space-xl">
      <Card
        elevation={1}
        className="flex flex-col lg:flex-row lg:items-center justify-between gap-space-md"
      >
        <div className="flex flex-col gap-1 min-w-0">
          <nav
            aria-label="Breadcrumb"
            className="flex items-center gap-2 font-body-sm text-body-sm text-espresso-400 flex-wrap"
          >
            <span>Catering Operations</span>
            <span aria-hidden="true">/</span>
            <span>Packages &amp; Buffets</span>
            <span aria-hidden="true">/</span>
            <span className="font-semibold text-espresso-850">
              {isEdit ? 'Edit Tier' : 'New Tier'}
            </span>
          </nav>
          <h1 className="font-headline-lg text-headline-lg text-espresso-900 tracking-tight">
            {isEdit ? 'Edit Package Tier' : 'New Package Tier'}
          </h1>
          <p className="font-body-md text-body-md text-espresso-600 max-w-3xl">
            The buffet tier record: its type, its display name, its per-guest rate, its
            minimum headcount and — for Regular and Premier — its extended location amount.
          </p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <Button variant="secondary" icon={ArrowLeft} onClick={handleCancel}>
            Back to the catalogue
          </Button>
        </div>
      </Card>

      {/*
       * The **client** checks render inline, under the field they belong to.
       * The **server's** rejections render here, verbatim (AC-P11).
       */}
      {serverErrors.length > 0 ? (
        <AlertBanner
          title="The tier was rejected"
          errors={serverErrors}
          action={
            <Button
              variant="secondary"
              size="sm"
              icon={RefreshCw}
              onClick={() => setServerErrors([])}
            >
              Dismiss
            </Button>
          }
        />
      ) : null}

      {isTierError ? (
        <AlertBanner
          title="The tier could not be loaded"
          errors={readErrorMessages(tierError)}
          action={
            <Button variant="secondary" size="sm" icon={RefreshCw} onClick={() => void refetch()}>
              Retry
            </Button>
          }
        />
      ) : null}

      {isEdit && isLoadingTier ? <SpinnerLoader label="Loading the tier" /> : null}

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter items-start">
        <div className="lg:col-span-7 xl:col-span-8 flex flex-col gap-space-md min-w-0">
          <Card elevation={1} className="flex flex-col gap-space-md">
            <div className="flex items-center justify-between gap-2 pb-1">
              <h2 className="font-headline-sm text-headline-sm text-espresso-900">
                Buffet Tier Definition
              </h2>
              <span className="font-caption text-caption px-2 py-0.5 rounded bg-culinary-100 text-culinary-800 font-bold uppercase tracking-wider">
                {isEdit ? 'Edit Mode' : 'Create Mode'}
              </span>
            </div>

            <div className="flex flex-col gap-3.5">
              {isEdit && tier ? (
                <div className="flex flex-col gap-1">
                  <span className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider">
                    Package code
                  </span>
                  <span className="font-data-md text-data-md text-espresso-900 font-bold">
                    {tier.packageCode}
                  </span>
                  <p className="font-caption text-caption text-espresso-600">
                    Generated when the tier was created and frozen — a later type or name change
                    never renumbers it.
                  </p>
                </div>
              ) : null}

              {/* Tier type */}
              <div className="flex flex-col gap-1">
                <div className="flex items-center gap-1">
                  <label
                    htmlFor="package-tier-type"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Tier type
                  </label>
                  <span className="text-error text-caption" aria-hidden="true">
                    *
                  </span>
                </div>
                <input
                  id="package-tier-type"
                  name="packageType"
                  type="text"
                  list="package-tier-type-options"
                  value={values.packageType}
                  onChange={(event) => handleChange('packageType', event.target.value)}
                  maxLength={50}
                  required
                  aria-required="true"
                  aria-invalid={fieldErrors.packageType ? true : undefined}
                  aria-describedby={
                    fieldErrors.packageType
                      ? 'package-tier-type-hint package-tier-type-error'
                      : 'package-tier-type-hint'
                  }
                  className={INPUT_CLASSES}
                />
                <datalist id="package-tier-type-options">
                  {KNOWN_PACKAGE_TYPES.map((knownType) => (
                    <option key={knownType} value={knownType} />
                  ))}
                </datalist>
                <p id="package-tier-type-hint" className="font-caption text-caption text-espresso-600">
                  Free text. The known tier names are offered as suggestions only — the stored
                  value is never rewritten.
                </p>
                {fieldErrors.packageType ? (
                  <p
                    id="package-tier-type-error"
                    role="alert"
                    className="font-caption text-caption text-error"
                  >
                    {fieldErrors.packageType}
                  </p>
                ) : null}
              </div>

              {/* Tier name */}
              <div className="flex flex-col gap-1">
                <div className="flex items-center gap-1">
                  <label
                    htmlFor="package-tier-name"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Tier name
                  </label>
                  <span className="text-error text-caption" aria-hidden="true">
                    *
                  </span>
                </div>
                <input
                  id="package-tier-name"
                  name="description"
                  type="text"
                  value={values.description}
                  onChange={(event) => handleChange('description', event.target.value)}
                  maxLength={250}
                  required
                  aria-required="true"
                  aria-invalid={fieldErrors.description ? true : undefined}
                  aria-describedby={
                    fieldErrors.description
                      ? 'package-tier-name-hint package-tier-name-error'
                      : 'package-tier-name-hint'
                  }
                  className={INPUT_CLASSES}
                />
                <p id="package-tier-name-hint" className="font-caption text-caption text-espresso-600">
                  The display name the coordinator gives the tier.
                </p>
                {fieldErrors.description ? (
                  <p
                    id="package-tier-name-error"
                    role="alert"
                    className="font-caption text-caption text-error"
                  >
                    {fieldErrors.description}
                  </p>
                ) : null}
              </div>

              {/* Rate per guest */}
              <div className="flex flex-col gap-1">
                <label
                  htmlFor="package-amount-pax"
                  className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                >
                  Rate per guest
                </label>
                <CurrencyInput
                  id="package-amount-pax"
                  name="amountPax"
                  value={values.amountPax}
                  onValueChange={(value) => handleChange('amountPax', value)}
                  aria-invalid={fieldErrors.amountPax ? true : undefined}
                  aria-describedby={
                    fieldErrors.amountPax
                      ? 'package-amount-pax-hint package-amount-pax-error'
                      : 'package-amount-pax-hint'
                  }
                  className={INPUT_CLASSES}
                />
                <p
                  id="package-amount-pax-hint"
                  className="font-caption text-caption text-espresso-600"
                >
                  Leave empty when the tier is not priced yet. Two decimals; the field
                  formats the amount as you type.
                </p>
                {fieldErrors.amountPax ? (
                  <p
                    id="package-amount-pax-error"
                    role="alert"
                    className="font-caption text-caption text-error"
                  >
                    {fieldErrors.amountPax}
                  </p>
                ) : null}
              </div>

              {/* Minimum pax */}
              <div className="flex flex-col gap-1">
                <label
                  htmlFor="package-min-pax"
                  className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                >
                  Minimum pax
                </label>
                <input
                  id="package-min-pax"
                  name="minPax"
                  type="number"
                  inputMode="numeric"
                  step="1"
                  min="1"
                  value={values.minPax}
                  onChange={(event) => handleChange('minPax', event.target.value)}
                  aria-invalid={fieldErrors.minPax ? true : undefined}
                  aria-describedby={
                    fieldErrors.minPax
                      ? 'package-min-pax-hint package-min-pax-error'
                      : 'package-min-pax-hint'
                  }
                  className={INPUT_CLASSES}
                />
                <p id="package-min-pax-hint" className="font-caption text-caption text-espresso-600">
                  Leave empty when the tier has no minimum headcount.
                </p>
                {fieldErrors.minPax ? (
                  <p
                    id="package-min-pax-error"
                    role="alert"
                    className="font-caption text-caption text-error"
                  >
                    {fieldErrors.minPax}
                  </p>
                ) : null}
              </div>

              {/* Extended location amount — Regular/Premier only (owner request 2026-10-24) */}
              {isExtendedLocationEligible(values.packageType) ? (
                <div className="flex flex-col gap-1">
                  <label
                    htmlFor="package-extended-location-amount"
                    className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
                  >
                    Extended location amount (per guest)
                  </label>
                  <CurrencyInput
                    id="package-extended-location-amount"
                    name="extendedLocationAmount"
                    value={values.extendedLocationAmount}
                    onValueChange={(value) => handleChange('extendedLocationAmount', value)}
                    aria-invalid={fieldErrors.extendedLocationAmount ? true : undefined}
                    aria-describedby={
                      fieldErrors.extendedLocationAmount
                        ? 'package-extended-location-hint package-extended-location-error'
                        : 'package-extended-location-hint'
                    }
                    className={INPUT_CLASSES}
                  />
                  <p
                    id="package-extended-location-hint"
                    className="font-caption text-caption text-espresso-600"
                  >
                    The tier&apos;s own per-guest out-of-town charge, added to the municipality&apos;s
                    rate. Leave empty when the tier carries none.
                  </p>
                  {fieldErrors.extendedLocationAmount ? (
                    <p
                      id="package-extended-location-error"
                      role="alert"
                      className="font-caption text-caption text-error"
                    >
                      {fieldErrors.extendedLocationAmount}
                    </p>
                  ) : null}
                </div>
              ) : null}
            </div>
          </Card>
        </div>

        <div className="lg:col-span-5 xl:col-span-4 flex flex-col gap-space-md min-w-0">
          {/*
            The reference's course-allocation matrix and venue-coverage list
            are real now: REQ-PKG-02 publishes both, so they are workbench
            actions rather than capability gaps (DEC-084). They are reachable
            once the tier is saved.
          */}
          <div className="flex flex-col gap-2 rounded-xl border border-artisan-border p-4">
            <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Configuration workbenches
            </span>
            <div className="flex flex-col gap-2">
              <Button
                variant="secondary"
                icon={UtensilsCrossed}
                onClick={() => handleOpenWorkbench('rules')}
                inactive={!isSavedTier}
              >
                Course Rules
              </Button>
              <Button
                variant="secondary"
                icon={MapPinned}
                onClick={() => handleOpenWorkbench('locations')}
                inactive={!isSavedTier}
              >
                Location Coverage
              </Button>
            </div>
            <p className="font-caption text-caption text-espresso-600">
              {isSavedTier
                ? 'The course rules and the location coverage of this tier.'
                : 'Save the tier first to open its course rules and its location coverage.'}
            </p>
          </div>

          <CapabilityGapNotice
            title="Stored per-tier food cost unavailable"
            missing="A tier stores no food cost and no committed margin, so a per-tier food cost cannot be published. The workbench derives it from a dish selection or from the standard-run estimate."
            requirement="REQ-PKG-01"
          />
          <CapabilityGapNotice
            title="Event inclusions unavailable"
            missing="No inclusion, rental or waitstaff-ratio record is stored against a tier."
            requirement="REQ-PKG-01"
          />
        </div>
      </div>

      <Card
        elevation={1}
        className="flex flex-col sm:flex-row sm:items-center justify-between gap-space-md"
      >
        <p className="font-body-sm text-body-sm text-espresso-600">
          The rate, the headcount and the extended location amount are the tier&apos;s
          commercial terms. Every other commercial figure the reference shows has no approved
          source and is not stored.
        </p>
        <div className="flex items-center gap-2 shrink-0">
          <Button variant="secondary" onClick={handleCancel}>
            Cancel
          </Button>
          <Button
            variant="primary"
            onClick={handleSave}
            disabled={isSaving}
          >
            {isSaving ? 'Saving…' : isEdit ? 'Save Changes' : 'Save Package'}
          </Button>
        </div>
      </Card>
    </div>
  )
}
