import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Loader2, PackagePlus } from 'lucide-react'
import { useDeferredValue, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { packingApi, productsApi } from '@/api/inventory'
import type { PackingEntry, PackingPlan } from '@/api/types'
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
 * Packing converts loose stock into packets (and packets into boxes). What the source loses is
 * worked out by the server from the product - packets × pieces ÷ the pappadam's pieces per kg, or
 * packets × weight - and shown here before saving. Packing more than the warehouse holds is refused.
 */
export function PackingPage() {
  const queryClient = useQueryClient()
  const [packedProductId, setPackedProductId] = useState('')
  const [packsProduced, setPacksProduced] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  // One id per packing on this screen, so a second press of Save packs once. A new one after each save.
  const [clientRequestId, setClientRequestId] = useState(() => crypto.randomUUID())

  const packedProducts = useQuery({
    queryKey: ['products', { kind: 'Packed' as const }],
    queryFn: () => productsApi.list({ kind: 'Packed' }),
  })

  const history = useQuery({ queryKey: ['packing'], queryFn: packingApi.history })

  const selected = packedProducts.data?.find((product) => product.id === packedProductId)
  const packs = Number(packsProduced)

  // The server's own calculation, deferred so typing a number does not fire a request per key.
  const request = useDeferredValue({ packedProductId, packsProduced: packs })
  const canPreview = request.packedProductId !== '' && request.packsProduced > 0

  const preview = useQuery({
    queryKey: ['packing-preview', request],
    queryFn: () => packingApi.preview(request),
    enabled: canPreview,
    placeholderData: keepPreviousData,
    retry: false,
  })

  const plan = canPreview && !preview.isError ? preview.data : undefined

  const pack = useMutation({
    mutationFn: () =>
      packingApi.create({ packedProductId, packsProduced: packs, notes: notes.trim() || undefined, clientRequestId }),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['packing'] })
      await queryClient.invalidateQueries({ queryKey: ['packing-preview'] })
      toast.success(
        `Packed ${formatQuantity(result.packsProduced)} × ${result.plan.packedProductName}, using ` +
          `${formatQuantity(result.sourceQuantityUsed)} ${result.plan.sourceUnitCode}`,
      )
      setPacksProduced('')
      setNotes('')
      setClientRequestId(crypto.randomUUID())
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not record the packing.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    pack.mutate()
  }

  const rows = history.data ?? []
  const planIsCurrent = plan?.packedProductId === packedProductId && plan?.packsProduced === packs
  const canSave = planIsCurrent && plan?.shortfall === null && !pack.isPending

  return (
    <>
      <PageHeader title="Packing" description="Turns loose stock into packets, and packets into boxes." />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,22rem)_1fr] lg:gap-5">
        <Card className="lg:sticky lg:top-6 lg:self-start">
          <CardHeader>
            <CardTitle>Record packing</CardTitle>
            <CardDescription>The loose stock it uses is worked out from the product.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="grid gap-4" onSubmit={handleSubmit}>
              <div className="grid gap-1.5">
                <Label htmlFor="packed-product">
                  Packed product <span className="text-destructive">*</span>
                </Label>
                <Select
                  value={packedProductId}
                  onValueChange={(value) => {
                    setPackedProductId(value)
                    setError(null)
                  }}
                >
                  <SelectTrigger id="packed-product" className="w-full">
                    <SelectValue placeholder="Choose a pack" />
                  </SelectTrigger>
                  <SelectContent>
                    {(packedProducts.data ?? [])
                      .filter((product) => product.isActive)
                      .map((product) => (
                        <SelectItem key={product.id} value={product.id}>
                          {product.name}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
                {selected && (
                  <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
                    <span className="truncate">{selected.sourceProductName}</span>
                    <ArrowRight className="size-3 shrink-0" />
                    <span className="truncate">{describeContents(selected.piecesPerPack, selected.sourcePerPack)}</span>
                  </p>
                )}
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="packs">
                  Packs produced <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="packs"
                  type="number"
                  inputMode="numeric"
                  step={selected?.piecesPerPack ? '1' : '0.001'}
                  min="0"
                  required
                  value={packsProduced}
                  onChange={(event) => {
                    setPacksProduced(event.target.value)
                    setError(null)
                  }}
                />
              </div>

              {plan && <Calculation plan={plan} />}

              {canPreview && preview.isError && (
                <Alert variant="destructive">
                  <AlertDescription>
                    {preview.error instanceof ApiError ? preview.error.message : 'Could not work this packing out.'}
                  </AlertDescription>
                </Alert>
              )}

              <div className="grid gap-1.5">
                <Label htmlFor="packing-notes">Note</Label>
                <Input
                  id="packing-notes"
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

              <Button type="submit" disabled={!canSave}>
                {pack.isPending && <Loader2 className="size-4 animate-spin" />}
                {pack.isPending ? 'Saving…' : 'Record packing'}
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent packing</CardTitle>
          </CardHeader>
          <CardContent className="@container px-0">
            {history.isPending ? (
              <TableSkeleton columns={4} />
            ) : history.isError ? (
              <ErrorState error={history.error} />
            ) : rows.length === 0 ? (
              <EmptyState
                icon={PackagePlus}
                title="Nothing packed yet"
                description="Record a packing run and it will be listed here with what it used."
              />
            ) : (
              <Table>
                <TableHeader sticky>
                  <TableRow>
                    <TableHead className="hidden @lg:table-cell">When</TableHead>
                    <TableHead>Packed</TableHead>
                    <TableHead className="text-right">Packs</TableHead>
                    <TableHead className="text-right">Used</TableHead>
                    <TableHead className="hidden text-right @2xl:table-cell">Before → after</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((entry) => (
                    <HistoryRow key={entry.id} entry={entry} />
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

/** "20 pieces (0.1 kg)" for a count-based packet, "0.25 per pack" for anything else. */
function describeContents(piecesPerPack: number | null, sourcePerPack: number | null) {
  if (piecesPerPack !== null) {
    return sourcePerPack === null
      ? `${piecesPerPack} pieces - set pieces per kg on the loose product`
      : `${piecesPerPack} pieces (${formatQuantity(sourcePerPack)} kg)`
  }
  return sourcePerPack === null ? 'contents not set' : `${formatQuantity(sourcePerPack)} per pack`
}

/** Available → used → left, as the server worked it out; the shortfall in place of Save when short. */
function Calculation({ plan }: { plan: PackingPlan }) {
  const unit = plan.sourceUnitCode
  const pieces = (quantity: number) =>
    plan.piecesPerKg === null ? null : `${formatQuantity(quantity * plan.piecesPerKg)} pieces`

  const line = (label: string, quantity: number, piecesText: string | null, strong = false) => (
    <div className="flex items-baseline justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn('text-right tabular-nums', strong && 'font-medium')}>
        {formatQuantity(quantity)} {unit}
        {piecesText && <span className="ml-1.5 text-xs text-muted-foreground">{piecesText}</span>}
      </span>
    </div>
  )

  return (
    <div className="grid gap-1.5 rounded-lg border bg-muted/40 p-3 text-sm" aria-live="polite">
      {line(`${plan.sourceProductName} now`, plan.sourceOnHandBefore, pieces(plan.sourceOnHandBefore))}
      {line(
        'Uses',
        plan.sourceUsed,
        plan.piecesUsed === null ? null : `${formatQuantity(plan.piecesUsed)} pieces`,
        true,
      )}
      {line('Left after', plan.sourceOnHandAfter, pieces(Math.max(plan.sourceOnHandAfter, 0)))}
      {plan.piecesPerKg !== null && (
        <p className="text-xs text-muted-foreground">
          {formatQuantity(plan.packsProduced)} × {plan.piecesPerPack} pieces ÷ {formatQuantity(plan.piecesPerKg)} pieces
          per kg
        </p>
      )}
      {plan.shortfall && <p className="text-xs font-medium text-destructive">{plan.shortfall}</p>}
    </div>
  )
}

function HistoryRow({ entry }: { entry: PackingEntry }) {
  return (
    <TableRow>
      <TableCell className="hidden text-xs text-muted-foreground @lg:table-cell">
        {formatDateTime(entry.occurredAt)}
      </TableCell>
      <TableCell className="max-w-[14rem]">
        <div className="truncate font-medium">{entry.packedProductName}</div>
        <div className="mt-0.5 truncate text-xs text-muted-foreground">from {entry.sourceProductName}</div>
        <div className="text-xs text-muted-foreground @lg:hidden">{formatDateTime(entry.occurredAt)}</div>
      </TableCell>
      <TableCell className="text-right font-medium tabular-nums">{formatQuantity(entry.packsProduced)}</TableCell>
      <TableCell className="text-right tabular-nums">
        {formatQuantity(entry.sourceQuantityUsed)} {entry.sourceUnitCode}
        {entry.piecesPerPack !== null && entry.piecesPerKg !== null && (
          <div className="text-xs text-muted-foreground">
            {formatQuantity(entry.packsProduced * entry.piecesPerPack)} pcs at {formatQuantity(entry.piecesPerKg)}/kg
          </div>
        )}
      </TableCell>
      <TableCell className="hidden text-right tabular-nums text-muted-foreground @2xl:table-cell">
        {entry.sourceOnHandBefore === null || entry.sourceOnHandAfter === null
          ? '-'
          : `${formatQuantity(entry.sourceOnHandBefore)} → ${formatQuantity(entry.sourceOnHandAfter)}`}
      </TableCell>
    </TableRow>
  )
}
