import { useQuery } from '@tanstack/react-query'
import { Plus, Undo2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { returnsApi, settlementLabels, type ReturnListItem } from '@/api/returns'
import { customersApi } from '@/api/sales'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney } from '@/lib/format'

const ALL = 'all'

export function SettlementBadge({ item }: { item: Pick<ReturnListItem, 'status' | 'settlement'> }) {
  if (item.status === 'Cancelled') return <Badge variant="destructive">Cancelled</Badge>
  if (item.settlement === 'Pending') return <Badge variant="warning">{settlementLabels.Pending}</Badge>
  return <Badge variant="outline">{settlementLabels[item.settlement]}</Badge>
}

export function ReturnsPage() {
  const [customerId, setCustomerId] = useState(ALL)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [pendingOnly, setPendingOnly] = useState(false)

  const customers = useQuery({ queryKey: ['customers', {}], queryFn: () => customersApi.list() })

  const filters = {
    customerId: customerId === ALL ? undefined : customerId,
    from: from || undefined,
    to: to || undefined,
    pendingOnly,
  }
  const returns = useQuery({ queryKey: ['returns', filters], queryFn: () => returnsApi.list(filters) })

  const rows = returns.data ?? []
  const live = rows.filter((row) => row.status === 'Recorded')
  const waiting = live.filter((row) => row.settlement === 'Pending').length
  const isFiltered = customerId !== ALL || from !== '' || to !== '' || pendingOnly

  return (
    <>
      <PageHeader
        title="Returns"
        description={
          returns.isPending ? undefined : (
            <span className="tabular-nums">
              {rows.length} shown · {formatMoney(live.reduce((sum, row) => sum + row.value, 0))} returned
              {waiting > 0 && <span className="text-warning"> · {waiting} for the office to decide</span>}
            </span>
          )
        }
        action={
          <Button asChild>
            <Link to="/returns/new">
              <Plus className="size-4" />
              New return
            </Link>
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Customer" htmlFor="return-filter-customer" className="col-span-2 sm:w-56">
          <Select value={customerId} onValueChange={setCustomerId}>
            <SelectTrigger id="return-filter-customer" className="w-full">
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

        <FilterField label="From" htmlFor="return-from">
          <Input id="return-from" type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} />
        </FilterField>

        <FilterField label="To" htmlFor="return-to">
          <Input id="return-to" type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} />
        </FilterField>

        <div className="col-span-2 flex h-9 items-center gap-2 self-end">
          <Checkbox id="return-pending" checked={pendingOnly} onCheckedChange={(value) => setPendingOnly(value === true)} />
          <Label htmlFor="return-pending" className="font-normal">
            Only those to decide
          </Label>
        </div>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {returns.isPending ? (
            <TableSkeleton columns={4} />
          ) : returns.isError ? (
            <ErrorState error={returns.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Undo2}
              title={isFiltered ? 'No returns match these filters' : 'No returns recorded yet'}
              description={
                isFiltered
                  ? 'Try a wider date range, or clear the filters.'
                  : 'When a shop gives back expired or damaged packets, record them here and decide what the shop gets.'
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Return</TableHead>
                  <TableHead className="hidden sm:table-cell">Shop</TableHead>
                  <TableHead>Settled by</TableHead>
                  <TableHead className="text-right">Value</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.id} className={row.status === 'Cancelled' ? 'text-muted-foreground' : undefined}>
                    <TableCell>
                      <Link className="font-mono text-sm font-medium hover:underline" to={`/returns/${row.id}`}>
                        {row.returnNumber}
                      </Link>
                      <div className="mt-0.5 text-xs text-muted-foreground">
                        {formatDay(row.returnDate)}
                        <span className="sm:hidden"> · {row.customerName}</span>
                      </div>
                    </TableCell>
                    <TableCell className="hidden max-w-[16rem] sm:table-cell">
                      <Link className="block truncate hover:underline" to={`/customers/${row.customerId}`}>
                        {row.customerName}
                      </Link>
                      {row.branchName && <div className="text-xs text-muted-foreground">{row.branchName}</div>}
                    </TableCell>
                    <TableCell>
                      <SettlementBadge item={row} />
                      {row.creditAmount > 0 && (
                        <div className="mt-0.5 text-xs tabular-nums text-muted-foreground">
                          {formatMoney(row.creditAmount)} credited
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right font-medium tabular-nums">{formatMoney(row.value)}</TableCell>
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
