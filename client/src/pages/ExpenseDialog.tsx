import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { expenseCategoriesApi, expensesApi } from '@/api/accounting'
import type { Expense, PaymentMethod, SaveExpense } from '@/api/types'
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
import { formatPaymentMethod, todayInIndia } from '@/lib/format'

const NONE = 'none'
const methods: PaymentMethod[] = ['Cash', 'UPI', 'BankTransfer', 'Cheque', 'Other']

const empty = {
  categoryId: '',
  expenseDate: '',
  amount: '',
  description: '',
  paymentMethod: 'Cash' as string,
  reference: '',
  reason: '',
}

/**
 * Adds or edits an expense. Editing keeps the version it replaces, so the reason typed here is saved
 * with the old figures. Employee wages is not offered: wages arrive by being paid.
 */
export function ExpenseDialog({
  open,
  onOpenChange,
  expense,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  expense: Expense | null
}) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState(empty)
  const [error, setError] = useState<string | null>(null)

  const categories = useQuery({
    queryKey: ['expense-categories', 'all'],
    queryFn: () => expenseCategoriesApi.list(true),
    enabled: open,
  })

  // Active categories, plus the expense's own one if it has since been deactivated.
  const choices = (categories.data ?? []).filter(
    (category) => !category.isSystem && (category.isActive || category.id === expense?.categoryId),
  )

  useEffect(() => {
    if (!open) return

    setError(null)
    setForm(
      expense
        ? {
            categoryId: expense.categoryId,
            expenseDate: expense.expenseDate,
            amount: String(expense.amount),
            description: expense.description ?? '',
            paymentMethod: expense.paymentMethod ?? NONE,
            reference: expense.reference ?? '',
            reason: '',
          }
        : { ...empty, expenseDate: todayInIndia() },
    )
  }, [open, expense])

  const save = useMutation({
    mutationFn: (payload: SaveExpense) =>
      expense ? expensesApi.update(expense.id, payload) : expensesApi.create(payload),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['expenses'] })
      toast.success(expense ? `Expense updated · ${saved.categoryName}` : `Expense added · ${saved.categoryName}`)
      onOpenChange(false)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the expense.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    if (!form.categoryId) {
      setError('Choose a category.')
      return
    }

    save.mutate({
      categoryId: form.categoryId,
      expenseDate: form.expenseDate,
      amount: Number(form.amount),
      description: form.description.trim() || null,
      paymentMethod: form.paymentMethod === NONE ? null : (form.paymentMethod as PaymentMethod),
      reference: form.reference.trim() || null,
      reason: expense ? form.reason.trim() || null : null,
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{expense ? 'Edit expense' : 'New expense'}</DialogTitle>
          <DialogDescription>
            {expense
              ? 'The version you replace is kept in the expense’s history.'
              : 'Money already spent. Wages are added automatically when you pay them on Weekly wages.'}
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="expense-category">
                Category <span className="text-destructive">*</span>
              </Label>
              <Select value={form.categoryId} onValueChange={(categoryId) => setForm({ ...form, categoryId })}>
                <SelectTrigger id="expense-category" className="w-full">
                  <SelectValue placeholder={categories.isPending ? 'Loading…' : 'Choose'} />
                </SelectTrigger>
                <SelectContent>
                  {choices.map((category) => (
                    <SelectItem key={category.id} value={category.id}>
                      {category.name}
                      {!category.isActive && ' (no longer used)'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="expense-amount">
                Amount (₹) <span className="text-destructive">*</span>
              </Label>
              <Input
                id="expense-amount"
                type="number"
                inputMode="decimal"
                min="0.01"
                step="0.01"
                required
                value={form.amount}
                onChange={(event) => setForm({ ...form, amount: event.target.value })}
              />
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="expense-date">
                Date <span className="text-destructive">*</span>
              </Label>
              <Input
                id="expense-date"
                type="date"
                required
                max={todayInIndia()}
                value={form.expenseDate}
                onChange={(event) => setForm({ ...form, expenseDate: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="expense-method">Paid by</Label>
              <Select value={form.paymentMethod} onValueChange={(paymentMethod) => setForm({ ...form, paymentMethod })}>
                <SelectTrigger id="expense-method" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {methods.map((method) => (
                    <SelectItem key={method} value={method}>
                      {formatPaymentMethod(method)}
                    </SelectItem>
                  ))}
                  <SelectItem value={NONE}>Not recorded</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="expense-description">Description</Label>
            <Input
              id="expense-description"
              maxLength={300}
              placeholder="Urad dal, 50 kg"
              value={form.description}
              onChange={(event) => setForm({ ...form, description: event.target.value })}
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="expense-reference">Bill or reference number</Label>
            <Input
              id="expense-reference"
              maxLength={100}
              value={form.reference}
              onChange={(event) => setForm({ ...form, reference: event.target.value })}
            />
          </div>

          {expense && (
            <div className="grid gap-1.5">
              <Label htmlFor="expense-reason">Why is it being changed?</Label>
              <Input
                id="expense-reason"
                maxLength={300}
                placeholder="Wrong amount entered"
                value={form.reason}
                onChange={(event) => setForm({ ...form, reason: event.target.value })}
              />
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
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
