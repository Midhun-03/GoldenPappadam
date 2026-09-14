import { useMutation, useQueryClient } from '@tanstack/react-query'
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
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  product: StockOnHand | null
}) {
  const queryClient = useQueryClient()
  const [movementType, setMovementType] = useState<EntryType>('Production')
  const [quantity, setQuantity] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    setMovementType('Production')
    setQuantity('')
    setNotes('')
    setError(null)
  }, [open])

  const save = useMutation({
    mutationFn: (entry: StockEntry) => stockApi.addEntry(entry),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['movements'] })
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
          <div className="grid gap-2">
            <Label>Reason</Label>
            <Select value={movementType} onValueChange={(value) => setMovementType(value as EntryType)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {entryTypes.map((type) => (
                  <SelectItem key={type.value} value={type.value}>
                    {type.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">{selected?.hint}</p>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="quantity">Quantity{product ? ` (${product.unitCode})` : ''}</Label>
            <Input
              id="quantity"
              type="number"
              step="0.001"
              min="0.001"
              required
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="notes">Note{movementType === 'Damage' ? '' : ' (optional)'}</Label>
            <Input
              id="notes"
              maxLength={300}
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
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
