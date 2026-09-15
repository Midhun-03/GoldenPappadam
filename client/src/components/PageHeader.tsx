import { ArrowLeft } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

/**
 * The top of every page: where you came from, what this is, and what you can do here.
 * Actions drop below the title on a phone rather than squeezing it.
 */
export function PageHeader({
  title,
  description,
  action,
  back,
}: {
  title: string
  description?: ReactNode
  action?: ReactNode
  back?: { to: string; label: string }
}) {
  return (
    <div className="mb-5 flex flex-col gap-3 sm:mb-6 sm:flex-row sm:items-start sm:justify-between sm:gap-6">
      <div className="min-w-0">
        {back && (
          <Link
            to={back.to}
            className="mb-1.5 -ml-1 inline-flex items-center gap-1 rounded px-1 py-0.5 text-xs font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            <ArrowLeft className="size-3.5" />
            {back.label}
          </Link>
        )}
        <h1 className="font-heading text-xl font-semibold tracking-tight text-balance sm:text-2xl">{title}</h1>
        {description && <div className="mt-1 text-sm text-muted-foreground">{description}</div>}
      </div>

      {action && <div className="flex shrink-0 flex-wrap items-center gap-2">{action}</div>}
    </div>
  )
}
