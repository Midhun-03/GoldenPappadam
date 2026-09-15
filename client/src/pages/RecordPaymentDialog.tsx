import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { customersApi, paymentsApi } from '@/api/sales'
import type { PaymentMethod } from '@/api/types'
import { Alert, AlertDescription } from '@/components/ui/alert'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ApiError } from '@/lib/api'
import { formatDay, formatMoney, formatPaymentMethod, todayInIndia } from '@/lib/format'

const methods: PaymentMethod[] = ['Cash', 'UPI', 'BankTransfer', 'Cheque', 'Other']

export function RecordPaymentDialog({
  open,
  onOpenChange,
  customerId,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Fixed when opened from a customer's page; otherwise the user picks one. */
  customerId?: string
}) {
  const queryClient = useQueryClient()
  const [selectedCustomerId, setSelectedCustomerId] = useState(customerId ?? '')
  const [paymentDate, setPaymentDate] = useState(todayInIndia())
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [reference, setReference] = useState('')
  const [notes, setNotes] = useState('')
  const [chooseBills, setChooseBills] = useState(false)
  const [allocations, setAllocations] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return

    setSelectedCustomerId(customerId ?? '')
    setPaymentDate(todayInIndia())
    setAmount('')
    setMethod('Cash')
    setReference('')
    setNotes('')
    setChooseBills(false)
    setAllocations({})
    setError(null)
  }, [open, customerId])

  const customers = useQuery({
    queryKey: ['customers', { forPayment: true }],
    queryFn: () => customersApi.list(),
    enabled: open && !customerId,
  })

  const outstanding = useQuery({
    queryKey: ['customers', selectedCustomerId, 'outstanding'],
    queryFn: () => customersApi.outstandingInvoices(selectedCustomerId),
    enabled: open && selectedCustomerId !== '',
  })

  const save = useMutation({
    mutationFn: () =>
      paymentsApi.create({
        customerId: selectedCustomerId,
        paymentDate,
        amount: Number(amount),
        method,
        reference: reference.trim() || undefined,
        notes: notes.trim() || undefined,
        allocations: chooseBills
          ? Object.entries(allocations)
              .filter(([, value]) => value.trim() !== '' && Number(value) > 0)
              .map(([invoiceId, value]) => ({ invoiceId, amount: Number(value) }))
          : undefined,
      }),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      await queryClient.invalidateQueries({ queryKey: ['payments'] })
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] })

      const settled = result.payment.allocations.length
      toast.success(
        `${formatMoney(result.payment.amount)} received. ` +
          (settled > 0 ? `Applied to ${settled} bill${settled > 1 ? 's' : ''}. ` : '') +
          `Balance ${formatMoney(result.customerBalance)}.`,
      )

      if (result.payment.unallocatedAmount > 0) {
        toast.info(`${formatMoney(result.payment.unallocatedAmount)} kept on account for the next bill.`)
      }

      onOpenChange(false)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not record the payment.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    save.mutate()
  }

  const bills = outstanding.data ?? []
  const totalOutstanding = bills.reduce((sum, bill) => sum + bill.outstanding, 0)
  const entered = amount.trim() === '' ? 0 : Number(amount)
  const overpaying = entered > totalOutstanding && totalOutstanding > 0

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Record a payment</DialogTitle>
          <DialogDescription>
            Money is applied to the oldest bills first, unless you choose the bills yourself.
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          {!customerId && (
            <div className="grid gap-1.5">
              <Label htmlFor="payment-shop">
                Customer <span className="text-destructive">*</span>
              </Label>
              <Select value={selectedCustomerId} onValueChange={setSelectedCustomerId}>
                <SelectTrigger id="payment-shop" className="w-full">
                  <SelectValue placeholder="Choose a shop" />
                </SelectTrigger>
                <SelectContent>
                  {(customers.data ?? []).map((customer) => (
                    <SelectItem key={customer.id} value={customer.id}>
                      {customer.name} · {formatMoney(customer.balance)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="grid gap-4 sm:grid-cols-3">
            <div className="grid gap-1.5">
              <Label htmlFor="amount">
                Amount <span className="text-destructive">*</span>
              </Label>
              <Input
                id="amount"
                type="number"
                inputMode="decimal"
                step="0.01"
                min="0.01"
                required
                autoFocus
                className="text-right"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="paymentDate">
                Date <span className="text-destructive">*</span>
              </Label>
              <Input
                id="paymentDate"
                type="date"
                required
                value={paymentDate}
                onChange={(event) => setPaymentDate(event.target.value)}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="payment-method">Method</Label>
              <Select value={method} onValueChange={(value) => setMethod(value as PaymentMethod)}>
                <SelectTrigger id="payment-method" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {methods.map((option) => (
                    <SelectItem key={option} value={option}>
                      {formatPaymentMethod(option)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="reference">Reference</Label>
              <Input
                id="reference"
                maxLength={100}
                placeholder="UPI or cheque number"
                value={reference}
                onChange={(event) => setReference(event.target.value)}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="payment-notes">Notes</Label>
              <Input
                id="payment-notes"
                maxLength={300}
                placeholder="Optional"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            </div>
          </div>

          {selectedCustomerId !== '' && (
            <div className="rounded-lg border bg-muted/40 p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="text-sm">
                  {outstanding.isPending ? (
                    'Loading unpaid bills…'
                  ) : (
                    <>
                      <span className="font-medium">{bills.length}</span> unpaid bill
                      {bills.length === 1 ? '' : 's'} ·{' '}
                      <span className="font-medium tabular-nums">{formatMoney(totalOutstanding)}</span>
                    </>
                  )}
                </div>
                {bills.length > 0 && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    aria-pressed={chooseBills}
                    onClick={() => setChooseBills(!chooseBills)}
                  >
                    {chooseBills ? 'Apply to oldest first' : 'Choose bills'}
                  </Button>
                )}
              </div>

              {chooseBills && bills.length > 0 && (
                <div className="mt-3 grid gap-2">
                  {bills.map((bill) => (
                    <div key={bill.invoiceId} className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                      <span className="font-mono text-xs font-medium">{bill.invoiceNumber}</span>
                      <span className="text-xs text-muted-foreground">{formatDay(bill.invoiceDate)}</span>
                      <span className="ml-auto text-xs tabular-nums text-muted-foreground">
                        {formatMoney(bill.outstanding)} due
                      </span>
                      <Input
                        className="w-24 text-right"
                        type="number"
                        inputMode="decimal"
                        step="0.01"
                        min="0"
                        max={bill.outstanding}
                        placeholder="0"
                        aria-label={`Amount to apply to bill ${bill.invoiceNumber}`}
                        value={allocations[bill.invoiceId] ?? ''}
                        onChange={(event) =>
                          setAllocations({ ...allocations, [bill.invoiceId]: event.target.value })
                        }
                      />
                    </div>
                  ))}
                </div>
              )}

              {!chooseBills && overpaying && (
                <p className="mt-2 text-xs text-warning">
                  {formatMoney(entered - totalOutstanding)} more than is due. The extra stays on account for the
                  next bill.
                </p>
              )}
            </div>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending || selectedCustomerId === ''}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {save.isPending ? 'Saving…' : 'Record payment'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
