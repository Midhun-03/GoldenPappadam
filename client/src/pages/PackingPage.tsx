import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { packingApi, productsApi, stockApi } from '@/api/inventory'
import { PageHeader } from '@/components/PageHeader'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDateTime, formatQuantity } from '@/lib/format'

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
      await queryClient.invalidateQueries({ queryKey: ['movements'] })
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

  return (
    <>
      <PageHeader title="Packing" description="Turns loose stock into packets, and packets into boxes." />

      <div className="grid gap-6 lg:grid-cols-[380px_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Record packing</CardTitle>
            <CardDescription>Source stock goes down, packs go up.</CardDescription>
          </CardHeader>
          <CardContent>
            <form className="grid gap-4" onSubmit={handleSubmit}>
              <div className="grid gap-2">
                <Label>Packed product</Label>
                <Select value={packedProductId} onValueChange={setPackedProductId}>
                  <SelectTrigger>
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
                {selected && (
                  <p className="text-xs text-muted-foreground">
                    From {selected.sourceProductName} · {formatQuantity(selected.sourceQuantityPerPack ?? 0)} per
                    pack
                    {sourceStock &&
                      ` · ${formatQuantity(sourceStock.quantityOnHand)} ${sourceStock.unitCode} available`}
                  </p>
                )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="packs">Packs produced</Label>
                <Input
                  id="packs"
                  type="number"
                  step="0.001"
                  min="0.001"
                  required
                  value={packsProduced}
                  onChange={(event) => setPacksProduced(event.target.value)}
                />
              </div>

              <div className="grid gap-2">
                <Label htmlFor="used">
                  Source used{sourceStock ? ` (${sourceStock.unitCode})` : ''}
                </Label>
                <Input
                  id="used"
                  type="number"
                  step="0.001"
                  min="0.001"
                  placeholder={suggested === null ? 'Calculated automatically' : formatQuantity(suggested)}
                  value={sourceQuantityUsed}
                  onChange={(event) => setSourceQuantityUsed(event.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  Leave empty to use the calculated amount. Enter the real figure when packing loss made it
                  different.
                </p>
              </div>

              <div className="grid gap-2">
                <Label htmlFor="packing-notes">Note (optional)</Label>
                <Input
                  id="packing-notes"
                  maxLength={300}
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                />
              </div>

              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Button type="submit" disabled={pack.isPending || packedProductId === ''}>
                {pack.isPending ? 'Saving…' : 'Record packing'}
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Recent packing</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>When</TableHead>
                  <TableHead>Packed</TableHead>
                  <TableHead className="text-right">Packs</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead className="text-right">Used</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(history.data ?? []).map((entry) => (
                  <TableRow key={entry.id}>
                    <TableCell className="whitespace-nowrap text-muted-foreground">
                      {formatDateTime(entry.occurredAt)}
                    </TableCell>
                    <TableCell className="font-medium">{entry.packedProductName}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatQuantity(entry.packsProduced)}</TableCell>
                    <TableCell>{entry.sourceProductName}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {formatQuantity(entry.sourceQuantityUsed)}
                    </TableCell>
                  </TableRow>
                ))}

                {history.data?.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="py-10 text-center text-muted-foreground">
                      Nothing packed yet.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      </div>
    </>
  )
}
