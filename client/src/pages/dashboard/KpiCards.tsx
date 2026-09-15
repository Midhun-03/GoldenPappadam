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

function ChangeBadge({ change, label }: { change: Change; label: string }) {
  const Icon = change.isUp ? TrendingUp : TrendingDown

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-xs font-medium',
        change.isUp ? 'bg-success-surface text-success' : 'bg-destructive-surface text-destructive',
      )}
      title={`${change.percent >= 0 ? '+' : ''}${change.percent.toFixed(1)}% ${label}`}
    >
      <Icon className="size-3" />
      {Math.abs(change.percent).toFixed(0)}%
    </span>
  )
}

function KpiCard({ kpi, index, isLoading }: { kpi: Kpi; index: number; isLoading: boolean }) {
  const counted = useCountUp(kpi.value)
  const display = kpi.kind === 'money' ? formatMoney(counted) : String(Math.round(counted))

  return (
    <Link
      to={kpi.to}
      style={{ animationDelay: `${index * 45}ms` }}
      className={cn(
        'animate-enter group flex flex-col rounded-xl border bg-card p-4 transition-colors',
        'hover:border-foreground/20 focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none',
        'max-sm:last:col-span-2',
      )}
    >
      <div className="flex items-center gap-2">
        <kpi.icon className="size-4 shrink-0 text-muted-foreground" />
        <span className="min-w-0 truncate text-xs font-medium text-muted-foreground">{kpi.label}</span>
      </div>

      {isLoading ? (
        <Skeleton className="mt-2.5 h-7 w-24" />
      ) : (
        <div className="mt-1.5 flex flex-wrap items-baseline gap-x-2 gap-y-1">
          <span
            className={cn(
              'font-heading text-2xl font-semibold tabular-nums',
              kpi.tone === 'owed' && kpi.value > 0 && 'text-destructive',
              kpi.tone === 'alert' && kpi.value > 0 && 'text-warning',
            )}
          >
            {display}
          </span>
          {kpi.change && kpi.changeLabel && <ChangeBadge change={kpi.change} label={kpi.changeLabel} />}
        </div>
      )}

      <div className="mt-1 text-xs text-muted-foreground">{kpi.hint}</div>
    </Link>
  )
}

export function KpiCards({ kpis, isLoading }: { kpis: Kpi[]; isLoading: boolean }) {
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
      {kpis.map((kpi, index) => (
        <KpiCard key={kpi.key} kpi={kpi} index={index} isLoading={isLoading} />
      ))}
    </div>
  )
}
