import { Building2, UserRound } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH } from '@/utils/currencyFormatter'
import type { CustomerDirectoryItem } from '@/types/customer.types'
import type { BookingWorkbenchDraft, BookingWorkbenchErrors } from '../bookingVocabulary'
import {
  BOOKING_SERVICE_TYPE_OPTIONS,
  BOOKING_TYPE_OPTIONS,
} from '../bookingVocabulary'
import {
  WorkbenchSelectField,
  WorkbenchTextField,
} from './WorkbenchFields'

export interface WorkbenchClientRailProps {
  draft: BookingWorkbenchDraft
  errors: BookingWorkbenchErrors
  onChange: (patch: Partial<BookingWorkbenchDraft>) => void
  /** The client search term. */
  clientSearch: string
  onClientSearchChange: (value: string) => void
  /** The client catalogue the picker reads. */
  clients: CustomerDirectoryItem[]
  isLoadingClients: boolean
  /** The client catalogue's own error, when it could not be read. */
  clientError: string[]
}

/**
 * Column 1 — the client & logistics rail (spec §5.3).
 *
 * The client picker reads the **existing** client master and the tier picker
 * reads the **existing** buffet catalogue; neither ever fabricates a record
 * (AC-BKG-28).
 *
 * The reference's **"Verified VIP"** badge is **not** rendered: there is no
 * VIP flag in the model. The badge is replaced by the **modelled customer
 * classification** — `Corporate` or `Private` (DEC-051).
 *
 * The reference's **"Official Email"** row is replaced by the customer's
 * **address**: the model carries no e-mail address on a customer.
 */
export function WorkbenchClientRail({
  draft,
  errors,
  onChange,
  clientSearch,
  onClientSearchChange,
  clients,
  isLoadingClients,
  clientError,
}: WorkbenchClientRailProps) {
  const selectedClient = clients.find((client) => String(client.customerId) === draft.customerId)

  return (
    <div className="flex flex-col gap-space-md min-w-0">
      <Card elevation={1} className="flex flex-col gap-space-md min-w-0">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Client &amp; event
        </h2>

        <WorkbenchTextField
          id="booking-client-search"
          label="Find a client"
          type="search"
          value={clientSearch}
          onChange={onClientSearchChange}
          placeholder="Search by name, company or contact…"
        />

        <WorkbenchSelectField
          id="booking-client"
          label="Client"
          value={draft.customerId}
          onChange={(value) => onChange({ customerId: value })}
          options={clients.map((client) => ({
            value: String(client.customerId),
            label: client.company ? `${client.fullName} · ${client.company}` : client.fullName,
          }))}
          placeholder={isLoadingClients ? 'Loading the client master…' : 'Select a client'}
          error={errors.customerId}
          disabled={isLoadingClients}
        />

        {clientError.length > 0 ? (
          <p role="alert" className="font-caption text-caption text-error">
            {clientError.join(' ')}
          </p>
        ) : null}

        {selectedClient ? (
          <div className="rounded-xl bg-surface-container-low border border-artisan-border p-space-sm flex flex-col gap-space-sm min-w-0">
            <div className="flex items-start gap-2 min-w-0">
              <span className="w-10 h-10 rounded-lg bg-culinary-50 text-culinary-700 flex items-center justify-center shrink-0">
                <UserRound className="h-5 w-5" aria-hidden="true" />
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-headline-sm text-headline-sm text-espresso-900 break-words">
                    {selectedClient.fullName}
                  </span>
                  <Badge tone="info" icon={Building2}>
                    {selectedClient.customerType}
                  </Badge>
                </div>
                {selectedClient.company ? (
                  <p className="font-body-sm text-body-sm text-espresso-600 break-words">
                    {selectedClient.company}
                  </p>
                ) : null}
              </div>
            </div>

            <dl className="flex flex-col gap-1.5 text-caption">
              <div className="flex items-center justify-between gap-2">
                <dt className="text-espresso-400">Contact</dt>
                <dd className="font-mono text-espresso-900 text-right break-all">
                  {selectedClient.contact1 ?? EM_DASH}
                </dd>
              </div>
              <div className="flex items-center justify-between gap-2">
                <dt className="text-espresso-400">Address</dt>
                <dd className="text-espresso-800 text-right break-words">
                  {selectedClient.address ?? EM_DASH}
                </dd>
              </div>
            </dl>
          </div>
        ) : (
          <EmptyState
            icon={UserRound}
            title="No client selected"
            description="Pick a client from the client master to see their profile. Nothing is created here."
          />
        )}

        <div className="flex flex-col gap-space-sm pt-space-sm border-t border-surface-container">
          <WorkbenchTextField
            id="booking-occasion"
            label="Occasion"
            value={draft.occasion}
            onChange={(value) => onChange({ occasion: value })}
            error={errors.occasion}
            placeholder="Silver Wedding Anniversary"
          />

          <WorkbenchTextField
            id="booking-venue"
            label="Venue"
            value={draft.venue}
            onChange={(value) => onChange({ venue: value })}
            error={errors.venue}
            placeholder="Grand Ballroom, Hotel Sal"
          />

          <WorkbenchTextField
            id="booking-event-date"
            label="Event date"
            type="date"
            value={draft.eventDate}
            onChange={(value) => onChange({ eventDate: value })}
            error={errors.eventDate}
          />

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-space-sm">
            <WorkbenchSelectField
              id="booking-book-type"
              label="Booking type"
              value={draft.bookType}
              onChange={(value) =>
                onChange({ bookType: value as BookingWorkbenchDraft['bookType'] })
              }
              options={BOOKING_TYPE_OPTIONS.map((option) => ({
                value: option.value,
                label: option.label,
              }))}
              placeholder="Inside"
            />

            <WorkbenchSelectField
              id="booking-service-type"
              label="Service"
              value={draft.typeOfService}
              onChange={(value) =>
                onChange({ typeOfService: value as BookingWorkbenchDraft['typeOfService'] })
              }
              options={BOOKING_SERVICE_TYPE_OPTIONS.map((option) => ({
                value: option.value,
                label: option.label,
              }))}
              placeholder="Catering"
            />
          </div>
        </div>
      </Card>
    </div>
  )
}
