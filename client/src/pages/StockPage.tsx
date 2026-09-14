import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { categoriesApi, stockApi } from '@/api/inventory'
import type { StockOnHand } from '@/api/types'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { formatQuantity } from '@/lib/format'
import { StockAdjustDialog } from './StockAdjustDialog'
import { StockEntryDialog } from './StockEntryDialog'

const ALL = 'all'

export function StockPage() {
  const [categoryId, setCategoryId] = useState(ALL)
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const [entryProduct, setEntryProduct] = useState<StockOnHand | null>(null)
  const [adjustProduct, setAdjustProduct] = useState<StockOnHand | null>(null)

  const categories = useQuery({ queryKey: ['categories'], queryFn: () => categoriesApi.list() })

  const filters = { categoryId: categoryId === ALL ? undefined : categoryId, lowStockOnly }
  const stock = useQuery({ queryKey: ['stock', filters], queryFn: () => stockApi.onHand(filters) })

  const rows = stock.data ?? []
  const lowCount = rows.filter((row) => row.isLowStock).length

  return (
    <>
      <PageHeader
        title="Stock on hand"
        description="Every balance is the sum of its movement history."
      />

      <Card className="mb-4">
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="grid gap-2">
            <Label>Category</Label>
            <Select value={categoryId} onValueChange={setCategoryId}>
              <SelectTrigger className="w-48">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>All categories</SelectItem>
                {(categories.data ?? []).map((category) => (
                  <SelectItem key={category.id} value={category.id}>
                    {category.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <Button variant={lowStockOnly ? 'default' : 'outline'} onClick={() => setLowStockOnly(!lowStockOnly)}>
            Low stock only{lowCount > 0 && !lowStockOnly ? ` (${lowCount})` : ''}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Product</TableHead>
                <TableHead>Type</TableHead>
                <TableHead className="text-right">On hand</TableHead>
                <TableHead className="text-right">Low below</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.productId}>
                  <TableCell>
                    <div className="font-medium">{row.name}</div>
                    <div className="font-mono text-xs text-muted-foreground">{row.productCode}</div>
                  </TableCell>
                  <TableCell>
                    <Badge variant={row.kind === 'Packed' ? 'secondary' : 'outline'}>{row.kind}</Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    <span
                      className={cn(
                        'font-medium tabular-nums',
                        row.quantityOnHand < 0 && 'text-destructive',
                      )}
                    >
                      {formatQuantity(row.quantityOnHand)} {row.unitCode}
                    </span>
                    {row.isLowStock && (
                      <Badge variant="destructive" className="ml-2">
                        Low
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="text-right text-muted-foreground tabular-nums">
                    {row.lowStockThreshold === null ? '—' : formatQuantity(row.lowStockThreshold)}
                  </TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    <Button variant="ghost" size="sm" onClick={() => setEntryProduct(row)}>
                      Add stock
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => setAdjustProduct(row)}>
                      Correct
                    </Button>
                    <Button variant="ghost" size="sm" asChild>
                      <Link to={`/stock/${row.productId}`}>History</Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}

              {rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="py-10 text-center text-muted-foreground">
                    {lowStockOnly ? 'Nothing is below its low-stock level.' : 'No products yet.'}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <StockEntryDialog
        open={entryProduct !== null}
        onOpenChange={(open) => !open && setEntryProduct(null)}
        product={entryProduct}
      />
      <StockAdjustDialog
        open={adjustProduct !== null}
        onOpenChange={(open) => !open && setAdjustProduct(null)}
        product={adjustProduct}
      />
    </>
  )
}
