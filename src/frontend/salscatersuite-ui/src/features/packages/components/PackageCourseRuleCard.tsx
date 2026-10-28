import { Pencil, Trash2, UtensilsCrossed } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { EM_DASH, formatCurrency } from '@/utils/currencyFormatter'
import type { PackageCourseRule } from '@/types/package.types'

export interface PackageCourseRuleCardProps {
  /** The rule as the workbench currently holds it. */
  rule: PackageCourseRule
  /** `true` while the editor holds this rule. */
  isEditing: boolean
  /** `true` when the rule has not been saved yet. */
  isNew: boolean
  /** Opens the editor for this rule. */
  onEdit: () => void
  /** Removes this rule from the pending ruleset. */
  onRemove: () => void
}

/**
 * One course rule card (spec §5.2, column 2).
 *
 * The card shows the rule the client is held to, the course's **qualified
 * pool** and the pool's average food cost. Every figure on the card is the
 * server's — the pool's size, its costed count and its mean are published by
 * the ruleset endpoint (DEC-002).
 *
 * A course whose pool holds no dish says so on the card; it never shows a
 * fabricated dish and never shows an average of nothing (BR-13).
 */
export function PackageCourseRuleCard({
  rule,
  isEditing,
  isNew,
  onEdit,
  onRemove,
}: PackageCourseRuleCardProps) {
  const hasPool = rule.pool.length > 0

  return (
    <Card
      elevation={1}
      className="flex flex-col gap-3"
      data-testid={`package-course-rule-${rule.courseId}`}
    >
      <div className="flex items-start justify-between gap-3 flex-wrap">
        <div className="min-w-0 space-y-1">
          <h3 className="font-headline-sm text-headline-sm text-espresso-900 break-words">
            {rule.courseName}
          </h3>
          <p className="font-body-sm text-body-sm text-espresso-600 break-words">
            {rule.ruleDescription}
          </p>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <Badge tone="info">Quota {rule.requiredQuota}</Badge>
        </div>
      </div>

      <dl className="grid grid-cols-1 sm:grid-cols-3 gap-3 bg-surface-container-low rounded-lg p-3">
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Standard portion
          </dt>
          <dd className="font-data-sm text-data-sm text-espresso-900 font-bold">
            {rule.portionGrams === null ? EM_DASH : `${rule.portionGrams} g`}
          </dd>
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Qualified pool
          </dt>
          <dd className="font-data-sm text-data-sm text-espresso-900 font-bold">
            {isNew ? 'Not published yet' : `${rule.poolSize} dishes · ${rule.poolCostedCount} costed`}
          </dd>
        </div>
        <div className="flex flex-col gap-0.5">
          <dt className="font-caption text-caption text-espresso-400 uppercase font-semibold">
            Pool average food cost
          </dt>
          <dd className="font-data-sm text-data-sm text-espresso-900 font-bold break-words">
            {formatCurrency(rule.poolAverageFoodCost)}
          </dd>
        </div>
      </dl>

      <div className="flex flex-col gap-1.5">
        <span className="font-caption text-caption text-espresso-400 uppercase font-semibold flex items-center gap-1">
          <UtensilsCrossed className="h-3.5 w-3.5" aria-hidden="true" />
          Qualified dishes
        </span>
        {isNew ? (
          <p className="font-body-sm text-body-sm text-espresso-600">
            This rule has not been applied yet. The server publishes the course&apos;s
            qualified pool once the ruleset is saved.
          </p>
        ) : hasPool ? (
          <ul className="flex flex-wrap gap-1.5 max-h-40 overflow-y-auto">
            {rule.pool.map((dish) => (
              <li key={dish.menuId}>
                <span className="inline-flex items-center gap-1.5 rounded border border-artisan-border bg-surface-container-low px-2 py-0.5">
                  <span className="font-body-sm text-body-sm text-espresso-700 break-words">
                    {dish.menuName}
                  </span>
                  <span className="font-data-sm text-data-sm text-espresso-500">
                    {formatCurrency(dish.foodCost)}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="font-body-sm text-body-sm text-espresso-600">
            No dish is qualified for this course yet, so the course has no pool and no
            average food cost.
          </p>
        )}
      </div>

      <div className="flex items-center gap-2 flex-wrap">
        <Button variant="secondary" size="sm" icon={Pencil} onClick={onEdit}>
          Edit rule
        </Button>
        <Button variant="tertiary" size="sm" icon={Trash2} onClick={onRemove}>
          Remove rule
        </Button>
        {isEditing ? (
          <span className="font-caption text-caption text-espresso-600">
            This rule is open in the editor.
          </span>
        ) : null}
      </div>
    </Card>
  )
}
