import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient, ApiError } from '@/api/apiClient'
import { API_ENDPOINTS } from '@/api/endpoints'
import { AlertBanner } from '@/components/feedback/AlertBanner'
import { Button } from '@/components/ui/Button'
import type { BookingDetails } from '@/types/booking.types'

function readErrorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? error.errors : [error.message]
  }
  return ['The request could not be completed.']
}

export function BookingEditPage() {
  const { transactionId } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const bookingId = Number(transactionId)
  const isValidBookingId = Number.isFinite(bookingId) && bookingId > 0

  const bookingQuery = useQuery({
    queryKey: ['booking-detail', bookingId],
    enabled: isValidBookingId,
    queryFn: async (): Promise<BookingDetails> => {
      const response = await apiClient.get<BookingDetails>(API_ENDPOINTS.bookings.detail(bookingId))
      return response.data
    },
  })

  const [form, setForm] = useState({ eventDate: '', numberOfHeads: '' })
  const [submitErrors, setSubmitErrors] = useState<string[]>([])

  useEffect(() => {
    if (!bookingQuery.data) return
    setForm({
      eventDate: bookingQuery.data.eventDate,
      numberOfHeads: String(bookingQuery.data.numberOfHeads),
    })
  }, [bookingQuery.data])

  const updateBooking = useMutation({
    mutationFn: async (payload: { eventDate: string; numberOfHeads: number }) => {
      await apiClient.put(API_ENDPOINTS.bookings.update(bookingId), payload)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['booking-detail', bookingId] })
      await queryClient.invalidateQueries({ queryKey: ['booking-register'] })
      navigate('/bookings')
    },
    onError: (error) => {
      setSubmitErrors(readErrorMessages(error))
    },
  })

  if (!isValidBookingId) {
    return <div className="p-space-lg text-espresso-700">The selected booking could not be loaded.</div>
  }

  if (bookingQuery.isPending) {
    return <div className="p-space-lg text-espresso-700">Loading booking…</div>
  }

  if (bookingQuery.isError || !bookingQuery.data) {
    return (
      <div className="p-space-lg">
        <AlertBanner
          variant="error"
          title="Booking not found"
          errors={readErrorMessages(bookingQuery.error ?? 'The booking could not be loaded.')}
        />
      </div>
    )
  }

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSubmitErrors([])

    const nextNumberOfHeads = Number(form.numberOfHeads)
    if (!form.eventDate) {
      setSubmitErrors(['Event date is required.'])
      return
    }
    if (!Number.isFinite(nextNumberOfHeads) || nextNumberOfHeads <= 0) {
      setSubmitErrors(['Number of heads must be greater than zero.'])
      return
    }

    updateBooking.mutate({
      eventDate: form.eventDate,
      numberOfHeads: nextNumberOfHeads,
    })
  }

  return (
    <div className="max-w-2xl mx-auto p-space-lg">
      <div className="mb-space-lg">
        <p className="text-caption uppercase tracking-[0.18em] text-espresso-500">Bookings & Events</p>
        <h1 className="text-headline-md font-headline text-espresso-900">Edit booking</h1>
      </div>

      {submitErrors.length > 0 && (
        <AlertBanner variant="error" title="Unable to save booking" errors={submitErrors} />
      )}

      <form
        onSubmit={handleSubmit}
        className="rounded-2xl border border-artisan-border bg-artisan-surface p-space-lg shadow-elevation-1"
      >
        <div className="grid gap-space-md md:grid-cols-2">
          <label className="flex flex-col gap-2 text-body-sm font-medium text-espresso-800">
            Booking number
            <input
              className="rounded-lg border border-surface-container bg-white px-3 py-2 text-espresso-900"
              value={bookingQuery.data.bookingNumber}
              readOnly
            />
          </label>

          <label className="flex flex-col gap-2 text-body-sm font-medium text-espresso-800">
            Event date
            <input
              type="date"
              className="rounded-lg border border-surface-container bg-white px-3 py-2 text-espresso-900"
              value={form.eventDate}
              onChange={(event) => setForm((current) => ({ ...current, eventDate: event.target.value }))}
            />
          </label>

          <label className="flex flex-col gap-2 text-body-sm font-medium text-espresso-800 md:col-span-2">
            Number of heads
            <input
              type="number"
              min={1}
              step={1}
              className="rounded-lg border border-surface-container bg-white px-3 py-2 text-espresso-900"
              value={form.numberOfHeads}
              onChange={(event) => setForm((current) => ({ ...current, numberOfHeads: event.target.value }))}
            />
          </label>
        </div>

        <div className="mt-space-lg flex items-center justify-end gap-3">
          <Button type="button" variant="secondary" onClick={() => navigate('/bookings')}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" disabled={updateBooking.isPending}>
            {updateBooking.isPending ? 'Saving…' : 'Save changes'}
          </Button>
        </div>
      </form>
    </div>
  )
}
