import { useQuery } from '@tanstack/react-query'
import { History } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { productsApi, stockApi } from '@/api/inventory'
import type { StockMovement, StockMovementType } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime, formatQuantity } from '@/lib/format'
import { cn } from '@/lib/utils'

type BadgeVariant = 'outline' | 'secondary' | 'success' | 'warning' | 'destructive' | 'info'

/** Plain English for why the number moved, and how alarming that is. */
const movements: Record<StockMovementType, { label: string; variant: BadgeVariant }> = {
  Opening: { label: 'Opening stock', variant: 'outline' },
  Production: { label: 'Production', variant: 'success' },
  Packing: { label: 'Packing', variant: 'info' },
  Sale: { label: 'Sale', variant: 'secondary' },
  SaleReversal: { label: 'Bill cancelled', variant: 'info' },
  Damage: { label: 'Damage', variant: 'destructive' },
  Adjustment: { label: 'Adjustment', variant: 'warning' },
}

/** The record behind a movement, when there is one to point at. */
function Reference({ movement }: { movement: StockMovement }) {
  if (movement.referenceType === 'Invoice' && movement.referenceId) {
    return (
      <Link
        to={`/invoices/${movement.referenceId}`}
        className="font-medium underline-offset-4 hover:underline"
      >
        View the bill
      </Link>
    )
  }

  if (movement.referenceType === 'PackingEntry') return <>From packing</>

  return <>—</>
}

export function StockHistoryPage() {
  const { productId = '' } = useParams()

  const product = useQuery({ queryKey: ['products', productId], queryFn: () => productsApi.get(productId) })
  const movementsQuery = useQuery({
    queryKey: ['stock', productId, 'movements'],
    queryFn: () => stockApi.movements(productId),
  })

  const rows = movementsQuery.data ?? []
  const balance = rows.length > 0 ? rows[rows.length - 1].runningBalance : 0

  return (
    <>
      <PageHeader
        back={{ to: '/stock', label: 'Stock on hand' }}
        title={product.data?.name ?? 'Stock history'}
        description={
          product.isPending ? (
            <Skeleton className="h-4 w-56" />
          ) : product.data ? (
            <>
              <span className="font-mono">{product.data.productCode}</span> · counted in {product.data.unitCode} ·
              every movement, oldest first
            </>
          ) : (
            'Every movement, oldest first.'
          )
        }
        action={
          !movementsQuery.isPending &&
          rows.length > 0 && (
            <div className="rounded-lg border bg-card px-4 py-2">
              <div className="text-xs text-muted-foreground">On hand now</div>
              <div className={cn('font-heading text-lg font-semibold tabular-nums', balance < 0 && 'text-destructive')}>
                {formatQuantity(balance)}{' '}
                <span className="text-xs font-normal text-muted-foreground">{product.data?.unitCode}</span>
              </div>
            </div>
          )
        }
      />

      <Card>
        <CardContent className="px-0">
          {movementsQuery.isPending ? (
            <TableSkeleton columns={5} />
          ) : movementsQuery.isError ? (
            <ErrorState error={movementsQuery.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={History}
              title="No movements yet"
              description="Opening stock, production, packing and sales all show up here with a reason."
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead className="hidden sm:table-cell">When</TableHead>
                  <TableHead>Reason</TableHead>
                  <TableHead className="text-right">Change</TableHead>
                  <TableHead className="text-right">Balance</TableHead>
                  <TableHead className="hidden md:table-cell">Note</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((movement) => {
                  const kind = movements[movement.movementType]

                  return (
                    <TableRow key={movement.id}>
                      <TableCell className="hidden text-xs text-muted-foreground sm:table-cell">
                        {formatDateTime(movement.occurredAt)}
                      </TableCell>
                      <TableCell className="max-w-[10rem] sm:max-w-none">
                        <Badge variant={kind.variant}>{kind.label}</Badge>
                        <div className="mt-1 truncate text-xs text-muted-foreground sm:hidden">
                          {formatDateTime(movement.occurredAt)}
                        </div>
                        <div className="mt-0.5 truncate text-xs text-muted-foreground md:hidden">
                          {movement.notes ?? <Reference movement={movement} />}
                        </div>
                      </TableCell>
                      <TableCell
                        className={cn(
                          'text-right font-medium tabular-nums',
                          movement.quantity < 0 ? 'text-destructive' : 'text-success',
                        )}
                      >
                        {movement.quantity > 0 ? '+' : ''}
                        {formatQuantity(movement.quantity)}
                      </TableCell>
                      <TableCell
                        className={cn('text-right tabular-nums', movement.runningBalance < 0 && 'text-destructive')}
                      >
                        {formatQuantity(movement.runningBalance)}
                      </TableCell>
                      <TableCell className="hidden max-w-[18rem] truncate text-muted-foreground md:table-cell">
                        {movement.notes ?? <Reference movement={movement} />}
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
