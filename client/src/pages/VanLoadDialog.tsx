import { useMutation, useQuery } from '@tanstack/react-query'
import { Loader2, Plus, X } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { vanLoadsApi } from '@/api/fieldsales'
import { productsApi } from '@/api/inventory'
import type { VanLoadDirection, VanLoadLine } from '@/api/types'
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
import { formatQuantity } from '@/lib/format'

type Row = { productId: string; quantity: string }

/**
 * Loading the van, or taking back what did not sell. The return starts filled in with whatever the
 * van is actually holding, because the common case is "all of it" and nobody should have to type
 * a list they already know.
 */
export function VanLoadDialog({
  open,
  onOpenChange,
  direction,
  vanLocationId,
  vanName,
  onVan,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  direction: VanLoadDirection
  vanLocationId: string
  vanName: string
  /** What the van is holding right now. */
  onVan: VanLoadLine[]
  onSaved: () => Promise<unknown>
}) {
  const isReturn = direction === 'Return'
  const [rows, setRows] = useState<Row[]>([])
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const products = useQuery({
    queryKey: ['products', { active: true }],
    queryFn: () => productsApi.list(),
    enabled: open,
  })

  useEffect(() => {
    if (!open) return

    setNotes('')
    setError(null)
    setRows(
      isReturn && onVan.length > 0
        ? onVan.map((line) => ({ productId: line.productId, quantity: String(line.quantity) }))
        : [{ productId: '', quantity: '' }],
    )
  }, [open, isReturn, onVan])

  const save = useMutation({
    mutationFn: () =>
      vanLoadsApi.create({
        vanLocationId,
        direction,
        notes: notes.trim() || undefined,
        lines: rows
          .filter((row) => row.productId !== '' && Number(row.quantity) > 0)
          .map((row) => ({ productId: row.productId, quantity: Number(row.quantity) })),
      }),
    onSuccess: async (result) => {
      await onSaved()
      toast.success(isReturn ? `Unsold stock is back from ${vanName}.` : `${vanName} is loaded.`)
      // Short stock warns, never blocks: the packets physically moved either way.
      for (const warning of result.warnings) toast.warning(warning)
      onOpenChange(false)
    },
    onError: (failure) =>
      setError(failure instanceof ApiError ? failure.message : 'Could not save this.'),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    const filled = rows.filter((row) => row.productId !== '' && row.quantity.trim() !== '')

    if (filled.length === 0) {
      setError('Add at least one product.')
      return
    }

    if (filled.some((row) => Number.isNaN(Number(row.quantity)) || Number(row.quantity) <= 0)) {
      setError('Every quantity must be greater than zero.')
      return
    }

    if (new Set(filled.map((row) => row.productId)).size !== filled.length) {
      setError('The same product is listed twice. Combine the quantities into one line.')
      return
    }

    save.mutate()
  }

  const update = (index: number, change: Partial<Row>) =>
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...change } : row)))

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>{isReturn ? 'Return unsold stock' : `Load ${vanName}`}</DialogTitle>
            <DialogDescription>
              {isReturn
                ? 'What comes back off the van and into the warehouse. Filled in with what the van is carrying.'
                : 'What goes from the warehouse onto the van this morning.'}
            </DialogDescription>
          </DialogHeader>

          <div className="grid max-h-[50vh] gap-3 overflow-y-auto py-4">
            {rows.map((row, index) => (
              <div key={index} className="flex items-end gap-2">
                <div className="grid min-w-0 flex-1 gap-1.5">
                  {index === 0 && <Label>Product</Label>}
                  <Select value={row.productId} onValueChange={(value) => update(index, { productId: value })}>
                    <SelectTrigger>
                      <SelectValue placeholder="Choose a product" />
                    </SelectTrigger>
                    <SelectContent>
                      {(products.data ?? []).map((product) => (
                        <SelectItem key={product.id} value={product.id}>
                          {product.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid w-28 gap-1.5">
                  {index === 0 && <Label>Quantity</Label>}
                  <Input
                    type="number"
                    inputMode="decimal"
                    step="0.001"
                    min="0"
                    value={row.quantity}
                    onChange={(event) => update(index, { quantity: event.target.value })}
                  />
                </div>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  aria-label="Remove this line"
                  onClick={() => setRows((current) => current.filter((_, i) => i !== index))}
                >
                  <X className="size-4" />
                </Button>
              </div>
            ))}

            <Button
              type="button"
              variant="outline"
              size="sm"
              className="justify-self-start"
              onClick={() => setRows((current) => [...current, { productId: '', quantity: '' }])}
            >
              <Plus className="size-4" />
              Add a product
            </Button>

            {isReturn && onVan.length > 0 && (
              <p className="text-xs text-muted-foreground">
                The van is carrying{' '}
                {onVan.map((line) => `${formatQuantity(line.quantity)} ${line.productName}`).join(', ')}.
              </p>
            )}

            <div className="grid gap-1.5">
              <Label htmlFor="van-notes">Notes</Label>
              <Input
                id="van-notes"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                placeholder="Optional"
              />
            </div>

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {isReturn ? 'Bring it back' : 'Load the van'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
