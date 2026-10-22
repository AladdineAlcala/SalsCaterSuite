import { Component } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import { CircleAlert, RefreshCw } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { cn } from '@/utils/cn'

export interface RouteErrorBoundaryProps {
  children: ReactNode
  /**
   * What failed, named in the fallback. `"the application"` at the root of
   * the route table, `"this screen"` inside the application shell.
   */
  area?: string
  /** Extra classes for the fallback container. */
  className?: string
}

interface RouteErrorBoundaryState {
  error: Error | null
}

/**
 * The render-error boundary.
 *
 * A **fetch** error is handled by the screen that made the request — the
 * dashboard's and the customer master's `AlertBanner` with a retry. A
 * **render** error is a different failure mode: a payload shape the client
 * does not expect, a null dereference, a component bug. Without a boundary,
 * React unmounts the whole tree and the user is left with a blank white page
 * — no navigation, no top bar, no message and no way back.
 *
 * This boundary contains that failure. It renders a fallback in the
 * application's design language with the error message and a **Try again**
 * control that clears the captured error and re-renders the children, and it
 * logs the error so the failure is diagnosable.
 */
export class RouteErrorBoundary extends Component<
  RouteErrorBoundaryProps,
  RouteErrorBoundaryState
> {
  state: RouteErrorBoundaryState = { error: null }

  static getDerivedStateFromError(error: Error): RouteErrorBoundaryState {
    return { error }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error(
      `[RouteErrorBoundary] A render error stopped ${this.props.area ?? 'this screen'} from displaying.`,
      error,
      errorInfo.componentStack,
    )
  }

  /** Clears the captured error and re-renders the children. */
  private handleReset = (): void => {
    this.setState({ error: null })
  }

  render(): ReactNode {
    const { error } = this.state
    const { children, area = 'this screen', className } = this.props

    if (!error) {
      return children
    }

    const isApplication = area === 'the application'

    return (
      <div
        role="alert"
        data-testid="route-error-boundary"
        className={cn('flex flex-col gap-space-md', className)}
      >
        <Card elevation={1} className="flex flex-col gap-space-md">
          <div className="flex items-start gap-3">
            <span className="w-10 h-10 rounded-lg bg-error-container text-on-error-container flex items-center justify-center shrink-0">
              <CircleAlert className="h-5 w-5" aria-hidden="true" />
            </span>
            <div className="min-w-0 flex-1">
              <h2 className="font-headline-sm text-headline-sm text-espresso-900">
                {isApplication
                  ? 'The application could not be displayed'
                  : 'This screen could not be displayed'}
              </h2>
              <p className="font-body-sm text-body-sm text-espresso-600 mt-1">
                {isApplication
                  ? 'An unexpected error stopped the application from starting. Nothing was changed — you can try again.'
                  : 'An unexpected error stopped this screen from rendering. The navigation, the top bar and your session are unaffected — you can try again.'}
              </p>
            </div>
          </div>

          <div className="p-3 rounded-lg bg-surface-container border border-artisan-border">
            <p className="font-caption text-caption text-espresso-600 uppercase font-semibold">
              Error detail
            </p>
            <p className="font-data-sm text-data-sm text-espresso-900 break-words mt-1">
              {error.message}
            </p>
          </div>

          <div>
            <Button variant="primary" icon={RefreshCw} onClick={this.handleReset}>
              Try again
            </Button>
          </div>
        </Card>
      </div>
    )
  }
}
