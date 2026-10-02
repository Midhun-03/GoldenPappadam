import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowUpRight,
  Boxes,
  FileText,
  Package,
  PieChart as PieChartIcon,
  Plus,
  RefreshCw,
  TrendingUp,
  Wallet,
} from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { dashboardApi } from '@/api/dashboard'
import { stockApi } from '@/api/inventory'
import { invoicesApi } from '@/api/sales'
import type { StockAgeAlerts } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney, formatQuantity, hourInIndia, todayInIndia } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  categorySales,
  changeAgainst,
  comparisonPeriods,
  dailySeries,
  earliestDayNeeded,
  paymentMix,
  previousRange,
  rangeLabels,
  rangeStart,
  startOfMonth,
  stockHealth,
  topProducts,
  totalBetween,
  type RangeKey,
} from './dashboard/analytics'
import { DonutChart } from './dashboard/DonutChart'
import { KpiCards, type Kpi } from './dashboard/KpiCards'
import { SalesTrendChart } from './dashboard/SalesTrendChart'
import { TopProductsChart } from './dashboard/TopProductsChart'

type ProductView = 'value' | 'quantity' | 'category'

function greeting() {
  const hour = hourInIndia()
  if (hour < 12) return 'Good morning'
  if (hour < 17) return 'Good afternoon'
  return 'Good evening'
}

function Section({
  title,
  icon: Icon,
  subtitle,
  action,
  children,
  className,
}: {
  title: string
  icon: typeof Boxes
  subtitle?: string
  action?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={cn('@container flex min-w-0 flex-col rounded-xl border bg-card', className)}>
      <div className="flex min-h-12 items-center gap-2 border-b px-4 py-2">
        <Icon className="size-4 shrink-0 text-muted-foreground" />
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-sm font-semibold">{title}</h2>
          {subtitle && <p className="truncate text-xs text-muted-foreground">{subtitle}</p>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
      <div className="flex-1">{children}</div>
    </section>
  )
}

function ViewAll({ to }: { to: string }) {
  return (
    <Button variant="ghost" size="xs" asChild>
      <Link to={to} aria-label="View all">
        <span className="hidden @sm:inline">View all</span>
        <ArrowUpRight />
      </Link>
    </Button>
  )
}

function ChartSkeleton({ height }: { height: number }) {
  return (
    <div className="flex items-end gap-1.5 p-4" style={{ height }}>
      {Array.from({ length: 12 }, (_, index) => (
        <Skeleton key={index} className="flex-1" style={{ height: `${30 + ((index * 37) % 60)}%` }} />
      ))}
    </div>
  )
}

export function DashboardPage() {
  const queryClient = useQueryClient()
  const [range, setRange] = useState<RangeKey>('30d')
  const [productView, setProductView] = useState<ProductView>('value')

  const summary = useQuery({ queryKey: ['dashboard'], queryFn: dashboardApi.summary })
  const today = summary.data?.today ?? todayInIndia()

  // One request covers every range the selector offers and the previous month they are
  // compared against, so changing the range never refetches the bills.
  const historyFrom = earliestDayNeeded(today)
  const bills = useQuery({
    queryKey: ['invoices', { from: historyFrom, to: today }],
    queryFn: () => invoicesApi.list({ from: historyFrom, to: today }),
  })

  const from = rangeStart(range, today)
  const productSales = useQuery({
    queryKey: ['dashboard', 'product-sales', from, today],
    queryFn: () => dashboardApi.productSales(from, today),
  })

  const stock = useQuery({ queryKey: ['stock', {}], queryFn: () => stockApi.onHand() })

  const isRefreshing =
    summary.isFetching || bills.isFetching || productSales.isFetching || stock.isFetching

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      queryClient.invalidateQueries({ queryKey: ['invoices'] }),
      queryClient.invalidateQueries({ queryKey: ['stock'] }),
    ])

  const invoices = bills.data ?? []
  const { yesterday, previousMonthStart, previousMonthSameDay } = comparisonPeriods(today)

  const inRange = totalBetween(invoices, from, today)
  const todayTotal = totalBetween(invoices, today, today)
  const yesterdayTotal = totalBetween(invoices, yesterday, yesterday)
  const monthToDate = totalBetween(invoices, startOfMonth(today), today)
  const lastMonthToSameDay = totalBetween(invoices, previousMonthStart, previousMonthSameDay)
  const before = previousRange(range, today)
  const beforeTotal = before ? totalBetween(invoices, before.from, before.to) : null

  const health = stockHealth(stock.data ?? [])
  const mix = paymentMix(invoices, from, today)
  const billedInRange = mix.reduce((sum, slice) => sum + slice.value, 0)

  const bars =
    productView === 'category'
      ? categorySales(productSales.data ?? [])
      : topProducts(productSales.data ?? [], productView)

  const kpis: Kpi[] = [
    {
      key: 'today',
      label: "Today's sales",
      value: summary.data?.todaySales ?? 0,
      kind: 'money',
      hint: `${summary.data?.todayInvoiceCount ?? 0} bills today`,
      icon: TrendingUp,
      to: '/invoices',
      change: bills.isPending ? null : changeAgainst(todayTotal.total, yesterdayTotal.total),
      changeLabel: 'against yesterday',
    },
    // Follows the range picked above, so "Last 30 days" shows the last 30 days here too - not only
    // in the charts. This month keeps the server's own figure.
    before && beforeTotal
      ? {
          key: 'range',
          label: rangeLabels[range],
          value: inRange.total,
          kind: 'money',
          hint: `${inRange.count} bills since ${formatDay(from)}`,
          icon: FileText,
          to: '/invoices',
          change: bills.isPending ? null : changeAgainst(inRange.total, beforeTotal.total),
          changeLabel: `against the ${before.days} days before`,
        }
      : {
          key: 'month',
          label: 'This month',
          value: summary.data?.monthSales ?? 0,
          kind: 'money',
          hint: `${summary.data?.monthInvoiceCount ?? 0} bills this month`,
          icon: FileText,
          to: '/invoices',
          change: bills.isPending ? null : changeAgainst(monthToDate.total, lastMonthToSameDay.total),
          changeLabel: 'against the same days last month',
        },
    {
      key: 'outstanding',
      label: 'Outstanding',
      value: summary.data?.outstandingTotal ?? 0,
      kind: 'money',
      hint: `across ${summary.data?.activeCustomers ?? 0} active customers`,
      icon: Wallet,
      to: '/customers',
      tone: 'owed',
    },
    {
      key: 'products',
      label: 'Products',
      value: health.total,
      kind: 'count',
      hint: 'active products',
      icon: Package,
      to: '/products',
    },
    {
      key: 'low',
      // A product sitting at zero with no threshold set is not "low", but it still needs
      // attention, so the tile counts both rather than quietly reading zero.
      label: 'Needs attention',
      value: health.low + health.out,
      kind: 'count',
      hint:
        health.low + health.out === 0
          ? 'every product is stocked'
          : `${health.low} low · ${health.out} out of stock`,
      icon: Boxes,
      to: '/stock',
      tone: 'alert',
    },
  ]

  if (summary.isError) {
    return (
      <>
        <h1 className="mb-5 font-heading text-xl font-semibold tracking-tight sm:text-2xl">Dashboard</h1>
        <div className="rounded-xl border bg-card">
          <ErrorState error={summary.error} />
        </div>
      </>
    )
  }

  return (
    <>
      <div className="mb-5 flex flex-col gap-3 sm:mb-6 sm:flex-row sm:items-start sm:justify-between sm:gap-6">
        <div className="min-w-0">
          <p className="text-sm text-muted-foreground">{greeting()}</p>
          <h1 className="font-heading text-xl font-semibold tracking-tight sm:text-2xl">Dashboard</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Business day {formatDay(today)}, Indian Standard Time.
          </p>
        </div>

        <div className="flex shrink-0 flex-wrap items-center gap-2">
          <Select value={range} onValueChange={(value) => setRange(value as RangeKey)}>
            <SelectTrigger aria-label="Chart date range" className="w-40">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {(Object.keys(rangeLabels) as RangeKey[]).map((key) => (
                <SelectItem key={key} value={key}>
                  {rangeLabels[key]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button variant="outline" size="icon" aria-label="Refresh" onClick={refresh} disabled={isRefreshing}>
            <RefreshCw className={cn('size-4', isRefreshing && 'animate-spin')} />
          </Button>

          <Button asChild>
            <Link to="/invoices/new">
              <Plus className="size-4" />
              New bill
            </Link>
          </Button>
        </div>
      </div>

      <div className="flex flex-col gap-4 lg:gap-5">
        <KpiCards kpis={kpis} isLoading={summary.isPending} />

        <div className="grid gap-4 lg:grid-cols-3 lg:gap-5">
          <Section
            title="Sales trend"
            icon={TrendingUp}
            subtitle={rangeLabels[range].toLowerCase()}
            className="lg:col-span-2"
            action={<ViewAll to="/invoices" />}
          >
            {bills.isPending ? (
              <ChartSkeleton height={248} />
            ) : bills.isError ? (
              <ErrorState error={bills.error} />
            ) : inRange.count === 0 ? (
              <EmptyState
                icon={TrendingUp}
                title="No sales in this period"
                description="Bills you create will appear here as a daily trend."
              />
            ) : (
              <div className="p-4">
                <SalesTrendChart points={dailySeries(invoices, from, today)} />
              </div>
            )}
          </Section>

          <Section
            title="Money back"
            icon={PieChartIcon}
            subtitle={`of what was billed ${rangeLabels[range].toLowerCase()}`}
          >
            {bills.isPending ? (
              <ChartSkeleton height={190} />
            ) : mix.length === 0 ? (
              <EmptyState icon={PieChartIcon} title="Nothing billed in this period" />
            ) : (
              <div className="p-4">
                <DonutChart
                  slices={mix}
                  centreValue={formatMoney(billedInRange)}
                  centreLabel="billed"
                  formatValue={formatMoney}
                />
              </div>
            )}
          </Section>
        </div>

        <div className="grid gap-4 lg:grid-cols-3 lg:gap-5">
          <Section
            title="Sales by product"
            icon={Package}
            subtitle={`${rangeLabels[range].toLowerCase()}, before bill discounts`}
            className="lg:col-span-2"
            action={
              <Select value={productView} onValueChange={(value) => setProductView(value as ProductView)}>
                <SelectTrigger size="sm" aria-label="Product chart measure" className="w-32">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="value">By value</SelectItem>
                  <SelectItem value="quantity">By quantity</SelectItem>
                  <SelectItem value="category">By category</SelectItem>
                </SelectContent>
              </Select>
            }
          >
            {productSales.isPending ? (
              <ChartSkeleton height={248} />
            ) : productSales.isError ? (
              <ErrorState error={productSales.error} />
            ) : bars.length === 0 ? (
              <EmptyState
                icon={Package}
                title="Nothing sold in this period"
                description="Once bills are raised, the products behind them show up here."
              />
            ) : (
              <div className="p-4">
                <TopProductsChart bars={bars} metric={productView === 'quantity' ? 'quantity' : 'value'} />
              </div>
            )}
          </Section>

          <Section title="Stock health" icon={Boxes} action={<ViewAll to="/stock" />}>
            {stock.isPending ? (
              <ChartSkeleton height={190} />
            ) : stock.isError ? (
              <ErrorState error={stock.error} />
            ) : health.total === 0 ? (
              <EmptyState
                icon={Boxes}
                title="No products yet"
                action={
                  <Button asChild>
                    <Link to="/products">
                      <Plus className="size-4" />
                      Add a product
                    </Link>
                  </Button>
                }
              />
            ) : (
              <div className="p-4">
                <DonutChart
                  slices={health.slices}
                  centreValue={String(health.total)}
                  centreLabel={health.total === 1 ? 'product' : 'products'}
                  formatValue={(value) => String(value)}
                />
                <StockAgeStrip alerts={summary.data?.stockAge} />
              </div>
            )}
          </Section>
        </div>

        <div className="grid gap-4 lg:grid-cols-3 lg:gap-5">
          <Section
            title="Recent bills"
            icon={FileText}
            className="lg:col-span-2"
            action={<ViewAll to="/invoices" />}
          >
            {summary.isPending ? (
              <div className="grid gap-3 p-4">
                {Array.from({ length: 5 }, (_, index) => (
                  <Skeleton key={index} className="h-5" />
                ))}
              </div>
            ) : summary.data.recentInvoices.length === 0 ? (
              <EmptyState
                icon={FileText}
                title="No bills yet"
                description="Create the first bill and it will appear here."
                action={
                  <Button asChild>
                    <Link to="/invoices/new">
                      <Plus className="size-4" />
                      New bill
                    </Link>
                  </Button>
                }
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="pl-4">Bill</TableHead>
                    <TableHead>Customer</TableHead>
                    <TableHead className="hidden text-right sm:table-cell">Total</TableHead>
                    <TableHead className="pr-4 text-right">Outstanding</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {summary.data.recentInvoices.map((invoice) => (
                    <TableRow key={invoice.id}>
                      <TableCell className="pl-4">
                        <Link
                          className="font-mono text-xs font-medium underline-offset-4 hover:underline"
                          to={`/invoices/${invoice.id}`}
                        >
                          {invoice.invoiceNumber}
                        </Link>
                        <div className="text-xs text-muted-foreground">{formatDay(invoice.invoiceDate)}</div>
                      </TableCell>
                      <TableCell className="max-w-[8rem] truncate sm:max-w-[10rem]">
                        {invoice.customerName}
                        {invoice.status === 'Cancelled' && (
                          <Badge variant="outline" className="ml-2">
                            Cancelled
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="hidden text-right tabular-nums sm:table-cell">
                        {formatMoney(invoice.totalAmount)}
                      </TableCell>
                      <TableCell
                        className={cn(
                          'pr-4 text-right tabular-nums',
                          invoice.outstanding > 0 ? 'font-medium text-destructive' : 'text-muted-foreground',
                        )}
                      >
                        {invoice.outstanding > 0 ? formatMoney(invoice.outstanding) : 'Settled'}
                        <div className="text-xs font-normal text-muted-foreground sm:hidden">
                          of {formatMoney(invoice.totalAmount)}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Section>

          <div className="flex flex-col gap-4 lg:gap-5">
            <Section title="Needs restocking" icon={Boxes} action={<ViewAll to="/stock" />}>
              {summary.isPending ? (
                <div className="grid gap-3 p-4">
                  {Array.from({ length: 3 }, (_, index) => (
                    <Skeleton key={index} className="h-5" />
                  ))}
                </div>
              ) : summary.data.lowStockProducts.length === 0 ? (
                <EmptyState icon={Boxes} title="Stock is healthy" description="Nothing is below its level." />
              ) : (
                <ul className="divide-y">
                  {summary.data.lowStockProducts.map((product) => (
                    <li key={product.productId}>
                      <Link
                        to={`/stock/${product.productId}`}
                        className="flex items-center justify-between gap-3 px-4 py-2.5 transition-colors hover:bg-muted/60"
                      >
                        <span className="min-w-0 truncate text-sm">{product.name}</span>
                        <Badge variant={product.quantityOnHand <= 0 ? 'destructive' : 'warning'}>
                          {formatQuantity(product.quantityOnHand)} {product.unitCode}
                        </Badge>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </Section>

            <Section
              title="Owes the most"
              icon={Wallet}
              subtitle={
                summary.isPending
                  ? undefined
                  : `${formatMoney(summary.data.outstandingTotal)} outstanding in total`
              }
              action={<ViewAll to="/customers" />}
            >
              {summary.isPending ? (
                <div className="grid gap-3 p-4">
                  {Array.from({ length: 3 }, (_, index) => (
                    <Skeleton key={index} className="h-5" />
                  ))}
                </div>
              ) : summary.data.topOutstanding.length === 0 ? (
                <EmptyState icon={Wallet} title="Nothing outstanding" description="Every bill has been settled." />
              ) : (
                <ul className="divide-y">
                  {summary.data.topOutstanding.map((customer) => (
                    <li key={customer.customerId}>
                      <Link
                        to={`/customers/${customer.customerId}`}
                        className="flex items-center justify-between gap-3 px-4 py-2.5 transition-colors hover:bg-muted/60"
                      >
                        <span className="min-w-0 truncate text-sm">{customer.name}</span>
                        <span className="shrink-0 text-sm font-medium tabular-nums text-destructive">
                          {formatMoney(customer.balance)}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </Section>
          </div>
        </div>
      </div>
    </>
  )
}

/**
 * Stock that needs a decision because of its age - repack it, or write it off - with a way straight
 * to the page where that is done. Silent when there is nothing to do.
 */
function StockAgeStrip({ alerts }: { alerts: StockAgeAlerts | undefined }) {
  if (!alerts || alerts.toRepack + alerts.expiringSoon + alerts.expired === 0) return null

  const items = [
    { count: alerts.expired, label: 'expired', tone: 'text-destructive' },
    { count: alerts.expiringSoon, label: 'expiring in 3 days', tone: 'text-warning' },
    { count: alerts.toRepack, label: 'worth repacking', tone: 'text-foreground' },
  ].filter((item) => item.count > 0)

  return (
    <Link
      to="/stock-age"
      className="mt-3 flex flex-wrap gap-x-4 gap-y-1 rounded-lg border bg-muted/40 px-3 py-2 text-sm hover:bg-muted"
    >
      {items.map((item) => (
        <span key={item.label}>
          <span className={cn('font-semibold tabular-nums', item.tone)}>{item.count}</span>{' '}
          {item.count === 1 ? 'product' : 'products'} {item.label}
        </span>
      ))}
    </Link>
  )
}
