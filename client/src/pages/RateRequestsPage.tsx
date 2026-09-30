import { useQuery } from '@tanstack/react-query'
import { Scale } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { describeRate, rateRequestsApi, type RateRequest, type RateRequestStatus } from '@/api/rateRequests'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime, formatMoney } from '@/lib/format'
import { RateRequestActions } from './RateRequestActions'

const ALL = 'all'

const statusBadge: Record<RateRequestStatus, 'warning' | 'success' | 'destructive' | 'outline'> = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'destructive',
  Cancelled: 'outline',
}

/**
 * Salesmen set rates only when they add a new shop; after that a change is a request the office
 * approves or rejects here (CLAUDE.md §4 "Rate-change approval"). Until then the shop keeps its rate.
 */
export function RateRequestsPage() {
  const [status, setStatus] = useState<RateRequestStatus | typeof ALL>('Pending')

  const filters = status === ALL ? {} : { status }
  const requests = useQuery({ queryKey: ['rate-requests', filters], queryFn: () => rateRequestsApi.list(filters) })
  const rows = requests.data ?? []

  return (
    <>
      <PageHeader
        title="Rate requests"
        description="A salesman's change to a shop's rate waits here. Nothing changes until you approve it."
      />

      <FilterBar>
        <FilterField label="Show" htmlFor="rate-request-status" className="col-span-2 sm:w-56">
          <Select value={status} onValueChange={(value) => setStatus(value as RateRequestStatus | typeof ALL)}>
            <SelectTrigger id="rate-request-status" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Pending">Waiting for you</SelectItem>
              <SelectItem value="Approved">Approved</SelectItem>
              <SelectItem value="Rejected">Rejected</SelectItem>
              <SelectItem value="Cancelled">Withdrawn or replaced</SelectItem>
              <SelectItem value={ALL}>All</SelectItem>
            </SelectContent>
          </Select>
        </FilterField>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {requests.isPending ? (
            <TableSkeleton columns={4} />
          ) : requests.isError ? (
            <ErrorState error={requests.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Scale}
              title={status === 'Pending' ? 'Nothing waiting' : 'No requests here'}
              description="When a salesman asks to change a shop's rate from the phone, it appears here for you to decide."
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Shop and product</TableHead>
                  <TableHead>Rate</TableHead>
                  <TableHead className="hidden md:table-cell">Asked by</TableHead>
                  <TableHead className="text-right">{status === 'Pending' ? '' : 'Decision'}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((request) => (
                  <RequestRow key={request.id} request={request} />
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </>
  )
}

function RequestRow({ request }: { request: RateRequest }) {
  const moved = request.status === 'Pending' && request.priceWhenRequested !== request.currentPrice

  return (
    <TableRow>
      <TableCell className="max-w-[14rem]">
        <Link
          to={`/customers/${request.customerId}`}
          className="block truncate font-medium underline-offset-4 hover:underline"
        >
          {request.customerName}
        </Link>
        <div className="truncate text-xs text-muted-foreground">
          {request.productName} · per {request.unitCode.toLowerCase()}
        </div>
      </TableCell>
      <TableCell className="tabular-nums">
        {describeRate(request.status === 'Pending' ? request.currentPrice : request.priceWhenRequested, request.standardPrice)}{' '}
        → <span className="font-medium">{formatMoney(request.requestedPrice)}</span>
        {moved && (
          <div className="text-xs text-warning">
            Was {describeRate(request.priceWhenRequested, request.standardPrice)} when asked
          </div>
        )}
        {request.reason && <div className="text-xs text-muted-foreground">“{request.reason}”</div>}
      </TableCell>
      <TableCell className="hidden text-sm md:table-cell">
        {request.requestedBy ?? 'Unknown'}
        <div className="text-xs text-muted-foreground">{formatDateTime(request.requestedAt)}</div>
      </TableCell>
      <TableCell className="text-right">
        {request.status === 'Pending' ? (
          <RateRequestActions request={request} />
        ) : (
          <div className="grid justify-items-end gap-0.5">
            <Badge variant={statusBadge[request.status]}>
              {request.status === 'Cancelled' ? 'Withdrawn' : request.status}
            </Badge>
            <span className="text-xs text-muted-foreground">
              {request.decidedBy ?? ''}
              {request.decidedAt && ` · ${formatDateTime(request.decidedAt)}`}
            </span>
            {request.decisionNote && <span className="text-xs text-muted-foreground">{request.decisionNote}</span>}
          </div>
        )}
      </TableCell>
    </TableRow>
  )
}
