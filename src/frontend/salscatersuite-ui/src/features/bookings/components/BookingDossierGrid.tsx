import { BookingDossierCard } from './BookingDossierCard'
import type { BookingRegisterItem } from '@/types/booking.types'

export interface BookingDossierGridProps {
  items: BookingRegisterItem[]
  /** The presentation of the register's page. */
  view: 'grid' | 'timeline'
  onDownloadContract: (transactionId: number) => void
  onOpenMenu: (transactionId: number) => void
  onOpenEdit?: (transactionId: number) => void
}

/**
 * The dossier grid (spec §5.2 item 4).
 *
 * The **grid** view lays the dossier cards out three across on a wide screen
 * and one across on a narrow one.
 *
 * The **timeline** view renders the **same server page** as a chronological
 * rail. It is not a second screen and it is not a local re-ordering — the
 * order is the server's own (spec §5.2).
 */
export function BookingDossierGrid({
  items,
  view,
  onDownloadContract,
  onOpenMenu,
  onOpenEdit,
}: BookingDossierGridProps) {
  const isTimeline = view === 'timeline'

  return (
    <div
      className={
        isTimeline
          ? 'flex flex-col gap-gutter'
          : 'grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-gutter'
      }
      data-testid="booking-dossier-grid"
      data-view={view}
    >
      {items.map((item) => (
        <BookingDossierCard
          key={item.transactionId}
          item={item}
          isTimeline={isTimeline}
          onDownloadContract={onDownloadContract}
          onOpenMenu={onOpenMenu}
          onOpenEdit={onOpenEdit}
        />
      ))}
    </div>
  )
}
