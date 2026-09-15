import { useQuery } from '@tanstack/react-query'
import { Boxes, History, MoreHorizontal, Plus, Ruler, Search } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { categoriesApi, stockApi } from '@/api/inventory'
import type { StockOnHand } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField, FilterToggle } from '@/components/FilterBar'
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
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatQuantity } from '@/lib/format'
import { cn } from '@/lib/utils'
import { StockAdjustDialog } from './StockAdjustDialog'
import { StockEntryDialog } from './StockEntryDialog'

const ALL = 'all'

type StockStatus = 'out' | 'low' | 'healthy'

/** Three states, because "0 left" and "nearly out" need different reactions. */
function statusOf(row: StockOnHand): StockStatus {
  if (row.quantityOnHand <= 0) return 'out'
  if (row.isLowStock) return 'low'
  return 'healthy'
}

const statusStyle: Record<StockStatus, { dot: string; label: string; badge: 'destructive' | 'warning' | null }> = {
  out: { dot: 'bg-destructive', label: 'Out of stock', badge: 'destructive' },
  low: { dot: 'bg-warning', label: 'Low', badge: 'warning' },
  healthy: { dot: 'bg-success', label: 'In stock', badge: null },
}

export function StockPage() {
  const [categoryId, setCategoryId] = useState(ALL)
  const [search, setSearch] = useState('')
  const [lowStockOnly, setLowStockOnly] = useState(false)
  const [entryProduct, setEntryProduct] = useState<StockOnHand | null>(null)
  const [adjustProduct, setAdjustProduct] = useState<StockOnHand | null>(null)

  const categories = useQuery({ queryKey: ['categories'], queryFn: () => categoriesApi.list() })

  const filters = { categoryId: categoryId === ALL ? undefined : categoryId, lowStockOnly }
  const stock = useQuery({ queryKey: ['stock', filters], queryFn: () => stockApi.onHand(filters) })

  const term = search.trim().toLowerCase()
  const all = stock.data ?? []
  const rows = term
    ? all.filter(
        (row) => row.name.toLowerCase().includes(term) || row.productCode.toLowerCase().includes(term),
      )
    : all
  const lowCount = all.filter((row) => row.isLowStock).length

  return (
    <>
      <PageHeader
        title="Stock on hand"
        description="Every balance is the sum of its movement history."
        action={
          <Button variant="outline" asChild>
            <Link to="/packing">
              <Ruler className="size-4" />
              Packing
            </Link>
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Search" htmlFor="stock-search" className="col-span-2 sm:w-56">
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="stock-search"
              className="pl-8"
              placeholder="Name or code"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
        </FilterField>

        <FilterField label="Category" htmlFor="stock-category" className="col-span-2 sm:w-48">
          <Select value={categoryId} onValueChange={setCategoryId}>
            <SelectTrigger id="stock-category" className="w-full">
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
        </FilterField>

        <FilterToggle
          pressed={lowStockOnly}
          onPressedChange={setLowStockOnly}
          className="col-span-2 sm:col-span-1"
        >
          Low stock only{lowCount > 0 && !lowStockOnly ? ` (${lowCount})` : ''}
        </FilterToggle>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {stock.isPending ? (
            <TableSkeleton columns={5} />
          ) : stock.isError ? (
            <ErrorState error={stock.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Boxes}
              title={
                lowStockOnly || term || categoryId !== ALL ? 'Nothing matches these filters' : 'No products yet'
              }
              description={
                lowStockOnly || term || categoryId !== ALL
                  ? 'Clear the filters to see everything you stock.'
                  : 'Add a loose variety first, then the packs made from it.'
              }
              action={
                !lowStockOnly && !term && categoryId === ALL ? (
                  <Button asChild>
                    <Link to="/products">
                      <Plus className="size-4" />
                      Add a product
                    </Link>
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Product</TableHead>
                  <TableHead className="hidden sm:table-cell">Type</TableHead>
                  <TableHead className="text-right">On hand</TableHead>
                  <TableHead className="hidden text-right lg:table-cell">Low below</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const status = statusStyle[statusOf(row)]

                  return (
                    <TableRow key={row.productId}>
                      <TableCell className="max-w-[11rem] sm:max-w-[16rem]">
                        <div className="flex items-center gap-2">
                          <span
                            aria-hidden="true"
                            className={cn('size-1.5 shrink-0 rounded-full', status.dot)}
                          />
                          <Link
                            to={`/stock/${row.productId}`}
                            className="truncate font-medium underline-offset-4 hover:underline"
                          >
                            {row.name}
                          </Link>
                        </div>
                        <div className="mt-0.5 pl-3.5 font-mono text-xs text-muted-foreground">
                          {row.productCode}
                          <span className="sm:hidden"> · {row.kind}</span>
                        </div>
                      </TableCell>

                      <TableCell className="hidden sm:table-cell">
                        <Badge variant={row.kind === 'Packed' ? 'secondary' : 'outline'}>{row.kind}</Badge>
                      </TableCell>

                      <TableCell className="text-right">
                        <div
                          className={cn(
                            'font-medium tabular-nums',
                            row.quantityOnHand < 0 && 'text-destructive',
                          )}
                        >
                          {formatQuantity(row.quantityOnHand)}{' '}
                          <span className="text-xs font-normal text-muted-foreground">{row.unitCode}</span>
                        </div>
                        {status.badge && (
                          <Badge variant={status.badge} className="mt-1">
                            {status.label}
                          </Badge>
                        )}
                      </TableCell>

                      <TableCell className="hidden text-right tabular-nums text-muted-foreground lg:table-cell">
                        {row.lowStockThreshold === null ? '—' : formatQuantity(row.lowStockThreshold)}
                      </TableCell>

                      <TableCell className="text-right">
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${row.name}`}>
                              <MoreHorizontal className="size-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end" className="w-52">
                            <DropdownMenuItem onClick={() => setEntryProduct(row)}>
                              <Plus className="size-4" />
                              Add stock
                            </DropdownMenuItem>
                            <DropdownMenuItem onClick={() => setAdjustProduct(row)}>
                              <Ruler className="size-4" />
                              Correct after counting
                            </DropdownMenuItem>
                            <DropdownMenuItem asChild>
                              <Link to={`/stock/${row.productId}`}>
                                <History className="size-4" />
                                Movement history
                              </Link>
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>
          )}
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
