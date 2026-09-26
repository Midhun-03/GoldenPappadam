import { FileSpreadsheet, Loader2, Printer } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { reportsApi, type ReportFormat } from '@/api/reports'
import { Button } from '@/components/ui/button'
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
import { ApiError } from '@/lib/api'
import { openInNewTab, saveFile } from '@/lib/files'
import { todayInIndia } from '@/lib/format'

/**
 * A shop's statement of account for a period: what it owed at the start, every bill and payment,
 * and what it owes at the end - to print, hand over or send.
 */
export function StatementDialog({
  customer,
  open,
  onOpenChange,
}: {
  customer: { id: string; name: string }
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const today = todayInIndia()
  const [from, setFrom] = useState(`${today.slice(0, 8)}01`)
  const [to, setTo] = useState(today)
  const [busy, setBusy] = useState<ReportFormat | null>(null)

  useEffect(() => {
    if (!open) return
    setFrom(`${today.slice(0, 8)}01`)
    setTo(today)
  }, [open, today])

  async function make(format: ReportFormat) {
    const request = { path: `statement/${customer.id}`, params: { from, to } }
    setBusy(format)

    try {
      if (format === 'pdf') {
        await openInNewTab(() => reportsApi.file(request, 'pdf'))
      } else {
        await saveFile(() => reportsApi.file(request, 'xlsx'), `Statement-${customer.name}-${from}-to-${to}.xlsx`)
      }
      onOpenChange(false)
    } catch (caught) {
      toast.error(caught instanceof ApiError ? caught.message : 'Could not make the statement.')
    } finally {
      setBusy(null)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Statement for {customer.name}</DialogTitle>
          <DialogDescription>
            What the shop owed at the start, every bill and payment in the period, and what it owes at the end.
          </DialogDescription>
        </DialogHeader>

        <div className="grid grid-cols-2 gap-4">
          <div className="grid gap-1.5">
            <Label htmlFor="statement-from">From</Label>
            <Input id="statement-from" type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="statement-to">To</Label>
            <Input id="statement-to" type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" disabled={busy !== null} onClick={() => make('xlsx')}>
            {busy === 'xlsx' ? <Loader2 className="size-4 animate-spin" /> : <FileSpreadsheet className="size-4" />}
            Excel
          </Button>
          <Button disabled={busy !== null} onClick={() => make('pdf')}>
            {busy === 'pdf' ? <Loader2 className="size-4 animate-spin" /> : <Printer className="size-4" />}
            Print / PDF
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
