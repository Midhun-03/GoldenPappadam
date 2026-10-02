import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MoreHorizontal, Package, Pencil, Plus, Search } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { categoriesApi, productsApi, unitsApi } from '@/api/inventory'
import type { Product, ProductKind } from '@/api/types'
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
import { ApiError } from '@/lib/api'
import { formatMoney, formatQuantity } from '@/lib/format'
import { ProductDialog } from './ProductDialog'

const ALL = 'all'

export function ProductsPage() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState(ALL)
  const [kind, setKind] = useState<ProductKind | typeof ALL>(ALL)
  const [includeInactive, setIncludeInactive] = useState(false)
  const [editing, setEditing] = useState<Product | null>(null)
  const [isDialogOpen, setIsDialogOpen] = useState(false)

  const categories = useQuery({ queryKey: ['categories'], queryFn: () => categoriesApi.list() })
  const units = useQuery({ queryKey: ['units'], queryFn: () => unitsApi.list() })

  const filters = {
    search: search || undefined,
    categoryId: categoryId === ALL ? undefined : categoryId,
    kind: kind === ALL ? undefined : kind,
    includeInactive,
  }

  const products = useQuery({
    queryKey: ['products', filters],
    queryFn: () => productsApi.list(filters),
  })

  // The dialog needs every active product as a possible packing source, not just the filtered ones.
  const allProducts = useQuery({ queryKey: ['products', {}], queryFn: () => productsApi.list() })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => productsApi.setActive(id, isActive),
    onSuccess: async (product) => {
      await queryClient.invalidateQueries({ queryKey: ['products'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      toast.success(`${product.name} is now ${product.isActive ? 'active' : 'inactive'}`)
    },
    onError: (caught) =>
      toast.error(caught instanceof ApiError ? caught.message : 'Could not change the product.'),
  })

  const rows = products.data ?? []
  const isFiltered = search !== '' || categoryId !== ALL || kind !== ALL

  function openDialog(product: Product | null) {
    setEditing(product)
    setIsDialogOpen(true)
  }

  return (
    <>
      <PageHeader
        title="Products"
        description="Loose varieties and the packs made from them."
        action={
          <Button onClick={() => openDialog(null)}>
            <Plus className="size-4" />
            New product
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Search" htmlFor="product-search" className="col-span-2 sm:w-56">
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="product-search"
              className="pl-8"
              placeholder="Name or code"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
        </FilterField>

        <FilterField label="Category" htmlFor="product-category" className="sm:w-48">
          <Select value={categoryId} onValueChange={setCategoryId}>
            <SelectTrigger id="product-category" className="w-full">
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

        <FilterField label="Type" htmlFor="product-kind" className="sm:w-36">
          <Select value={kind} onValueChange={(value) => setKind(value as ProductKind | typeof ALL)}>
            <SelectTrigger id="product-kind" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>All types</SelectItem>
              <SelectItem value="Loose">Loose</SelectItem>
              <SelectItem value="Packed">Packed</SelectItem>
              <SelectItem value="Pieces">Shop pieces</SelectItem>
            </SelectContent>
          </Select>
        </FilterField>

        <FilterToggle
          pressed={includeInactive}
          onPressedChange={setIncludeInactive}
          className="col-span-2 sm:col-span-1"
        >
          Show inactive
        </FilterToggle>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {products.isPending ? (
            <TableSkeleton columns={5} />
          ) : products.isError ? (
            <ErrorState error={products.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Package}
              title={isFiltered ? 'Nothing matches these filters' : 'No products yet'}
              description={
                isFiltered
                  ? 'Try a different search, category or type.'
                  : 'Add a loose variety first, then the packs made from it.'
              }
              action={
                isFiltered ? undefined : (
                  <Button onClick={() => openDialog(null)}>
                    <Plus className="size-4" />
                    New product
                  </Button>
                )
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Product</TableHead>
                  <TableHead className="hidden md:table-cell">Category</TableHead>
                  <TableHead className="hidden sm:table-cell">Type</TableHead>
                  <TableHead className="hidden xl:table-cell">Packed from</TableHead>
                  <TableHead className="text-right">Price</TableHead>
                  <TableHead className="hidden text-right xl:table-cell">Low below</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((product) => (
                  <TableRow key={product.id} className={product.isActive ? undefined : 'opacity-60'}>
                    <TableCell className="max-w-[14rem]">
                      <div className="flex items-center gap-2">
                        <span className="truncate font-medium">{product.name}</span>
                        {!product.isActive && <Badge variant="outline">Inactive</Badge>}
                      </div>
                      <div className="mt-0.5 font-mono text-xs text-muted-foreground">
                        {product.productCode} · {product.unitCode}
                        <span className="md:hidden"> · {product.categoryName}</span>
                      </div>
                    </TableCell>

                    <TableCell className="hidden text-muted-foreground md:table-cell">
                      {product.categoryName}
                    </TableCell>

                    <TableCell className="hidden sm:table-cell">
                      <Badge variant={product.kind === 'Loose' ? 'outline' : 'secondary'}>
                        {product.kind === 'Pieces' ? 'Shop pieces' : product.kind}
                      </Badge>
                    </TableCell>

                    <TableCell className="hidden max-w-[14rem] truncate text-muted-foreground xl:table-cell">
                      {product.sourceProductName === null
                        ? '—'
                        : product.kind === 'Pieces'
                          ? `${product.sourceProductName} · by the piece`
                          : product.piecesPerPack !== null
                            ? `${product.sourceProductName} · ${product.piecesPerPack} pieces per pack`
                            : `${product.sourceProductName} · ${formatQuantity(product.sourceQuantityPerPack ?? 0)} per pack`}
                    </TableCell>

                    <TableCell className="text-right tabular-nums">
                      {product.sellingPrice === null ? (
                        <span className="text-xs text-muted-foreground">Not priced</span>
                      ) : (
                        formatMoney(product.sellingPrice)
                      )}
                    </TableCell>

                    <TableCell className="hidden text-right tabular-nums text-muted-foreground xl:table-cell">
                      {product.lowStockThreshold === null ? '—' : formatQuantity(product.lowStockThreshold)}
                    </TableCell>

                    <TableCell className="text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${product.name}`}>
                            <MoreHorizontal className="size-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem onClick={() => openDialog(product)}>
                            <Pencil className="size-4" />
                            Edit
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onClick={() => setActive.mutate({ id: product.id, isActive: !product.isActive })}
                          >
                            {product.isActive ? 'Deactivate' : 'Activate'}
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

      <ProductDialog
        open={isDialogOpen}
        onOpenChange={setIsDialogOpen}
        product={editing}
        categories={categories.data ?? []}
        units={units.data ?? []}
        products={allProducts.data ?? []}
      />
    </>
  )
}
