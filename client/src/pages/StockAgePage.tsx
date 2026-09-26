import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FileSpreadsheet, Hourglass, Loader2, PackageOpen, Printer, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { stockAgeApi } from '@/api/inventory'
import { reportsApi, type ReportFormat } from '@/api/reports'
import type { StockAgeRow } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { openInNewTab, saveFile } from '@/lib/files'
import { formatDay, formatQuantity, todayInIndia } from '@/lib/format'
import { cn } from '@/lib/utils'

const WAREHOUSE_CODE = 'MAIN'

/** Blank for nothing, so the eye goes to the numbers that matter. */
function Amount({ value, tone }: { value: number; tone?: 'warning' | 'danger' }) {
  if (value === 0) return <span className="text-muted-foreground">—</span>
  return (
    <span
      className={cn(
        'tabular-nums',
        tone === 'warning' && 'font-medium text-warning',
        tone === 'danger' && 'font-semibold text-destructive',
      )}
    >
      {formatQuantity(value)}
    </span>
  )
}

/**
 * How old the stock is in each place, counted from packing. This is where the office decides what
 * to repack and writes off what has expired - the system never does either on its own.
 */
export function StockAgePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [writingOff, setWritingOff] = useState<StockAgeRow | null>(null)
  const [busy, setBusy] = useState<ReportFormat | null>(null)

  const rows = useQuery({ queryKey: ['stock', 'age'], queryFn: stockAgeApi.list })

  const writeOff = useMutation({
    mutationFn: (row: StockAgeRow) => stockAgeApi.writeOff(row.productId, row.locationId),
    onSuccess: async (_, row) => {
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      toast.success(`${formatQuantity(row.expired)} ${row.unitCode} of ${row.productName} written off as expired`)
      setWritingOff(null)
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not write it off.'),
  })

  async function exportAs(format: ReportFormat) {
    const request = { path: 'stock-age', params: {} }
    setBusy(format)
    try {
      if (format === 'pdf') await openInNewTab(() => reportsApi.file(request, 'pdf'))
      else await saveFile(() => reportsApi.file(request, 'xlsx'), `stock-age-${todayInIndia()}.xlsx`)
    } catch (caught) {
      toast.error(caught instanceof ApiError ? caught.message : 'Could not make the file.')
    } finally {
      setBusy(null)
    }
  }

  const data = rows.data ?? []
  const places = [...new Set(data.map((row) => row.locationName))]
  const bandNotes = [...new Map(data.map((row) => [row.shelfLifeDays, row])).values()]

  return (
    <>
      <PageHeader
        title="Stock age"
        description="How old the stock is, counted from packing. Repack or write off from here."
        action={
          <>
            <Button variant="outline" disabled={busy !== null} onClick={() => exportAs('pdf')}>
              {busy === 'pdf' ? <Loader2 className="size-4 animate-spin" /> : <Printer className="size-4" />}
              Print / PDF
            </Button>
            <Button variant="outline" disabled={busy !== null} onClick={() => exportAs('xlsx')}>
              {busy === 'xlsx' ? <Loader2 className="size-4 animate-spin" /> : <FileSpreadsheet className="size-4" />}
              Excel
            </Button>
          </>
        }
      />

      {rows.isPending ? (
        <Card>
          <CardContent className="px-0">
            <TableSkeleton columns={5} />
          </CardContent>
        </Card>
      ) : rows.isError ? (
        <ErrorState error={rows.error} />
      ) : data.length === 0 ? (
        <Card>
          <CardContent>
            <EmptyState
              icon={Hourglass}
              title="No stock with a shelf life"
              description="Set the shelf life on each product (pappadam lasts 20 days) and its stock shows up here by age."
              action={
                <Button asChild variant="outline">
                  <Link to="/products">Go to products</Link>
                </Button>
              }
            />
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4 lg:gap-5">
          {places.map((place) => (
            <Card key={place}>
              <CardHeader>
                <CardTitle>{place}</CardTitle>
                {bandNotes.map((row) => (
                  <CardDescription key={row.shelfLifeDays}>
                    {row.shelfLifeDays}-day shelf life: fresh {row.freshLabel}, worth repacking {row.repackLabel},
                    ageing {row.ageingLabel}, expiring soon {row.expiringLabel}, expired after {row.shelfLifeDays} days.
                  </CardDescription>
                ))}
              </CardHeader>
              <CardContent className="px-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Product</TableHead>
                      <TableHead className="hidden text-right md:table-cell">Fresh</TableHead>
                      <TableHead className="text-right">To repack</TableHead>
                      <TableHead className="hidden text-right md:table-cell">Ageing</TableHead>
                      <TableHead className="hidden text-right sm:table-cell">Expiring</TableHead>
                      <TableHead className="text-right">Expired</TableHead>
                      <TableHead className="hidden text-right lg:table-cell">In stock</TableHead>
                      <TableHead className="w-0">
                        <span className="sr-only">Actions</span>
                      </TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {data
                      .filter((row) => row.locationName === place)
                      .map((row) => {
                        const inWarehouse = row.locationCode === WAREHOUSE_CODE
                        const toRepack = row.repackWindow > 0 ? row.repackWindow : row.total - row.expired

                        return (
                          <TableRow key={`${row.productId}-${row.locationId}`}>
                            <TableCell className="max-w-[14rem]">
                              <div className="truncate font-medium">{row.productName}</div>
                              <div className="text-xs text-muted-foreground">
                                {formatQuantity(row.total)} {row.unitCode}
                                {row.oldestPackedOn && <> · oldest packed {formatDay(row.oldestPackedOn)}</>}
                              </div>
                            </TableCell>
                            <TableCell className="hidden text-right md:table-cell">
                              <Amount value={row.fresh} />
                            </TableCell>
                            <TableCell className="text-right">
                              <Amount value={row.repackWindow} tone="warning" />
                            </TableCell>
                            <TableCell className="hidden text-right md:table-cell">
                              <Amount value={row.ageing} />
                            </TableCell>
                            <TableCell className="hidden text-right sm:table-cell">
                              <Amount value={row.expiringSoon} tone="warning" />
                            </TableCell>
                            <TableCell className="text-right">
                              <Amount value={row.expired} tone="danger" />
                            </TableCell>
                            <TableCell className="hidden text-right tabular-nums lg:table-cell">
                              {formatQuantity(row.total)}
                            </TableCell>
                            <TableCell>
                              <div className="flex justify-end gap-1">
                                {inWarehouse && toRepack > 0 && (
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => navigate(`/repacking?from=${row.productId}&quantity=${toRepack}`)}
                                  >
                                    <PackageOpen className="size-4" />
                                    <span className="hidden sm:inline">Repack</span>
                                  </Button>
                                )}
                                {row.expired > 0 && (
                                  <Button variant="outline" size="sm" onClick={() => setWritingOff(row)}>
                                    <Trash2 className="size-4" />
                                    <span className="hidden sm:inline">Write off</span>
                                  </Button>
                                )}
                              </div>
                            </TableCell>
                          </TableRow>
                        )
                      })}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          ))}
          <p className="text-xs text-muted-foreground">
            Van stock comes back to the warehouse before it is repacked, so Repack is offered for the warehouse only.
          </p>
        </div>
      )}

      <Dialog open={writingOff !== null} onOpenChange={(open) => !open && setWritingOff(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Write off expired stock?</DialogTitle>
            <DialogDescription>
              {writingOff && (
                <>
                  {formatQuantity(writingOff.expired)} {writingOff.unitCode} of {writingOff.productName} at{' '}
                  {writingOff.locationName} {writingOff.expired === 1 ? 'is' : 'are'} past the {writingOff.shelfLifeDays}
                  -day shelf life. They are recorded as damaged and taken off stock; the rest keeps its age.
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setWritingOff(null)}>
              Keep for now
            </Button>
            <Button
              variant="destructive"
              disabled={writeOff.isPending}
              onClick={() => writingOff && writeOff.mutate(writingOff)}
            >
              {writeOff.isPending && <Loader2 className="size-4 animate-spin" />}
              Write off
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
