import { useMemo } from 'react'

const tokens = [
  'primary',
  'success',
  'warning',
  'destructive',
  'muted',
  'muted-foreground',
  'foreground',
  'border',
  'card',
  'chart-1',
  'chart-2',
  'chart-3',
  'chart-4',
  'chart-5',
] as const

export type ChartToken = (typeof tokens)[number]

export type ChartColors = Record<ChartToken, string>

/**
 * Recharts paints through SVG presentation attributes, and those do not resolve `var()`:
 * a shape given `fill="var(--success)"` draws with the right geometry and no colour. So the
 * palette is read off the document once and handed to the charts as real colour values.
 */
export function useChartColors(): ChartColors {
  return useMemo(() => {
    const styles = getComputedStyle(document.documentElement)
    const entries = tokens.map((token) => [token, styles.getPropertyValue(`--${token}`).trim()])
    return Object.fromEntries(entries) as ChartColors
  }, [])
}
