import { formatCount } from '@/utils/currencyFormatter'
import { cn } from '@/utils/cn'
import type { BookingMenuCourse } from '@/types/booking.types'

export interface BookingMenuCourseTabsProps {
  /** The tier's own course rules — the only source of the tabs (DEC-147). */
  courses: BookingMenuCourse[]
  /** The active course, or `null` for *All*. */
  activeCourseId: number | null
  onChange: (courseId: number | null) => void
}

/**
 * The course tabs (spec §5.3, AC-BKG-80).
 *
 * One tab per course rule, plus **All**. The tabs are **derived from the
 * tier's own rules** — the reference's hardcoded groupings ("Proteins",
 * "Starters & Carbs", "Dessert & Drinks") are not a taxonomy the model
 * carries, so they are never rendered (DEC-147).
 */
export function BookingMenuCourseTabs({
  courses,
  activeCourseId,
  onChange,
}: BookingMenuCourseTabsProps) {
  const tabs: Array<{ key: number | null; label: string; count: number | null }> = [
    { key: null, label: 'All courses', count: null },
    ...courses.map((course) => ({
      key: course.courseId,
      label: course.courseName,
      count: course.requiredQuota,
    })),
  ]

  return (
    <div
      role="tablist"
      aria-label="Course rules of the selected tier"
      className="flex flex-wrap items-center gap-2"
      data-testid="booking-menu-course-tabs"
    >
      {tabs.map((tab) => {
        const isActive = tab.key === activeCourseId
        return (
          <button
            key={tab.key === null ? 'all' : tab.key}
            type="button"
            role="tab"
            aria-selected={isActive}
            onClick={() => onChange(tab.key)}
            className={cn(
              'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full border font-caption text-caption font-bold transition-colors focus:outline-none focus:ring-2 focus:ring-culinary-500/20',
              isActive
                ? 'bg-culinary-600 text-white border-culinary-600'
                : 'bg-artisan-surface text-espresso-700 border-artisan-border hover:bg-surface-container-low',
            )}
          >
            <span>{tab.label}</span>
            {tab.count === null ? null : (
              <span aria-hidden="true" className="font-mono">
                {formatCount(tab.count)}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
