import { AlertTriangle, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { ApiError } from '@/lib/api'
import { cn } from '@/lib/utils'

/**
 * What a list shows when it has nothing to show: no rows, no matches, or a failed request.
 * Always says which of the three it is, because "empty" and "broken" look the same otherwise.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  variant = 'default',
  className,
}: {
  icon?: LucideIcon
  title: string
  description?: string
  action?: ReactNode
  variant?: 'default' | 'error'
  className?: string
}) {
  const isError = variant === 'error'
  const Glyph = Icon ?? (isError ? AlertTriangle : undefined)

  return (
    <div className={cn('flex flex-col items-center justify-center px-6 py-12 text-center', className)}>
      {Glyph && (
        <div
          className={cn(
            'mb-3 flex size-10 items-center justify-center rounded-full',
            isError ? 'bg-destructive-surface text-destructive' : 'bg-muted text-muted-foreground',
          )}
        >
          <Glyph className="size-5" />
        </div>
      )}
      <p className="text-sm font-medium">{title}</p>
      {description && <p className="mt-1 max-w-sm text-sm text-muted-foreground">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}

/** Turns a failed query into an empty state that says what went wrong. */
export function ErrorState({ error, className }: { error: unknown; className?: string }) {
  const message =
    error instanceof ApiError
      ? error.message
      : 'Could not reach the server. Check that it is running, then try again.'

  return <EmptyState variant="error" title="Something went wrong" description={message} className={className} />
}
