import { CircleCheck, CircleX } from 'lucide-react'
import { Badge } from '@/components/ui/Badge'
import { Card } from '@/components/ui/Card'
import { EmptyState } from '@/components/feedback/EmptyState'
import type { PackageCourseRulePreview } from '@/types/package.types'
import {
  packageComplianceLabel,
  packageComplianceTone,
  packageOverallVerdictLabel,
  packageOverallVerdictTone,
  packageQuotaVerdictLabel,
  packageQuotaVerdictTone,
} from '../packageTypes'

export interface PackageQuotaComplianceListProps {
  /** The server's preview — it carries both verdicts and the per-rule list. */
  preview: PackageCourseRulePreview | undefined
  /** `true` while the preview is in flight. */
  isPreviewing: boolean
  /**
   * `true` while the typed selection has not reached the server yet, or the
   * preview is in flight.
   *
   * While it holds, neither verdict nor the per-rule list is rendered: a
   * verdict belongs to the selection the operator is looking at, not to the one
   * they have just changed. The panel shows the explicit **recalculating**
   * state instead.
   */
  isRecalculating: boolean
}

/**
 * Says **which** of the two verdicts failed.
 *
 * A selection whose food cost is unknown cannot be judged against the cost
 * ceiling, so the screen says that rather than implying the selection is
 * inside the ceiling.
 */
function describeVerdict(preview: PackageCourseRulePreview): string {
  if (!preview.isCostKnown) {
    return 'The food cost of this selection is unknown, so the cost ceiling cannot be judged. The course-rule quota verdict above still applies.'
  }
  if (preview.isCompliant) {
    return 'Every course rule is satisfied and the selection stays within the cost ceiling.'
  }
  if (preview.isQuotaCompliant) {
    return 'Every course rule is satisfied, but the selection breaches the cost ceiling — the overall verdict is therefore not compliant. The breach warns; it changes no price.'
  }
  return 'At least one course rule is not satisfied, so the selection is not compliant.'
}

/**
 * The rule-compliance verdict (spec §5.2, column 3).
 *
 * **Two** verdicts are published and they are not the same thing (DEC-080):
 * `isQuotaCompliant` covers the rules alone, while `isCompliant` is the quota
 * verdict **and** the absence of a cost-ceiling breach. The screen says which
 * of the two failed.
 *
 * The verdict lives in an `aria-live="polite"` region, so a re-run of the
 * preview is announced.
 *
 * **Why a pending preview is never shown as the current one.** A verdict is
 * the answer to "is this client's choice valid?". While the operator's new
 * selection is on its way to the server, the previous selection's verdict is
 * still in the browser's memory — and a coordinator who has just picked a
 * dish that breaks the quota must not read "quota satisfied" for it. So while
 * the preview is pending the panel shows the explicit **recalculating** state
 * and neither verdict nor the per-rule list is rendered. The location
 * workbench withholds its figures in exactly the same window and for exactly
 * the same reason.
 */
export function PackageQuotaComplianceList({
  preview,
  isPreviewing,
  isRecalculating,
}: PackageQuotaComplianceListProps) {
  return (
    <Card
      elevation={1}
      className="flex flex-col gap-space-md"
      data-testid="package-quota-compliance"
    >
      <div className="flex items-center justify-between gap-2 pb-1 flex-wrap">
        <h2 className="font-headline-sm text-headline-sm text-espresso-900">
          Rule Compliance
        </h2>
        {isPreviewing ? (
          <span className="font-caption text-caption text-espresso-600">Recalculating…</span>
        ) : null}
      </div>

      <div aria-live="polite" className="flex flex-col gap-2">
        {isRecalculating ? (
          <p className="font-caption text-caption text-espresso-500">Recalculating…</p>
        ) : preview ? (
          <>
            <div className="flex items-center justify-between gap-2 flex-wrap rounded-lg bg-surface-container-low p-3">
              <span className="font-body-sm text-body-sm text-espresso-700">
                Overall verdict
              </span>
              <Badge
                tone={packageOverallVerdictTone(preview.isCompliant)}
                icon={preview.isCompliant ? CircleCheck : CircleX}
              >
                {packageOverallVerdictLabel(preview.isCompliant)}
              </Badge>
            </div>
            <div className="flex items-center justify-between gap-2 flex-wrap rounded-lg bg-surface-container-low p-3">
              <span className="font-body-sm text-body-sm text-espresso-700">
                Course-rule quota verdict
              </span>
              <Badge
                tone={packageQuotaVerdictTone(preview.isQuotaCompliant)}
                icon={preview.isQuotaCompliant ? CircleCheck : CircleX}
              >
                {packageQuotaVerdictLabel(preview.isQuotaCompliant)}
              </Badge>
            </div>
            <p className="font-caption text-caption text-espresso-600">
              {describeVerdict(preview)}
            </p>
          </>
        ) : (
          <p className="font-body-sm text-body-sm text-espresso-600">
            The compliance verdict appears once the server has priced the selection.
          </p>
        )}
      </div>

      {!isRecalculating && preview && preview.compliance.length > 0 ? (
        <ul className="flex flex-col gap-1.5">
          {preview.compliance.map((entry) => (
            <li
              key={`${entry.courseId}-${entry.courseName}`}
              className="flex items-center justify-between gap-2 flex-wrap rounded border border-artisan-border bg-surface-container-low px-3 py-2"
            >
              <div className="min-w-0">
                <span className="font-body-sm text-body-sm text-espresso-900 break-words">
                  {entry.courseName}
                </span>
                {!entry.hasRule ? (
                  <span className="block font-caption text-caption text-caramel-700">
                    This tier holds no rule for the course, so the selection cannot satisfy
                    one.
                  </span>
                ) : (
                  <span className="block font-caption text-caption text-espresso-600">
                    Required {entry.requiredQuota} · selected {entry.selectedCount}
                  </span>
                )}
              </div>
              <Badge
                tone={entry.hasRule ? packageComplianceTone(entry.isCompliant) : 'warning'}
                icon={entry.isCompliant ? CircleCheck : CircleX}
              >
                {entry.hasRule
                  ? packageComplianceLabel(entry.isCompliant)
                  : 'No rule for this course'}
              </Badge>
            </li>
          ))}
        </ul>
      ) : null}

      {!isRecalculating && preview && preview.compliance.length === 0 ? (
        <EmptyState
          title="No course rule to judge"
          description="The tier holds no course rule yet, so there is no quota to compare a selection against."
        />
      ) : null}
    </Card>
  )
}
