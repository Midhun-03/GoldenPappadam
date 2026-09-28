import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { toast } from 'sonner'
import { wagesApi } from '@/api/staff'
import type { EmployeeWage, PaymentMethod, WageWeek } from '@/api/types'
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
import { formatMoney, formatPaymentMethod, formatQuantity, todayInIndia } from '@/lib/format'

const methods: PaymentMethod[] = ['Cash', 'UPI', 'BankTransfer', 'Cheque', 'Other']

/**
 * The confirmation before wages are marked paid: who, how much, the total, and how it was paid.
 * The amounts sent back are the ones shown here, so if attendance changed in the meantime the server
 * refuses rather than recording a figure nobody saw.
 */
export function PayWagesDialog({
  week,
  employees,
  open,
  onOpenChange,
}: {
  week: WageWeek
  employees: EmployeeWage[]
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const queryClient = useQueryClient()
  const today = todayInIndia()
  const [paymentDate, setPaymentDate] = useState(today)
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [reference, setReference] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    setError(null)
    setReference('')
    setPaymentDate(today < week.weekEnd ? week.weekEnd : today)
  }, [open, today, week.weekEnd])

  const total = employees.reduce((sum, employee) => sum + employee.payable, 0)

  const pay = useMutation({
    mutationFn: () =>
      wagesApi.pay({
        weekStart: week.weekStart,
        paymentDate,
        method,
        reference: reference.trim() || null,
        notes: null,
        employees: employees.map((employee) => ({ employeeId: employee.employeeId, expectedAmount: employee.payable })),
      }),
    onSuccess: async (response) => {
      await queryClient.invalidateQueries({ queryKey: ['wages'] })
      await queryClient.invalidateQueries({ queryKey: ['expenses'] })
      await queryClient.invalidateQueries({ queryKey: ['attendance'] })
      toast.success(
        `${formatMoney(response.total)} paid to ${response.payments.length} employee${response.payments.length === 1 ? '' : 's'} · added to Employee wages expenses`,
      )
      onOpenChange(false)
    },
    onError: async (caught) => {
      setError(caught instanceof ApiError ? caught.message : 'Could not record the payment.')
      await queryClient.invalidateQueries({ queryKey: ['wages'] })
    },
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {employees.length === 1 ? `Pay ${employees[0].name}?` : `Pay ${employees.length} employees?`}
          </DialogTitle>
          <DialogDescription>
            Wages for {week.label}. Once paid, the amounts are kept exactly as they are now, and the total is
            recorded as an Employee wages expense.
          </DialogDescription>
        </DialogHeader>

        <ul className="max-h-60 divide-y overflow-y-auto rounded-lg border text-sm">
          {employees.map((employee) => (
            <li key={employee.employeeId} className="flex items-center gap-3 px-3 py-2">
              <div className="min-w-0 flex-1">
                <div className="truncate font-medium">{employee.name}</div>
                <div className="text-xs text-muted-foreground">
                  {formatQuantity(employee.daysWorked)} days
                  {employee.adjustmentAmount !== 0 && ` · adjustments ${formatMoney(employee.adjustmentAmount)}`}
                </div>
              </div>
              <span className="font-medium tabular-nums">{formatMoney(employee.payable)}</span>
            </li>
          ))}
          <li className="flex items-center justify-between bg-muted/50 px-3 py-2 font-medium">
            <span>Total</span>
            <span className="tabular-nums">{formatMoney(total)}</span>
          </li>
        </ul>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="grid gap-1.5">
            <Label htmlFor="pay-date">Paid on</Label>
            <Input
              id="pay-date"
              type="date"
              min={week.weekEnd}
              max={today}
              value={paymentDate}
              onChange={(event) => setPaymentDate(event.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="pay-method">Paid by</Label>
            <Select value={method} onValueChange={(value) => setMethod(value as PaymentMethod)}>
              <SelectTrigger id="pay-method" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {methods.map((item) => (
                  <SelectItem key={item} value={item}>
                    {formatPaymentMethod(item)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        {method !== 'Cash' && (
          <div className="grid gap-1.5">
            <Label htmlFor="pay-reference">Reference</Label>
            <Input
              id="pay-reference"
              maxLength={100}
              placeholder="UPI or cheque number"
              value={reference}
              onChange={(event) => setReference(event.target.value)}
            />
          </div>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Not yet
          </Button>
          <Button disabled={pay.isPending || employees.length === 0} onClick={() => pay.mutate()}>
            {pay.isPending && <Loader2 className="size-4 animate-spin" />}
            {pay.isPending ? 'Recording…' : `Mark ${formatMoney(total)} paid`}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
