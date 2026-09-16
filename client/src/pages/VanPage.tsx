import { useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, PackageCheck, Smartphone, Truck } from 'lucide-react'
import { useState } from 'react'
import { vanLoadsApi } from '@/api/fieldsales'
import { stockApi } from '@/api/inventory'
import type { VanLoadDirection } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatQuantity, todayInIndia } from '@/lib/format'
import { cn } from '@/lib/utils'
import { VanLoadDialog } from './VanLoadDialog'

/**
 * The van's day. The number that matters is the last column: what the van is still holding after
 * the evening return. It should be zero, and when it is not, the office is told rather than the
 * books being quietly balanced — an unrecorded sale and a miscount look identical to arithmetic.
 */
export function VanPage() {
  const [businessDate, setBusinessDate] = useState(todayInIndia())
  const [vanId, setVanId] = useState('')
  const [dialog, setDialog] = useState<VanLoadDirection | null>(null)
  const queryClient = useQueryClient()

  const locations = useQuery({ queryKey: ['stock', 'locations'], queryFn: stockApi.locations })
  const vans = (locations.data ?? []).filter((location) => location.kind === 'Van')
  const selectedVan = vans.find((van) => van.id === vanId) ?? vans[0]

  const day = useQuery({
    queryKey: ['van-loads', 'reconciliation', selectedVan?.id, businessDate],
    queryFn: () => vanLoadsApi.reconciliation(selectedVan!.id, businessDate),
    enabled: selectedVan !== undefined,
  })

  const loads = useQuery({
    queryKey: ['van-loads', 'list', selectedVan?.id, businessDate],
    queryFn: () => vanLoadsApi.list({ vanLocationId: selectedVan!.id, from: businessDate, to: businessDate }),
    enabled: selectedVan !== undefined,
  })

  const onVan = useQuery({
    queryKey: ['van-loads', 'on-van', selectedVan?.id],
    queryFn: () => vanLoadsApi.onVan(selectedVan!.id),
    enabled: selectedVan !== undefined,
  })

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['van-loads'] }),
      queryClient.invalidateQueries({ queryKey: ['stock'] }),
    ])

  const lines = day.data?.lines ?? []
  const unsettled = lines.filter((line) => line.unaccounted !== 0)

  // Loads the salesman entered himself. Both are legitimate — the packing book says what was
  // packed, his book says what he took — but the office should know which it is looking at.
  const fromTheVan = (loads.data ?? []).filter((load) => load.deviceName !== null)

  return (
    <>
      <PageHeader
        title="Van"
        description="What went out this morning, what sold, and what came back."
        action={
          <>
            <Button variant="outline" onClick={() => setDialog('Return')} disabled={!selectedVan}>
              <PackageCheck className="size-4" />
              Return unsold
            </Button>
            <Button onClick={() => setDialog('Loading')} disabled={!selectedVan}>
              <Truck className="size-4" />
              Load the van
            </Button>
          </>
        }
      />

      <FilterBar>
        {vans.length > 1 && (
          <div className="grid gap-1.5">
            <Label htmlFor="van">Van</Label>
            <Select value={selectedVan?.id ?? ''} onValueChange={setVanId}>
              <SelectTrigger id="van" className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {vans.map((van) => (
                  <SelectItem key={van.id} value={van.id}>
                    {van.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}
        <div className="grid gap-1.5">
          <Label htmlFor="van-date">Day</Label>
          <Input
            id="van-date"
            type="date"
            className="w-44"
            value={businessDate}
            onChange={(event) => setBusinessDate(event.target.value)}
          />
        </div>
      </FilterBar>

      {day.data && lines.length > 0 && (
        <div
          className={cn(
            'mb-4 flex items-start gap-2.5 rounded-lg border px-3 py-2.5 text-sm lg:mb-5',
            day.data.isSettled
              ? 'border-success/30 bg-success/5 text-success'
              : 'border-warning/30 bg-warning/5 text-warning',
          )}
        >
          {day.data.isSettled ? (
            <CheckCircle2 className="mt-0.5 size-4 shrink-0" />
          ) : (
            <AlertTriangle className="mt-0.5 size-4 shrink-0" />
          )}
          <p>
            {day.data.isSettled
              ? `Everything on ${formatDay(businessDate)} is accounted for.`
              : `${unsettled.length} ${unsettled.length === 1 ? 'product is' : 'products are'} still on the van for ${formatDay(businessDate)}. Record what happened — a sale, damage, or a recount — rather than leaving it.`}
          </p>
        </div>
      )}

      {fromTheVan.length > 0 && (
        <div className="mb-4 flex items-start gap-2.5 rounded-lg border border-info/30 bg-info/5 px-3 py-2.5 text-sm lg:mb-5">
          <Smartphone className="mt-0.5 size-4 shrink-0 text-info" />
          <p>
            {fromTheVan.length === 1 ? 'A load was' : `${fromTheVan.length} loads were`} entered on the
            phone by {[...new Set(fromTheVan.map((load) => load.deviceName))].join(', ')} — what the
            salesman actually took, rather than what was packed.
          </p>
        </div>
      )}

      <Card>
        <CardContent className="px-0">
          {day.isPending ? (
            <TableSkeleton columns={5} />
          ) : day.isError ? (
            <ErrorState error={day.error} />
          ) : lines.length === 0 ? (
            <EmptyState
              icon={Truck}
              title="Nothing on the van that day"
              description="Load the van to start the day. Everything it carries, sells and brings back appears here."
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Product</TableHead>
                  <TableHead className="text-right">Loaded</TableHead>
                  <TableHead className="text-right">Sold</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Returned</TableHead>
                  <TableHead className="text-right">Unaccounted</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {lines.map((line) => (
                  <TableRow key={line.productId}>
                    <TableCell className="max-w-[10rem] truncate">
                      {line.productName}
                      {line.opening !== 0 && (
                        <div className="text-xs text-muted-foreground">
                          {formatQuantity(line.opening)} carried over
                        </div>
                      )}
                      {line.other !== 0 && (
                        <div className="text-xs text-muted-foreground">
                          {formatQuantity(line.other)} adjusted or damaged
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{formatQuantity(line.loaded)}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatQuantity(line.sold)}</TableCell>
                    <TableCell className="hidden text-right tabular-nums sm:table-cell">
                      {formatQuantity(line.returned)}
                    </TableCell>
                    <TableCell
                      className={cn(
                        'text-right font-medium tabular-nums',
                        line.unaccounted === 0 ? 'text-muted-foreground' : 'text-warning',
                      )}
                    >
                      {line.unaccounted === 0 ? '—' : formatQuantity(line.unaccounted)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {selectedVan && (
        <VanLoadDialog
          open={dialog !== null}
          onOpenChange={(open) => setDialog(open ? dialog : null)}
          direction={dialog ?? 'Loading'}
          vanLocationId={selectedVan.id}
          vanName={selectedVan.name}
          onVan={onVan.data ?? []}
          onSaved={refresh}
        />
      )}
    </>
  )
}
