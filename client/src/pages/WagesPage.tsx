import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  HandCoins,
  Loader2,
  MoreHorizontal,
  Users,
} from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { wagesApi } from '@/api/staff'
import type { EmployeeWage, WageStatus } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
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
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import {
  formatDay,
  formatDayShort,
  formatMoney,
  formatPaymentMethod,
  formatQuantity,
  formatWeekday,
  shiftDay,
  todayInIndia,
  weekStartOf,
} from '@/lib/format'
import { cn } from '@/lib/utils'
import { PayWagesDialog } from './PayWagesDialog'

const statusBadge: Record<WageStatus, { label: string; variant: 'success' | 'warning' | 'outline' }> = {
  Paid: { label: 'Paid', variant: 'success' },
  Pending: { label: 'Pending', variant: 'warning' },
  NothingToPay: { label: 'Nothing to pay', variant: 'outline' },
}

/** The week that can be paid most recently: this one on a Saturday, otherwise last week. */
function defaultWeekDay(today: string) {
  const start = weekStartOf(today)
  return today === shiftDay(start, 6) ? start : shiftDay(start, -7)
}

const canBePaid = (row: EmployeeWage) => row.status === 'Pending' && row.missingWageDates.length === 0

/**
 * Saturday's wages. One row per employee: the week's attendance, the rate, the days, the amount and
 * whether it has been paid. Paying records the figures exactly as shown and adds the expense.
 */
export function WagesPage() {
  const today = todayInIndia()
  const [searchParams, setSearchParams] = useSearchParams()
  const weekStart = weekStartOf(searchParams.get('date') ?? defaultWeekDay(today))
  const [expanded, setExpanded] = useState<string | null>(null)
  const [paying, setPaying] = useState<EmployeeWage[] | null>(null)
  const [cancelling, setCancelling] = useState<EmployeeWage | null>(null)

  const week = useQuery({ queryKey: ['wages', 'week', weekStart], queryFn: () => wagesApi.week(weekStart) })

  const rows = week.data?.employees ?? []
  const payable = rows.filter(canBePaid)
  const isCurrentWeek = weekStart === weekStartOf(today)

  const goTo = (start: string) => setSearchParams({ date: start })

  return (
    <>
      <PageHeader
        title="Weekly wages"
        description={week.data ? `Sunday to Saturday · ${week.data.label} · paid on ${formatDay(week.data.weekEnd)}` : undefined}
        action={
          <Button
            disabled={!week.data?.canPay || payable.length === 0}
            onClick={() => setPaying(payable)}
          >
            <HandCoins className="size-4" />
            Pay all pending
          </Button>
        }
      />

      <FilterBar>
        <div className="col-span-2 flex items-center gap-1">
          <Button variant="outline" size="icon" aria-label="Previous week" onClick={() => goTo(shiftDay(weekStart, -7))}>
            <ChevronLeft className="size-4" />
          </Button>
          <div className="min-w-44 px-2 text-center text-sm font-medium tabular-nums">
            {formatDayShort(weekStart)} – {formatDay(shiftDay(weekStart, 6))}
          </div>
          <Button
            variant="outline"
            size="icon"
            aria-label="Next week"
            disabled={weekStart >= weekStartOf(today)}
            onClick={() => goTo(shiftDay(weekStart, 7))}
          >
            <ChevronRight className="size-4" />
          </Button>
        </div>
        <Button variant="outline" onClick={() => goTo(weekStartOf(today))}>
          This week
        </Button>
        <Button asChild variant="ghost">
          <Link to="/attendance">Mark attendance</Link>
        </Button>
      </FilterBar>

      {week.data && (
        <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
          <SummaryTile label="To pay" value={formatMoney(week.data.pendingTotal)} detail={`${week.data.pendingCount} pending`} />
          <SummaryTile label="Paid" value={formatMoney(week.data.paidTotal)} detail={`${week.data.paidCount} paid`} />
          <SummaryTile
            className="col-span-2 sm:col-span-1"
            label="Week total"
            value={formatMoney(week.data.pendingTotal + week.data.paidTotal)}
            detail="Paid and pending"
          />
        </div>
      )}

      {week.data && !week.data.canPay && (
        <p className="mb-3 text-sm text-muted-foreground">
          {isCurrentWeek ? 'This week is still running. ' : ''}It can be paid from Saturday{' '}
          {formatDay(week.data.weekEnd)}, once that day’s attendance is in.
        </p>
      )}

      <Card>
        <CardContent className="px-0">
          {week.isPending ? (
            <TableSkeleton columns={5} />
          ) : week.isError ? (
            <ErrorState error={week.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Users}
              title="No employees"
              description="Add employees and mark their attendance, and their wages are worked out here."
              action={
                <Button asChild>
                  <Link to="/employees">Employees</Link>
                </Button>
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead className="w-8" />
                  <TableHead>Employee</TableHead>
                  <TableHead className="hidden md:table-cell">Attendance</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Daily wage</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Days</TableHead>
                  <TableHead className="text-right">Wages</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const isOpen = expanded === row.employeeId

                  return (
                    <Fragment key={row.employeeId}>
                      <TableRow className={cn(!row.isActive && 'opacity-60')}>
                        <TableCell className="pr-0">
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            aria-expanded={isOpen}
                            aria-label={`${isOpen ? 'Hide' : 'Show'} ${row.name}'s days`}
                            onClick={() => setExpanded(isOpen ? null : row.employeeId)}
                          >
                            <ChevronDown className={cn('size-4 transition-transform', isOpen && 'rotate-180')} />
                          </Button>
                        </TableCell>
                        <TableCell className="w-full min-w-32 whitespace-normal">
                          <Link
                            className="block font-medium underline-offset-4 hover:underline"
                            to={`/employees/${row.employeeId}`}
                          >
                            {row.name}
                          </Link>
                          <div className="mt-0.5 text-xs text-muted-foreground md:hidden">
                            {describeAttendance(row)}
                          </div>
                          <div className="mt-0.5 text-xs text-muted-foreground sm:hidden">
                            {formatQuantity(row.daysWorked)} days × {row.dailyWages.map((w) => formatMoney(w)).join(' / ') || '—'}
                          </div>
                          {row.missingWageDates.length > 0 && (
                            <div className="mt-1 flex items-center gap-1 text-xs text-destructive">
                              <AlertTriangle className="size-3.5" />
                              No daily wage set for {formatDay(row.missingWageDates[0])}
                            </div>
                          )}
                          {row.changedSincePaid.length > 0 && (
                            <div className="mt-1 text-xs text-info">
                              Changed after payment: {formatMoney(row.changedSincePaidAmount)} to settle next week
                            </div>
                          )}
                        </TableCell>
                        <TableCell className="hidden text-sm text-muted-foreground md:table-cell">
                          {describeAttendance(row)}
                        </TableCell>
                        <TableCell className="hidden text-right tabular-nums sm:table-cell">
                          {row.dailyWages.length === 0 ? '—' : row.dailyWages.map((w) => formatMoney(w)).join(' / ')}
                        </TableCell>
                        <TableCell className="hidden text-right tabular-nums sm:table-cell">
                          {formatQuantity(row.daysWorked)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          <span className="font-medium">{formatMoney(row.payable)}</span>
                          {row.adjustmentAmount !== 0 && (
                            <div className="text-xs text-muted-foreground">
                              incl. {formatMoney(row.adjustmentAmount)} adjusted
                            </div>
                          )}
                          {row.carriedForward < 0 && (
                            <div className="text-xs text-warning">{formatMoney(row.carriedForward)} to next week</div>
                          )}
                          {/* On a phone the status column is gone, so status and its action sit under the amount. */}
                          <div className="mt-1 sm:hidden">
                            <WageStatusControl
                              compact
                              row={row}
                              canPay={week.data.canPay}
                              onPay={() => setPaying([row])}
                              onCancel={() => setCancelling(row)}
                            />
                          </div>
                        </TableCell>
                        <TableCell className="hidden text-right sm:table-cell">
                          <WageStatusControl
                            row={row}
                            canPay={week.data.canPay}
                            onPay={() => setPaying([row])}
                            onCancel={() => setCancelling(row)}
                          />
                        </TableCell>
                      </TableRow>

                      {isOpen && (
                        <TableRow className="bg-muted/30 hover:bg-muted/30">
                          <TableCell />
                          <TableCell colSpan={6} className="whitespace-normal">
                            <WeekDetail row={row} />
                          </TableCell>
                        </TableRow>
                      )}
                    </Fragment>
                  )
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {week.data && paying && (
        <PayWagesDialog
          week={week.data}
          employees={paying}
          open={paying !== null}
          onOpenChange={(open) => !open && setPaying(null)}
        />
      )}

      <CancelWagePaymentDialog row={cancelling} onClose={() => setCancelling(null)} />
    </>
  )
}

/** Paid (with how, and a way to cancel), a Pay button, or why there is nothing to pay. */
function WageStatusControl({
  row,
  canPay,
  onPay,
  onCancel,
  compact = false,
}: {
  compact?: boolean
  row: EmployeeWage
  canPay: boolean
  onPay: () => void
  onCancel: () => void
}) {
  const badge = statusBadge[row.status]

  if (row.status === 'Paid' && row.payment) {
    return (
      <div className="flex items-center justify-end gap-1">
        <div className="text-right">
          <Badge variant={badge.variant}>{badge.label}</Badge>
          {!compact && (
            <div className="mt-0.5 text-xs whitespace-nowrap text-muted-foreground">
              {formatDayShort(row.payment.paymentDate)} · {formatPaymentMethod(row.payment.method)}
            </div>
          )}
        </div>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${row.name}`}>
              <MoreHorizontal className="size-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem variant="destructive" onSelect={onCancel}>
              Cancel this payment
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    )
  }

  if (canBePaid(row) && canPay) {
    return (
      <Button size="sm" onClick={onPay}>
        Pay
      </Button>
    )
  }

  return <Badge variant={badge.variant}>{badge.label}</Badge>
}

function describeAttendance(row: EmployeeWage) {
  const parts = row.statusCounts.map((count) => `${count.days} ${count.name.toLowerCase()}`)
  if (row.notRecordedDays > 0 && row.notRecordedDays < 7) parts.push(`${row.notRecordedDays} not marked`)
  return parts.length > 0 ? parts.join(' · ') : 'No attendance marked'
}

function SummaryTile({
  label,
  value,
  detail,
  className,
}: {
  label: string
  value: string
  detail: string
  className?: string
}) {
  return (
    <Card className={cn('gap-1 py-3', className)}>
      <CardContent className="px-4">
        <div className="text-xs text-muted-foreground">{label}</div>
        <div className="font-heading text-lg font-semibold tabular-nums">{value}</div>
        <div className="text-xs text-muted-foreground">{detail}</div>
      </CardContent>
    </Card>
  )
}

/** The seven days, then anything carried in from earlier weeks. */
function WeekDetail({ row }: { row: EmployeeWage }) {
  return (
    <div className="grid gap-3 py-1">
      <div className="grid grid-cols-4 gap-1.5 sm:grid-cols-7">
        {row.days.map((day) => (
          <div
            key={day.date}
            className={cn('rounded-md border bg-card px-2 py-1.5 text-xs', day.statusId === null && 'border-dashed')}
          >
            <div className="font-medium">
              {formatWeekday(day.date)} {formatDayShort(day.date)}
            </div>
            <div className={cn(day.statusId === null ? 'text-muted-foreground' : 'text-foreground')}>{day.statusName}</div>
            <div className="tabular-nums text-muted-foreground">
              {day.dayFraction > 0 && day.dailyWage !== null
                ? `${formatQuantity(day.dayFraction)} × ${formatMoney(day.dailyWage)}`
                : '—'}
            </div>
          </div>
        ))}
      </div>

      {row.adjustments.length > 0 && (
        <ul className="grid gap-1 text-xs">
          {row.adjustments.map((adjustment, index) => (
            <li key={index} className="flex justify-between gap-3">
              <span className="text-muted-foreground">{adjustment.description}</span>
              <span className={cn('tabular-nums', adjustment.amount < 0 ? 'text-destructive' : 'text-success')}>
                {formatMoney(adjustment.amount)}
              </span>
            </li>
          ))}
        </ul>
      )}

      {row.changedSincePaid.length > 0 && (
        <div className="text-xs">
          <div className="font-medium text-info">Changed after payment - settled in the next unpaid week</div>
          <ul className="mt-1 grid gap-1">
            {row.changedSincePaid.map((change, index) => (
              <li key={index} className="flex justify-between gap-3">
                <span className="text-muted-foreground">{change.description}</span>
                <span className="tabular-nums">{formatMoney(change.amount)}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {row.payment && (
        <div className="text-xs text-muted-foreground">
          Paid {formatDay(row.payment.paymentDate)} by {formatPaymentMethod(row.payment.method)}
          {row.payment.reference && ` (${row.payment.reference})`}
          {row.payment.paidByName && ` · recorded by ${row.payment.paidByName}`}
        </div>
      )}
    </div>
  )
}

function CancelWagePaymentDialog({ row, onClose }: { row: EmployeeWage | null; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [reason, setReason] = useState('')

  const cancel = useMutation({
    mutationFn: () => wagesApi.cancel(row!.payment!.id, reason.trim()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['wages'] })
      await queryClient.invalidateQueries({ queryKey: ['expenses'] })
      toast.success(`${row?.name}'s payment cancelled`)
      setReason('')
      onClose()
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not cancel the payment.'),
  })

  return (
    <Dialog open={row !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cancel {row?.name}'s payment?</DialogTitle>
          <DialogDescription>
            Only for a payment recorded that never happened. It is kept and marked cancelled, its expense is cancelled
            with it, and the week can be paid again. To correct attendance, change the attendance instead - the
            difference is settled next week.
          </DialogDescription>
        </DialogHeader>

        {row?.payment ? (
          <div className="grid gap-1.5">
            <Label htmlFor="cancel-wage-reason">
              Reason <span className="text-destructive">*</span>
            </Label>
            <Input
              id="cancel-wage-reason"
              maxLength={300}
              placeholder="Recorded but not paid"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
        ) : (
          <Skeleton className="h-9" />
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Keep the payment
          </Button>
          <Button variant="destructive" disabled={cancel.isPending || reason.trim() === ''} onClick={() => cancel.mutate()}>
            {cancel.isPending && <Loader2 className="size-4 animate-spin" />}
            Cancel payment
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
