import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { invoicesApi } from '@/api/sales'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDay, formatMoney, formatQuantity } from '@/lib/format'

export function InvoiceDetailPage() {
  const { invoiceId = '' } = useParams()
  const queryClient = useQueryClient()
  const [isCancelOpen, setIsCancelOpen] = useState(false)
  const [reason, setReason] = useState('')

  const invoice = useQuery({ queryKey: ['invoices', invoiceId], queryFn: () => invoicesApi.get(invoiceId) })

  const cancel = useMutation({
    mutationFn: () => invoicesApi.cancel(invoiceId, reason.trim()),
    onSuccess: async (cancelled) => {
      await queryClient.invalidateQueries({ queryKey: ['invoices', invoiceId] })
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      toast.success(`${cancelled.invoiceNumber} cancelled and the stock put back`)
      setIsCancelOpen(false)
      setReason('')
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not cancel the bill.'),
  })

  const data = invoice.data

  return (
    <>
      <PageHeader
        title={data?.invoiceNumber ?? 'Bill'}
        description={data ? `${data.customerName} · ${formatDay(data.invoiceDate)}` : undefined}
        action={
          <div className="flex gap-2">
            <Button variant="outline" asChild>
              <Link to="/invoices">
                <ArrowLeft className="size-4" />
                All bills
              </Link>
            </Button>
            {data?.status === 'Issued' && (
              <Button variant="outline" onClick={() => setIsCancelOpen(true)}>
                Cancel bill
              </Button>
            )}
          </div>
        }
      />

      {data?.status === 'Cancelled' && (
        <Card className="mb-6 border-destructive/40">
          <CardContent className="py-1">
            <Badge variant="destructive">Cancelled</Badge>
            <span className="ml-3 text-sm text-muted-foreground">
              {data.cancellationReason} · stock was returned
            </span>
          </CardContent>
        </Card>
      )}

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <Card>
          <CardHeader>
            <CardTitle>Items</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Product</TableHead>
                  <TableHead className="text-right">Quantity</TableHead>
                  <TableHead className="text-right">Price</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(data?.lines ?? []).map((line) => (
                  <TableRow key={line.id}>
                    <TableCell className="font-medium">{line.description}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {formatQuantity(line.quantity)} {line.unitCode}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoney(line.unitPrice)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoney(line.lineTotal)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            {data?.notes && <p className="mt-4 text-sm text-muted-foreground">{data.notes}</p>}
          </CardContent>
        </Card>

        <Card>
          <CardContent className="grid gap-3">
            <div className="flex justify-between text-sm">
              <span className="text-muted-foreground">Subtotal</span>
              <span className="tabular-nums">{formatMoney(data?.subTotal ?? 0)}</span>
            </div>
            {(data?.discountAmount ?? 0) > 0 && (
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Discount</span>
                <span className="tabular-nums">− {formatMoney(data?.discountAmount ?? 0)}</span>
              </div>
            )}
            <div className="flex justify-between border-t pt-3">
              <span className="font-medium">Total</span>
              <span className="font-heading text-xl font-semibold tabular-nums">
                {formatMoney(data?.totalAmount ?? 0)}
              </span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-muted-foreground">Paid</span>
              <span className="tabular-nums text-emerald-600">{formatMoney(data?.amountPaid ?? 0)}</span>
            </div>
            <div className="flex justify-between text-sm">
              <span className="text-muted-foreground">Outstanding</span>
              <span className="font-medium tabular-nums">{formatMoney(data?.outstanding ?? 0)}</span>
            </div>

            {data && (
              <Button variant="outline" asChild className="mt-2">
                <Link to={`/customers/${data.customerId}`}>Open customer ledger</Link>
              </Button>
            )}
          </CardContent>
        </Card>
      </div>

      <Dialog open={isCancelOpen} onOpenChange={setIsCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel {data?.invoiceNumber}</DialogTitle>
            <DialogDescription>
              The bill is kept and marked cancelled, and every item goes back into stock. A bill with payments
              on it cannot be cancelled.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-2">
            <Label htmlFor="reason">Reason</Label>
            <Input
              id="reason"
              maxLength={300}
              placeholder="Shop refused delivery"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCancelOpen(false)}>
              Keep the bill
            </Button>
            <Button
              variant="destructive"
              disabled={cancel.isPending || reason.trim() === ''}
              onClick={() => cancel.mutate()}
            >
              {cancel.isPending ? 'Cancelling…' : 'Cancel bill'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
