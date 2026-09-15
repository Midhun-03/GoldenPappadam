import type { InvoiceListItem, ProductSales, StockOnHand } from '@/api/types'
import type { ChartToken } from '@/lib/chartColors'
import { shiftDay } from '@/lib/format'

/**
 * Everything on the dashboard is derived here from what the API already returns, so no figure
 * on the screen is invented. Cancelled bills are excluded from every sales total, matching the
 * way the server computes today's and this month's sales.
 */

export type RangeKey = '7d' | '30d' | 'month'

export const rangeLabels: Record<RangeKey, string> = {
  '7d': 'Last 7 days',
  '30d': 'Last 30 days',
  month: 'This month',
}

export const startOfMonth = (day: string) => `${day.slice(0, 7)}-01`

/** The first day of the month before the one `day` falls in. */
export const startOfPreviousMonth = (day: string) => startOfMonth(shiftDay(startOfMonth(day), -1))

export function rangeStart(range: RangeKey, today: string) {
  if (range === '7d') return shiftDay(today, -6)
  if (range === '30d') return shiftDay(today, -29)
  return startOfMonth(today)
}

/**
 * The earliest day the dashboard needs in one request: far enough back to cover the widest
 * range the user can pick and the previous month it compares against.
 */
export function earliestDayNeeded(today: string) {
  const thirtyDays = shiftDay(today, -29)
  const previousMonth = startOfPreviousMonth(today)
  return previousMonth < thirtyDays ? previousMonth : thirtyDays
}

const isSale = (invoice: InvoiceListItem) => invoice.status === 'Issued'

const inRange = (invoice: InvoiceListItem, from: string, to: string) =>
  invoice.invoiceDate >= from && invoice.invoiceDate <= to

export type DayPoint = { day: string; total: number; count: number }

/** One point per day including days with no bills, so the line has no gaps to misread. */
export function dailySeries(invoices: InvoiceListItem[], from: string, to: string): DayPoint[] {
  const points = new Map<string, DayPoint>()

  for (let day = from; day <= to; day = shiftDay(day, 1)) {
    points.set(day, { day, total: 0, count: 0 })
  }

  for (const invoice of invoices) {
    if (!isSale(invoice)) continue

    const point = points.get(invoice.invoiceDate)
    if (!point) continue

    point.total += invoice.totalAmount
    point.count += 1
  }

  return [...points.values()]
}

export function totalBetween(invoices: InvoiceListItem[], from: string, to: string) {
  let total = 0
  let count = 0

  for (const invoice of invoices) {
    if (!isSale(invoice) || !inRange(invoice, from, to)) continue
    total += invoice.totalAmount
    count += 1
  }

  return { total, count }
}

export type Change = { percent: number; isUp: boolean }

/**
 * Null when there is nothing to compare against: a jump from zero has no meaningful
 * percentage, and showing one would be inventing a trend.
 */
export function changeAgainst(current: number, previous: number): Change | null {
  if (previous <= 0) return null
  if (current === previous) return { percent: 0, isUp: true }

  return { percent: ((current - previous) / previous) * 100, isUp: current > previous }
}

/** Yesterday, and the same stretch of last month, both real periods from the same data. */
export function comparisonPeriods(today: string) {
  const previousMonthEnd = shiftDay(startOfMonth(today), -1)
  const lastDayOfPreviousMonth = Number(previousMonthEnd.slice(8, 10))
  const dayOfMonth = Number(today.slice(8, 10))

  return {
    yesterday: shiftDay(today, -1),
    previousMonthStart: startOfMonth(previousMonthEnd),
    // Clamp so the 31st never asks for a day the previous month does not have.
    previousMonthSameDay:
      dayOfMonth >= lastDayOfPreviousMonth
        ? previousMonthEnd
        : `${previousMonthEnd.slice(0, 8)}${String(dayOfMonth).padStart(2, '0')}`,
  }
}

export type MixSlice = { key: string; label: string; value: number; token: ChartToken }

/**
 * How the billed money in a range splits by how much of it has come back. This is the
 * shape of the business: most of it runs on credit.
 */
export function paymentMix(invoices: InvoiceListItem[], from: string, to: string): MixSlice[] {
  let paid = 0
  let partPaid = 0
  let unpaid = 0

  for (const invoice of invoices) {
    if (!isSale(invoice) || !inRange(invoice, from, to)) continue

    if (invoice.outstanding <= 0) paid += invoice.totalAmount
    else if (invoice.amountPaid > 0) partPaid += invoice.totalAmount
    else unpaid += invoice.totalAmount
  }

  const slices: MixSlice[] = [
    { key: 'paid', label: 'Settled', value: paid, token: 'success' },
    { key: 'part', label: 'Part paid', value: partPaid, token: 'warning' },
    { key: 'unpaid', label: 'Unpaid', value: unpaid, token: 'destructive' },
  ]

  return slices.filter((slice) => slice.value > 0)
}

export type StockHealth = {
  slices: MixSlice[]
  total: number
  low: number
  out: number
}

export function stockHealth(stock: StockOnHand[]): StockHealth {
  let healthy = 0
  let low = 0
  let out = 0

  for (const row of stock) {
    if (row.quantityOnHand <= 0) out += 1
    else if (row.isLowStock) low += 1
    else healthy += 1
  }

  const slices: MixSlice[] = [
    { key: 'healthy', label: 'In stock', value: healthy, token: 'success' },
    { key: 'low', label: 'Low stock', value: low, token: 'warning' },
    { key: 'out', label: 'Out of stock', value: out, token: 'destructive' },
  ]

  return {
    slices: slices.filter((slice) => slice.value > 0),
    total: stock.length,
    low,
    out,
  }
}

export type ChartMetric = 'value' | 'quantity'

export type Bar = { id: string; label: string; value: number; unitCode?: string }

/** The products that sold the most, biggest first, capped so the bars stay readable. */
export function topProducts(sales: ProductSales[], metric: ChartMetric, limit = 6): Bar[] {
  return [...sales]
    .map((row) => ({
      id: row.productId,
      label: row.productName,
      value: metric === 'value' ? row.salesValue : row.quantitySold,
      unitCode: row.unitCode,
    }))
    .filter((row) => row.value > 0)
    .sort((a, b) => b.value - a.value)
    .slice(0, limit)
}

/**
 * The same sales rolled up by category. Quantities are not summed here: a category can mix
 * kilograms and packets, and adding those together would be meaningless.
 */
export function categorySales(sales: ProductSales[], limit = 6): Bar[] {
  const totals = new Map<string, number>()

  for (const row of sales) {
    totals.set(row.categoryName, (totals.get(row.categoryName) ?? 0) + row.salesValue)
  }

  return [...totals.entries()]
    .map(([label, value]) => ({ id: label, label, value }))
    .filter((row) => row.value > 0)
    .sort((a, b) => b.value - a.value)
    .slice(0, limit)
}
