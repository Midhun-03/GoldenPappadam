import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { stockApi, type StockEntry } from '@/api/inventory'
import type { StockOnHand } from '@/api/types'
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

type EntryType = StockEntry['movementType']

const entryTypes: { value: EntryType; label: string; hint: string }[] = [
  { value: 'Production', label: 'Production', hint: 'Pappadams produced and added to loose stock.' },
  { value: 'Opening', label: 'Opening stock', hint: 'One-time starting balance, before any other movement.' },
  { value: 'Damage', label: 'Damage', hint: 'Stock written off. A note is required.' },
]

export function StockEntryDialog({
  open,
  onOpenChange,
  product,
  locationId,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  product: StockOnHand | null
  /** Where the stock is. Left out means the main warehouse, the only place production is recorded. */
  locationId?: string
}) {
  const queryClient = useQueryClient()
  const types = locationId ? entryTypes.filter((type) => type.value !== 'Production') : entryTypes
  const [movementType, setMovementType] = useState<EntryType>(types[0].value)
  const [quantity, setQuantity] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    setMovementType(locationId ? 'Opening' : 'Production')
    setQuantity('')
    setNotes('')
    setError(null)
  }, [open, locationId])

  const save = useMutation({
    mutationFn: (entry: StockEntry) => stockApi.addEntry(entry),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      toast.success(`${product?.name}: ${formatQuantity(result.quantityOnHand)} ${product?.unitCode} on hand`)
      if (result.warning) toast.warning(result.warning)
      onOpenChange(false)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the entry.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!product) return

    setError(null)
    save.mutate({
      productId: product.productId,
      movementType,
      quantity: Number(quantity),
      notes: notes.trim() || undefined,
      locationId,
    })
  }

  const selected = entryTypes.find((type) => type.value === movementType)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add stock</DialogTitle>
          <DialogDescription>
            {product && `${product.name} · ${formatQuantity(product.quantityOnHand)} ${product.unitCode} on hand`}
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-1.5">
            <Label htmlFor="entry-reason">Reason</Label>
            <Select value={movementType} onValueChange={(value) => setMovementType(value as EntryType)}>
              <SelectTrigger id="entry-reason" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {types.map((type) => (
                  <SelectItem key={type.value} value={type.value}>
                    {type.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">{selected?.hint}</p>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="quantity">
              Quantity{product ? ` (${product.unitCode})` : ''} <span className="text-destructive">*</span>
            </Label>
            <Input
              id="quantity"
              type="number"
              step={product?.unitCode === 'PCS' ? '1' : '0.001'}
              min={product?.unitCode === 'PCS' ? '1' : '0.001'}
              required
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="notes">
              Note{movementType === 'Damage' && <span className="text-destructive">*</span>}
            </Label>
            <Input
              id="notes"
              maxLength={300}
              placeholder={movementType === 'Damage' ? 'What was damaged, and how' : 'Optional'}
              required={movementType === 'Damage'}
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
            />
          </div>

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
