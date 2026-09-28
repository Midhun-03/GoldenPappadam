import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { History, Loader2, MoreHorizontal, Pencil, Plus, ReceiptIndianRupee, Search, Settings2, XCircle } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { expenseCategoriesApi, expensesApi, type ExpenseFilters } from '@/api/accounting'
import type { Expense } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField, FilterToggle } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
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
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDateTime, formatDay, formatMoney, formatPaymentMethod, todayInIndia, weekStartOf } from '@/lib/format'
import { cn } from '@/lib/utils'
import { ExpenseDialog } from './ExpenseDialog'

type Period = 'today' | 'week' | 'month' | 'custom'

const ALL = 'all'

/** The first day of the month a plain date falls in: "2026-09-23" -> "2026-09-01". */
const monthStart = (day: string) => `${day.slice(0, 8)}01`

function rangeFor(period: Period, today: string, from: string, to: string) {
  switch (period) {
    case 'today':
      return { from: today, to: today }
    case 'week':
      return { from: weekStartOf(today), to: today }
    case 'month':
      return { from: monthStart(today), to: today }
    default:
      return { from: from || undefined, to: to || undefined }
  }
}

const periodLabels: Record<Period, string> = {
  today: 'Today',
  week: 'This week',
  month: 'This month',
  custom: 'Custom',
}

/**
 * Money spent, by category. Totals come from the server for the same filters as the list, cancelled
 * expenses left out. Wages appear here by being paid, never by being typed in.
 */
export function ExpensesPage() {
  const today = todayInIndia()
  const [period, setPeriod] = useState<Period>('month')
  const [customFrom, setCustomFrom] = useState(monthStart(today))
  const [customTo, setCustomTo] = useState(today)
  const [categoryId, setCategoryId] = useState(ALL)
  const [search, setSearch] = useState('')
  const [includeCancelled, setIncludeCancelled] = useState(false)
  const [editing, setEditing] = useState<Expense | null>(null)
  const [isDialogOpen, setIsDialogOpen] = useState(false)
  const [historyOf, setHistoryOf] = useState<Expense | null>(null)
  const [cancelling, setCancelling] = useState<Expense | null>(null)

  const range = rangeFor(period, today, customFrom, customTo)
  const filters: ExpenseFilters = {
    ...range,
    categoryId: categoryId === ALL ? undefined : categoryId,
    search: search.trim() || undefined,
  }

  const categories = useQuery({ queryKey: ['expense-categories', 'all'], queryFn: () => expenseCategoriesApi.list(true) })
  const expenses = useQuery({
    queryKey: ['expenses', 'list', filters, includeCancelled],
    queryFn: () => expensesApi.list({ ...filters, includeCancelled }),
  })
  const summary = useQuery({ queryKey: ['expenses', 'summary', filters], queryFn: () => expensesApi.summary(filters) })

  const rows = expenses.data ?? []
  const isFiltered = categoryId !== ALL || search.trim() !== ''
  const periodText =
    range.from && range.to
      ? range.from === range.to
        ? formatDay(range.from)
        : `${formatDay(range.from)} – ${formatDay(range.to)}`
      : 'All dates'

  function openDialog(expense: Expense | null) {
    setEditing(expense)
    setIsDialogOpen(true)
  }

  return (
    <>
      <PageHeader
        title="Expenses"
        description={
          summary.data ? (
            <span className="tabular-nums">
              {periodText} · <span className="font-medium text-foreground">{formatMoney(summary.data.total)}</span> spent
            </span>
          ) : (
            periodText
          )
        }
        action={
          <>
            <Button asChild variant="outline">
              <Link to="/settings">
                <Settings2 className="size-4" />
                Categories
              </Link>
            </Button>
            <Button onClick={() => openDialog(null)}>
              <Plus className="size-4" />
              New expense
            </Button>
          </>
        }
      />

      <FilterBar>
        <div className="col-span-2 flex flex-wrap gap-2">
          {(Object.keys(periodLabels) as Period[]).map((key) => (
            <FilterToggle key={key} pressed={period === key} onPressedChange={() => setPeriod(key)}>
              {periodLabels[key]}
            </FilterToggle>
          ))}
        </div>

        {period === 'custom' && (
          <>
            <FilterField label="From" htmlFor="expense-from">
              <Input
                id="expense-from"
                type="date"
                value={customFrom}
                max={customTo || undefined}
                onChange={(event) => setCustomFrom(event.target.value)}
              />
            </FilterField>
            <FilterField label="To" htmlFor="expense-to">
              <Input
                id="expense-to"
                type="date"
                value={customTo}
                min={customFrom || undefined}
                onChange={(event) => setCustomTo(event.target.value)}
              />
            </FilterField>
          </>
        )}

        <FilterField label="Category" htmlFor="expense-category-filter" className="sm:w-48">
          <Select value={categoryId} onValueChange={setCategoryId}>
            <SelectTrigger id="expense-category-filter" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>All categories</SelectItem>
              {(categories.data ?? []).map((category) => (
                <SelectItem key={category.id} value={category.id}>
                  {category.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FilterField>

        <FilterField label="Search" htmlFor="expense-search" className="sm:w-56">
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="expense-search"
              className="pl-8"
              placeholder="Description or bill no."
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
        </FilterField>

        <FilterToggle pressed={includeCancelled} onPressedChange={setIncludeCancelled}>
          Show cancelled
        </FilterToggle>
      </FilterBar>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:gap-5">
        <Card className="lg:order-2 lg:self-start">
          <CardHeader>
            <CardTitle>By category</CardTitle>
            <CardDescription>{periodText}</CardDescription>
          </CardHeader>
          <CardContent>
            {summary.isPending ? (
              <div className="grid gap-2">
                {Array.from({ length: 4 }, (_, index) => (
                  <Skeleton key={index} className="h-6" />
                ))}
              </div>
            ) : summary.isError ? (
              <ErrorState error={summary.error} />
            ) : summary.data.categories.length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing spent in this period.</p>
            ) : (
              <ul className="grid gap-2.5 text-sm">
                {summary.data.categories.map((category) => (
                  <li key={category.categoryId}>
                    <button
                      type="button"
                      className="flex w-full items-baseline justify-between gap-3 rounded text-left hover:underline focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none"
                      onClick={() => setCategoryId(categoryId === category.categoryId ? ALL : category.categoryId)}
                    >
                      <span className="truncate">{category.name}</span>
                      <span className="font-medium tabular-nums">{formatMoney(category.total)}</span>
                    </button>
                    <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                      <div
                        className="h-full rounded-full bg-primary/60"
                        style={{ width: `${Math.max(2, (category.total / summary.data.total) * 100)}%` }}
                      />
                    </div>
                  </li>
                ))}
                <li className="flex items-baseline justify-between border-t pt-2.5 font-medium">
                  <span>Total expenses</span>
                  <span className="tabular-nums">{formatMoney(summary.data.total)}</span>
                </li>
              </ul>
            )}
          </CardContent>
        </Card>

        <Card className="lg:order-1">
          <CardContent className="px-0">
            {expenses.isPending ? (
              <TableSkeleton columns={4} />
            ) : expenses.isError ? (
              <ErrorState error={expenses.error} />
            ) : rows.length === 0 ? (
              <EmptyState
                icon={ReceiptIndianRupee}
                title={isFiltered ? 'No expenses match' : 'No expenses in this period'}
                description={
                  isFiltered
                    ? 'Try another category or search, or a wider period.'
                    : 'Record fuel, electricity, raw material and the rest as money goes out.'
                }
                action={
                  isFiltered ? undefined : (
                    <Button onClick={() => openDialog(null)}>
                      <Plus className="size-4" />
                      New expense
                    </Button>
                  )
                }
              />
            ) : (
              <Table>
                <TableHeader sticky>
                  <TableRow>
                    <TableHead>Expense</TableHead>
                    <TableHead className="hidden md:table-cell">Paid by</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                    <TableHead className="w-12" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((expense) => {
                    const isCancelled = expense.status === 'Cancelled'

                    return (
                      <TableRow key={expense.id} className={cn(isCancelled && 'opacity-60')}>
                        <TableCell className="w-full min-w-32 whitespace-normal">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-medium">{expense.categoryName}</span>
                            {isCancelled && <Badge variant="destructive">Cancelled</Badge>}
                            {expense.isWageExpense && <Badge variant="secondary">From wages</Badge>}
                            {expense.versionCount > 0 && !isCancelled && <Badge variant="outline">Edited</Badge>}
                          </div>
                          <div className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">
                            {formatDay(expense.expenseDate)}
                            {expense.description && ` · ${expense.description}`}
                          </div>
                        </TableCell>
                        <TableCell className="hidden text-sm text-muted-foreground md:table-cell">
                          {expense.paymentMethod ? formatPaymentMethod(expense.paymentMethod) : '—'}
                          {expense.reference && <div className="font-mono text-xs">{expense.reference}</div>}
                        </TableCell>
                        <TableCell className={cn('text-right font-medium tabular-nums', isCancelled && 'line-through')}>
                          {formatMoney(expense.amount)}
                        </TableCell>
                        <TableCell className="text-right">
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon-sm" aria-label="Expense actions">
                                <MoreHorizontal className="size-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              {expense.isWageExpense ? (
                                expense.employeeId && (
                                  <DropdownMenuItem asChild>
                                    <Link to={`/employees/${expense.employeeId}`}>View employee’s payments</Link>
                                  </DropdownMenuItem>
                                )
                              ) : (
                                !isCancelled && (
                                  <DropdownMenuItem onClick={() => openDialog(expense)}>
                                    <Pencil className="size-4" />
                                    Edit
                                  </DropdownMenuItem>
                                )
                              )}
                              <DropdownMenuItem onClick={() => setHistoryOf(expense)}>
                                <History className="size-4" />
                                History
                              </DropdownMenuItem>
                              {!expense.isWageExpense && !isCancelled && (
                                <DropdownMenuItem variant="destructive" onSelect={() => setCancelling(expense)}>
                                  <XCircle className="size-4" />
                                  Cancel expense
                                </DropdownMenuItem>
                              )}
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    )
                  })}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>
      </div>

      <ExpenseDialog open={isDialogOpen} onOpenChange={setIsDialogOpen} expense={editing} />
      <ExpenseHistoryDialog expense={historyOf} onClose={() => setHistoryOf(null)} />
      <CancelExpenseDialog expense={cancelling} onClose={() => setCancelling(null)} />
    </>
  )
}

/** Every version the expense has had, with who changed it, when and why. */
function ExpenseHistoryDialog({ expense, onClose }: { expense: Expense | null; onClose: () => void }) {
  const history = useQuery({
    queryKey: ['expenses', 'history', expense?.id],
    queryFn: () => expensesApi.history(expense!.id),
    enabled: expense !== null,
  })

  return (
    <Dialog open={expense !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Expense history</DialogTitle>
          <DialogDescription>Nothing recorded is ever lost: each change keeps the version before it.</DialogDescription>
        </DialogHeader>

        {history.isPending ? (
          <Skeleton className="h-24" />
        ) : history.isError ? (
          <ErrorState error={history.error} />
        ) : (
          <ol className="grid max-h-96 gap-3 overflow-y-auto">
            {history.data.map((version, index) => (
              <li key={index} className="rounded-lg border p-3 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <Badge variant={version.change === 'Cancelled' ? 'destructive' : version.change === 'Edited' ? 'outline' : 'secondary'}>
                    {version.change}
                  </Badge>
                  <span className="text-xs text-muted-foreground">
                    {formatDateTime(version.changedAt)}
                    {version.changedByName && ` · ${version.changedByName}`}
                  </span>
                </div>
                {version.reason && <p className="mt-1.5 text-xs text-muted-foreground">“{version.reason}”</p>}
                <div className="mt-2 flex items-baseline justify-between gap-3">
                  <span>
                    {version.categoryName} · {formatDay(version.expenseDate)}
                  </span>
                  <span className="font-medium tabular-nums">{formatMoney(version.amount)}</span>
                </div>
                <div className="text-xs text-muted-foreground">
                  {[version.description, version.paymentMethod && formatPaymentMethod(version.paymentMethod), version.reference]
                    .filter(Boolean)
                    .join(' · ') || 'No description'}
                </div>
              </li>
            ))}
          </ol>
        )}
      </DialogContent>
    </Dialog>
  )
}

function CancelExpenseDialog({ expense, onClose }: { expense: Expense | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')

  const cancel = useMutation({
    mutationFn: () => expensesApi.cancel(expense!.id, reason.trim()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['expenses'] })
      toast.success('Expense cancelled')
      setReason('')
      onClose()
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not cancel the expense.'),
  })

  return (
    <Dialog open={expense !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            Cancel {expense?.categoryName} expense of {formatMoney(expense?.amount ?? null)}?
          </DialogTitle>
          <DialogDescription>
            For an expense entered by mistake. It is kept, marked cancelled, and left out of every total.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-1.5">
          <Label htmlFor="cancel-expense-reason">
            Reason <span className="text-destructive">*</span>
          </Label>
          <Input
            id="cancel-expense-reason"
            maxLength={300}
            placeholder="Entered twice"
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Keep it
          </Button>
          <Button variant="destructive" disabled={cancel.isPending || reason.trim() === ''} onClick={() => cancel.mutate()}>
            {cancel.isPending && <Loader2 className="size-4 animate-spin" />}
            Cancel expense
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
