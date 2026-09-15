import type { ReactElement, ReactNode } from 'react'
import { ResponsiveContainer } from 'recharts'
import { cn } from '@/lib/utils'

/**
 * Every chart sits in the same responsive box. Recharts measures its parent, so the height
 * lives here and the width always follows the card, which is what keeps charts from
 * overflowing on a phone.
 */
export function ChartFrame({
  height,
  className,
  children,
}: {
  height: number
  className?: string
  children: ReactElement
}) {
  return (
    <div className={cn('w-full', className)} style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        {children}
      </ResponsiveContainer>
    </div>
  )
}

export type TooltipEntry = {
  name?: string
  value?: number
  color?: string
  /** Recharts hands the whole row back, which is where our own fields live. */
  payload?: Record<string, unknown>
}

/** The shape Recharts passes to a custom `Tooltip content`. */
export type ChartTooltipProps = {
  active?: boolean
  label?: string | number
  payload?: TooltipEntry[]
}

/**
 * One tooltip for every chart: a heading, then a swatch, name and value per series.
 */
export function ChartTooltip({
  active,
  label,
  payload,
  formatLabel,
  formatValue,
}: ChartTooltipProps & {
  formatLabel?: (label: string | number) => string
  formatValue: (value: number, entry: TooltipEntry) => string
}) {
  if (!active || !payload?.length) return null

  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs shadow-md">
      {label !== undefined && label !== '' && (
        <div className="mb-1.5 font-medium">{formatLabel ? formatLabel(label) : label}</div>
      )}
      <div className="grid gap-1">
        {payload.map((entry, index) => (
          <div key={entry.name ?? index} className="flex items-center gap-2">
            {entry.color && (
              <span
                aria-hidden="true"
                className="size-2 shrink-0 rounded-[2px]"
                style={{ background: entry.color }}
              />
            )}
            {entry.name && <span className="text-muted-foreground">{entry.name}</span>}
            <span className="ml-auto font-medium tabular-nums">
              {formatValue(entry.value ?? 0, entry)}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

/** A legend row shared by the donut charts, so their keys read the same way. */
export function ChartLegend({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn('grid gap-2', className)}>{children}</div>
}

export function ChartLegendItem({
  color,
  label,
  value,
  hint,
}: {
  color: string
  label: string
  value: string
  hint?: string
}) {
  return (
    <div className="flex items-center gap-2 text-sm">
      <span aria-hidden="true" className="size-2 shrink-0 rounded-[2px]" style={{ background: color }} />
      <span className="min-w-0 truncate text-muted-foreground">{label}</span>
      <span className="ml-auto shrink-0 font-medium tabular-nums">{value}</span>
      {hint && <span className="w-10 shrink-0 text-right text-xs text-muted-foreground">{hint}</span>}
    </div>
  )
}
