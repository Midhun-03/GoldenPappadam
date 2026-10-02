import { useQuery } from '@tanstack/react-query'
import { History, MoreHorizontal, Plus, ShoppingCart, Store, Truck } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { stockApi } from '@/api/inventory'
import { shopStockApi, type ShopStock } from '@/api/ownShop'
import type { StockOnHand } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatMoney, formatQuantity } from '@/lib/format'
import { StockAdjustDialog } from './StockAdjustDialog'
import { StockEntryDialog } from './StockEntryDialog'

/** The shape the shared stock dialogs take. */
function asStockRow(row: ShopStock): StockOnHand {
  return {
    productId: row.productId,
    productCode: row.productCode,
    name: row.name,
    kind: 'Pieces',
    unitCode: 'PCS',
    quantityOnHand: row.shopPieces,
    lowStockThreshold: row.lowStockThreshold,
    isLowStock: row.isLowStock,
    isActive: row.isActive,
  }
}

/**
 * What the own shop has to sell, in pieces, with each variety's rate band and the loose kg the factory
 * could still send. The 15/30/50-piece bundles the shop makes up are still these pieces.
 */
export function OwnShopStockPage() {
  const [entryRow, setEntryRow] = useState<StockOnHand | null>(null)
  const [countRow, setCountRow] = useState<StockOnHand | null>(null)

  const stock = useQuery({ queryKey: ['own-shop', 'stock'], queryFn: () => shopStockApi.list() })
  const locations = useQuery({ queryKey: ['stock', 'locations'], queryFn: stockApi.locations })
  const shopId = locations.data?.find((location) => location.kind === 'Shop')?.id

  const rows = stock.data ?? []
  const totalPieces = rows.reduce((sum, row) => sum + row.shopPieces, 0)

  return (
    <>
      <PageHeader
        title="Own shop"
        description={
          stock.isPending ? undefined : (
            <span className="tabular-nums">{formatQuantity(totalPieces)} pieces on the shelf</span>
          )
        }
        action={
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" asChild>
              <Link to="/own-shop/transfers">
                <Truck className="size-4" />
                Receive from factory
              </Link>
            </Button>
            <Button asChild>
              <Link to="/own-shop/sales/new">
                <ShoppingCart className="size-4" />
                New sale
              </Link>
            </Button>
          </div>
        }
      />

      <Card>
        <CardContent className="@container px-0">
          {stock.isPending ? (
            <TableSkeleton columns={4} />
          ) : stock.isError ? (
            <ErrorState error={stock.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Store}
              title="The shop has nothing to sell yet"
              description={
                'Add a product of type "Shop pieces" for each loose pappadam the shop sells, with its rate per ' +
                'piece and lowest rate. Then receive stock from the factory.'
              }
              action={
                <Button asChild>
                  <Link to="/products">
                    <Plus className="size-4" />
                    Add a shop product
                  </Link>
                </Button>
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Variety</TableHead>
                  <TableHead className="text-right">Pieces</TableHead>
                  <TableHead className="hidden text-right @md:table-cell">Rate / lowest</TableHead>
                  <TableHead className="hidden text-right @2xl:table-cell">At the factory</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => (
                  <TableRow key={row.productId}>
                    <TableCell className="max-w-[12rem] sm:max-w-[18rem]">
                      <div className="truncate font-medium">{row.name}</div>
                      <div className="mt-0.5 truncate text-xs text-muted-foreground">
                        from {row.sourceProductName}
                        {row.piecesPerKg !== null && ` · ${formatQuantity(row.piecesPerKg)} pieces per kg`}
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      <span className="font-medium tabular-nums">{formatQuantity(row.shopPieces)}</span>
                      {row.shopPieces <= 0 ? (
                        <Badge variant="destructive" className="ml-2">
                          Out
                        </Badge>
                      ) : (
                        row.isLowStock && (
                          <Badge variant="warning" className="ml-2">
                            Low
                          </Badge>
                        )
                      )}
                    </TableCell>
                    <TableCell className="hidden text-right tabular-nums @md:table-cell">
                      {formatMoney(row.defaultRate)}
                      <span className="text-muted-foreground"> / {formatMoney(row.minimumRate)}</span>
                    </TableCell>
                    <TableCell className="hidden text-right tabular-nums text-muted-foreground @2xl:table-cell">
                      {formatQuantity(row.factoryKg)} kg
                    </TableCell>
                    <TableCell>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" aria-label={`Actions for ${row.name}`}>
                            <MoreHorizontal className="size-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem disabled={!shopId} onSelect={() => setCountRow(asStockRow(row))}>
                            Correct after counting
                          </DropdownMenuItem>
                          <DropdownMenuItem disabled={!shopId} onSelect={() => setEntryRow(asStockRow(row))}>
                            Opening stock or damage
                          </DropdownMenuItem>
                          <DropdownMenuItem asChild>
                            <Link to={`/stock/${row.productId}`}>
                              <History className="size-4" />
                              History
                            </Link>
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <StockEntryDialog
        open={entryRow !== null}
        onOpenChange={(open) => !open && setEntryRow(null)}
        product={entryRow}
        locationId={shopId}
      />
      <StockAdjustDialog
        open={countRow !== null}
        onOpenChange={(open) => !open && setCountRow(null)}
        product={countRow}
        locationId={shopId}
      />
    </>
  )
}
