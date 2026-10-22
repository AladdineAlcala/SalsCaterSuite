import type { ReactNode } from 'react'
import { Info } from 'lucide-react'
import { cn } from '@/utils/cn'

export interface CapabilityGapNoticeProps {
  /** What the reference panel was called. */
  title: string
  /**
   * What the approved data model does **not** carry, and therefore why the
   * panel cannot be rendered. It names the missing concept — it never
   * substitutes a placeholder table (spec §1.2, AC-M13).
   */
  missing: string
  /** The blocked requirement the gap is tracked under. */
  requirement?: string
  /**
   * An optional closing note, rendered under the message (DEC-089).
   *
   * A **deliberate exclusion** is not an unruled rule, so it states its own
   * reason instead of borrowing the `requirement` footer. The prop is optional
   * and additive: a notice without it renders exactly as it did before.
   */
  footnote?: string
  className?: string
  children?: ReactNode
}

/**
 * The explicit capability-gap notice (spec §1.2, AC-M13).
 *
 * The design reference's ingredient, yield, batch, allergen and package-tier
 * panels are built on a recipe engine that the approved data model does not
 * carry. Wherever the reference shows one of those panels, this notice says
 * what is missing and why — it is a visible, honest statement, never a
 * disabled form and never a fabricated table.
 *
 * It is shared by the Menu & Food Costing, the Packages & Buffets and the
 * Package Configuration Workbench features (DEC-067, DEC-089).
 */
export function CapabilityGapNotice({
  title,
  missing,
  requirement,
  footnote,
  className,
  children,
}: CapabilityGapNoticeProps) {
  return (
    <aside
      role="note"
      data-testid="capability-gap-notice"
      className={cn(
        'rounded-lg border border-dashed border-caramel-200 bg-caramel-50 px-3 py-2.5 flex flex-col gap-1',
        className,
      )}
    >
      <div className="flex items-start gap-2">
        <Info className="h-4 w-4 shrink-0 mt-0.5 text-caramel-700" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          <p className="font-caption text-caption font-bold uppercase tracking-wider text-caramel-700">
            {title}
          </p>
          <p className="font-body-sm text-body-sm text-espresso-700 mt-0.5">{missing}</p>
          {children}
          {requirement ? (
            <p className="font-caption text-caption text-espresso-600 mt-1">
              Blocked under {requirement} — the business rules are not ruled yet.
            </p>
          ) : null}
          {footnote ? (
            <p className="font-caption text-caption text-espresso-600 mt-1">{footnote}</p>
          ) : null}
        </div>
      </div>
    </aside>
  )
}
