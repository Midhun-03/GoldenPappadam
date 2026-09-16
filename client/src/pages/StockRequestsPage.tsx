import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ClipboardList, Package } from 'lucide-react'
import { Fragment, useState } from 'react'
import { toast } from 'sonner'
import { stockRequestsApi } from '@/api/fieldsales'
import type { StockRequestStatus } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDateTime, formatDay, formatQuantity, shiftDay, todayInIndia } from '@/lib/format'

const ALL = 'all'

const statusLabels: Record<StockRequestStatus, { label: string; variant: 'outline' | 'success' | 'secondary' }> = {
  Requested: { label: 'Asked for', variant: 'outline' },
  Fulfilled: { label: 'Packed', variant: 'success' },
  Cancelled: { label: 'Cancelled', variant: 'secondary' },
}

/**
 * What the sales team has asked the packing unit for.
 *
 * Two readings of the same thing: the figures to pack, added up per product per day, and the
 * individual requests behind them. Somebody standing at the packing table wants the first; somebody
 * answering the salesperson wants the second.
 */
export function StockRequestsPage() {
  const today = todayInIndia()
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(shiftDay(today, 7))
  const [status, setStatus] = useState(ALL)
  const queryClient = useQueryClient()

  const filters = { from, to }
  const needs = useQuery({
    queryKey: ['stock-requests', 'packing-needs', filters],
    queryFn: () => stockRequestsApi.packingNeeds(filters),
  })

  const requests = useQuery({
    queryKey: ['stock-requests', { ...filters, status }],
    queryFn: () =>
      stockRequestsApi.list({
        ...filters,
        status: status === ALL ? undefined : (status as StockRequestStatus),
      }),
  })

  const setRequestStatus = useMutation({
    mutationFn: ({ id, next }: { id: string; next: StockRequestStatus }) =>
      stockRequestsApi.setStatus(id, next),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['stock-requests'] })
      toast.success('Request updated')
    },
    onError: (caught) =>
      toast.error(caught instanceof ApiError ? caught.message : 'Could not update the request.'),
  })

  // One heading per day, so the packing table reads as a plan rather than a list.
  const byDay = (needs.data ?? []).reduce<Record<string, typeof needs.data>>((days, need) => {
    ;(days[need.requiredDate] ??= []).push(need)
    return days
  }, {})

  return (
    <>
      <PageHeader
        title="Stock requests"
        description="What the sales team needs packed, and when they need it."
      />

      <FilterBar>
        <FilterField label="From" htmlFor="requests-from" className="col-span-2 sm:w-40">
          <Input id="requests-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        </FilterField>
        <FilterField label="To" htmlFor="requests-to" className="col-span-2 sm:w-40">
          <Input id="requests-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        </FilterField>
        <FilterField label="Status" htmlFor="requests-status" className="col-span-2 sm:w-40">
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger id="requests-status" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>All</SelectItem>
              <SelectItem value="Requested">Asked for</SelectItem>
              <SelectItem value="Fulfilled">Packed</SelectItem>
              <SelectItem value="Cancelled">Cancelled</SelectItem>
            </SelectContent>
          </Select>
        </FilterField>
      </FilterBar>

      <Card className="mb-4 lg:mb-5">
        <CardHeader>
          <CardTitle>To pack</CardTitle>
          <CardDescription>
            Every request added up, per product per day. Cancelled ones are left out.
          </CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          {needs.isPending ? (
            <TableSkeleton columns={3} />
          ) : needs.isError ? (
            <ErrorState error={needs.error} />
          ) : (needs.data ?? []).length === 0 ? (
            <EmptyState
              icon={Package}
              title="Nothing to pack for these days"
              description="Requests from the sales team appear here as soon as their phones sync."
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Product</TableHead>
                  <TableHead className="text-right">Quantity</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Requests</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {Object.entries(byDay).map(([day, dayNeeds]) => (
                  // A Fragment rather than <>, because the day group is the list item and needs
                  // the key; the rows inside it are not what React is reconciling here.
                  <Fragment key={day}>
                    <TableRow className="bg-muted/50 hover:bg-muted/50">
                      <TableCell colSpan={3} className="py-2 text-xs font-medium">
                        Needed by {formatDay(day)}
                      </TableCell>
                    </TableRow>
                    {(dayNeeds ?? []).map((need) => (
                      <TableRow key={`${day}-${need.productId}`}>
                        <TableCell className="max-w-[14rem] truncate">{need.productName}</TableCell>
                        <TableCell className="text-right font-medium tabular-nums">
                          {formatQuantity(need.quantity)}{' '}
                          <span className="text-xs font-normal text-muted-foreground">
                            {need.unitCode.toLowerCase()}
                          </span>
                        </TableCell>
                        <TableCell className="hidden text-right tabular-nums text-muted-foreground sm:table-cell">
                          {need.requestCount}
                        </TableCell>
                      </TableRow>
                    ))}
                  </Fragment>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Requests</CardTitle>
          <CardDescription>Who asked, for what, and whether it has been packed.</CardDescription>
        </CardHeader>
        <CardContent className="px-0">
          {requests.isPending ? (
            <TableSkeleton columns={4} />
          ) : requests.isError ? (
            <ErrorState error={requests.error} />
          ) : requests.data.length === 0 ? (
            <EmptyState icon={ClipboardList} title="No requests in this range" />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Needed by</TableHead>
                  <TableHead>Products</TableHead>
                  <TableHead className="hidden md:table-cell">Asked by</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {requests.data.map((request) => (
                  <TableRow key={request.id}>
                    <TableCell className="whitespace-nowrap">
                      {formatDay(request.requiredDate)}
                      <div className="text-xs text-muted-foreground">
                        asked {formatDateTime(request.createdAt)}
                      </div>
                    </TableCell>
                    <TableCell className="max-w-[16rem]">
                      {request.lines.map((line) => (
                        <div key={line.productId} className="truncate text-sm">
                          {formatQuantity(line.quantity)} {line.unitCode.toLowerCase()} ·{' '}
                          {line.productName}
                        </div>
                      ))}
                      {request.notes && (
                        <div className="mt-0.5 truncate text-xs text-muted-foreground">
                          {request.notes}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="hidden max-w-[10rem] truncate text-sm md:table-cell">
                      {request.requestedBy}
                      {request.deviceName && (
                        <div className="text-xs text-muted-foreground">{request.deviceName}</div>
                      )}
                    </TableCell>
                    <TableCell>
                      <Badge variant={statusLabels[request.status].variant}>
                        {statusLabels[request.status].label}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      {request.status === 'Requested' && (
                        <div className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="ghost"
                            disabled={setRequestStatus.isPending}
                            onClick={() =>
                              setRequestStatus.mutate({ id: request.id, next: 'Fulfilled' })
                            }
                          >
                            Packed
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            disabled={setRequestStatus.isPending}
                            onClick={() =>
                              setRequestStatus.mutate({ id: request.id, next: 'Cancelled' })
                            }
                          >
                            Cancel
                          </Button>
                        </div>
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
