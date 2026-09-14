import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { dashboardApi } from '@/api/dashboard'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney, formatQuantity } from '@/lib/format'

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <Card>
      <CardContent className="py-1">
        <div className="text-sm text-muted-foreground">{label}</div>
        <div className="mt-1 font-heading text-2xl font-semibold tabular-nums">{value}</div>
        {hint && <div className="mt-1 text-xs text-muted-foreground">{hint}</div>}
      </CardContent>
    </Card>
  )
}

export function DashboardPage() {
  const { data } = useQuery({ queryKey: ['dashboard'], queryFn: dashboardApi.summary })

  return (
    <>
      <PageHeader
        title="Dashboard"
        description={data ? `Business day ${formatDay(data.today)}, Indian Standard Time.` : undefined}
      />

      <div className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat
          label="Today's sales"
          value={formatMoney(data?.todaySales ?? 0)}
          hint={`${data?.todayInvoiceCount ?? 0} bills`}
        />
        <Stat
          label="This month"
          value={formatMoney(data?.monthSales ?? 0)}
          hint={`${data?.monthInvoiceCount ?? 0} bills`}
        />
        <Stat
          label="Outstanding"
          value={formatMoney(data?.outstandingTotal ?? 0)}
          hint={`${data?.activeCustomers ?? 0} active customers`}
        />
        <Stat
          label="Low stock"
          value={String(data?.lowStockCount ?? 0)}
          hint={data?.lowStockCount ? 'products below their level' : 'nothing below its level'}
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Recent bills</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Customer</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead className="text-right">Outstanding</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(data?.recentInvoices ?? []).map((invoice) => (
                  <TableRow key={invoice.id}>
                    <TableCell>
                      <Link className="font-mono text-xs underline-offset-4 hover:underline" to={`/invoices/${invoice.id}`}>
                        {invoice.invoiceNumber}
                      </Link>
                      <div className="text-xs text-muted-foreground">{formatDay(invoice.invoiceDate)}</div>
                    </TableCell>
                    <TableCell>
                      {invoice.customerName}
                      {invoice.status === 'Cancelled' && (
                        <Badge variant="outline" className="ml-2">
                          Cancelled
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoney(invoice.totalAmount)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoney(invoice.outstanding)}</TableCell>
                  </TableRow>
                ))}

                {data?.recentInvoices.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={4} className="py-8 text-center text-muted-foreground">
                      No bills yet.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <div className="grid gap-6">
          <Card>
            <CardHeader>
              <CardTitle>Who owes the most</CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableBody>
                  {(data?.topOutstanding ?? []).map((customer) => (
                    <TableRow key={customer.customerId}>
                      <TableCell>
                        <Link className="hover:underline" to={`/customers/${customer.customerId}`}>
                          {customer.name}
                        </Link>
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {formatMoney(customer.balance)}
                      </TableCell>
                    </TableRow>
                  ))}

                  {data?.topOutstanding.length === 0 && (
                    <TableRow>
                      <TableCell className="py-8 text-center text-muted-foreground">Nothing outstanding.</TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Running low</CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableBody>
                  {(data?.lowStockProducts ?? []).map((product) => (
                    <TableRow key={product.productId}>
                      <TableCell>
                        <Link className="hover:underline" to={`/stock/${product.productId}`}>
                          {product.name}
                        </Link>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatQuantity(product.quantityOnHand)} {product.unitCode}
                      </TableCell>
                    </TableRow>
                  ))}

                  {data?.lowStockProducts.length === 0 && (
                    <TableRow>
                      <TableCell className="py-8 text-center text-muted-foreground">
                        Nothing below its low-stock level.
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </div>
      </div>
    </>
  )
}
