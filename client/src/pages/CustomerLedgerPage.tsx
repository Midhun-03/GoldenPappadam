import { useQuery } from '@tanstack/react-query'
import { ArrowLeft, IndianRupee } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { customersApi, invoicesApi } from '@/api/sales'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import { RecordPaymentDialog } from './RecordPaymentDialog'

export function CustomerLedgerPage() {
  const { customerId = '' } = useParams()
  const [isPaymentOpen, setIsPaymentOpen] = useState(false)

  const customer = useQuery({ queryKey: ['customers', customerId], queryFn: () => customersApi.get(customerId) })
  const ledger = useQuery({ queryKey: ['customers', customerId, 'ledger'], queryFn: () => customersApi.ledger(customerId) })
  const invoices = useQuery({
    queryKey: ['invoices', { customerId }],
    queryFn: () => invoicesApi.list({ customerId }),
  })

  const balance = customer.data?.balance ?? 0

  return (
    <>
      <PageHeader
        title={customer.data?.name ?? 'Customer'}
        description={
          customer.data
            ? [customer.data.contactPerson, customer.data.phone, customer.data.address]
                .filter(Boolean)
                .join(' · ') || 'No contact details yet.'
            : undefined
        }
        action={
          <div className="flex gap-2">
            <Button variant="outline" asChild>
              <Link to="/customers">
                <ArrowLeft className="size-4" />
                All customers
              </Link>
            </Button>
            <Button variant="outline" asChild>
              <Link to={`/invoices/new?customerId=${customerId}`}>New bill</Link>
            </Button>
            <Button onClick={() => setIsPaymentOpen(true)}>
              <IndianRupee className="size-4" />
              Record payment
            </Button>
          </div>
        }
      />

      <Card className="mb-6">
        <CardContent className="flex flex-wrap items-center gap-8 py-1">
          <div>
            <div className="text-sm text-muted-foreground">Balance</div>
            <div
              className={cn(
                'font-heading text-2xl font-semibold tabular-nums',
                balance > 0 && 'text-destructive',
                balance < 0 && 'text-emerald-600',
              )}
            >
              {formatMoney(balance)}
            </div>
            {balance < 0 && <div className="text-xs text-muted-foreground">paid in advance</div>}
          </div>

          <div>
            <div className="text-sm text-muted-foreground">Opening balance</div>
            <div className="mt-1 tabular-nums">{formatMoney(customer.data?.openingBalance ?? 0)}</div>
          </div>

          <div>
            <div className="text-sm text-muted-foreground">Bills</div>
            <div className="mt-1 tabular-nums">{invoices.data?.length ?? 0}</div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Account statement</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Entry</TableHead>
                  <TableHead className="text-right">Billed</TableHead>
                  <TableHead className="text-right">Paid</TableHead>
                  <TableHead className="text-right">Balance</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(ledger.data ?? []).map((entry, index) => (
                  <TableRow key={`${entry.entryType}-${entry.reference}-${index}`}>
                    <TableCell className="whitespace-nowrap text-muted-foreground">
                      {formatDay(entry.date)}
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">{entry.entryType}</Badge>
                      <span className="ml-2 font-mono text-xs text-muted-foreground">{entry.reference}</span>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {entry.billed ? formatMoney(entry.billed) : '—'}
                    </TableCell>
                    <TableCell className="text-right tabular-nums text-emerald-600">
                      {entry.paid ? formatMoney(entry.paid) : '—'}
                    </TableCell>
                    <TableCell className="text-right font-medium tabular-nums">
                      {formatMoney(entry.balance)}
                    </TableCell>
                  </TableRow>
                ))}

                {ledger.data?.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="py-10 text-center text-muted-foreground">
                      Nothing billed or paid yet.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Bills</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead className="text-right">Outstanding</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(invoices.data ?? []).map((invoice) => (
                  <TableRow key={invoice.id}>
                    <TableCell>
                      <Link
                        className="font-mono text-xs underline-offset-4 hover:underline"
                        to={`/invoices/${invoice.id}`}
                      >
                        {invoice.invoiceNumber}
                      </Link>
                      {invoice.status === 'Cancelled' && (
                        <Badge variant="outline" className="ml-2">
                          Cancelled
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-muted-foreground">{formatDay(invoice.invoiceDate)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoney(invoice.totalAmount)}</TableCell>
                    <TableCell
                      className={cn(
                        'text-right tabular-nums',
                        invoice.outstanding > 0 && 'font-medium text-destructive',
                      )}
                    >
                      {formatMoney(invoice.outstanding)}
                    </TableCell>
                  </TableRow>
                ))}

                {invoices.data?.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={4} className="py-10 text-center text-muted-foreground">
                      No bills for this customer yet.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>

      <RecordPaymentDialog open={isPaymentOpen} onOpenChange={setIsPaymentOpen} customerId={customerId} />
    </>
  )
}
