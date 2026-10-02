import { useQuery } from '@tanstack/react-query'
import { ReceiptText, ShoppingCart } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { shopSalesApi } from '@/api/ownShop'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime, formatMoney, formatPaymentMethod, formatQuantity, todayInIndia } from '@/lib/format'

/** The own shop's counter sales, today by default. Cancelled sales are listed but never counted. */
export function ShopSalesPage() {
  const navigate = useNavigate()
  const [from, setFrom] = useState(todayInIndia)
  const [to, setTo] = useState(todayInIndia)

  const filters = { from: from || undefined, to: to || undefined }
  const sales = useQuery({ queryKey: ['own-shop', 'sales', filters], queryFn: () => shopSalesApi.list(filters) })

  const rows = sales.data ?? []
  const completed = rows.filter((row) => row.status === 'Completed')
  const revenue = completed.reduce((sum, row) => sum + row.totalAmount, 0)
  const pieces = completed.reduce((sum, row) => sum + row.pieces, 0)

  return (
    <>
      <PageHeader
        title="Shop sales"
        description={
          sales.isPending ? undefined : (
            <span className="tabular-nums">
              {completed.length} sales · {formatQuantity(pieces)} pieces · {formatMoney(revenue)}
            </span>
          )
        }
        action={
          <Button asChild>
            <Link to="/own-shop/sales/new">
              <ShoppingCart className="size-4" />
              New sale
            </Link>
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="From" htmlFor="shop-sales-from" className="sm:w-40">
          <Input id="shop-sales-from" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        </FilterField>
        <FilterField label="To" htmlFor="shop-sales-to" className="sm:w-40">
          <Input id="shop-sales-to" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        </FilterField>
      </FilterBar>

      <Card>
        <CardContent className="@container px-0">
          {sales.isPending ? (
            <TableSkeleton columns={4} />
          ) : sales.isError ? (
            <ErrorState error={sales.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={ReceiptText}
              title="No shop sales in these dates"
              description="Sales recorded at the counter are listed here."
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Sale</TableHead>
                  <TableHead className="hidden @md:table-cell">Customer</TableHead>
                  <TableHead className="text-right">Pieces</TableHead>
                  <TableHead className="hidden @lg:table-cell">Paid by</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow
                    key={row.id}
                    className="cursor-pointer"
                    onClick={() => navigate(`/own-shop/sales/${row.id}`)}
                  >
                    <TableCell>
                      <Link
                        to={`/own-shop/sales/${row.id}`}
                        className="font-medium underline-offset-4 hover:underline"
                        onClick={(event) => event.stopPropagation()}
                      >
                        {row.saleNumber}
                      </Link>
                      <div className="text-xs text-muted-foreground">{formatDateTime(row.createdAt)}</div>
                      <div className="text-xs text-muted-foreground @md:hidden">{row.customerName ?? 'Walk-in'}</div>
                    </TableCell>
                    <TableCell className="hidden @md:table-cell">
                      {row.customerName ?? <span className="text-muted-foreground">Walk-in</span>}
                      {row.belowStandardRate && (
                        <Badge variant="info" className="ml-2">
                          Lower rate
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{formatQuantity(row.pieces)}</TableCell>
                    <TableCell className="hidden text-muted-foreground @lg:table-cell">
                      {formatPaymentMethod(row.paymentMethod)}
                    </TableCell>
                    <TableCell className="text-right">
                      {row.status === 'Cancelled' ? (
                        <Badge variant="destructive">Cancelled</Badge>
                      ) : (
                        <span className="font-medium tabular-nums">{formatMoney(row.totalAmount)}</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </>
  )
}
