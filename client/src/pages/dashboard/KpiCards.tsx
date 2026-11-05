import { TrendingDown, TrendingUp, type LucideIcon } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Skeleton } from '@/components/ui/skeleton'
import { formatMoney } from '@/lib/format'
import { useCountUp } from '@/lib/useCountUp'
import { cn } from '@/lib/utils'
import type { Change } from './analytics'

export type Kpi = {
  key: string
  label: string
  value: number
  /** Money is formatted as currency; a count is shown as it is. */
  kind: 'money' | 'count'
  hint: string
  icon: LucideIcon
  to: string
  /** Only set where the API gives a real earlier period to compare against. */
  change?: Change | null
  /** What the comparison is against, spelled out so the percentage cannot be misread. */
  changeLabel?: string
  tone?: 'default' | 'owed' | 'alert'
}

function ChangeBadge({ change, label, lead }: { change: Change; label: string; lead?: boolean }) {
  const Icon = change.isUp ? TrendingUp : TrendingDown

  return (
    <span
      className={cn(
        // The comparison arrives after the figure, so it fades in rather than popping.
        'animate-enter inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-xs font-medium',
        lead
          ? 'bg-primary-foreground/15 text-primary-foreground'
          : change.isUp
            ? 'bg-success-surface text-success'
            : 'bg-destructive-surface text-destructive',
      )}
      title={`${change.percent >= 0 ? '+' : ''}${change.percent.toFixed(1)}% ${label}`}
    >
      <Icon className="size-3" />
      {Math.abs(change.percent).toFixed(0)}%
    </span>
  )
}

function KpiCard({
  kpi,
  index,
  isLoading,
  lead,
}: {
  kpi: Kpi
  index: number
  isLoading: boolean
  /** The one figure the day is read by: today's sales. It gets the brand ground and the size. */
  lead?: boolean
}) {
  // The lead figure is the one authored moment on the page, so it settles a little more slowly.
  const counted = useCountUp(kpi.value, lead ? 750 : 450)
  const display = kpi.kind === 'money' ? formatMoney(counted) : String(Math.round(counted))

  return (
    <Link
      to={kpi.to}
      style={{ animationDelay: `${index * 45}ms` }}
      className={cn(
        'animate-enter group flex flex-col rounded-xl border p-4 transition-[color,background-color,border-color,transform] duration-150 active:scale-[0.99]',
        'focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none',
        lead
          ? 'col-span-2 justify-between gap-6 border-transparent bg-brand-deep p-5 text-primary-foreground hover:bg-brand-deep/92 sm:col-span-4 lg:col-span-2'
          : 'bg-card hover:border-foreground/20',
      )}
    >
      <div className="flex items-center gap-2">
        <kpi.icon
          className={cn('shrink-0', lead ? 'size-5 text-brand' : 'size-4 text-muted-foreground')}
        />
        <span
          className={cn(
            'min-w-0 truncate font-medium',
            lead ? 'text-sm text-primary-foreground/85' : 'text-xs text-muted-foreground',
          )}
        >
          {kpi.label}
        </span>
      </div>

      <div>
        {isLoading ? (
          <Skeleton className={cn('mt-2.5', lead ? 'h-12 w-48 bg-primary-foreground/20' : 'h-7 w-24')} />
        ) : (
          <div className="mt-1.5 flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <span
              className={cn(
                'font-heading font-semibold tabular-nums',
                lead ? 'text-4xl tracking-tight sm:text-5xl' : 'text-2xl',
                kpi.tone === 'owed' && kpi.value > 0 && 'text-destructive',
                kpi.tone === 'alert' && kpi.value > 0 && 'text-warning',
              )}
            >
              {display}
            </span>
            {kpi.change && kpi.changeLabel && (
              <ChangeBadge change={kpi.change} label={kpi.changeLabel} lead={lead} />
            )}
          </div>
        )}

        <div
          className={cn('mt-1 text-xs', lead ? 'text-sm text-primary-foreground/80' : 'text-muted-foreground')}
        >
          {kpi.hint}
        </div>
      </div>
    </Link>
  )
}

export function KpiCards({ kpis, isLoading }: { kpis: Kpi[]; isLoading: boolean }) {
  // The first KPI leads (wide, brand ground); the rest stay quiet so the lead is legible.
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6">
      {kpis.map((kpi, index) => (
        <KpiCard key={kpi.key} kpi={kpi} index={index} isLoading={isLoading} lead={index === 0} />
      ))}
    </div>
  )
}
