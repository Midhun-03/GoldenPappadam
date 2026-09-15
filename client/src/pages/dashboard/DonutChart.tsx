import { Cell, Pie, PieChart, Tooltip } from 'recharts'
import { ChartFrame, ChartLegend, ChartLegendItem, ChartTooltip } from '@/components/ui/chart'
import { useChartColors } from '@/lib/chartColors'
import { useReducedMotion } from '@/lib/motion'
import type { MixSlice } from './analytics'

/**
 * A donut is only worth it when the slices mean something, so both uses here carry status
 * colours: how much of what was billed has come back, and how stock is holding up.
 */
export function DonutChart({
  slices,
  centreValue,
  centreLabel,
  formatValue,
}: {
  slices: MixSlice[]
  centreValue: string
  centreLabel: string
  formatValue: (value: number) => string
}) {
  const reduced = useReducedMotion()
  const colors = useChartColors()

  const total = slices.reduce((sum, slice) => sum + slice.value, 0)
  const share = (value: number) => (total > 0 ? `${Math.round((value / total) * 100)}%` : '—')

  return (
    <div className="grid items-center gap-4 @sm:grid-cols-[9.5rem_minmax(0,1fr)]">
      <div className="relative mx-auto w-full max-w-[9.5rem]">
        <ChartFrame height={152}>
          <PieChart>
            <Tooltip
              content={<ChartTooltip formatValue={(value) => `${formatValue(value)} · ${share(value)}`} />}
            />
            <Pie
              data={slices}
              dataKey="value"
              nameKey="label"
              innerRadius={48}
              outerRadius={72}
              paddingAngle={slices.length > 1 ? 2 : 0}
              strokeWidth={0}
              isAnimationActive={!reduced}
              animationDuration={600}
            >
              {slices.map((slice) => (
                <Cell key={slice.key} fill={colors[slice.token]} />
              ))}
            </Pie>
          </PieChart>
        </ChartFrame>

        {/* The headline sits in the hole rather than being repeated above the chart. */}
        <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
          <span className="font-heading text-base font-semibold tabular-nums">{centreValue}</span>
          <span className="max-w-[6rem] text-[11px] leading-tight text-muted-foreground">{centreLabel}</span>
        </div>
      </div>

      <ChartLegend>
        {slices.map((slice) => (
          <ChartLegendItem
            key={slice.key}
            color={colors[slice.token]}
            label={slice.label}
            value={formatValue(slice.value)}
            hint={share(slice.value)}
          />
        ))}
      </ChartLegend>
    </div>
  )
}
