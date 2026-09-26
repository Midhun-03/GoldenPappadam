import { useQuery } from '@tanstack/react-query'
import { FileSpreadsheet, Loader2, Printer } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { reportsApi, type ReportFormat, type ReportRequest } from '@/api/reports'
import { ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/lib/api'
import { openInNewTab, saveFile } from '@/lib/files'
import { shiftDay, todayInIndia } from '@/lib/format'
import { ReportView } from './ReportView'

type Props = {
  path: string
  title: string
  description: string
  /** A report over a period (from–to), or a position on one day (as on). */
  mode: 'range' | 'asOf'
}

/** The first day of the month a plain date falls in: "2026-09-23" -> "2026-09-01". */
const monthStart = (day: string) => `${day.slice(0, 8)}01`

/**
 * One page for every report: pick the day or period, read it on screen, then print it or take it
 * to Excel. The figures come from the server, the same in all three.
 */
export function ReportPage({ path, title, description, mode }: Props) {
  const today = todayInIndia()
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(today)
  const [busy, setBusy] = useState<ReportFormat | null>(null)

  const request: ReportRequest =
    mode === 'range' ? { path, params: { from, to } } : { path, params: { asOf: to } }

  const report = useQuery({ queryKey: ['reports', path, request.params], queryFn: () => reportsApi.get(request) })

  async function exportAs(format: ReportFormat) {
    setBusy(format)
    try {
      const fileName = `${path}-${mode === 'range' && from !== to ? `${from}-to-${to}` : to}.${format}`
      if (format === 'pdf') {
        await openInNewTab(() => reportsApi.file(request, 'pdf'))
      } else {
        await saveFile(() => reportsApi.file(request, 'xlsx'), fileName)
      }
    } catch (caught) {
      toast.error(caught instanceof ApiError ? caught.message : 'Could not make the file.')
    } finally {
      setBusy(null)
    }
  }

  const setPeriod = (start: string, end: string) => {
    setFrom(start)
    setTo(end)
  }

  return (
    <>
      <PageHeader
        title={title}
        description={report.data ? report.data.period : description}
        action={
          <>
            <Button variant="outline" disabled={busy !== null || !report.data} onClick={() => exportAs('pdf')}>
              {busy === 'pdf' ? <Loader2 className="size-4 animate-spin" /> : <Printer className="size-4" />}
              Print / PDF
            </Button>
            <Button variant="outline" disabled={busy !== null || !report.data} onClick={() => exportAs('xlsx')}>
              {busy === 'xlsx' ? <Loader2 className="size-4 animate-spin" /> : <FileSpreadsheet className="size-4" />}
              Excel
            </Button>
          </>
        }
      />

      <FilterBar>
        {mode === 'range' ? (
          <>
            <FilterField label="From" htmlFor="report-from">
              <Input id="report-from" type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} />
            </FilterField>
            <FilterField label="To" htmlFor="report-to">
              <Input id="report-to" type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} />
            </FilterField>
            <div className="col-span-2 flex flex-wrap gap-2 sm:col-span-1">
              <Button variant="outline" size="sm" onClick={() => setPeriod(today, today)}>
                Today
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPeriod(shiftDay(today, -1), shiftDay(today, -1))}
              >
                Yesterday
              </Button>
              <Button variant="outline" size="sm" onClick={() => setPeriod(monthStart(today), today)}>
                This month
              </Button>
            </div>
          </>
        ) : (
          <FilterField label="As on" htmlFor="report-as-of">
            <Input id="report-as-of" type="date" value={to} max={today} onChange={(e) => setTo(e.target.value)} />
          </FilterField>
        )}
      </FilterBar>

      {report.isError ? (
        <ErrorState error={report.error} />
      ) : !report.data ? (
        <div className="grid gap-3">
          <Skeleton className="h-20" />
          <Skeleton className="h-64" />
        </div>
      ) : (
        <ReportView report={report.data} />
      )}
    </>
  )
}

export const SalesReportPage = () => (
  <ReportPage
    path="sales"
    title="Sales report"
    description="Every bill, and the same sales by product, shop and who billed it."
    mode="range"
  />
)

export const CollectionsReportPage = () => (
  <ReportPage
    path="collections"
    title="Collections"
    description="Money received, how it was paid and which bills it settled."
    mode="range"
  />
)

export const OutstandingReportPage = () => (
  <ReportPage
    path="outstanding"
    title="Outstanding credit"
    description="What every shop owes, and for how long."
    mode="asOf"
  />
)

export const StockReportPage = () => (
  <ReportPage
    path="stock"
    title="Stock movement"
    description="What there was, everything that happened to it, and what is left - for each product and place."
    mode="range"
  />
)

export const ReturnsReportPage = () => (
  <ReportPage
    path="returns"
    title="Returns and expiry"
    description="Packets shops gave back, what they got for them, and our own stock written off as expired."
    mode="range"
  />
)
