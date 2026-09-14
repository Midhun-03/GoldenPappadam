import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { customersApi, invoicesApi } from '@/api/sales'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'

const ALL = 'all'

export function InvoicesPage() {
  const [customerId, setCustomerId] = useState(ALL)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [unpaidOnly, setUnpaidOnly] = useState(false)

  const customers = useQuery({ queryKey: ['customers', {}], queryFn: () => customersApi.list() })

  const filters = {
    customerId: customerId === ALL ? undefined : customerId,
    from: from || undefined,
    to: to || undefined,
    unpaidOnly,
  }
  const invoices = useQuery({ queryKey: ['invoices', filters], queryFn: () => invoicesApi.list(filters) })

  const rows = invoices.data ?? []
  const billed = rows.filter((i) => i.status === 'Issued').reduce((sum, i) => sum + i.totalAmount, 0)
  const outstanding = rows.reduce((sum, i) => sum + i.outstanding, 0)

  return (
    <>
      <PageHeader
        title="Bills"
        description={`${rows.length} shown · ${formatMoney(billed)} billed · ${formatMoney(outstanding)} outstanding`}
        action={
          <Button asChild>
            <Link to="/invoices/new">
              <Plus className="size-4" />
              New bill
            </Link>
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

          <Button variant={unpaidOnly ? 'default' : 'outline'} onClick={() => setUnpaidOnly(!unpaidOnly)}>
            Unpaid only
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Number</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Customer</TableHead>
                <TableHead className="text-right">Total</TableHead>
                <TableHead className="text-right">Paid</TableHead>
                <TableHead className="text-right">Outstanding</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((invoice) => (
                <TableRow key={invoice.id} className={invoice.status === 'Cancelled' ? 'opacity-60' : undefined}>
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
                  <TableCell>
                    <Link className="hover:underline" to={`/customers/${invoice.customerId}`}>
                      {invoice.customerName}
                    </Link>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">{formatMoney(invoice.totalAmount)}</TableCell>
                  <TableCell className="text-right tabular-nums text-emerald-600">
                    {invoice.amountPaid ? formatMoney(invoice.amountPaid) : '—'}
                  </TableCell>
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

              {rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="py-10 text-center text-muted-foreground">
                    No bills match these filters.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </>
  )
}
