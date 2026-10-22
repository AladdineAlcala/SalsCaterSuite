import { Skeleton } from '@/components/ui/Skeleton'

/**
 * The loading state of the Packages & Buffets Catalogue (spec §5.2 item 6).
 *
 * It mirrors the real layout so the page does not jump when the data lands.
 */
export function PackageCatalogueSkeleton() {
  return (
    <div
      className="flex flex-col gap-space-xl"
      role="status"
      aria-label="Loading the packages catalogue"
    >
      <Skeleton className="h-32 w-full" />

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-gutter">
        <Skeleton className="h-36 w-full" />
        <Skeleton className="h-36 w-full" />
        <Skeleton className="h-36 w-full" />
        <Skeleton className="h-36 w-full" />
      </div>

      <Skeleton className="h-24 w-full" />

      <div className="grid grid-cols-1 xl:grid-cols-12 gap-gutter items-start">
        <div className="xl:col-span-7 flex flex-col gap-space-md">
          <Skeleton className="h-96 w-full" />
        </div>
        <div className="xl:col-span-5 flex flex-col">
          <Skeleton className="h-96 w-full" />
        </div>
      </div>
    </div>
  )
}
