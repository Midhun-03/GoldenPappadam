import { Area, AreaChart, CartesianGrid, Tooltip, XAxis, YAxis } from 'recharts'
import { ChartFrame, ChartTooltip } from '@/components/ui/chart'
import { useChartColors } from '@/lib/chartColors'
import { formatDay, formatDayShort, formatMoney, formatMoneyCompact } from '@/lib/format'
import { useReducedMotion } from '@/lib/motion'
import type { DayPoint } from './analytics'

export function SalesTrendChart({ points }: { points: DayPoint[] }) {
  const reduced = useReducedMotion()
  const colors = useChartColors()

  return (
    <ChartFrame height={248}>
      <AreaChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
        <defs>
          <linearGradient id="gp-sales-fill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={colors.primary} stopOpacity={0.16} />
            <stop offset="100%" stopColor={colors.primary} stopOpacity={0} />
          </linearGradient>
        </defs>

        <CartesianGrid vertical={false} stroke={colors.border} strokeDasharray="3 3" />

        <XAxis
          dataKey="day"
          tickLine={false}
          axisLine={false}
          tickMargin={10}
          minTickGap={28}
          tick={{ fill: colors['muted-foreground'], fontSize: 11 }}
          tickFormatter={formatDayShort}
        />

        <YAxis
          width={52}
          tickLine={false}
          axisLine={false}
          tick={{ fill: colors['muted-foreground'], fontSize: 11 }}
          tickFormatter={(value: number) => formatMoneyCompact(value)}
        />

        <Tooltip
          cursor={{ stroke: colors.border, strokeWidth: 1 }}
          content={
            <ChartTooltip
              formatLabel={(label) => formatDay(String(label))}
              formatValue={(value, entry) => {
                const count = Number(entry.payload?.count ?? 0)
                return `${formatMoney(value)} · ${count} bill${count === 1 ? '' : 's'}`
              }}
            />
          }
        />

        <Area
          name="Sales"
          type="monotone"
          dataKey="total"
          stroke={colors.primary}
          strokeWidth={2}
          fill="url(#gp-sales-fill)"
          dot={false}
          activeDot={{ r: 4, fill: colors.primary, stroke: colors.card, strokeWidth: 2 }}
          isAnimationActive={!reduced}
          animationDuration={600}
        />
      </AreaChart>
    </ChartFrame>
  )
}
