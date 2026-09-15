import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Loader2, PackagePlus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { packingApi, productsApi, stockApi } from '@/api/inventory'
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

export function PackingPage() {
  const queryClient = useQueryClient()
  const [packedProductId, setPackedProductId] = useState('')
  const [packsProduced, setPacksProduced] = useState('')
  const [sourceQuantityUsed, setSourceQuantityUsed] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string | null>(null)

  const packedProducts = useQuery({
    queryKey: ['products', { kind: 'Packed' as const }],
    queryFn: () => productsApi.list({ kind: 'Packed' }),
  })

  const history = useQuery({ queryKey: ['packing'], queryFn: packingApi.history })
  const stock = useQuery({ queryKey: ['stock', {}], queryFn: () => stockApi.onHand() })

  const selected = packedProducts.data?.find((product) => product.id === packedProductId)
  const sourceStock = stock.data?.find((row) => row.productId === selected?.sourceProductId)

  // What the recipe says this run should consume; the user can overwrite it with what really went in.
  const suggested =
    selected && packsProduced.trim() !== ''
      ? Number(packsProduced) * (selected.sourceQuantityPerPack ?? 0)
      : null

  const willUse = sourceQuantityUsed.trim() !== '' ? Number(sourceQuantityUsed) : suggested
  const isShort = willUse !== null && sourceStock !== undefined && willUse > sourceStock.quantityOnHand

  const pack = useMutation({
    mutationFn: () =>
      packingApi.create({
        packedProductId,
        packsProduced: Number(packsProduced),
        sourceQuantityUsed: sourceQuantityUsed.trim() === '' ? undefined : Number(sourceQuantityUsed),
        notes: notes.trim() || undefined,
      }),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['packing'] })
      toast.success(
        `Packed ${formatQuantity(result.packsProduced)} × ${selected?.name}, using ${formatQuantity(
          result.sourceQuantityUsed,
        )} ${sourceStock?.unitCode ?? ''}`.trim(),
      )
      if (result.warning) toast.warning(result.warning)
      setPacksProduced('')
      setSourceQuantityUsed('')
      setNotes('')
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not record the packing.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    pack.mutate()
  }

  const rows = history.data ?? []

  return (
    <>
      <PageHeader title="Packing" description="Turns loose stock into packets, and packets into boxes." />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,22rem)_1fr] lg:gap-5">
        <Card className="lg:sticky lg:top-6 lg:self-start">
          <CardHeader>
            <CardTitle>Record packing</CardTitle>
            <CardDescription>Source stock goes down, packs go up.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="grid gap-4" onSubmit={handleSubmit}>
              <div className="grid gap-1.5">
                <Label htmlFor="packed-product">
                  Packed product <span className="text-destructive">*</span>
                </Label>
                <Select value={packedProductId} onValueChange={setPackedProductId}>
                  <SelectTrigger id="packed-product" className="w-full">
                    <SelectValue placeholder="Choose a pack" />
                  </SelectTrigger>
                  <SelectContent>
                    {(packedProducts.data ?? []).map((product) => (
                      <SelectItem key={product.id} value={product.id}>
                        {product.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {selected && (
                <div className="rounded-lg border bg-muted/40 p-3 text-sm">
                  <div className="flex items-center gap-2">
                    <span className="min-w-0 truncate font-medium">{selected.sourceProductName}</span>
                    <ArrowRight className="size-3.5 shrink-0 text-muted-foreground" />
                    <span className="min-w-0 truncate font-medium">{selected.name}</span>
                  </div>
                  <div className="mt-1 text-xs text-muted-foreground">
                    {formatQuantity(selected.sourceQuantityPerPack ?? 0)} {sourceStock?.unitCode ?? ''} per pack
                    {sourceStock && (
                      <>
                        {' · '}
                        <span className={cn(sourceStock.quantityOnHand <= 0 && 'text-destructive')}>
                          {formatQuantity(sourceStock.quantityOnHand)} {sourceStock.unitCode} available
                        </span>
                      </>
                    )}
                  </div>
                </div>
              )}

              <div className="grid gap-1.5">
                <Label htmlFor="packs">
                  Packs produced <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="packs"
                  type="number"
                  inputMode="decimal"
                  step="0.001"
                  min="0.001"
                  required
                  value={packsProduced}
                  onChange={(event) => setPacksProduced(event.target.value)}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="used">Source used{sourceStock ? ` (${sourceStock.unitCode})` : ''}</Label>
                <Input
                  id="used"
                  type="number"
                  inputMode="decimal"
                  step="0.001"
                  min="0.001"
                  aria-describedby="used-hint"
                  placeholder={suggested === null ? 'Calculated automatically' : formatQuantity(suggested)}
                  value={sourceQuantityUsed}
                  onChange={(event) => setSourceQuantityUsed(event.target.value)}
                />
                <p id="used-hint" className="text-xs text-muted-foreground">
                  Leave empty to use the calculated amount. Enter the real figure when packing loss made it
                  different.
                </p>
              </div>

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

              {isShort && (
                <p className="text-xs text-warning">
                  This uses more than is on hand. It will be recorded and the source stock will go negative.
                </p>
              )}

              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Button type="submit" disabled={pack.isPending || packedProductId === ''}>
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
                    <TableHead className="hidden @xl:table-cell">From</TableHead>
                    <TableHead className="text-right">Used</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((entry) => (
                    <TableRow key={entry.id}>
                      <TableCell className="hidden text-xs text-muted-foreground @lg:table-cell">
                        {formatDateTime(entry.occurredAt)}
                      </TableCell>
                      <TableCell className="max-w-[14rem]">
                        <div className="truncate font-medium">{entry.packedProductName}</div>
                        <div className="mt-0.5 text-xs text-muted-foreground @xl:hidden">
                          from {entry.sourceProductName}
                        </div>
                        <div className="text-xs text-muted-foreground @lg:hidden">
                          {formatDateTime(entry.occurredAt)}
                        </div>
                      </TableCell>
                      <TableCell className="text-right font-medium tabular-nums">
                        {formatQuantity(entry.packsProduced)}
                      </TableCell>
                      <TableCell className="hidden max-w-[12rem] truncate text-muted-foreground @xl:table-cell">
                        {entry.sourceProductName}
                      </TableCell>
                      <TableCell className="text-right tabular-nums text-muted-foreground">
                        {formatQuantity(entry.sourceQuantityUsed)}
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
