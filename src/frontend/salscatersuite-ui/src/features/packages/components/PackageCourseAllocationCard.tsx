import { UtensilsCrossed } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { CapabilityGapNotice } from '@/components/feedback/CapabilityGapNotice'
import { EmptyState } from '@/components/feedback/EmptyState'
import { EM_DASH, formatCurrency } from '@/utils/currencyFormatter'
import type { PackageCourseRuleset } from '@/types/package.types'

export interface PackageCourseAllocationCardProps {
  /** The tier's ruleset — the compact course profile of the tier. */
  ruleset: PackageCourseRuleset | undefined
}

/**
 * The course-allocation card (spec §5.3).
 *
 * The tier's ruleset is shown as a compact card grid — the course, its quota,
 * its standard portion and the pool's average food cost — so the operator can
 * see what the tier offers without leaving the location workbench.
 *
 * The reference's **free-inclusion ribbon** is a capability-gap notice: no
 * add-on is configured for the tier, and no entity carries the free
 * inclusions (DEC-084).
 */
export function PackageCourseAllocationCard({ ruleset }: PackageCourseAllocationCardProps) {
  const rules = ruleset?.rules ?? []

  return (
    <Card
      elevation={1}
      className="flex flex-col gap-space-md"
      data-testid="package-course-allocation"
    >
      <div className="flex items-center justify-between gap-2 flex-wrap pb-1">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Course Allocation
        </h2>
        <span className="font-caption text-caption text-espresso-600">
          {rules.length} course rules
        </span>
      </div>

      {rules.length === 0 ? (
        <EmptyState
          icon={UtensilsCrossed}
          title="This tier holds no course rule"
          description="The course coverage of the tier is configured on the course-rules workbench. Nothing is invented here."
        />
      ) : (
        <ul className="grid grid-cols-1 sm:grid-cols-2 gap-2">
          {rules.map((rule) => (
            <li
              key={rule.packageCourseRuleId}
              className="flex flex-col gap-1 rounded-lg border border-artisan-border bg-surface-container-low p-3"
            >
              <div className="flex items-center justify-between gap-2 flex-wrap">
                <span className="font-body-md text-body-md text-espresso-900 break-words">
                  {rule.courseName}
                </span>
                <Badge tone="info">Quota {rule.requiredQuota}</Badge>
              </div>
              <p className="font-caption text-caption text-espresso-600">
                Standard portion{' '}
                {rule.portionGrams === null ? EM_DASH : `${rule.portionGrams} g`} · Pool average
                food cost {formatCurrency(rule.poolAverageFoodCost)}
              </p>
              <p className="font-caption text-caption text-espresso-600">
                {rule.poolSize} qualified dishes · {rule.poolCostedCount} costed
              </p>
            </li>
          ))}
        </ul>
      )}

      <CapabilityGapNotice
        title="Complimentary staples unavailable"
        missing="The reference shows a free-inclusion ribbon beside the course profile, but no add-on is configured against a tier and no entity carries the free inclusions."
        footnote="Deliberately excluded by DEC-084 — no entity carries this data."
      />
    </Card>
  )
}
