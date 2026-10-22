import { formatTimestamp } from '@/utils/dateFormatter'

export type ApiConnectionState = 'live' | 'refreshing' | 'offline'

export interface StatusFooterProps {
  /** Server UTC timestamp of the aggregate. */
  generatedAt: string
  /**
   * Derived from the last fetch result — `live` after a successful load,
   * `refreshing` while a refetch is in flight and `offline` after a failure.
   */
  connection: ApiConnectionState
}

const STATE_LABELS: Record<ApiConnectionState, string> = {
  live: 'API connected',
  refreshing: 'Refreshing',
  offline: 'API unreachable',
}

const DOT_CLASSES: Record<ApiConnectionState, string> = {
  live: 'bg-culinary-600',
  refreshing: 'bg-caramel-500',
  offline: 'bg-error',
}

/**
 * The status footer (spec §9.2 item 6).
 *
 * It states the real stack, the database name, a **live** connection indicator
 * derived from the last fetch result and the timestamp of the data. It carries
 * no product version number and makes no connection claim the fetch did not
 * support (AC-F10).
 */
export function StatusFooter({ generatedAt, connection }: StatusFooterProps) {
  return (
    <footer className="flex flex-col sm:flex-row items-center justify-between gap-space-sm pt-space-md pb-space-lg border-t border-artisan-border text-espresso-600 font-caption text-caption">
      <div className="flex items-center gap-2 flex-wrap">
        <span
          className={`w-1.5 h-1.5 rounded-full ${DOT_CLASSES[connection]}`}
          aria-hidden="true"
        />
        <span className="font-semibold text-espresso-700">{STATE_LABELS[connection]}</span>
        <span className="text-espresso-200" aria-hidden="true">
          &bull;
        </span>
        <span>Sal&apos;s CaterSuite &bull; .NET 10 Clean Architecture &amp; EF Core</span>
        <span className="text-espresso-200" aria-hidden="true">
          &bull;
        </span>
        <span>
          Database{' '}
          <code className="font-mono text-data-sm text-espresso-850">SalsCaterSuiteDb</code>
        </span>
      </div>

      <div className="flex items-center gap-2">
        <span>Data as of {formatTimestamp(generatedAt)}</span>
      </div>
    </footer>
  )
}
