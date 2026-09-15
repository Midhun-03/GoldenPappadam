import { Bar, BarChart, Cell, Tooltip, XAxis, YAxis } from 'recharts'
import { ChartFrame, ChartTooltip } from '@/components/ui/chart'
import { useChartColors, type ChartToken } from '@/lib/chartColors'
import { formatMoney, formatMoneyCompact, formatQuantity } from '@/lib/format'
import { useReducedMotion } from '@/lib/motion'
import type { Bar as BarDatum, ChartMetric } from './analytics'

/** Neutral ramp: these bars are ranked, not categorised, so they only need to read as one set. */
const ramp: ChartToken[] = ['chart-1', 'chart-2', 'chart-3', 'chart-4', 'chart-5']

/**
 * Horizontal bars rather than a pie: product names stay readable and the ranking is obvious
 * however many products there are.
 */
export function TopProductsChart({ bars, metric }: { bars: BarDatum[]; metric: ChartMetric }) {
  const reduced = useReducedMotion()
  const colors = useChartColors()

  const format = (value: number, unitCode?: string) =>
    metric === 'value' ? formatMoney(value) : `${formatQuantity(value)}${unitCode ? ` ${unitCode}` : ''}`

  return (
    <ChartFrame height={Math.max(180, bars.length * 42 + 32)}>
      <BarChart data={bars} layout="vertical" margin={{ top: 0, right: 44, bottom: 0, left: 0 }}>
        <XAxis type="number" hide />
        <YAxis
          type="category"
          dataKey="label"
          width={128}
          tickLine={false}
          axisLine={false}
          tick={{ fill: colors.foreground, fontSize: 12 }}
          tickFormatter={(label: string) => (label.length > 20 ? `${label.slice(0, 19)}…` : label)}
        />

        <Tooltip
          cursor={{ fill: colors.muted }}
          content={
            <ChartTooltip
              formatLabel={(label) => String(label)}
              formatValue={(value, entry) => format(value, entry.payload?.unitCode as string | undefined)}
            />
          }
        />

        <Bar
          name={metric === 'value' ? 'Sales' : 'Quantity'}
          dataKey="value"
          radius={[0, 4, 4, 0]}
          barSize={18}
          isAnimationActive={!reduced}
          animationDuration={600}
          label={{
            position: 'right',
            fontSize: 11,
            fill: colors['muted-foreground'],
            // Left unannotated so Recharts' own label type flows in.
            formatter: (value) =>
              metric === 'value' ? formatMoneyCompact(Number(value)) : formatQuantity(Number(value)),
          }}
        >
          {bars.map((bar, index) => (
            <Cell key={bar.id} fill={colors[ramp[Math.min(index, ramp.length - 1)]]} />
          ))}
        </Bar>
      </BarChart>
    </ChartFrame>
  )
}
