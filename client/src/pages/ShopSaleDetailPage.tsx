import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, XCircle } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { shopSalesApi } from '@/api/ownShop'
import { ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
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
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDateTime, formatMoney, formatPaymentMethod, formatQuantity } from '@/lib/format'

export function ShopSaleDetailPage() {
  const { saleId = '' } = useParams()
  const queryClient = useQueryClient()
  const [isCancelOpen, setIsCancelOpen] = useState(false)
  const [reason, setReason] = useState('')

  const sale = useQuery({ queryKey: ['own-shop', 'sales', saleId], queryFn: () => shopSalesApi.get(saleId) })
  const data = sale.data

  const cancel = useMutation({
    mutationFn: () => shopSalesApi.cancel(saleId, reason.trim()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['own-shop'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      setIsCancelOpen(false)
      toast.success('Sale cancelled. The pieces are back on the shelf.')
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not cancel the sale.'),
  })

  function handleCancel(event: FormEvent) {
    event.preventDefault()
    cancel.mutate()
  }

  return (
    <>
      <PageHeader
        back={{ to: '/own-shop/sales', label: 'Shop sales' }}
        title={data?.saleNumber ?? 'Shop sale'}
        description={
          data && (
            <>
              {data.customerName ?? 'Walk-in customer'} · {formatDateTime(data.createdAt)}
              {data.createdByName && ` · by ${data.createdByName}`}
            </>
          )
        }
        action={
          data?.status === 'Completed' && (
            <Button
              variant="outline"
              onClick={() => {
                setReason('')
                setIsCancelOpen(true)
              }}
            >
              <XCircle className="size-4" />
              Cancel sale
            </Button>
          )
        }
      />

      {data?.status === 'Cancelled' && (
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <Badge variant="destructive">Cancelled</Badge>
          <span className="text-sm text-destructive">{data.cancellationReason}</span>
          {data.cancelledAt && (
            <span className="text-xs text-muted-foreground">
              {formatDateTime(data.cancelledAt)}
              {data.cancelledByName && ` by ${data.cancelledByName}`}
            </span>
          )}
        </div>
      )}

      <Card>
        <CardContent className="@container px-0">
          {sale.isPending ? (
            <TableSkeleton columns={4} />
          ) : sale.isError ? (
            <ErrorState error={sale.error} />
          ) : (
            data && (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Pappadam</TableHead>
                    <TableHead className="text-right">Pieces</TableHead>
                    <TableHead className="text-right">Rate</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.lines.map((line) => (
                    <TableRow key={line.lineNumber}>
                      <TableCell className="font-medium">{line.description}</TableCell>
                      <TableCell className="text-right tabular-nums">{formatQuantity(line.quantity)}</TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatMoney(line.unitPrice)}
                        {line.unitPrice < line.defaultPrice && (
                          <div className="text-xs text-muted-foreground">
                            standard {formatMoney(line.defaultPrice)}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatMoney(line.lineTotal)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
                <TableFooter>
                  <TableRow>
                    <TableCell colSpan={3}>
                      Paid by {formatPaymentMethod(data.paymentMethod)} · {data.locationName}
                      {data.notes && <div className="text-xs font-normal text-muted-foreground">{data.notes}</div>}
                    </TableCell>
                    <TableCell className="text-right font-semibold tabular-nums">{formatMoney(data.totalAmount)}</TableCell>
                  </TableRow>
                </TableFooter>
              </Table>
            )
          )}
        </CardContent>
      </Card>

      <Dialog open={isCancelOpen} onOpenChange={setIsCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel {data?.saleNumber}?</DialogTitle>
            <DialogDescription>
              The pieces go back on the shop's shelf. The sale is kept, marked cancelled, and its number is never
              reused. Hand any money back at the counter.
            </DialogDescription>
          </DialogHeader>
          <form className="grid gap-4" onSubmit={handleCancel}>
            <div className="grid gap-1.5">
              <Label htmlFor="shop-sale-cancel-reason">
                Reason <span className="text-destructive">*</span>
              </Label>
              <Input
                id="shop-sale-cancel-reason"
                required
                maxLength={300}
                value={reason}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setIsCancelOpen(false)}>
                Keep the sale
              </Button>
              <Button type="submit" variant="destructive" disabled={cancel.isPending || reason.trim() === ''}>
                {cancel.isPending && <Loader2 className="size-4 animate-spin" />}
                Cancel sale
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}
