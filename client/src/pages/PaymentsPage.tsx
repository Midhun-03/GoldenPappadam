import { useQuery } from '@tanstack/react-query'
import { Wallet } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { customersApi, paymentsApi } from '@/api/sales'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney, formatPaymentMethod } from '@/lib/format'
import { RecordPaymentDialog } from './RecordPaymentDialog'

const ALL = 'all'

export function PaymentsPage() {
  const [customerId, setCustomerId] = useState(ALL)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [isDialogOpen, setIsDialogOpen] = useState(false)

  const customers = useQuery({ queryKey: ['customers', {}], queryFn: () => customersApi.list() })

  const filters = {
    customerId: customerId === ALL ? undefined : customerId,
    from: from || undefined,
    to: to || undefined,
  }
  const payments = useQuery({ queryKey: ['payments', filters], queryFn: () => paymentsApi.list(filters) })

  const rows = payments.data ?? []
  const received = rows.reduce((sum, payment) => sum + payment.amount, 0)
  const isFiltered = customerId !== ALL || from !== '' || to !== ''

  return (
    <>
      <PageHeader
        title="Payments"
        description={
          payments.isPending ? undefined : (
            <span className="tabular-nums">
              {rows.length} shown · <span className="font-medium text-success">{formatMoney(received)}</span>{' '}
              received
            </span>
          )
        }
        action={
          <Button onClick={() => setIsDialogOpen(true)}>
            <Wallet className="size-4" />
            Record payment
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Customer" htmlFor="payment-customer" className="col-span-2 sm:w-56">
          <Select value={customerId} onValueChange={setCustomerId}>
            <SelectTrigger id="payment-customer" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>All customers</SelectItem>
              {(customers.data ?? []).map((customer) => (
                <SelectItem key={customer.id} value={customer.id}>
                  {customer.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FilterField>

        <FilterField label="From" htmlFor="payment-from">
          <Input
            id="payment-from"
            type="date"
            value={from}
            max={to || undefined}
            onChange={(event) => setFrom(event.target.value)}
          />
        </FilterField>

        <FilterField label="To" htmlFor="payment-to">
          <Input
            id="payment-to"
            type="date"
            value={to}
            min={from || undefined}
            onChange={(event) => setTo(event.target.value)}
          />
        </FilterField>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {payments.isPending ? (
            <TableSkeleton columns={4} />
          ) : payments.isError ? (
            <ErrorState error={payments.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Wallet}
              title={isFiltered ? 'No payments match these filters' : 'No payments recorded yet'}
              description={
                isFiltered
                  ? 'Try a wider date range, or clear the customer filter.'
                  : 'Record money as it comes in and it will settle the oldest bills first.'
              }
              action={
                isFiltered ? undefined : (
                  <Button onClick={() => setIsDialogOpen(true)}>
                    <Wallet className="size-4" />
                    Record payment
                  </Button>
                )
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Customer</TableHead>
                  <TableHead className="hidden sm:table-cell">Method</TableHead>
                  <TableHead className="hidden md:table-cell">Applied to</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((payment) => (
                  <TableRow key={payment.id}>
                    <TableCell className="max-w-[14rem]">
                      <Link className="block truncate font-medium hover:underline" to={`/customers/${payment.customerId}`}>
                        {payment.customerName}
                      </Link>
                      <div className="mt-0.5 text-xs text-muted-foreground">
                        {formatDay(payment.paymentDate)}
                        <span className="sm:hidden"> · {formatPaymentMethod(payment.method)}</span>
                      </div>
                    </TableCell>

                    <TableCell className="hidden sm:table-cell">
                      <Badge variant="outline">{formatPaymentMethod(payment.method)}</Badge>
                      {payment.reference && (
                        <div className="mt-0.5 font-mono text-xs text-muted-foreground">{payment.reference}</div>
                      )}
                    </TableCell>

                    <TableCell className="hidden max-w-[16rem] text-sm text-muted-foreground md:table-cell">
                      {payment.allocations.length === 0 ? (
                        <Badge variant="warning">On account</Badge>
                      ) : (
                        <div className="flex flex-wrap gap-x-2 gap-y-0.5">
                          {payment.allocations.map((allocation) => (
                            <Link
                              key={allocation.invoiceId}
                              className="font-mono text-xs underline-offset-4 hover:underline"
                              to={`/invoices/${allocation.invoiceId}`}
                            >
                              {allocation.invoiceNumber}
                            </Link>
                          ))}
                          {payment.unallocatedAmount > 0 && (
                            <span className="text-xs">
                              · {formatMoney(payment.unallocatedAmount)} on account
                            </span>
                          )}
                        </div>
                      )}
                    </TableCell>

                    <TableCell className="text-right font-medium tabular-nums text-success">
                      {formatMoney(payment.amount)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <RecordPaymentDialog open={isDialogOpen} onOpenChange={setIsDialogOpen} />
    </>
  )
}
