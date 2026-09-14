import { useQuery } from '@tanstack/react-query'
import { IndianRupee } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { customersApi, paymentsApi } from '@/api/sales'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney } from '@/lib/format'
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

  return (
    <>
      <PageHeader
        title="Payments"
        description={`${rows.length} shown · ${formatMoney(received)} received`}
        action={
          <Button onClick={() => setIsDialogOpen(true)}>
            <IndianRupee className="size-4" />
            Record payment
          </Button>
        }
      />

      <Card className="mb-4">
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="grid gap-2">
            <Label>Customer</Label>
            <Select value={customerId} onValueChange={setCustomerId}>
              <SelectTrigger className="w-56">
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
          </div>

          <div className="grid gap-2">
            <Label htmlFor="from">From</Label>
            <Input id="from" type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="to">To</Label>
            <Input id="to" type="date" value={to} onChange={(event) => setTo(event.target.value)} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Date</TableHead>
                <TableHead>Customer</TableHead>
                <TableHead>Method</TableHead>
                <TableHead>Applied to</TableHead>
                <TableHead className="text-right">Amount</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((payment) => (
                <TableRow key={payment.id}>
                  <TableCell className="whitespace-nowrap text-muted-foreground">
                    {formatDay(payment.paymentDate)}
                  </TableCell>
                  <TableCell>
                    <Link className="hover:underline" to={`/customers/${payment.customerId}`}>
                      {payment.customerName}
                    </Link>
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">
                      {payment.method === 'BankTransfer' ? 'Bank transfer' : payment.method}
                    </Badge>
                    {payment.reference && (
                      <span className="ml-2 font-mono text-xs text-muted-foreground">{payment.reference}</span>
                    )}
                  </TableCell>
                  <TableCell className="text-sm text-muted-foreground">
                    {payment.allocations.length === 0
                      ? 'On account'
                      : payment.allocations.map((allocation) => (
                          <Link
                            key={allocation.invoiceId}
                            className="mr-2 font-mono text-xs underline-offset-4 hover:underline"
                            to={`/invoices/${allocation.invoiceId}`}
                          >
                            {allocation.invoiceNumber}
                          </Link>
                        ))}
                    {payment.unallocatedAmount > 0 && payment.allocations.length > 0 && (
                      <span className="text-xs"> · {formatMoney(payment.unallocatedAmount)} on account</span>
                    )}
                  </TableCell>
                  <TableCell className="text-right font-medium tabular-nums">
                    {formatMoney(payment.amount)}
                  </TableCell>
                </TableRow>
              ))}

              {rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="py-10 text-center text-muted-foreground">
                    No payments recorded yet.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <RecordPaymentDialog open={isDialogOpen} onOpenChange={setIsDialogOpen} />
    </>
  )
}
