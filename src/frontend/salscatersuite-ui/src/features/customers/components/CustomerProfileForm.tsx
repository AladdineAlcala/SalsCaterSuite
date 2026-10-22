import { CalendarCheck, Building2, UserRound } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { EM_DASH, formatCount, formatCurrency } from '@/utils/currencyFormatter'
import { formatEventDate } from '@/utils/dateFormatter'
import type { CustomerDetail, CustomerType, CustomerUpsertRequest } from '@/types/customer.types'

/** The seven editable attributes of a customer (spec §4.8). */
export interface CustomerFormValues {
  lastName: string
  firstName: string
  middle: string
  company: string
  contact1: string
  contact2: string
  address: string
}

export const EMPTY_CUSTOMER_FORM_VALUES: CustomerFormValues = {
  lastName: '',
  firstName: '',
  middle: '',
  company: '',
  contact1: '',
  contact2: '',
  address: '',
}

/** Seeds the form from a customer the API published. */
export function formValuesFromCustomer(customer: CustomerDetail): CustomerFormValues {
  return {
    lastName: customer.lastName ?? '',
    firstName: customer.firstName ?? '',
    middle: customer.middle ?? '',
    company: customer.company ?? '',
    contact1: customer.contact1 ?? '',
    contact2: customer.contact2 ?? '',
    address: customer.address ?? '',
  }
}

/**
 * The derived classification (DEC-051): `Corporate` when the company
 * attribute is non-blank, `Private` otherwise. It is a presentation of an
 * existing attribute — never persisted, never an editable control.
 */
export function deriveCustomerType(company: string): CustomerType {
  return company.trim().length > 0 ? 'Corporate' : 'Private'
}

/** Maps the form onto the API request body; an empty field is sent as null. */
export function toUpsertRequest(values: CustomerFormValues): CustomerUpsertRequest {
  const optional = (value: string): string | null =>
    value.trim().length > 0 ? value : null

  return {
    lastName: values.lastName,
    firstName: values.firstName,
    middle: optional(values.middle),
    address: optional(values.address),
    contact1: optional(values.contact1),
    contact2: optional(values.contact2),
    company: optional(values.company),
  }
}

export interface CustomerProfileFormProps {
  mode: 'view' | 'create'
  values: CustomerFormValues
  onChange: (field: keyof CustomerFormValues, value: string) => void
  /** The customer the API published — `null` while creating. */
  customer: CustomerDetail | null
}

interface FieldProps {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  required?: boolean
  type?: 'text' | 'tel'
  placeholder?: string
}

const INPUT_CLASSES =
  'h-9 px-3 rounded-lg bg-artisan-surface text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm w-full'

/** A labelled input — the label is always tied to the control. */
function Field({ id, label, value, onChange, required, type = 'text', placeholder }: FieldProps) {
  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-1">
        <label
          htmlFor={id}
          className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
        >
          {label}
        </label>
        {required ? (
          <span className="text-error text-caption" aria-hidden="true">
            *
          </span>
        ) : null}
      </div>
      <input
        id={id}
        name={id}
        type={type}
        value={value}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
        aria-required={required ? true : undefined}
        className={INPUT_CLASSES}
      />
    </div>
  )
}

/**
 * The Profile & Contact tab (spec §4.8).
 *
 * Only the seven attributes that exist on `Customer` are editable. The
 * account classification is a read-only derived chip and the upcoming booking
 * is a read-only ribbon — neither is a stored attribute of the form.
 */
export function CustomerProfileForm({
  mode,
  values,
  onChange,
  customer,
}: CustomerProfileFormProps) {
  const classification =
    mode === 'create'
      ? deriveCustomerType(values.company)
      : (customer?.customerType ?? deriveCustomerType(values.company))

  const upcomingBooking = customer?.upcomingBooking ?? null

  const upcomingText = upcomingBooking
    ? [
        upcomingBooking.bookingNumber,
        upcomingBooking.occasion.length > 0 ? upcomingBooking.occasion : null,
        formatEventDate(upcomingBooking.eventDate),
      ]
        .filter((part): part is string => part !== null && part.length > 0)
        .join(' • ')
    : ''

  return (
    <div className="flex flex-col gap-space-md">
      <div className="flex flex-col gap-1.5">
        <span className="font-caption text-caption text-espresso-600 uppercase font-bold tracking-wider">
          Account Classification
        </span>
        <div>
          <Badge tone={classification === 'Corporate' ? 'info' : 'neutral'} shape="pill" icon={Building2}>
            {classification}
          </Badge>
        </div>
      </div>

      {/*
        Read-only record facts. The registration date and the booking
        population are published by the API but are not editable attributes
        (spec §4.8). A customer record may carry neither, so both render an em
        dash — never a zero and never a guess.
      */}
      {customer ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
          <div className="flex flex-col gap-0.5">
            <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Registered
            </span>
            <span className="font-data-sm text-data-sm text-espresso-900">
              {formatEventDate(customer.dateRegistered)}
            </span>
          </div>
          <div className="flex flex-col gap-0.5">
            <span className="font-caption text-caption text-espresso-400 uppercase font-semibold">
              Bookings
            </span>
            <span className="font-data-sm text-data-sm text-espresso-900">
              {customer.bookingCounts
                ? `${formatCount(customer.bookingCounts.past)} past • ${formatCount(
                    customer.bookingCounts.upcoming,
                  )} upcoming`
                : EM_DASH}
            </span>
          </div>
        </div>
      ) : null}

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
        <Field
          id="customer-last-name"
          label="Last name"
          required
          value={values.lastName}
          onChange={(value) => onChange('lastName', value)}
        />
        <Field
          id="customer-first-name"
          label="First name"
          required
          value={values.firstName}
          onChange={(value) => onChange('firstName', value)}
        />
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
        <Field
          id="customer-middle-name"
          label="Middle name"
          value={values.middle}
          onChange={(value) => onChange('middle', value)}
        />
        <Field
          id="customer-company"
          label="Company"
          value={values.company}
          onChange={(value) => onChange('company', value)}
        />
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
        <Field
          id="customer-contact-1"
          label="Mobile / Primary Contact"
          type="tel"
          value={values.contact1}
          onChange={(value) => onChange('contact1', value)}
        />
        <Field
          id="customer-contact-2"
          label="Secondary Contact"
          type="tel"
          value={values.contact2}
          onChange={(value) => onChange('contact2', value)}
        />
      </div>

      <div className="flex flex-col gap-1">
        <label
          htmlFor="customer-address"
          className="font-caption text-caption text-espresso-600 font-semibold uppercase tracking-wider"
        >
          Address
        </label>
        <textarea
          id="customer-address"
          name="customer-address"
          rows={2}
          value={values.address}
          onChange={(event) => onChange('address', event.target.value)}
          className="p-2.5 rounded-lg bg-artisan-surface text-espresso-900 border border-artisan-border focus:border-culinary-600 focus:outline-none focus:ring-2 focus:ring-culinary-500/15 font-body-sm text-body-sm w-full"
        />
      </div>

      {upcomingBooking ? (
        <div className="p-2.5 rounded-lg bg-culinary-50 border border-culinary-200 flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2">
            <CalendarCheck className="h-4 w-4 text-culinary-700" aria-hidden="true" />
            <div className="flex flex-col">
              <span className="font-caption text-caption text-culinary-800 font-bold">
                Upcoming Booking
              </span>
              <span className="font-data-sm text-data-sm text-culinary-700">
                {upcomingText}
              </span>
            </div>
          </div>
          <span className="font-data-sm text-data-sm font-bold text-espresso-900">
            {formatCurrency(upcomingBooking.amountDue)}
          </span>
        </div>
      ) : null}

      <p className="font-caption text-caption text-espresso-400 flex items-center gap-1.5">
        <UserRound className="h-3.5 w-3.5" aria-hidden="true" />
        Changes are validated by the API and are never stored in the browser.
      </p>
    </div>
  )
}
