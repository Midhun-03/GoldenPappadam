import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { productsApi } from '@/api/inventory'
import type { Category, Product, ProductKind, SaveProduct, TaxTreatment, Unit } from '@/api/types'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ApiError } from '@/lib/api'

type Props = {
  open: boolean
  onOpenChange: (open: boolean) => void
  product: Product | null
  categories: Category[]
  units: Unit[]
  products: Product[]
}

const empty = {
  productCode: '',
  name: '',
  categoryId: '',
  kind: 'Loose' as ProductKind,
  unitOfMeasureId: '',
  sourceProductId: '',
  sourceQuantityPerPack: '',
  sellingPrice: '',
  lowStockThreshold: '',
  hsnCode: '',
  taxTreatment: '' as TaxTreatment | '',
  gstRate: '',
}

const NOT_DECIDED = 'undecided'

const treatments: { value: TaxTreatment; label: string }[] = [
  { value: 'Taxable', label: 'Taxable' },
  { value: 'Exempt', label: 'Exempt' },
  { value: 'NilRated', label: 'Nil rated' },
  { value: 'NonGst', label: 'Non-GST' },
]

const toNumber = (value: string) => (value.trim() === '' ? null : Number(value))

export function ProductDialog({ open, onOpenChange, product, categories, units, products }: Props) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState(empty)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return

    setError(null)
    setForm(
      product
        ? {
            productCode: product.productCode,
            name: product.name,
            categoryId: product.categoryId,
            kind: product.kind,
            unitOfMeasureId: product.unitOfMeasureId,
            sourceProductId: product.sourceProductId ?? '',
            sourceQuantityPerPack: product.sourceQuantityPerPack?.toString() ?? '',
            sellingPrice: product.sellingPrice?.toString() ?? '',
            lowStockThreshold: product.lowStockThreshold?.toString() ?? '',
            hsnCode: product.hsnCode ?? '',
            taxTreatment: product.taxTreatment ?? '',
            gstRate: product.gstRate?.toString() ?? '',
          }
        : empty,
    )
  }, [open, product])

  const save = useMutation({
    mutationFn: (payload: SaveProduct) =>
      product ? productsApi.update(product.id, payload) : productsApi.create(payload),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['products'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      toast.success(product ? `Updated ${saved.name}` : `Added ${saved.name}`)
      onOpenChange(false)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the product.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const isPacked = form.kind === 'Packed'

    save.mutate({
      productCode: form.productCode,
      name: form.name,
      categoryId: form.categoryId,
      kind: form.kind,
      unitOfMeasureId: form.unitOfMeasureId,
      sourceProductId: isPacked ? form.sourceProductId || null : null,
      sourceQuantityPerPack: isPacked ? toNumber(form.sourceQuantityPerPack) : null,
      sellingPrice: toNumber(form.sellingPrice),
      lowStockThreshold: toNumber(form.lowStockThreshold),
      hsnCode: form.hsnCode.trim() || null,
      taxTreatment: form.taxTreatment || null,
      gstRate: form.taxTreatment === 'Taxable' ? toNumber(form.gstRate) : null,
    })
  }

  // A pack can come from loose stock or from another pack, but never from itself.
  const sourceOptions = products.filter((candidate) => candidate.id !== product?.id && candidate.isActive)
  const sourceUnit = units.find(
    (unit) => unit.id === sourceOptions.find((candidate) => candidate.id === form.sourceProductId)?.unitOfMeasureId,
  )

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{product ? 'Edit product' : 'New product'}</DialogTitle>
          <DialogDescription>
            {product
              ? 'Loose or packed cannot be changed after a product is created.'
              : 'Loose products are bulk stock. Packed products are made from another product.'}
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="productCode">Code <span className="text-destructive">*</span></Label>
              <Input
                id="productCode"
                required
                maxLength={30}
                value={form.productCode}
                onChange={(event) => setForm({ ...form, productCode: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="name">Name <span className="text-destructive">*</span></Label>
              <Input
                id="name"
                required
                maxLength={150}
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div className="grid gap-1.5">
              <Label htmlFor="product-category-field">Category <span className="text-destructive">*</span></Label>
              <Select value={form.categoryId} onValueChange={(value) => setForm({ ...form, categoryId: value })}>
                <SelectTrigger id="product-category-field" className="w-full">
                  <SelectValue placeholder="Choose" />
                </SelectTrigger>
                <SelectContent>
                  {categories.map((category) => (
                    <SelectItem key={category.id} value={category.id}>
                      {category.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="product-kind-field">Type</Label>
              <Select
                value={form.kind}
                disabled={product !== null}
                onValueChange={(value) => setForm({ ...form, kind: value as ProductKind })}
              >
                <SelectTrigger id="product-kind-field" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Loose">Loose</SelectItem>
                  <SelectItem value="Packed">Packed</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="product-unit-field">Unit <span className="text-destructive">*</span></Label>
              <Select
                value={form.unitOfMeasureId}
                onValueChange={(value) => setForm({ ...form, unitOfMeasureId: value })}
              >
                <SelectTrigger id="product-unit-field" className="w-full">
                  <SelectValue placeholder="Choose" />
                </SelectTrigger>
                <SelectContent>
                  {units.map((unit) => (
                    <SelectItem key={unit.id} value={unit.id}>
                      {unit.code}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          {form.kind === 'Packed' && (
            <div className="grid gap-4 rounded-lg border bg-muted/40 p-3 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label htmlFor="product-source-field">Packed from <span className="text-destructive">*</span></Label>
                <Select
                  value={form.sourceProductId}
                  onValueChange={(value) => setForm({ ...form, sourceProductId: value })}
                >
                  <SelectTrigger id="product-source-field" className="w-full">
                    <SelectValue placeholder="Choose" />
                  </SelectTrigger>
                  <SelectContent>
                    {sourceOptions.map((candidate) => (
                      <SelectItem key={candidate.id} value={candidate.id}>
                        {candidate.name} ({candidate.unitCode})
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="sourceQuantityPerPack">
                  Used per pack{sourceUnit ? ` (${sourceUnit.code})` : ''}
                </Label>
                <Input
                  id="sourceQuantityPerPack"
                  type="number"
                  step="0.001"
                  min="0.001"
                  value={form.sourceQuantityPerPack}
                  onChange={(event) => setForm({ ...form, sourceQuantityPerPack: event.target.value })}
                />
              </div>
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="sellingPrice">Selling price</Label>
              <Input
                id="sellingPrice"
                type="number"
                step="0.01"
                min="0"
                placeholder="Not sold directly"
                value={form.sellingPrice}
                onChange={(event) => setForm({ ...form, sellingPrice: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="lowStockThreshold">Low stock below</Label>
              <Input
                id="lowStockThreshold"
                type="number"
                step="0.001"
                min="0"
                placeholder="No alert"
                value={form.lowStockThreshold}
                onChange={(event) => setForm({ ...form, lowStockThreshold: event.target.value })}
              />
            </div>
          </div>

          <fieldset className="grid gap-3 rounded-lg border p-3">
            <legend className="px-1 text-xs font-medium text-muted-foreground">GST - confirm with the accountant</legend>

            <div className="grid gap-4 sm:grid-cols-3">
              <div className="grid gap-1.5">
                <Label htmlFor="product-hsn">HSN code</Label>
                <Input
                  id="product-hsn"
                  inputMode="numeric"
                  maxLength={8}
                  className="font-mono"
                  placeholder="19059040"
                  value={form.hsnCode}
                  onChange={(event) => setForm({ ...form, hsnCode: event.target.value })}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="product-treatment">Treatment</Label>
                <Select
                  value={form.taxTreatment || NOT_DECIDED}
                  onValueChange={(value) =>
                    setForm({ ...form, taxTreatment: value === NOT_DECIDED ? '' : (value as TaxTreatment) })
                  }
                >
                  <SelectTrigger id="product-treatment" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NOT_DECIDED}>Not decided</SelectItem>
                    {treatments.map((treatment) => (
                      <SelectItem key={treatment.value} value={treatment.value}>
                        {treatment.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="product-gst-rate">GST rate %</Label>
                <Input
                  id="product-gst-rate"
                  type="number"
                  step="0.01"
                  min="0.01"
                  max="100"
                  disabled={form.taxTreatment !== 'Taxable'}
                  placeholder={form.taxTreatment === 'Taxable' ? '5' : '—'}
                  value={form.taxTreatment === 'Taxable' ? form.gstRate : ''}
                  onChange={(event) => setForm({ ...form, gstRate: event.target.value })}
                />
              </div>
            </div>

            <p className="text-xs text-muted-foreground">
              Only used once the business GSTIN is entered in Settings. From then on a product still "Not decided"
              cannot be billed - the app will not guess a tax. A change applies from the next bill.
            </p>
          </fieldset>

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
