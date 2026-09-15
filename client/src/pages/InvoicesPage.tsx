import { useQuery } from '@tanstack/react-query'
import { FileText, Plus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { customersApi, invoicesApi } from '@/api/sales'
import type { InvoiceListItem } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField, FilterToggle } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'

const ALL = 'all'

type BadgeVariant = 'outline' | 'secondary' | 'success' | 'warning' | 'destructive'

/** Where a bill stands, in one word. */
function settlement(invoice: InvoiceListItem): { label: string; variant: BadgeVariant } {
  if (invoice.status === 'Cancelled') return { label: 'Cancelled', variant: 'outline' }
  if (invoice.outstanding <= 0) return { label: 'Paid', variant: 'success' }
  if (invoice.amountPaid > 0) return { label: 'Part paid', variant: 'warning' }
  return { label: 'Unpaid', variant: 'destructive' }
}

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
  const billed = rows.filter((invoice) => invoice.status === 'Issued').reduce((sum, i) => sum + i.totalAmount, 0)
  const outstanding = rows.reduce((sum, invoice) => sum + invoice.outstanding, 0)
  const isFiltered = customerId !== ALL || from !== '' || to !== '' || unpaidOnly

  return (
    <>
      <PageHeader
        title="Bills"
        description={
          invoices.isPending ? undefined : (
            <span className="tabular-nums">
              {rows.length} shown · {formatMoney(billed)} billed ·{' '}
              <span className={cn(outstanding > 0 && 'font-medium text-destructive')}>
                {formatMoney(outstanding)} outstanding
              </span>
            </span>
          )
        }
        action={
          <Button asChild>
            <Link to="/invoices/new">
              <Plus className="size-4" />
              New bill
            </Link>
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Customer" htmlFor="invoice-customer" className="col-span-2 sm:w-56">
          <Select value={customerId} onValueChange={setCustomerId}>
            <SelectTrigger id="invoice-customer" className="w-full">
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

        <FilterField label="From" htmlFor="invoice-from">
          <Input
            id="invoice-from"
            type="date"
            value={from}
            max={to || undefined}
            onChange={(event) => setFrom(event.target.value)}
          />
        </FilterField>

        <FilterField label="To" htmlFor="invoice-to">
          <Input
            id="invoice-to"
            type="date"
            value={to}
            min={from || undefined}
            onChange={(event) => setTo(event.target.value)}
          />
        </FilterField>

        <FilterToggle
          pressed={unpaidOnly}
          onPressedChange={setUnpaidOnly}
          className="col-span-2 sm:col-span-1"
        >
          Unpaid only
        </FilterToggle>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {invoices.isPending ? (
            <TableSkeleton columns={5} />
          ) : invoices.isError ? (
            <ErrorState error={invoices.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={FileText}
              title={isFiltered ? 'No bills match these filters' : 'No bills yet'}
              description={
                isFiltered
                  ? 'Try a wider date range, or clear the customer filter.'
                  : 'Create a bill when goods go out to a shop.'
              }
              action={
                isFiltered ? undefined : (
                  <Button asChild>
                    <Link to="/invoices/new">
                      <Plus className="size-4" />
                      New bill
                    </Link>
                  </Button>
                )
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Bill</TableHead>
                  <TableHead>Customer</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                  <TableHead className="hidden text-right md:table-cell">Paid</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Outstanding</TableHead>
                  <TableHead className="hidden sm:table-cell">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((invoice) => {
                  const state = settlement(invoice)

                  return (
                    <TableRow key={invoice.id} className={invoice.status === 'Cancelled' ? 'opacity-60' : undefined}>
                      <TableCell>
                        <Link
                          className="font-mono text-xs font-medium underline-offset-4 hover:underline"
                          to={`/invoices/${invoice.id}`}
                        >
                          {invoice.invoiceNumber}
                        </Link>
                        <div className="mt-0.5 text-xs text-muted-foreground">{formatDay(invoice.invoiceDate)}</div>
                      </TableCell>

                      <TableCell className="max-w-[8rem] sm:max-w-[12rem]">
                        <Link className="block truncate hover:underline" to={`/customers/${invoice.customerId}`}>
                          {invoice.customerName}
                        </Link>
                        <div className="mt-1 sm:hidden">
                          <Badge variant={state.variant}>{state.label}</Badge>
                        </div>
                      </TableCell>

                      <TableCell className="text-right font-medium tabular-nums">
                        {formatMoney(invoice.totalAmount)}
                        {invoice.outstanding > 0 && (
                          <div className="text-xs font-normal text-destructive sm:hidden">
                            {formatMoney(invoice.outstanding)} due
                          </div>
                        )}
                      </TableCell>

                      <TableCell className="hidden text-right tabular-nums md:table-cell">
                        {invoice.amountPaid ? (
                          <span className="text-success">{formatMoney(invoice.amountPaid)}</span>
                        ) : (
                          <span className="text-muted-foreground">—</span>
                        )}
                      </TableCell>

                      <TableCell
                        className={cn(
                          'hidden text-right tabular-nums sm:table-cell',
                          invoice.outstanding > 0 ? 'font-medium text-destructive' : 'text-muted-foreground',
                        )}
                      >
                        {formatMoney(invoice.outstanding)}
                      </TableCell>

                      <TableCell className="hidden sm:table-cell">
                        <Badge variant={state.variant}>{state.label}</Badge>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </>
  )
}
