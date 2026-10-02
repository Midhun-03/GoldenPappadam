import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Truck } from 'lucide-react'
import { useDeferredValue, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { shopStockApi, shopTransfersApi, type ShopTransferPlan } from '@/api/ownShop'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDateTime, formatQuantity } from '@/lib/format'
import { cn } from '@/lib/utils'

/**
 * Loose pappadam sent from the factory to the own shop. The factory records kg; the shop receives
 * pieces, worked out by the server from the variety's pieces per kg and rounded to the nearest whole
 * piece. Sending more than the warehouse holds is refused.
 */
export function ShopTransfersPage() {
  const queryClient = useQueryClient()
  const [sourceProductId, setSourceProductId] = useState('')
  const [quantityKg, setQuantityKg] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  // One id per transfer on this screen, so a second press of Save records it once.
  const [clientRequestId, setClientRequestId] = useState(() => crypto.randomUUID())

  const varieties = useQuery({ queryKey: ['own-shop', 'stock'], queryFn: () => shopStockApi.list() })
  const history = useQuery({ queryKey: ['own-shop', 'transfers'], queryFn: () => shopTransfersApi.history() })

  const kg = Number(quantityKg)
  const request = useDeferredValue({ sourceProductId, quantityKg: kg })
  const canPreview = request.sourceProductId !== '' && request.quantityKg > 0

  const preview = useQuery({
    queryKey: ['own-shop', 'transfer-preview', request],
    queryFn: () => shopTransfersApi.preview(request),
    enabled: canPreview,
    placeholderData: keepPreviousData,
    retry: false,
  })

  const plan = canPreview && !preview.isError ? preview.data : undefined
  const planIsCurrent = plan?.sourceProductId === sourceProductId && plan?.quantityKg === kg

  const send = useMutation({
    mutationFn: () =>
      shopTransfersApi.create({ sourceProductId, quantityKg: kg, notes: notes.trim() || undefined, clientRequestId }),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['own-shop'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      toast.success(
        `${formatQuantity(result.quantityKg)} kg of ${result.sourceProductName} received at the shop as ` +
          `${formatQuantity(result.piecesReceived)} pieces`,
      )
      setQuantityKg('')
      setNotes('')
      setClientRequestId(crypto.randomUUID())
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not record the transfer.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    send.mutate()
  }

  const options = (varieties.data ?? []).filter((row) => row.isActive)
  const selected = options.find((row) => row.sourceProductId === sourceProductId)
  const rows = history.data ?? []

  return (
    <>
      <PageHeader
        back={{ to: '/own-shop', label: 'Own shop' }}
        title="Receive from factory"
        description="Loose pappadam sent to the shop: kg out of the warehouse, pieces onto the shop's shelf."
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,22rem)_1fr] lg:gap-5">
        <Card className="lg:sticky lg:top-6 lg:self-start">
          <CardHeader>
            <CardTitle>Record a transfer</CardTitle>
            <CardDescription>Enter the kg the factory sent. The pieces are worked out for you.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="grid gap-4" onSubmit={handleSubmit}>
              <div className="grid gap-1.5">
                <Label htmlFor="transfer-variety">
                  Pappadam <span className="text-destructive">*</span>
                </Label>
                <Select
                  value={sourceProductId}
                  onValueChange={(value) => {
                    setSourceProductId(value)
                    setError(null)
                  }}
                >
                  <SelectTrigger id="transfer-variety" className="w-full">
                    <SelectValue placeholder="Choose a variety" />
                  </SelectTrigger>
                  <SelectContent>
                    {options.map((row) => (
                      <SelectItem key={row.sourceProductId} value={row.sourceProductId}>
                        {row.sourceProductName}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {selected && (
                  <p className="text-xs text-muted-foreground">
                    {formatQuantity(selected.factoryKg)} kg at the factory
                    {selected.piecesPerKg !== null && ` · ${formatQuantity(selected.piecesPerKg)} pieces per kg`}
                  </p>
                )}
                {varieties.isSuccess && options.length === 0 && (
                  <p className="text-xs text-muted-foreground">
                    No variety is set up for the shop yet. Add a "Shop pieces" product on the Products screen.
                  </p>
                )}
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="transfer-kg">
                  Kg sent <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="transfer-kg"
                  type="number"
                  inputMode="decimal"
                  step="0.001"
                  min="0"
                  required
                  value={quantityKg}
                  onChange={(event) => {
                    setQuantityKg(event.target.value)
                    setError(null)
                  }}
                />
              </div>

              {plan && <Calculation plan={plan} />}

              {canPreview && preview.isError && (
                <Alert variant="destructive">
                  <AlertDescription>
                    {preview.error instanceof ApiError ? preview.error.message : 'Could not work this transfer out.'}
                  </AlertDescription>
                </Alert>
              )}

              <div className="grid gap-1.5">
                <Label htmlFor="transfer-notes">Note</Label>
                <Input
                  id="transfer-notes"
                  maxLength={300}
                  placeholder="Optional"
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                />
              </div>

              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Button type="submit" disabled={!planIsCurrent || plan?.shortfall !== null || send.isPending}>
                {send.isPending && <Loader2 className="size-4 animate-spin" />}
                {send.isPending ? 'Saving…' : 'Record transfer'}
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Transfers</CardTitle>
          </CardHeader>
          <CardContent className="@container px-0">
            {history.isPending ? (
              <TableSkeleton columns={4} />
            ) : history.isError ? (
              <ErrorState error={history.error} />
            ) : rows.length === 0 ? (
              <EmptyState
                icon={Truck}
                title="Nothing sent to the shop yet"
                description="Each transfer is listed here with the conversion it used."
              />
            ) : (
              <Table>
                <TableHeader sticky>
                  <TableRow>
                    <TableHead className="hidden @lg:table-cell">When</TableHead>
                    <TableHead>Pappadam</TableHead>
                    <TableHead className="text-right">Kg</TableHead>
                    <TableHead className="text-right">Pieces</TableHead>
                    <TableHead className="hidden text-right @2xl:table-cell">Factory · shop</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((row) => (
                    <TableRow key={row.id}>
                      <TableCell className="hidden text-xs text-muted-foreground @lg:table-cell">
                        {formatDateTime(row.occurredAt)}
                        {row.createdByName && <div>by {row.createdByName}</div>}
                      </TableCell>
                      <TableCell className="max-w-[14rem]">
                        <div className="truncate font-medium">{row.sourceProductName}</div>
                        <div className="text-xs text-muted-foreground @lg:hidden">{formatDateTime(row.occurredAt)}</div>
                        {row.notes && <div className="truncate text-xs text-muted-foreground">{row.notes}</div>}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatQuantity(row.quantityKg)}</TableCell>
                      <TableCell className="text-right">
                        <div className="font-medium tabular-nums">{formatQuantity(row.piecesReceived)}</div>
                        <div className="text-xs text-muted-foreground">at {formatQuantity(row.piecesPerKg)}/kg</div>
                      </TableCell>
                      <TableCell className="hidden text-right text-xs tabular-nums text-muted-foreground @2xl:table-cell">
                        <div>
                          {formatQuantity(row.sourceOnHandBefore)} → {formatQuantity(row.sourceOnHandAfter)} kg
                        </div>
                        <div>
                          {formatQuantity(row.shopOnHandBefore)} → {formatQuantity(row.shopOnHandAfter)} pcs
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>
    </>
  )
}

/** Kg × pieces per kg, and both ends before and after, as the server worked it out. */
function Calculation({ plan }: { plan: ShopTransferPlan }) {
  const line = (label: string, value: string, strong = false) => (
    <div className="flex items-baseline justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn('text-right tabular-nums', strong && 'font-medium')}>{value}</span>
    </div>
  )

  return (
    <div className="grid gap-1.5 rounded-lg border bg-muted/40 p-3 text-sm" aria-live="polite">
      {line('Shop receives', `${formatQuantity(plan.pieces)} pieces`, true)}
      <p className="text-xs text-muted-foreground">
        {formatQuantity(plan.quantityKg)} kg × {formatQuantity(plan.piecesPerKg)} pieces per kg, to the nearest piece
      </p>
      {line(
        'Factory',
        `${formatQuantity(plan.factoryOnHandBefore)} → ${formatQuantity(plan.factoryOnHandAfter)} kg`,
      )}
      {line('Shop', `${formatQuantity(plan.shopOnHandBefore)} → ${formatQuantity(plan.shopOnHandAfter)} pieces`)}
      {plan.shortfall && <p className="text-xs font-medium text-destructive">{plan.shortfall}</p>}
    </div>
  )
}
