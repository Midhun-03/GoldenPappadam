import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, IndianRupee, Route, Store } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { fieldSalesApi } from '@/api/fieldsales'
import { customerPriceChangesApi } from '@/api/sales'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime, formatMoney, todayInIndia } from '@/lib/format'
import { PriceChangesTable } from './PriceChangesTable'

/** How long the sale sat on the phone before the server heard about it. */
function SyncGap({ recordedAt, receivedAt }: { recordedAt: string; receivedAt: string }) {
  const minutes = Math.round(
    (new Date(receivedAt).getTime() - new Date(recordedAt).getTime()) / 60_000,
  )

  if (minutes < 2) {
    return <Badge variant="success">Live</Badge>
  }

  const gap = minutes < 60 ? `${minutes}m` : `${Math.round(minutes / 60)}h`

  return (
    <div>
      <Badge variant="info">Synced</Badge>
      <div className="mt-0.5 text-xs text-muted-foreground">{gap} offline</div>
    </div>
  )
}

const outcomes: Record<string, string> = {
  Sold: 'Sold',
  NoOrder: 'No order',
  Closed: 'Shop closed',
  Skipped: 'Skipped',
}

export function FieldSalesDayPage() {
  const [businessDate, setBusinessDate] = useState(todayInIndia())

  const rateChanges = useQuery({
    queryKey: ['price-changes', 'sales-team', businessDate],
    queryFn: () =>
      customerPriceChangesApi.list({ salespersonOnly: true, from: businessDate, to: businessDate }),
  })

  const day = useQuery({
    queryKey: ['fieldsales', 'day', businessDate],
    queryFn: () => fieldSalesApi.day(businessDate),
  })

  const tiles = [
    { label: 'Sales', value: formatMoney(day.data?.totalSales ?? 0), note: `${day.data?.saleCount ?? 0} bills` },
    { label: 'Shops visited', value: String(day.data?.shopsVisited ?? 0), note: 'stops on the route' },
    { label: 'Cash collected', value: formatMoney(day.data?.cashCollected ?? 0), note: 'paid on the spot' },
    { label: 'Sold on credit', value: formatMoney(day.data?.creditSales ?? 0), note: 'settled later' },
    {
      label: 'Outstanding today',
      value: formatMoney(day.data?.outstandingCreatedToday ?? 0),
      note: 'still owed from today',
    },
  ]

  return (
    <>
      <PageHeader
        title="Today on the road"
        description="Only what has reached the server. Anything still on a phone with no signal appears once it syncs."
      />

      <FilterBar>
        <FilterField label="Day" htmlFor="field-day" className="col-span-2 sm:w-44">
          <Input
            id="field-day"
            type="date"
            value={businessDate}
            onChange={(event) => setBusinessDate(event.target.value)}
          />
        </FilterField>
      </FilterBar>

      <div className="mb-4 grid min-w-0 grid-cols-2 gap-px overflow-hidden rounded-xl border bg-border lg:mb-5 lg:grid-cols-5">
        {tiles.map((tile) => (
          <div key={tile.label} className="bg-card p-4">
            <div className="text-xs font-medium text-muted-foreground">{tile.label}</div>
            <div className="mt-1.5 font-heading text-xl font-semibold tabular-nums lg:text-2xl">{tile.value}</div>
            <div className="mt-1 text-xs text-muted-foreground">{tile.note}</div>
          </div>
        ))}
      </div>

      {(day.data?.priceMismatchCount ?? 0) > 0 && (
        <div className="mb-4 flex items-start gap-2.5 rounded-lg border border-warning/30 bg-warning/5 px-3 py-2.5 text-sm text-warning lg:mb-5">
          <AlertTriangle className="mt-0.5 size-4 shrink-0" />
          <p>
            {day.data!.priceMismatchCount} {day.data!.priceMismatchCount === 1 ? 'bill was' : 'bills were'} charged
            at a price that has since changed. They keep the price the shop was told — cancel and re-bill if that
            is wrong.
          </p>
        </div>
      )}

      <Card className="mb-4 lg:mb-5">
        <CardHeader>
          <CardTitle>Sales</CardTitle>
        </CardHeader>
        <CardContent className="px-0">
          {day.isPending ? (
            <TableSkeleton columns={5} />
          ) : day.isError ? (
            <ErrorState error={day.error} />
          ) : day.data.sales.length === 0 ? (
            <EmptyState
              icon={Route}
              title="Nothing has come in for this day"
              description="Sales recorded on the phone appear here as soon as it finds signal."
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Time</TableHead>
                  <TableHead>Shop</TableHead>
                  <TableHead className="hidden lg:table-cell">Products</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Payment</TableHead>
                  <TableHead className="hidden md:table-cell">Salesperson</TableHead>
                  <TableHead>Sync</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {day.data.sales.map((sale) => (
                  <TableRow key={sale.invoiceId} className={sale.status === 'Cancelled' ? 'opacity-60' : undefined}>
                    <TableCell className="whitespace-nowrap text-xs text-muted-foreground">
                      {formatDateTime(sale.recordedAt)}
                    </TableCell>
                    <TableCell className="max-w-[9rem] truncate">
                      <Link
                        to={`/invoices/${sale.invoiceId}`}
                        className="underline-offset-4 hover:underline"
                      >
                        {sale.customerName}
                      </Link>
                      <div className="mt-0.5 flex items-center gap-1.5">
                        <span className="font-mono text-xs text-muted-foreground">{sale.invoiceNumber}</span>
                        {sale.priceMismatch && <Badge variant="warning">Price changed</Badge>}
                      </div>
                    </TableCell>
                    <TableCell className="hidden max-w-[16rem] truncate text-xs text-muted-foreground lg:table-cell">
                      {sale.products}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoney(sale.totalAmount)}</TableCell>
                    <TableCell className="hidden text-right sm:table-cell">
                      {sale.amountPaid >= sale.totalAmount ? (
                        <Badge variant="success">Paid</Badge>
                      ) : sale.amountPaid > 0 ? (
                        <Badge variant="info">Part {formatMoney(sale.amountPaid)}</Badge>
                      ) : (
                        <Badge variant="outline">Credit</Badge>
                      )}
                    </TableCell>
                    <TableCell className="hidden max-w-[8rem] truncate text-xs md:table-cell">
                      {sale.salesperson}
                      <div className="text-muted-foreground">{sale.deviceName}</div>
                    </TableCell>
                    <TableCell>
                      <SyncGap recordedAt={sale.recordedAt} receivedAt={sale.receivedAt} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Visits</CardTitle>
        </CardHeader>
        <CardContent className="px-0">
          {day.isPending ? (
            <TableSkeleton columns={3} />
          ) : day.isError ? (
            <ErrorState error={day.error} />
          ) : day.data.visits.length === 0 ? (
            <EmptyState
              icon={Store}
              title="No visits recorded"
              description="Stops that sold nothing are worth recording too — they show where the route went."
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Time</TableHead>
                  <TableHead>Shop</TableHead>
                  <TableHead>Outcome</TableHead>
                  <TableHead className="hidden sm:table-cell">Notes</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {day.data.visits.map((visit) => (
                  <TableRow key={`${visit.customerId}-${visit.visitedAt}`}>
                    <TableCell className="whitespace-nowrap text-xs text-muted-foreground">
                      {formatDateTime(visit.visitedAt)}
                    </TableCell>
                    <TableCell className="max-w-[10rem] truncate">
                      <Link to={`/customers/${visit.customerId}`} className="underline-offset-4 hover:underline">
                        {visit.customerName}
                      </Link>
                      <div className="text-xs text-muted-foreground">{visit.salesperson}</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={visit.outcome === 'Sold' ? 'success' : 'outline'}>
                        {outcomes[visit.outcome] ?? visit.outcome}
                      </Badge>
                    </TableCell>
                    <TableCell className="hidden max-w-[14rem] truncate text-xs text-muted-foreground sm:table-cell">
                      {visit.notes ?? '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card className="mt-4 lg:mt-5">
        <CardHeader>
          <CardTitle>Rates changed by the sales team</CardTitle>
        </CardHeader>
        <CardContent className="px-0">
          {rateChanges.isPending ? (
            <TableSkeleton columns={3} />
          ) : rateChanges.isError ? (
            <ErrorState error={rateChanges.error} />
          ) : rateChanges.data.length === 0 ? (
            <EmptyState
              icon={IndianRupee}
              title="No rates changed on the road"
              description="When a salesperson agrees a new rate with a shop, it appears here for you to check."
            />
          ) : (
            <PriceChangesTable changes={rateChanges.data} showCustomer />
          )}
        </CardContent>
      </Card>
    </>
  )
}
