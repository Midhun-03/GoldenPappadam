import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { stockApi } from '@/api/inventory'
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
import { ApiError } from '@/lib/api'
import { formatQuantity } from '@/lib/format'

export function StockAdjustDialog({
  open,
  onOpenChange,
  product,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  product: StockOnHand | null
}) {
  const queryClient = useQueryClient()
  const [countedQuantity, setCountedQuantity] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    setCountedQuantity('')
    setNotes('')
    setError(null)
  }, [open])

  const save = useMutation({
    mutationFn: () =>
      stockApi.adjust({
        productId: product!.productId,
        countedQuantity: Number(countedQuantity),
        notes: notes.trim(),
      }),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['movements'] })
      toast.success(`${product?.name} corrected to ${formatQuantity(result.quantityOnHand)} ${product?.unitCode}`)
      if (result.warning) toast.warning(result.warning)
      onOpenChange(false)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the adjustment.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    save.mutate()
  }

  const difference = countedQuantity.trim() === '' || !product ? null : Number(countedQuantity) - product.quantityOnHand

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Correct stock after counting</DialogTitle>
          <DialogDescription>
            Enter what you actually counted. The difference is recorded as an adjustment, so the history stays
            intact.
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-2">
            <Label htmlFor="counted">Counted quantity{product ? ` (${product.unitCode})` : ''}</Label>
            <Input
              id="counted"
              type="number"
              step="0.001"
              min="0"
              required
              value={countedQuantity}
              onChange={(event) => setCountedQuantity(event.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              {product && `Recorded now: ${formatQuantity(product.quantityOnHand)} ${product.unitCode}.`}
              {difference !== null &&
                difference !== 0 &&
                ` Adjustment: ${difference > 0 ? '+' : ''}${formatQuantity(difference)}.`}
            </p>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="adjust-notes">Reason</Label>
            <Input
              id="adjust-notes"
              required
              maxLength={300}
              placeholder="Monthly count"
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
              {save.isPending ? 'Saving…' : 'Save adjustment'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
