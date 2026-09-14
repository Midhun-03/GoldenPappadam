import { useQuery } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { productsApi, stockApi } from '@/api/inventory'
import type { StockMovementType } from '@/api/types'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime, formatQuantity } from '@/lib/format'
import { cn } from '@/lib/utils'

const movementLabels: Record<StockMovementType, string> = {
  Opening: 'Opening stock',
  Production: 'Production',
  Packing: 'Packing',
  Sale: 'Sale',
  SaleReversal: 'Sale reversed',
  Damage: 'Damage',
  Adjustment: 'Adjustment',
}

export function StockHistoryPage() {
  const { productId = '' } = useParams()

  const product = useQuery({ queryKey: ['product', productId], queryFn: () => productsApi.get(productId) })
  const movements = useQuery({ queryKey: ['movements', productId], queryFn: () => stockApi.movements(productId) })

  const rows = movements.data ?? []

  return (
    <>
      <PageHeader
        title={product.data?.name ?? 'Stock history'}
        description={
          product.data
            ? `${product.data.productCode} · counted in ${product.data.unitCode}`
            : 'Every movement, oldest first.'
        }
        action={
          <Button variant="outline" asChild>
            <Link to="/stock">
              <ArrowLeft className="size-4" />
              Back to stock
            </Link>
          </Button>
        }
      />

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>When</TableHead>
                <TableHead>Reason</TableHead>
                <TableHead className="text-right">Change</TableHead>
                <TableHead className="text-right">Balance</TableHead>
                <TableHead>Note</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((movement) => (
                <TableRow key={movement.id}>
                  <TableCell className="whitespace-nowrap text-muted-foreground">
                    {formatDateTime(movement.occurredAt)}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">{movementLabels[movement.movementType]}</Badge>
                  </TableCell>
                  <TableCell
                    className={cn(
                      'text-right font-medium tabular-nums',
                      movement.quantity < 0 ? 'text-destructive' : 'text-emerald-600',
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
                  <TableCell className="text-muted-foreground">
                    {movement.notes ?? (movement.referenceType === 'PackingEntry' ? 'From packing' : '—')}
                  </TableCell>
                </TableRow>
              ))}

              {rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="py-10 text-center text-muted-foreground">
                    No movements yet for this product.
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
