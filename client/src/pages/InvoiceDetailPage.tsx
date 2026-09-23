import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Ban,
  CheckCircle2,
  Download,
  FileText,
  Loader2,
  Mail,
  MoreHorizontal,
  Printer,
  RefreshCw,
  Store,
  Wallet,
  XCircle,
} from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { invoicesApi } from '@/api/sales'
import type { InvoiceDetail, InvoiceParty } from '@/api/types'
import { ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { useStateName } from '@/lib/indianStates'
import { formatDateTime, formatDay, formatMoney, formatQuantity } from '@/lib/format'
import { cn } from '@/lib/utils'
import { documentTitle, useInvoicePdf } from '@/lib/invoicePdf'
import { EmailInvoiceDialog } from './InvoiceActions'

function Amount({
  label,
  value,
  className,
}: {
  label: string
  value: string
  className?: string
}) {
  return (
    <div className="flex justify-between text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className={cn('tabular-nums', className)}>{value}</span>
    </div>
  )
}

export function InvoiceDetailPage() {
  const { invoiceId = '' } = useParams()
  const queryClient = useQueryClient()
  const pdf = useInvoicePdf()
  const [isCancelOpen, setIsCancelOpen] = useState(false)
  const [isEmailOpen, setIsEmailOpen] = useState(false)
  const [reason, setReason] = useState('')

  const invoice = useQuery({ queryKey: ['invoices', invoiceId], queryFn: () => invoicesApi.get(invoiceId) })
  const emails = useQuery({ queryKey: ['invoices', invoiceId, 'emails'], queryFn: () => invoicesApi.emails(invoiceId) })

  const cancel = useMutation({
    mutationFn: () => invoicesApi.cancel(invoiceId, reason.trim()),
    onSuccess: async (cancelled) => {
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      toast.success(`${cancelled.invoiceNumber} cancelled and the stock put back`)
      setIsCancelOpen(false)
      setReason('')
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not cancel the bill.'),
  })

  const generate = useMutation({
    mutationFn: () => invoicesApi.generatePdf(invoiceId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      toast.success('PDF made and stored')
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not make the PDF.'),
  })

  const data = invoice.data

  if (invoice.isError) {
    return (
      <>
        <PageHeader back={{ to: '/invoices', label: 'Bills' }} title="Bill" />
        <Card>
          <CardContent className="px-0">
            <ErrorState error={invoice.error} />
          </CardContent>
        </Card>
      </>
    )
  }

  const isIssued = data?.status === 'Issued'

  return (
    <>
      <PageHeader
        back={{ to: '/invoices', label: 'Bills' }}
        title={data?.invoiceNumber ?? 'Bill'}
        description={
          !data ? (
            <Skeleton className="h-4 w-48" />
          ) : (
            <>
              {documentTitle(data.documentType)} ·{' '}
              <Link to={`/customers/${data.customerId}`} className="font-medium hover:underline">
                {data.customerName}
              </Link>
              {data.branchName && <> &middot; {data.branchName}</>}
              {' · '}
              {formatDay(data.invoiceDate)}
            </>
          )
        }
        action={
          data && (
            <>
              <Button variant="outline" disabled={pdf.isBusy(data.id, 'print')} onClick={() => pdf.print(data.id)}>
                {pdf.isBusy(data.id, 'print') ? <Loader2 className="size-4 animate-spin" /> : <Printer className="size-4" />}
                Print
              </Button>
              {isIssued && (
                <Button variant="outline" onClick={() => setIsEmailOpen(true)}>
                  <Mail className="size-4" />
                  Email
                </Button>
              )}
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="outline" size="icon" aria-label="More actions">
                    <MoreHorizontal className="size-4" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onSelect={() => pdf.view(data.id)}>
                    <FileText />
                    Open PDF
                  </DropdownMenuItem>
                  <DropdownMenuItem onSelect={() => pdf.download(data.id, data.invoiceNumber)}>
                    <Download />
                    Download PDF
                  </DropdownMenuItem>
                  {isIssued && (
                    <>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem variant="destructive" onSelect={() => setIsCancelOpen(true)}>
                        <Ban />
                        Cancel bill
                      </DropdownMenuItem>
                    </>
                  )}
                </DropdownMenuContent>
              </DropdownMenu>
            </>
          )
        }
      />

      {data?.status === 'Cancelled' && (
        <div className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-destructive/25 bg-destructive-surface px-3 py-2.5 lg:mb-5">
          <Badge variant="destructive">Cancelled</Badge>
          <span className="text-sm text-destructive">{data.cancellationReason}</span>
          <span className="text-sm text-muted-foreground">
            Every item went back into stock. The number {data.invoiceNumber} stays on record.
          </span>
        </div>
      )}

      {data && (
        <DocumentStatus
          invoice={data}
          lastEmail={emails.data?.[0]}
          isGenerating={generate.isPending}
          onGenerate={() => generate.mutate()}
          onEmail={() => setIsEmailOpen(true)}
        />
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-5">
        <div className="grid gap-4 lg:gap-5">
          <Card>
            <CardHeader>
              <CardTitle>Items</CardTitle>
            </CardHeader>
            <CardContent className="px-0">
              {!data ? (
                <div className="grid gap-3 px-4">
                  {Array.from({ length: 3 }, (_, index) => (
                    <Skeleton key={index} className="h-5" />
                  ))}
                </div>
              ) : (
                <ItemsTable invoice={data} />
              )}

              {data?.notes && <p className="mt-4 px-4 text-sm text-muted-foreground">{data.notes}</p>}
            </CardContent>
          </Card>

          {data && <PartiesCard invoice={data} />}

          {(emails.data?.length ?? 0) > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>Email history</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-3">
                {emails.data!.map((attempt) => (
                  <div key={attempt.id} className="flex items-start gap-3 text-sm">
                    {attempt.status === 'Sent' ? (
                      <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" />
                    ) : (
                      <XCircle className="mt-0.5 size-4 shrink-0 text-destructive" />
                    )}
                    <div className="min-w-0">
                      <div>
                        <span className="font-medium">{attempt.status === 'Sent' ? 'Sent' : 'Failed'}</span> to{' '}
                        <span className="break-all">{attempt.recipient}</span>
                      </div>
                      {attempt.errorMessage && <div className="text-destructive">{attempt.errorMessage}</div>}
                      <div className="text-xs text-muted-foreground">
                        {formatDateTime(attempt.attemptedAt)}
                        {attempt.sentByName && ` · ${attempt.sentByName}`}
                      </div>
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          )}
        </div>

        <Card className="lg:sticky lg:top-6">
          <CardContent className="grid gap-3">
            {data ? <Totals invoice={data} /> : <Skeleton className="h-32" />}

            <Amount
              label="Paid"
              value={formatMoney(data?.amountPaid ?? 0)}
              className={data?.amountPaid ? 'text-success' : undefined}
            />
            <Amount
              label="Outstanding"
              value={formatMoney(data?.outstanding ?? 0)}
              className={cn('font-medium', (data?.outstanding ?? 0) > 0 && 'text-destructive')}
            />

            {data && (
              <div className="mt-1 grid gap-2">
                <Button variant="outline" asChild>
                  <Link to={`/customers/${data.customerId}`}>
                    <Store className="size-4" />
                    Customer ledger
                  </Link>
                </Button>
                {data.outstanding > 0 && (
                  <Button asChild>
                    <Link to="/payments">
                      <Wallet className="size-4" />
                      Record a payment
                    </Link>
                  </Button>
                )}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      <EmailInvoiceDialog invoice={data ?? null} open={isEmailOpen} onOpenChange={setIsEmailOpen} />

      <Dialog open={isCancelOpen} onOpenChange={setIsCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel {data?.invoiceNumber}?</DialogTitle>
            <DialogDescription>
              The bill is kept and marked cancelled, and every item goes back into stock. Its number is never reused.
              A bill that already has payments on it cannot be cancelled.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor="reason">
              Reason <span className="text-destructive">*</span>
            </Label>
            <Input
              id="reason"
              maxLength={300}
              placeholder="Shop refused delivery"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCancelOpen(false)}>
              Keep the bill
            </Button>
            <Button
              variant="destructive"
              disabled={cancel.isPending || reason.trim() === ''}
              onClick={() => cancel.mutate()}
            >
              {cancel.isPending && <Loader2 className="size-4 animate-spin" />}
              {cancel.isPending ? 'Cancelling…' : 'Cancel bill'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

/** Finalized, PDF, email: the three things the office needs to know, in one line. */
function DocumentStatus({
  invoice,
  lastEmail,
  isGenerating,
  onGenerate,
  onEmail,
}: {
  invoice: InvoiceDetail
  lastEmail: { status: 'Sent' | 'Failed'; recipient: string } | undefined
  isGenerating: boolean
  onGenerate: () => void
  onEmail: () => void
}) {
  return (
    <div className="mb-4 flex flex-wrap items-center gap-x-5 gap-y-2 rounded-lg border bg-card px-3 py-2.5 text-sm lg:mb-5">
      <span className="text-muted-foreground">
        Finalized {formatDateTime(invoice.finalizedAt)}
        {invoice.finalizedByName && <> by {invoice.finalizedByName}</>}
        {invoice.recordedOnDevice && <> from {invoice.recordedOnDevice}</>}
      </span>

      <span className="flex items-center gap-1.5">
        PDF
        {invoice.document ? (
          <Badge variant="success">Stored</Badge>
        ) : (
          <>
            <Badge variant="warning">Not made yet</Badge>
            <Button size="sm" variant="ghost" disabled={isGenerating} onClick={onGenerate}>
              {isGenerating ? <Loader2 className="size-3.5 animate-spin" /> : <RefreshCw className="size-3.5" />}
              Make PDF
            </Button>
          </>
        )}
      </span>

      <span className="flex items-center gap-1.5">
        Email
        {!lastEmail ? (
          <Badge variant="outline">Not sent</Badge>
        ) : lastEmail.status === 'Sent' ? (
          <Badge variant="success">Sent</Badge>
        ) : (
          <>
            <Badge variant="destructive">Failed</Badge>
            {invoice.status === 'Issued' && (
              <Button size="sm" variant="ghost" onClick={onEmail}>
                <RefreshCw className="size-3.5" />
                Retry
              </Button>
            )}
          </>
        )}
      </span>

      {invoice.status === 'Cancelled' && invoice.cancelledAt && (
        <span className="text-muted-foreground">
          Cancelled {formatDateTime(invoice.cancelledAt)}
          {invoice.cancelledByName && <> by {invoice.cancelledByName}</>}
        </span>
      )}
    </div>
  )
}

function ItemsTable({ invoice }: { invoice: InvoiceDetail }) {
  const taxed = invoice.documentType === 'TaxInvoice'
  const hasDiscount = invoice.lines.some((line) => line.discountAmount > 0)

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Product</TableHead>
          <TableHead className="text-right">Quantity</TableHead>
          <TableHead className="hidden text-right sm:table-cell">Rate</TableHead>
          {hasDiscount && <TableHead className="hidden text-right md:table-cell">Disc.</TableHead>}
          {taxed && <TableHead className="hidden text-right md:table-cell">Taxable</TableHead>}
          {taxed && <TableHead className="hidden text-right md:table-cell">GST</TableHead>}
          <TableHead className="text-right">Amount</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {invoice.lines.map((line) => {
          const tax = line.cgstAmount + line.sgstAmount + line.igstAmount + line.cessAmount

          return (
            <TableRow key={line.id}>
              <TableCell className="max-w-[16rem]">
                <div className="truncate font-medium">{line.description}</div>
                <div className="mt-0.5 text-xs text-muted-foreground">
                  {line.hsnCode && <>HSN {line.hsnCode}</>}
                  {line.taxTreatment && line.taxTreatment !== 'Taxable' && (
                    <>
                      {line.hsnCode && ' · '}
                      {line.taxTreatment === 'NilRated' ? 'Nil rated' : line.taxTreatment === 'NonGst' ? 'Non-GST' : 'Exempt'}
                    </>
                  )}
                  <span className="sm:hidden">
                    {(line.hsnCode || (line.taxTreatment && line.taxTreatment !== 'Taxable')) && ' · '}at{' '}
                    {formatMoney(line.unitPrice)}
                  </span>
                </div>
              </TableCell>
              <TableCell className="text-right tabular-nums">
                {formatQuantity(line.quantity)} <span className="text-xs text-muted-foreground">{line.unitCode}</span>
              </TableCell>
              <TableCell className="hidden text-right tabular-nums sm:table-cell">{formatMoney(line.unitPrice)}</TableCell>
              {hasDiscount && (
                <TableCell className="hidden text-right tabular-nums text-muted-foreground md:table-cell">
                  {line.discountAmount > 0 ? formatMoney(line.discountAmount) : '—'}
                </TableCell>
              )}
              {taxed && (
                <TableCell className="hidden text-right tabular-nums md:table-cell">
                  {formatMoney(line.taxableValue)}
                </TableCell>
              )}
              {taxed && (
                <TableCell className="hidden text-right tabular-nums md:table-cell">
                  {line.gstRate > 0 ? (
                    <>
                      {formatMoney(tax)} <span className="text-xs text-muted-foreground">@ {line.gstRate}%</span>
                    </>
                  ) : (
                    <span className="text-muted-foreground">—</span>
                  )}
                </TableCell>
              )}
              <TableCell className="text-right font-medium tabular-nums">
                {formatMoney(taxed ? line.amount : line.lineTotal)}
              </TableCell>
            </TableRow>
          )
        })}
      </TableBody>
    </Table>
  )
}

function Totals({ invoice }: { invoice: InvoiceDetail }) {
  const taxed = invoice.documentType === 'TaxInvoice'

  return (
    <>
      <Amount label="Gross amount" value={formatMoney(invoice.subTotal)} />
      {invoice.discountAmount > 0 && <Amount label="Discount" value={`− ${formatMoney(invoice.discountAmount)}`} />}
      {taxed && (
        <>
          <Amount label="Taxable value" value={formatMoney(invoice.taxableAmount)} />
          {invoice.isInterState ? (
            <Amount label="IGST" value={formatMoney(invoice.igstAmount)} />
          ) : (
            <>
              <Amount label="CGST" value={formatMoney(invoice.cgstAmount)} />
              <Amount label="SGST" value={formatMoney(invoice.sgstAmount)} />
            </>
          )}
          {invoice.cessAmount > 0 && <Amount label="Cess" value={formatMoney(invoice.cessAmount)} />}
        </>
      )}
      {invoice.roundOff !== 0 && (
        <Amount label="Round off" value={`${invoice.roundOff > 0 ? '+' : '−'} ${formatMoney(Math.abs(invoice.roundOff))}`} />
      )}

      <div className="flex items-baseline justify-between border-t pt-3">
        <span className="text-sm font-medium">Grand total</span>
        <span className="font-heading text-2xl font-semibold tabular-nums">{formatMoney(invoice.totalAmount)}</span>
      </div>

      {taxed && invoice.pricesIncludeTax && (
        <p className="-mt-1 text-xs text-muted-foreground">Rates include GST.</p>
      )}
    </>
  )
}

/** Who and where, exactly as printed: the invoice's own copy, not today's customer record. */
function PartiesCard({ invoice }: { invoice: InvoiceDetail }) {
  const stateName = useStateName()

  const party = (label: string, value: InvoiceParty) => (
    <div className="grid content-start gap-0.5 text-sm">
      <div className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{label}</div>
      <div className="font-medium">{value.name}</div>
      {value.address && <div>{value.address}</div>}
      {value.phone && <div className="text-muted-foreground">{value.phone}</div>}
      {value.gstin && <div className="font-mono text-xs">GSTIN {value.gstin}</div>}
      {value.stateCode && <div className="text-muted-foreground">{stateName(value.stateCode)}</div>}
    </div>
  )

  return (
    <Card>
      <CardHeader>
        <CardTitle>As printed</CardTitle>
        <CardDescription>
          Copied onto the invoice when it was finalized. Later changes to the customer or branch do not alter it.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {party('Bill to', invoice.customer)}
        {invoice.branch && party('Deliver to (branch)', invoice.branch)}
        <div className="grid content-start gap-0.5 text-sm">
          <div className="text-xs font-medium tracking-wide text-muted-foreground uppercase">Tax</div>
          <div>
            {invoice.supplier.gstin ? (
              <>
                Supplier GSTIN <span className="font-mono text-xs">{invoice.supplier.gstin}</span>
              </>
            ) : (
              'No GSTIN on file: no GST charged'
            )}
          </div>
          {invoice.placeOfSupplyStateCode && (
            <div className="text-muted-foreground">Place of supply {stateName(invoice.placeOfSupplyStateCode)}</div>
          )}
          {invoice.documentType === 'TaxInvoice' && (
            <div className="text-muted-foreground">{invoice.isInterState ? 'Inter-state: IGST' : 'Intra-state: CGST + SGST'}</div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
