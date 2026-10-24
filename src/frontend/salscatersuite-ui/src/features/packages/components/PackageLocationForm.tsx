import { useEffect, useState } from 'react'
import { Save, X } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { CurrencyInput } from '@/components/ui/CurrencyInput'
import type { AreaDto, AreaRequest } from '@/types/package.types'

/** The editable attributes of a municipality (contract §13.10). */
export interface AreaFormValues {
  /** `Area.AreaDetails` — the municipality or zone name. */
  areaDetails: string
  /** The per-pax out-of-town surcharge. */
  additionalRate: string
  /** The distance from the commissary, in kilometres. */
  distanceKm: string
  /** The dispatch mode. */
  logisticsNote: string
  /** The per-event logistics cost. */
  logisticsCost: string
}

export const EMPTY_AREA_FORM_VALUES: AreaFormValues = {
  areaDetails: '',
  additionalRate: '',
  distanceKm: '',
  logisticsNote: '',
  logisticsCost: '',
}

/** The per-field client validation messages of the municipality editor. */
export interface AreaFormErrors {
  areaDetails?: string
  additionalRate?: string
  distanceKm?: string
  logisticsNote?: string
  logisticsCost?: string
}

/** The column limits the server enforces. */
export const AREA_NAME_MAX_LENGTH = 150
export const LOGISTICS_NOTE_MAX_LENGTH = 150

/**
 * Reads a typed number.
 *
 * The browser only parses what the operator typed.
 */
export function parseAreaNumber(value: string): number | null {
  const trimmed = value.trim()
  if (trimmed.length === 0) return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

/** Seeds the editor from a municipality the server published. */
export function areaFormValuesFromArea(area: AreaDto): AreaFormValues {
  return {
    areaDetails: area.areaDetails,
    additionalRate: area.additionalRate === null ? '' : String(area.additionalRate),
    distanceKm: area.distanceKm === null ? '' : String(area.distanceKm),
    logisticsNote: area.logisticsNote ?? '',
    logisticsCost: area.logisticsCost === null ? '' : String(area.logisticsCost),
  }
}

/**
 * The presence and sign checks the municipality editor makes before it calls
 * the API.
 *
 * The wording is the **server's own**, so the operator reads one message
 * whether the request is rejected in the browser or by the API.
 */
export function validateAreaForm(values: AreaFormValues): AreaFormErrors {
  const errors: AreaFormErrors = {}

  if (values.areaDetails.trim().length === 0) {
    errors.areaDetails = 'Area name is required.'
  } else if (values.areaDetails.trim().length > AREA_NAME_MAX_LENGTH) {
    errors.areaDetails = 'Area name must not exceed 150 characters.'
  }

  if (values.additionalRate.trim().length > 0) {
    const rate = parseAreaNumber(values.additionalRate)
    if (rate === null) {
      errors.additionalRate = 'Additional rate must be a number.'
    } else if (rate < 0) {
      errors.additionalRate = 'Additional rate must be zero or greater.'
    }
  }

  if (values.distanceKm.trim().length > 0) {
    const distance = parseAreaNumber(values.distanceKm)
    if (distance === null) {
      errors.distanceKm = 'Distance must be a number.'
    } else if (distance < 0) {
      errors.distanceKm = 'Distance must be zero or greater.'
    }
  }

  if (values.logisticsNote.trim().length > LOGISTICS_NOTE_MAX_LENGTH) {
    errors.logisticsNote = 'Logistics note must not exceed 150 characters.'
  }

  if (values.logisticsCost.trim().length > 0) {
    const cost = parseAreaNumber(values.logisticsCost)
    if (cost === null) {
      errors.logisticsCost = 'Logistics cost must be a number.'
    } else if (cost < 0) {
      errors.logisticsCost = 'Logistics cost must be zero or greater.'
    }
  }

  return errors
}

/**
 * Maps the editor onto the API request body (contract §13.10).
 *
 * An empty numeric field is submitted as `null` — "not stated" — and never as
 * a zero.
 */
export function toAreaRequest(values: AreaFormValues): AreaRequest | null {
  const rate = parseAreaNumber(values.additionalRate)
  const distance = parseAreaNumber(values.distanceKm)
  const cost = parseAreaNumber(values.logisticsCost)

  if (values.additionalRate.trim().length > 0 && rate === null) return null
  if (values.distanceKm.trim().length > 0 && distance === null) return null
  if (values.logisticsCost.trim().length > 0 && cost === null) return null

  return {
    areaDetails: values.areaDetails.trim(),
    additionalRate: rate,
    distanceKm: distance === null ? null : Math.trunc(distance),
    logisticsNote: values.logisticsNote.trim().length === 0 ? null : values.logisticsNote.trim(),
    logisticsCost: cost,
  }
}

export interface PackageLocationFormProps {
  /** The municipality being edited, or `null` when a new one is being added. */
  area: AreaDto | null
  /** Sends the municipality to the API. */
  onSubmit: (request: AreaRequest) => void
  /** Closes the editor without a write. */
  onCancel: () => void
  /** `true` while the write is in flight. */
  isSaving: boolean
  /** The server's rejection of the write, rendered verbatim (BR-11). */
  serverErrors: string[]
}

const INPUT_CLASSES =
  'h-10 px-3.5 rounded-lg bg-surface-container-low text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-data-md text-data-md w-full'

const LABEL_CLASSES =
  'font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider'

/**
 * The municipality editor (spec §5.3).
 *
 * A municipality is a **catalogue record**, not a tier's property: its
 * out-of-town rate is shared by every tier that covers it, and a coverage row
 * cannot override it (DEC-078). The editor says so, and it is accurate about
 * the consequence: editing the rate changes **no booking** today — the
 * extended-area fee is not charged by the as-built code — it changes only the
 * workbench's own quotes.
 */
export function PackageLocationForm({
  area,
  onSubmit,
  onCancel,
  isSaving,
  serverErrors,
}: PackageLocationFormProps) {
  const [values, setValues] = useState<AreaFormValues>(EMPTY_AREA_FORM_VALUES)
  const [fieldErrors, setFieldErrors] = useState<AreaFormErrors>({})

  useEffect(() => {
    setValues(area === null ? EMPTY_AREA_FORM_VALUES : areaFormValuesFromArea(area))
    setFieldErrors({})
  }, [area])

  const handleChange = (field: keyof AreaFormValues, value: string) => {
    setValues((current) => ({ ...current, [field]: value }))
    setFieldErrors((current) => {
      if (current[field] === undefined) return current
      const next = { ...current }
      delete next[field]
      return next
    })
  }

  const handleSubmit = () => {
    const problems = validateAreaForm(values)
    if (Object.keys(problems).length > 0) {
      setFieldErrors(problems)
      return
    }

    const request = toAreaRequest(values)
    if (request === null) {
      setFieldErrors({
        additionalRate:
          values.additionalRate.trim().length > 0
            ? 'Additional rate must be a number.'
            : undefined,
        distanceKm:
          values.distanceKm.trim().length > 0 ? 'Distance must be a number.' : undefined,
        logisticsCost:
          values.logisticsCost.trim().length > 0 ? 'Logistics cost must be a number.' : undefined,
      })
      return
    }

    setFieldErrors({})
    onSubmit(request)
  }

  return (
    <Card elevation={1} className="flex flex-col gap-space-md" data-testid="package-location-form">
      <h3 className="font-headline-sm text-headline-sm text-espresso-900">
        {area === null ? 'Add a municipality' : `Edit ${area.areaDetails}`}
      </h3>

      {serverErrors.length > 0 ? (
        <div role="alert" className="flex flex-col gap-1">
          {serverErrors.map((message) => (
            <p key={message} className="font-body-sm text-body-sm text-error break-words">
              {message}
            </p>
          ))}
        </div>
      ) : null}

      <div className="flex flex-col gap-1">
        <label htmlFor="area-details" className={LABEL_CLASSES}>
          Municipality name
        </label>
        <input
          id="area-details"
          name="areaDetails"
          type="text"
          value={values.areaDetails}
          onChange={(event) => handleChange('areaDetails', event.target.value)}
          maxLength={AREA_NAME_MAX_LENGTH}
          required
          aria-required="true"
          aria-invalid={fieldErrors.areaDetails ? true : undefined}
          aria-describedby={
            fieldErrors.areaDetails ? 'area-details-error' : 'area-details-hint'
          }
          className={INPUT_CLASSES}
        />
        <p id="area-details-hint" className="font-caption text-caption text-espresso-600">
          The town or zone the operation can serve.
        </p>
        {fieldErrors.areaDetails ? (
          <p id="area-details-error" role="alert" className="font-caption text-caption text-error">
            {fieldErrors.areaDetails}
          </p>
        ) : null}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="flex flex-col gap-1">
          <label htmlFor="area-additional-rate" className={LABEL_CLASSES}>
            Out-of-town rate per pax
          </label>
          <CurrencyInput
            id="area-additional-rate"
            name="additionalRate"
            value={values.additionalRate}
            onValueChange={(value) => handleChange('additionalRate', value)}
            aria-invalid={fieldErrors.additionalRate ? true : undefined}
            aria-describedby={
              fieldErrors.additionalRate ? 'area-additional-rate-error' : 'area-additional-rate-hint'
            }
            className={INPUT_CLASSES}
          />
          <p id="area-additional-rate-hint" className="font-caption text-caption text-espresso-600">
            Leave empty when the municipality has stated no rate.
          </p>
          {fieldErrors.additionalRate ? (
            <p
              id="area-additional-rate-error"
              role="alert"
              className="font-caption text-caption text-error"
            >
              {fieldErrors.additionalRate}
            </p>
          ) : null}
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="area-distance" className={LABEL_CLASSES}>
            Distance (km)
          </label>
          <input
            id="area-distance"
            name="distanceKm"
            type="number"
            inputMode="numeric"
            step="1"
            min="0"
            value={values.distanceKm}
            onChange={(event) => handleChange('distanceKm', event.target.value)}
            aria-invalid={fieldErrors.distanceKm ? true : undefined}
            aria-describedby={
              fieldErrors.distanceKm ? 'area-distance-error' : 'area-distance-hint'
            }
            className={INPUT_CLASSES}
          />
          <p id="area-distance-hint" className="font-caption text-caption text-espresso-600">
            The distance from the commissary. Leave empty when it is not stated.
          </p>
          {fieldErrors.distanceKm ? (
            <p id="area-distance-error" role="alert" className="font-caption text-caption text-error">
              {fieldErrors.distanceKm}
            </p>
          ) : null}
        </div>
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="area-logistics-note" className={LABEL_CLASSES}>
          Dispatch note
        </label>
        <input
          id="area-logistics-note"
          name="logisticsNote"
          type="text"
          value={values.logisticsNote}
          onChange={(event) => handleChange('logisticsNote', event.target.value)}
          maxLength={LOGISTICS_NOTE_MAX_LENGTH}
          aria-invalid={fieldErrors.logisticsNote ? true : undefined}
          aria-describedby={
            fieldErrors.logisticsNote ? 'area-logistics-note-error' : 'area-logistics-note-hint'
          }
          className={INPUT_CLASSES}
        />
        <p id="area-logistics-note-hint" className="font-caption text-caption text-espresso-600">
          The dispatch mode. Leave empty when it is not stated.
        </p>
        {fieldErrors.logisticsNote ? (
          <p
            id="area-logistics-note-error"
            role="alert"
            className="font-caption text-caption text-error"
          >
            {fieldErrors.logisticsNote}
          </p>
        ) : null}
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="area-logistics-cost" className={LABEL_CLASSES}>
          Logistics cost per event
        </label>
        <CurrencyInput
          id="area-logistics-cost"
          name="logisticsCost"
          value={values.logisticsCost}
          onValueChange={(value) => handleChange('logisticsCost', value)}
          aria-invalid={fieldErrors.logisticsCost ? true : undefined}
          aria-describedby={
            fieldErrors.logisticsCost ? 'area-logistics-cost-error' : 'area-logistics-cost-hint'
          }
          className={INPUT_CLASSES}
        />
        <p id="area-logistics-cost-hint" className="font-caption text-caption text-espresso-600">
          A flat cost per event, not per guest. Leave empty when the municipality states
          none — the gross-profit band is then omitted rather than shown as zero.
        </p>
        {fieldErrors.logisticsCost ? (
          <p
            id="area-logistics-cost-error"
            role="alert"
            className="font-caption text-caption text-error"
          >
            {fieldErrors.logisticsCost}
          </p>
        ) : null}
      </div>

      <p className="font-caption text-caption text-espresso-600">
        The rate belongs to the <strong>municipality</strong> and is shared by every tier
        that covers it — a tier cannot negotiate its own rate there. Editing it changes
        <strong> no booking</strong> today: the extended-area fee is not charged by the
        as-built code, so the rate moves only this workbench&apos;s own quotes.
      </p>

      <div className="flex items-center gap-2 flex-wrap">
        <Button variant="primary" icon={Save} onClick={handleSubmit} disabled={isSaving}>
          {isSaving ? 'Saving…' : area === null ? 'Add municipality' : 'Save municipality'}
        </Button>
        <Button variant="secondary" icon={X} onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </Card>
  )
}
