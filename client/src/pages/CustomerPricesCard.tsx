import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { IndianRupee, Loader2, Plus, X } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { customerPricesApi } from '@/api/sales'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatMoney } from '@/lib/format'

/**
 * What this shop pays. Only the office can change these: the salesperson's app shows the price
 * as plain text with no way to edit it, and the API refuses them outright.
 */
export function CustomerPricesCard({ customerId }: { customerId: string }) {
  const [isOpen, setIsOpen] = useState(false)
  const [editing, setEditing] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const prices = useQuery({
    queryKey: ['customers', customerId, 'prices'],
    queryFn: () => customerPricesApi.list(customerId),
  })

  const agreed = (prices.data ?? []).filter((price) => price.agreedPrice !== null)

  const remove = useMutation({
    mutationFn: (productId: string) => customerPricesApi.remove(customerId, productId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['customers', customerId, 'prices'] })
      toast.success('Back to the standard price for this shop.')
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not remove the price.'),
  })

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between gap-3 space-y-0">
        <CardTitle>Prices for this shop</CardTitle>
        <Button
          size="sm"
          variant="outline"
          onClick={() => {
            setEditing(null)
            setIsOpen(true)
          }}
        >
          <Plus className="size-4" />
          Set a price
        </Button>
      </CardHeader>

      <CardContent className="px-0">
        {prices.isPending ? (
          <TableSkeleton columns={3} />
        ) : prices.isError ? (
          <ErrorState error={prices.error} />
        ) : agreed.length === 0 ? (
          <EmptyState
            icon={IndianRupee}
            title="This shop pays the standard price"
            description="Set a price here when this shop has agreed something different. Everything else keeps the product's own price."
          />
        ) : (
          <Table>
            <TableHeader sticky>
              <TableRow>
                <TableHead>Product</TableHead>
                <TableHead className="hidden text-right sm:table-cell">Standard</TableHead>
                <TableHead className="text-right">This shop</TableHead>
                <TableHead className="w-10" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {agreed.map((price) => (
                <TableRow key={price.productId}>
                  <TableCell className="max-w-[10rem] truncate">
                    {price.productName}
                    <div className="text-xs text-muted-foreground">
                      per {price.unitCode.toLowerCase()}
                    </div>
                  </TableCell>
                  <TableCell className="hidden text-right tabular-nums text-muted-foreground sm:table-cell">
                    {formatMoney(price.defaultPrice)}
                  </TableCell>
                  <TableCell className="text-right">
                    <button
                      type="button"
                      className="font-medium tabular-nums underline-offset-4 hover:underline"
                      onClick={() => {
                        setEditing(price.productId)
                        setIsOpen(true)
                      }}
                    >
                      {formatMoney(price.agreedPrice)}
                    </button>
                  </TableCell>
                  <TableCell>
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={`Remove the agreed price for ${price.productName}`}
                      disabled={remove.isPending}
                      onClick={() => remove.mutate(price.productId)}
                    >
                      <X className="size-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <SetPriceDialog
        open={isOpen}
        onOpenChange={setIsOpen}
        customerId={customerId}
        productId={editing}
      />
    </Card>
  )
}

function SetPriceDialog({
  open,
  onOpenChange,
  customerId,
  productId,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  customerId: string
  /** Set when changing an existing arrangement; null when adding a new one. */
  productId: string | null
}) {
  const queryClient = useQueryClient()
  const [selectedProductId, setSelectedProductId] = useState('')
  const [price, setPrice] = useState('')
  const [error, setError] = useState<string | null>(null)

  const products = useQuery({
    queryKey: ['customers', customerId, 'prices'],
    queryFn: () => customerPricesApi.list(customerId),
    enabled: open,
  })

  const chosen = (products.data ?? []).find((product) => product.productId === selectedProductId)

  useEffect(() => {
    if (!open) return

    setSelectedProductId(productId ?? '')
    setError(null)
    // Changing an existing price starts from what it is now; a new one starts empty rather than
    // pre-filled with the standard price, so nobody agrees a price by accident.
    const existing = (products.data ?? []).find((product) => product.productId === productId)
    setPrice(existing?.agreedPrice != null ? String(existing.agreedPrice) : '')
  }, [open, productId, products.data])

  const save = useMutation({
    mutationFn: () => customerPricesApi.set(customerId, selectedProductId, Number(price)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['customers', customerId, 'prices'] })
      toast.success('Price saved. It applies to the next bill, not to bills already made.')
      onOpenChange(false)
    },
    onError: (failure) =>
      setError(failure instanceof ApiError ? failure.message : 'Could not save the price.'),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    if (selectedProductId === '') {
      setError('Choose a product first.')
      return
    }

    if (price.trim() === '' || Number.isNaN(Number(price)) || Number(price) < 0) {
      setError('Enter a price of zero or more.')
      return
    }

    save.mutate()
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>{productId ? 'Change the price' : 'Set a price for this shop'}</DialogTitle>
            <DialogDescription>
              Bills already made keep the price they were made at. This applies from the next one.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="price-product">Product</Label>
              <Select
                value={selectedProductId}
                onValueChange={setSelectedProductId}
                disabled={productId !== null}
              >
                <SelectTrigger id="price-product">
                  <SelectValue placeholder="Choose a product" />
                </SelectTrigger>
                <SelectContent>
                  {(products.data ?? []).map((product) => (
                    <SelectItem key={product.productId} value={product.productId}>
                      {product.productName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="price-amount">Price per {chosen?.unitCode.toLowerCase() ?? 'unit'}</Label>
              <Input
                id="price-amount"
                type="number"
                inputMode="decimal"
                step="0.01"
                min="0"
                value={price}
                onChange={(event) => setPrice(event.target.value)}
                autoFocus
              />
              {chosen && (
                <p className="text-xs text-muted-foreground">
                  {chosen.defaultPrice === null
                    ? 'This product has no standard price of its own.'
                    : `The standard price is ${formatMoney(chosen.defaultPrice)}.`}
                </p>
              )}
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
              Save price
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
