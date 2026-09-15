import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Loader2, Store, Wallet } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { invoicesApi } from '@/api/sales'
import { ErrorState } from '@/components/EmptyState'
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
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDay, formatMoney, formatQuantity } from '@/lib/format'
import { cn } from '@/lib/utils'

function Amount({
  label,
  value,
  className,
}: {
  label: string
  value: string
  className?: string
}) {
  return (
    <div className="flex justify-between text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn('tabular-nums', className)}>{value}</span>
    </div>
  )
}

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

  if (invoice.isError) {
    return (
      <>
        <PageHeader back={{ to: '/invoices', label: 'Bills' }} title="Bill" />
        <Card>
          <CardContent className="px-0">
            <ErrorState error={invoice.error} />
          </CardContent>
        </Card>
      </>
    )
  }

  return (
    <>
      <PageHeader
        back={{ to: '/invoices', label: 'Bills' }}
        title={data?.invoiceNumber ?? 'Bill'}
        description={
          !data ? (
            <Skeleton className="h-4 w-48" />
          ) : (
            <>
              <Link to={`/customers/${data.customerId}`} className="font-medium hover:underline">
                {data.customerName}
              </Link>
              {' · '}
              {formatDay(data.invoiceDate)}
            </>
          )
        }
        action={
          data?.status === 'Issued' && (
            <Button variant="outline" onClick={() => setIsCancelOpen(true)}>
              <Ban className="size-4" />
              Cancel bill
            </Button>
          )
        }
      />

      {data?.status === 'Cancelled' && (
        <div className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-destructive/25 bg-destructive-surface px-3 py-2.5 lg:mb-5">
          <Badge variant="destructive">Cancelled</Badge>
          <span className="text-sm text-destructive">{data.cancellationReason}</span>
          <span className="text-sm text-muted-foreground">Every item went back into stock.</span>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-5">
        <Card>
          <CardHeader>
            <CardTitle>Items</CardTitle>
          </CardHeader>
          <CardContent className="px-0">
            {!data ? (
              <div className="grid gap-3 px-4">
                {Array.from({ length: 3 }, (_, index) => (
                  <Skeleton key={index} className="h-5" />
                ))}
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Product</TableHead>
                    <TableHead className="text-right">Quantity</TableHead>
                    <TableHead className="hidden text-right sm:table-cell">Price</TableHead>
                    <TableHead className="text-right">Total</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.lines.map((line) => (
                    <TableRow key={line.id}>
                      <TableCell className="max-w-[16rem]">
                        <div className="truncate font-medium">{line.description}</div>
                        <div className="mt-0.5 text-xs text-muted-foreground sm:hidden">
                          at {formatMoney(line.unitPrice)}
                        </div>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatQuantity(line.quantity)}{' '}
                        <span className="text-xs text-muted-foreground">{line.unitCode}</span>
                      </TableCell>
                      <TableCell className="hidden text-right tabular-nums sm:table-cell">
                        {formatMoney(line.unitPrice)}
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {formatMoney(line.lineTotal)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}

            {data?.notes && <p className="mt-4 px-4 text-sm text-muted-foreground">{data.notes}</p>}
          </CardContent>
        </Card>

        <Card className="lg:sticky lg:top-6">
          <CardContent className="grid gap-3">
            <Amount label="Subtotal" value={formatMoney(data?.subTotal ?? 0)} />
            {(data?.discountAmount ?? 0) > 0 && (
              <Amount label="Discount" value={`− ${formatMoney(data?.discountAmount ?? 0)}`} />
            )}

            <div className="flex items-baseline justify-between border-t pt-3">
              <span className="text-sm font-medium">Total</span>
              <span className="font-heading text-2xl font-semibold tabular-nums">
                {formatMoney(data?.totalAmount ?? 0)}
              </span>
            </div>

            <Amount
              label="Paid"
              value={formatMoney(data?.amountPaid ?? 0)}
              className={data?.amountPaid ? 'text-success' : undefined}
            />
            <Amount
              label="Outstanding"
              value={formatMoney(data?.outstanding ?? 0)}
              className={cn('font-medium', (data?.outstanding ?? 0) > 0 && 'text-destructive')}
            />

            {data && (
              <div className="mt-1 grid gap-2">
                <Button variant="outline" asChild>
                  <Link to={`/customers/${data.customerId}`}>
                    <Store className="size-4" />
                    Customer ledger
                  </Link>
                </Button>
                {data.outstanding > 0 && (
                  <Button asChild>
                    <Link to="/payments">
                      <Wallet className="size-4" />
                      Record a payment
                    </Link>
                  </Button>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <Dialog open={isCancelOpen} onOpenChange={setIsCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel {data?.invoiceNumber}?</DialogTitle>
            <DialogDescription>
              The bill is kept and marked cancelled, and every item goes back into stock. A bill that already has
              payments on it cannot be cancelled.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor="reason">
              Reason <span className="text-destructive">*</span>
            </Label>
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
              {cancel.isPending && <Loader2 className="size-4 animate-spin" />}
              {cancel.isPending ? 'Cancelling…' : 'Cancel bill'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
