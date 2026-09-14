import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { categoriesApi, productsApi, unitsApi } from '@/api/inventory'
import type { Product, ProductKind } from '@/api/types'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
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

  return (
    <>
      <PageHeader
        title="Products"
        description="Loose varieties and the packs made from them."
        action={
          <Button
            onClick={() => {
              setEditing(null)
              setIsDialogOpen(true)
            }}
          >
            <Plus className="size-4" />
            New product
          </Button>
        }
      />

      <Card className="mb-4">
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="grid gap-2">
            <Label htmlFor="search">Search</Label>
            <Input
              id="search"
              className="w-56"
              placeholder="Name or code"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>

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

          <div className="grid gap-2">
            <Label>Type</Label>
            <Select value={kind} onValueChange={(value) => setKind(value as ProductKind | typeof ALL)}>
              <SelectTrigger className="w-36">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>All types</SelectItem>
                <SelectItem value="Loose">Loose</SelectItem>
                <SelectItem value="Packed">Packed</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <Button variant="ghost" size="sm" onClick={() => setIncludeInactive(!includeInactive)}>
            {includeInactive ? 'Hide inactive' : 'Show inactive'}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Unit</TableHead>
                <TableHead>Packed from</TableHead>
                <TableHead className="text-right">Price</TableHead>
                <TableHead className="text-right">Low below</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(products.data ?? []).map((product) => (
                <TableRow key={product.id} className={product.isActive ? undefined : 'opacity-60'}>
                  <TableCell className="font-mono text-xs">{product.productCode}</TableCell>
                  <TableCell className="font-medium">
                    {product.name}
                    {!product.isActive && (
                      <Badge variant="outline" className="ml-2">
                        Inactive
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>{product.categoryName}</TableCell>
                  <TableCell>
                    <Badge variant={product.kind === 'Packed' ? 'secondary' : 'outline'}>{product.kind}</Badge>
                  </TableCell>
                  <TableCell>{product.unitCode}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {product.sourceProductName
                      ? `${product.sourceProductName} · ${formatQuantity(product.sourceQuantityPerPack ?? 0)} per pack`
                      : '—'}
                  </TableCell>
                  <TableCell className="text-right">{formatMoney(product.sellingPrice)}</TableCell>
                  <TableCell className="text-right">
                    {product.lowStockThreshold === null ? '—' : formatQuantity(product.lowStockThreshold)}
                  </TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setEditing(product)
                        setIsDialogOpen(true)
                      }}
                    >
                      Edit
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setActive.mutate({ id: product.id, isActive: !product.isActive })}
                    >
                      {product.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}

              {products.data?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={9} className="py-10 text-center text-muted-foreground">
                    No products yet. Add a loose variety first, then the packs made from it.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
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
