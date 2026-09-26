import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Loader2, Printer, Scale } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { returnsApi, settlementRequest, type SettlementState } from '@/api/returns'
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
import { openInNewTab } from '@/lib/files'
import { formatDateTime, formatDay, formatMoney, formatQuantity } from '@/lib/format'
import { ReturnSettlementFields } from './ReturnSettlementFields'
import { SettlementBadge } from './ReturnsPage'

export function ReturnDetailPage() {
  const { returnId = '' } = useParams()
  const queryClient = useQueryClient()
  const [isSettleOpen, setIsSettleOpen] = useState(false)
  const [isCancelOpen, setIsCancelOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [settlement, setSettlement] = useState<SettlementState>({ settlement: 'Credit', creditAmount: '', locationId: '' })
  const [isPrinting, setIsPrinting] = useState(false)

  const note = useQuery({ queryKey: ['returns', returnId], queryFn: () => returnsApi.get(returnId) })
  const data = note.data

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['returns'] })
    await queryClient.invalidateQueries({ queryKey: ['stock'] })
    await queryClient.invalidateQueries({ queryKey: ['customers'] })
    await queryClient.invalidateQueries({ queryKey: ['invoices'] })
    await queryClient.invalidateQueries({ queryKey: ['payments'] })
  }

  const settle = useMutation({
    mutationFn: () => returnsApi.settle(returnId, settlementRequest(settlement)),
    onSuccess: async (result) => {
      await refresh()
      setIsSettleOpen(false)
      toast.success(`${result.return.returnNumber} settled`)
      result.warnings.forEach((warning) => toast.warning(warning))
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not settle the return.'),
  })

  const cancel = useMutation({
    mutationFn: () => returnsApi.cancel(returnId, reason.trim()),
    onSuccess: async () => {
      await refresh()
      setIsCancelOpen(false)
      toast.success('Return cancelled')
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not cancel the return.'),
  })

  async function print() {
    setIsPrinting(true)
    try {
      await openInNewTab(() => returnsApi.pdf(returnId))
    } catch (caught) {
      toast.error(caught instanceof ApiError ? caught.message : 'Could not make the PDF.')
    } finally {
      setIsPrinting(false)
    }
  }

  if (note.isError) {
    return (
      <>
        <PageHeader back={{ to: '/returns', label: 'Returns' }} title="Return" />
        <ErrorState error={note.error} />
      </>
    )
  }

  const isRecorded = data?.status === 'Recorded'
  const isPending = isRecorded && data?.settlement === 'Pending'
  // A credit counts like a payment, and payments are never taken back.
  const canCancel = isRecorded && data?.settlement !== 'Credit'

  return (
    <>
      <PageHeader
        back={{ to: '/returns', label: 'Returns' }}
        title={data?.returnNumber ?? 'Return'}
        description={data ? `${formatDay(data.returnDate)} · ${data.customerName}${data.branchName ? ` – ${data.branchName}` : ''}` : undefined}
        action={
          data && (
            <>
              {isPending && (
                <Button onClick={() => setIsSettleOpen(true)}>
                  <Scale className="size-4" />
                  Decide
                </Button>
              )}
              <Button variant="outline" disabled={isPrinting} onClick={print}>
                {isPrinting ? <Loader2 className="size-4 animate-spin" /> : <Printer className="size-4" />}
                Print
              </Button>
              {canCancel && (
                <Button variant="outline" onClick={() => setIsCancelOpen(true)}>
                  <Ban className="size-4" />
                  Cancel
                </Button>
              )}
            </>
          )
        }
      />

      {data?.status === 'Cancelled' && (
        <div className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-destructive/25 bg-destructive-surface px-3 py-2.5 lg:mb-5">
          <Badge variant="destructive">Cancelled</Badge>
          <span className="text-sm text-destructive">{data.cancellationReason}</span>
          <span className="text-sm text-muted-foreground">
            {data.settlement === 'Replacement' && 'The fresh packets went back into stock. '}
            The number {data.returnNumber} stays on record.
          </span>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-5">
        <Card>
          <CardHeader>
            <CardTitle>Packets returned</CardTitle>
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
                    <TableHead className="hidden text-right sm:table-cell">Rate</TableHead>
                    <TableHead className="text-right">Value</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.lines.map((line) => (
                    <TableRow key={line.id}>
                      <TableCell>
                        <div className="font-medium">{line.description}</div>
                        <div className="text-xs text-muted-foreground">{line.reason}</div>
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {formatQuantity(line.quantity)} {line.unitCode}
                      </TableCell>
                      <TableCell className="hidden text-right tabular-nums sm:table-cell">
                        {formatMoney(line.unitRate)}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatMoney(line.value)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
            {data?.notes && <p className="mt-4 px-4 text-sm text-muted-foreground">{data.notes}</p>}
          </CardContent>
        </Card>

        <Card className="lg:sticky lg:top-6">
          <CardContent className="grid gap-3 text-sm">
            {!data ? (
              <Skeleton className="h-32" />
            ) : (
              <>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Settled by</span>
                  <SettlementBadge item={data} />
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Value of packets</span>
                  <span className="font-medium tabular-nums">{formatMoney(data.value)}</span>
                </div>
                {data.settlement === 'Credit' && (
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Credited to the shop</span>
                    <span className="font-medium tabular-nums text-success">{formatMoney(data.creditAmount)}</span>
                  </div>
                )}
                {data.replacementFrom && (
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Fresh packets from</span>
                    <span>{data.replacementFrom}</span>
                  </div>
                )}
                <div className="grid gap-0.5 border-t pt-3 text-xs text-muted-foreground">
                  <span>
                    Recorded {formatDateTime(data.recordedAt)}
                    {data.recordedByName && ` by ${data.recordedByName}`}
                  </span>
                  {data.settledAt && data.settlement !== 'Pending' && <span>Settled {formatDateTime(data.settledAt)}</span>}
                  {data.cancelledAt && (
                    <span>
                      Cancelled {formatDateTime(data.cancelledAt)}
                      {data.cancelledByName && ` by ${data.cancelledByName}`}
                    </span>
                  )}
                </div>
                <Button variant="outline" asChild>
                  <Link to={`/customers/${data.customerId}`}>Customer ledger</Link>
                </Button>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <Dialog open={isSettleOpen} onOpenChange={setIsSettleOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>What does the shop get?</DialogTitle>
            <DialogDescription>
              {data?.returnNumber} · packets worth {formatMoney(data?.value ?? 0)}. This can be decided only once.
            </DialogDescription>
          </DialogHeader>

          <ReturnSettlementFields
            value={settlement}
            onChange={setSettlement}
            packetsValue={data?.value ?? 0}
            allowPending={false}
          />

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsSettleOpen(false)}>
              Not yet
            </Button>
            <Button disabled={settle.isPending} onClick={() => settle.mutate()}>
              {settle.isPending && <Loader2 className="size-4 animate-spin" />}
              {settle.isPending ? 'Saving…' : 'Save decision'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isCancelOpen} onOpenChange={setIsCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel {data?.returnNumber}?</DialogTitle>
            <DialogDescription>
              The return is kept and marked cancelled; its number is never reused.
              {data?.settlement === 'Replacement' && ' The fresh packets given to the shop go back into stock.'}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor="return-cancel-reason">
              Reason <span className="text-destructive">*</span>
            </Label>
            <Input
              id="return-cancel-reason"
              maxLength={300}
              placeholder="Recorded against the wrong shop"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCancelOpen(false)}>
              Keep the return
            </Button>
            <Button variant="destructive" disabled={cancel.isPending || reason.trim() === ''} onClick={() => cancel.mutate()}>
              {cancel.isPending && <Loader2 className="size-4 animate-spin" />}
              {cancel.isPending ? 'Cancelling…' : 'Cancel return'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
