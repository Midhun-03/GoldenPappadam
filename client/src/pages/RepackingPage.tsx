import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Loader2, PackageOpen } from 'lucide-react'
import { useDeferredValue, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { productsApi, repackingApi, type RepackRequest } from '@/api/inventory'
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

/**
 * Unsold packets opened and packed again - the same size or another - when the office decides they
 * have sat too long. Nothing is lost: the server works out exactly how many packets come out and
 * sends any pieces that do not fill one more packet back to loose stock. The new packets start a
 * fresh shelf life.
 */
export function RepackingPage() {
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()
  const [fromProductId, setFromProductId] = useState(searchParams.get('from') ?? '')
  const [quantity, setQuantity] = useState(searchParams.get('quantity') ?? '')
  const [toProductId, setToProductId] = useState(searchParams.get('from') ?? '')
  const [notes, setNotes] = useState('')

  const packed = useQuery({
    queryKey: ['products', { kind: 'Packed' as const }],
    queryFn: () => productsApi.list({ kind: 'Packed' }),
  })
  const history = useQuery({ queryKey: ['repacking'], queryFn: repackingApi.history })

  const request: RepackRequest = {
    fromProductId,
    fromQuantity: Number(quantity),
    toProductId,
    notes: notes.trim() || undefined,
  }
  const deferred = useDeferredValue(request)
  const canPreview = deferred.fromProductId !== '' && deferred.toProductId !== '' && deferred.fromQuantity > 0

  const preview = useQuery({
    queryKey: ['repacking', 'preview', deferred.fromProductId, deferred.fromQuantity, deferred.toProductId],
    queryFn: () => repackingApi.preview(deferred),
    enabled: canPreview,
    placeholderData: keepPreviousData,
    retry: false,
  })
  const plan = canPreview && !preview.isError ? preview.data : undefined

  const repack = useMutation({
    mutationFn: () => repackingApi.create(request),
    onSuccess: async ({ result }) => {
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['repacking'] })
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      toast.success(
        `Opened ${formatQuantity(result.fromQuantity)} × ${result.fromProductName}, made ` +
          `${formatQuantity(result.toQuantity)} × ${result.toProductName}`,
      )
      if (result.warning) toast.warning(result.warning)
      setQuantity('')
      setNotes('')
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not repack.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    repack.mutate()
  }

  const choose = (id: string) => {
    setFromProductId(id)
    // Most repacking is into the same size, with a fresh date; start there.
    if (toProductId === '' || toProductId === fromProductId) setToProductId(id)
  }

  return (
    <>
      <PageHeader
        title="Repacking"
        description="Open unsold packets and pack them again. Nothing is lost, and the new packets start a fresh shelf life."
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem] lg:items-start lg:gap-5">
        <Card>
          <CardHeader>
            <CardTitle>Repack</CardTitle>
            <CardDescription>Done at the warehouse. Bring the van's stock back first if that is where the packets are.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="grid gap-4" onSubmit={handleSubmit}>
              <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_8rem]">
                <div className="grid gap-1.5">
                  <Label htmlFor="repack-from">Open packets of</Label>
                  <Select value={fromProductId} onValueChange={choose}>
                    <SelectTrigger id="repack-from" className="w-full">
                      <SelectValue placeholder="Choose a product" />
                    </SelectTrigger>
                    <SelectContent>
                      {(packed.data ?? []).map((product) => (
                        <SelectItem key={product.id} value={product.id}>
                          {product.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-1.5">
                  <Label htmlFor="repack-quantity">How many</Label>
                  <Input
                    id="repack-quantity"
                    type="number"
                    inputMode="numeric"
                    min="1"
                    step="1"
                    className="text-right"
                    value={quantity}
                    onChange={(event) => setQuantity(event.target.value)}
                  />
                </div>
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="repack-to">Pack them as</Label>
                <Select value={toProductId} onValueChange={setToProductId}>
                  <SelectTrigger id="repack-to" className="w-full">
                    <SelectValue placeholder="Choose a product" />
                  </SelectTrigger>
                  <SelectContent>
                    {(packed.data ?? []).map((product) => (
                      <SelectItem key={product.id} value={product.id}>
                        {product.name}
                        {product.id === fromProductId ? ' (same size, fresh date)' : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="repack-notes">Notes</Label>
                <Input
                  id="repack-notes"
                  maxLength={300}
                  placeholder="Back from the van, packets getting old"
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                />
              </div>

              {canPreview && preview.isError && (
                <Alert variant="destructive">
                  <AlertDescription>
                    {preview.error instanceof ApiError ? preview.error.message : 'Could not work out the repack.'}
                  </AlertDescription>
                </Alert>
              )}

              {plan && (
                <div className="grid gap-2 rounded-lg border bg-muted/40 p-3 text-sm">
                  <div className="flex flex-wrap items-center gap-2 font-medium">
                    <span>
                      {formatQuantity(plan.fromQuantity)} × {plan.fromProductName}
                    </span>
                    <ArrowRight className="size-4 text-muted-foreground" />
                    <span>
                      {formatQuantity(plan.toQuantity)} × {plan.toProductName}
                    </span>
                  </div>
                  {plan.leftoverQuantity > 0 && (
                    <p className="text-muted-foreground">
                      {formatQuantity(plan.leftoverQuantity)} {plan.looseUnitCode} left over go back to{' '}
                      {plan.leftoverProductName}.
                    </p>
                  )}
                  {plan.warning && <p className="text-warning">{plan.warning}</p>}
                </div>
              )}

              <Button type="submit" className="justify-self-start" disabled={!plan || repack.isPending}>
                {repack.isPending ? <Loader2 className="size-4 animate-spin" /> : <PackageOpen className="size-4" />}
                {repack.isPending ? 'Repacking…' : 'Repack'}
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent repacking</CardTitle>
          </CardHeader>
          <CardContent className="px-0">
            {history.isPending ? (
              <TableSkeleton columns={2} />
            ) : history.isError ? (
              <ErrorState error={history.error} />
            ) : history.data.length === 0 ? (
              <EmptyState icon={PackageOpen} title="Nothing repacked yet" description="Repacked batches show up here." />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Repacked</TableHead>
                    <TableHead className="text-right">Made</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {history.data.map((entry) => (
                    <TableRow key={entry.id}>
                      <TableCell className="max-w-[12rem]">
                        <div className="truncate">
                          {formatQuantity(entry.fromQuantity)} × {entry.fromProductName}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {formatDateTime(entry.occurredAt)}
                          {entry.leftoverQuantity > 0 && (
                            <> · {formatQuantity(entry.leftoverQuantity)} to {entry.leftoverProductName}</>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="tabular-nums">{formatQuantity(entry.toQuantity)}</div>
                        <div className="max-w-[9rem] truncate text-xs text-muted-foreground">{entry.toProductName}</div>
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
