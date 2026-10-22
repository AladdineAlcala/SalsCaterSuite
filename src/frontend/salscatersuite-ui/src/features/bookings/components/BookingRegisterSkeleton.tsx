import { Skeleton } from '@/components/ui/Skeleton'

/**
 * The register's loading skeleton (spec §5.2 "States").
 *
 * A skeleton grid — the shape of the page that is coming — rather than a
 * spinner over an empty screen.
 */
export function BookingRegisterSkeleton() {
  return (
    <div
      className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-gutter"
      aria-hidden="true"
      data-testid="booking-register-skeleton"
    >
      {Array.from({ length: 6 }, (_unused, index) => (
        <div
          key={index}
          className="bg-artisan-surface rounded-xl shadow-elevation-1 border border-artisan-border p-space-md flex flex-col gap-space-md"
        >
          <div className="flex items-center justify-between gap-2">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-5 w-24" />
          </div>
          <Skeleton className="h-5 w-3/5" />
          <Skeleton className="h-3 w-2/5" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-12 w-full" />
        </div>
      ))}
    </div>
  )
}
