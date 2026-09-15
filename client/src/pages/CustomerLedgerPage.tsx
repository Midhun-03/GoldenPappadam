import { useQuery } from '@tanstack/react-query'
import { FileText, Plus, Scroll, Wallet } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { customersApi, invoicesApi } from '@/api/sales'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import { RecordPaymentDialog } from './RecordPaymentDialog'

export function CustomerLedgerPage() {
  const { customerId = '' } = useParams()
  const [isPaymentOpen, setIsPaymentOpen] = useState(false)

  const customer = useQuery({ queryKey: ['customers', customerId], queryFn: () => customersApi.get(customerId) })
  const ledger = useQuery({
    queryKey: ['customers', customerId, 'ledger'],
    queryFn: () => customersApi.ledger(customerId),
  })
  const invoices = useQuery({
    queryKey: ['invoices', { customerId }],
    queryFn: () => invoicesApi.list({ customerId }),
  })

  const balance = customer.data?.balance ?? 0
  const contact = customer.data
    ? [customer.data.contactPerson, customer.data.phone, customer.data.address].filter(Boolean).join(' · ')
    : ''

  if (customer.isError) {
    return (
      <>
        <PageHeader back={{ to: '/customers', label: 'Customers' }} title="Customer" />
        <Card>
          <CardContent className="px-0">
            <ErrorState error={customer.error} />
          </CardContent>
        </Card>
      </>
    )
  }

  return (
    <>
      <PageHeader
        back={{ to: '/customers', label: 'Customers' }}
        title={customer.data?.name ?? 'Customer'}
        description={
          customer.isPending ? <Skeleton className="h-4 w-64" /> : contact || 'No contact details yet.'
        }
        action={
          <>
            <Button variant="outline" asChild>
              <Link to={`/invoices/new?customerId=${customerId}`}>
                <Plus className="size-4" />
                New bill
              </Link>
            </Button>
            <Button onClick={() => setIsPaymentOpen(true)}>
              <Wallet className="size-4" />
              Record payment
            </Button>
          </>
        }
      />

      <div className="mb-4 grid min-w-0 grid-cols-2 gap-px overflow-hidden rounded-xl border bg-border lg:mb-5 lg:grid-cols-4">
        <div className="bg-card p-4">
          <div className="text-xs font-medium text-muted-foreground">Balance</div>
          {customer.isPending ? (
            <Skeleton className="mt-2 h-7 w-24" />
          ) : (
            <div
              className={cn(
                'mt-1.5 font-heading text-2xl font-semibold tabular-nums',
                balance > 0 && 'text-destructive',
                balance < 0 && 'text-success',
              )}
            >
              {formatMoney(balance)}
            </div>
          )}
          <div className="mt-1 text-xs text-muted-foreground">
            {balance > 0 ? 'owed to you' : balance < 0 ? 'paid in advance' : 'all settled'}
          </div>
        </div>

        <div className="bg-card p-4">
          <div className="text-xs font-medium text-muted-foreground">Opening balance</div>
          <div className="mt-1.5 font-heading text-2xl font-semibold tabular-nums">
            {formatMoney(customer.data?.openingBalance ?? 0)}
          </div>
          <div className="mt-1 text-xs text-muted-foreground">before this app</div>
        </div>

        <div className="bg-card p-4">
          <div className="text-xs font-medium text-muted-foreground">Bills</div>
          <div className="mt-1.5 font-heading text-2xl font-semibold tabular-nums">
            {invoices.data?.length ?? 0}
          </div>
          <div className="mt-1 text-xs text-muted-foreground">all time</div>
        </div>

        <div className="bg-card p-4">
          <div className="text-xs font-medium text-muted-foreground">Unpaid bills</div>
          <div className="mt-1.5 font-heading text-2xl font-semibold tabular-nums">
            {(invoices.data ?? []).filter((invoice) => invoice.outstanding > 0).length}
          </div>
          <div className="mt-1 text-xs text-muted-foreground">still to settle</div>
        </div>
      </div>

      {customer.data?.notes && (
        <p className="mb-4 rounded-lg border bg-card px-3 py-2.5 text-sm text-muted-foreground lg:mb-5">
          {customer.data.notes}
        </p>
      )}

      <div className="grid gap-4 xl:grid-cols-2 xl:gap-5">
        <Card>
          <CardHeader>
            <CardTitle>Account statement</CardTitle>
          </CardHeader>
          <CardContent className="px-0">
            {ledger.isPending ? (
              <TableSkeleton columns={4} />
            ) : ledger.isError ? (
              <ErrorState error={ledger.error} />
            ) : ledger.data.length === 0 ? (
              <EmptyState
                icon={Scroll}
                title="Nothing billed or paid yet"
                description="Bills and payments for this shop will appear here in order."
              />
            ) : (
              <Table>
                <TableHeader sticky>
                  <TableRow>
                    <TableHead>Entry</TableHead>
                    <TableHead className="text-right">Billed</TableHead>
                    <TableHead className="text-right">Paid</TableHead>
                    <TableHead className="text-right">Balance</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {ledger.data.map((entry, index) => (
                    <TableRow key={`${entry.entryType}-${entry.reference}-${index}`}>
                      <TableCell className="max-w-[7.5rem] sm:max-w-[12rem]">
                        <Badge variant={entry.entryType === 'Payment' ? 'success' : 'outline'}>
                          {entry.entryType}
                        </Badge>
                        <div className="mt-0.5 truncate text-xs text-muted-foreground">
                          {formatDay(entry.date)} · <span className="font-mono">{entry.reference}</span>
                        </div>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {entry.billed ? formatMoney(entry.billed) : <span className="text-muted-foreground">—</span>}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {entry.paid ? (
                          <span className="text-success">{formatMoney(entry.paid)}</span>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {formatMoney(entry.balance)}
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
            <CardTitle>Bills</CardTitle>
          </CardHeader>
          <CardContent className="px-0">
            {invoices.isPending ? (
              <TableSkeleton columns={3} />
            ) : invoices.isError ? (
              <ErrorState error={invoices.error} />
            ) : invoices.data.length === 0 ? (
              <EmptyState
                icon={FileText}
                title="No bills for this shop yet"
                action={
                  <Button asChild>
                    <Link to={`/invoices/new?customerId=${customerId}`}>
                      <Plus className="size-4" />
                      New bill
                    </Link>
                  </Button>
                }
              />
            ) : (
              <Table>
                <TableHeader sticky>
                  <TableRow>
                    <TableHead>Bill</TableHead>
                    <TableHead className="text-right">Total</TableHead>
                    <TableHead className="text-right">Outstanding</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {invoices.data.map((invoice) => (
                    <TableRow key={invoice.id} className={invoice.status === 'Cancelled' ? 'opacity-60' : undefined}>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Link
                            className="font-mono text-xs font-medium underline-offset-4 hover:underline"
                            to={`/invoices/${invoice.id}`}
                          >
                            {invoice.invoiceNumber}
                          </Link>
                          {invoice.status === 'Cancelled' && <Badge variant="outline">Cancelled</Badge>}
                        </div>
                        <div className="mt-0.5 text-xs text-muted-foreground">{formatDay(invoice.invoiceDate)}</div>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatMoney(invoice.totalAmount)}</TableCell>
                      <TableCell
                        className={cn(
                          'text-right tabular-nums',
                          invoice.outstanding > 0 ? 'font-medium text-destructive' : 'text-muted-foreground',
                        )}
                      >
                        {invoice.outstanding > 0 ? formatMoney(invoice.outstanding) : 'Settled'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      <RecordPaymentDialog open={isPaymentOpen} onOpenChange={setIsPaymentOpen} customerId={customerId} />
    </>
  )
}
