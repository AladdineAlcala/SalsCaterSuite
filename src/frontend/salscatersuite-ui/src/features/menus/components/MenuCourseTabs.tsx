import { cn } from '@/utils/cn'
import { formatCount } from '@/utils/currencyFormatter'
import type { MenuCourses } from '@/types/menu.types'

export interface MenuCourseTabsProps {
  /**
   * The course tab counts published by the API.
   *
   * `total` is the count of the search- and status-filtered scope with **no
   * course filter** — the "All Dishes (n)" tab count. It does not change
   * when a course is selected, so the tab keeps describing the scope the
   * operator is narrowing.
   */
  courses: MenuCourses
  /** The course the operator selected, or `null` for the whole catalogue. */
  activeCourseId: number | null
  onCourseChange: (courseId: number | null) => void
}

/**
 * The course tab strip (spec §4.3).
 *
 * The reference's taxonomy is a list of dish *types*; the approved
 * `CourseCategory` rows are *course positions*, so the tabs use the real
 * course categories with the reference's interaction — a horizontally
 * scrollable strip, live counts and an active pill (DEC-061).
 *
 * The counts come from the API's `courses` block: `courses.total` for the
 * "All Dishes" tab and `courses.items[].dishCount` for each course. The
 * client never sums or derives them.
 */
export function MenuCourseTabs({
  courses,
  activeCourseId,
  onCourseChange,
}: MenuCourseTabsProps) {
  const allCount = formatCount(courses.total)

  return (
    <div
      role="group"
      aria-label="Course categories"
      className="flex items-center gap-1.5 overflow-x-auto pb-1 text-espresso-600"
    >
      <button
        type="button"
        onClick={() => onCourseChange(null)}
        aria-pressed={activeCourseId === null}
        // The name is spelled out so the count is announced as part of the
        // label rather than run together with it.
        aria-label={`All Dishes (${allCount})`}
        className={cn(
          'px-3 py-1.5 rounded-lg font-headline-sm text-body-sm whitespace-nowrap transition-colors',
          activeCourseId === null
            ? 'bg-culinary-600 text-white shadow-elevation-1'
            : 'bg-surface-container-low hover:bg-surface-container text-espresso-700',
        )}
      >
        All Dishes
        <span className="font-data-sm text-data-sm"> ({allCount})</span>
      </button>

      {courses.items.map((course) => {
        const isActive = activeCourseId === course.courseId
        const count = formatCount(course.dishCount)
        return (
          <button
            key={course.courseId}
            type="button"
            onClick={() => onCourseChange(course.courseId)}
            aria-pressed={isActive}
            aria-label={`${course.courseName} (${count})`}
            className={cn(
              'px-3 py-1.5 rounded-lg font-body-sm text-body-sm whitespace-nowrap transition-colors',
              isActive
                ? 'bg-culinary-600 text-white shadow-elevation-1'
                : 'bg-surface-container-low hover:bg-surface-container text-espresso-700',
            )}
          >
            {course.courseName}
            <span className="font-data-sm text-data-sm"> ({count})</span>
          </button>
        )
      })}
    </div>
  )
}
