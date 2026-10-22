import { Skeleton } from '@/components/ui/Skeleton'

/**
 * Loading placeholders that mirror the card layout (spec §9.3 "Loading").
 * There is no spinner over stale data and no layout shift when the payload
 * arrives.
 */
export function DashboardSkeleton() {
  return (
    <div className="flex flex-col gap-space-xl" aria-busy="true">
      <span role="status" className="sr-only">
        Loading the operations dashboard…
      </span>

      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-space-md">
        <div className="flex flex-col gap-2">
          <Skeleton className="h-3 w-64 max-w-full" />
          <Skeleton className="h-8 w-96 max-w-full" />
          <Skeleton className="h-4 w-[30rem] max-w-full" />
        </div>
        <Skeleton className="h-12 w-80 max-w-full rounded-xl" />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-gutter">
        {Array.from({ length: 4 }).map((_, index) => (
          <Skeleton key={index} className="h-48 rounded-xl" />
        ))}
      </div>

      <Skeleton className="h-72 rounded-2xl" />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-gutter">
        {Array.from({ length: 3 }).map((_, index) => (
          <Skeleton key={index} className="h-96 rounded-xl" />
        ))}
      </div>
    </div>
  )
}
