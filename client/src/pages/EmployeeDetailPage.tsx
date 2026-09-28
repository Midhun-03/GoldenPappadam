import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { HandCoins, IndianRupee, Loader2, Pencil } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toast } from 'sonner'
import { employeesApi, wagesApi } from '@/api/staff'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDateTime, formatDay, formatMoney, formatPaymentMethod, formatQuantity, todayInIndia } from '@/lib/format'
import { EmployeeDialog } from './EmployeeDialog'

export function EmployeeDetailPage() {
  const { employeeId = '' } = useParams()
  const queryClient = useQueryClient()
  const [isEditOpen, setIsEditOpen] = useState(false)

  const employee = useQuery({ queryKey: ['employees', employeeId], queryFn: () => employeesApi.get(employeeId) })

  const setActive = useMutation({
    mutationFn: (isActive: boolean) => employeesApi.setActive(employeeId, isActive),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['employees'] })
      await queryClient.invalidateQueries({ queryKey: ['attendance'] })
      toast.success(`${saved.name} is now ${saved.isActive ? 'active' : 'inactive'}`)
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not change the employee.'),
  })

  if (employee.isError) {
    return (
      <>
        <PageHeader back={{ to: '/employees', label: 'Employees' }} title="Employee" />
        <Card>
          <CardContent className="px-0">
            <ErrorState error={employee.error} />
          </CardContent>
        </Card>
      </>
    )
  }

  const data = employee.data
  const details = data ? [data.designation, data.phone, data.address].filter(Boolean).join(' · ') : ''

  return (
    <>
      <PageHeader
        back={{ to: '/employees', label: 'Employees' }}
        title={data?.name ?? 'Employee'}
        description={
          !data ? (
            <Skeleton className="h-4 w-48" />
          ) : (
            <span className="flex flex-wrap items-center gap-2">
              {!data.isActive && <Badge variant="outline">Inactive</Badge>}
              <span>{details || 'No details yet'}</span>
              {data.joinedOn && <span>· Joined {formatDay(data.joinedOn)}</span>}
            </span>
          )
        }
        action={
          data && (
            <>
              <Button variant="outline" onClick={() => setIsEditOpen(true)}>
                <Pencil className="size-4" />
                Edit
              </Button>
              <Button variant="outline" disabled={setActive.isPending} onClick={() => setActive.mutate(!data.isActive)}>
                {data.isActive ? 'Deactivate' : 'Activate'}
              </Button>
            </>
          )
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] lg:gap-5">
        <WageCard employeeId={employeeId} />
        <PaymentsCard employeeId={employeeId} />
      </div>

      <EmployeeDialog open={isEditOpen} onOpenChange={setIsEditOpen} employee={data ?? null} />
    </>
  )
}

/** The current wage, a form to change it from a date, and every rate it has ever had. */
function WageCard({ employeeId }: { employeeId: string }) {
  const queryClient = useQueryClient()
  const [amount, setAmount] = useState('')
  const [from, setFrom] = useState(todayInIndia())
  const [error, setError] = useState<string | null>(null)

  const rates = useQuery({ queryKey: ['employees', employeeId, 'wage-rates'], queryFn: () => employeesApi.wageRates(employeeId) })

  const add = useMutation({
    mutationFn: () => employeesApi.addWageRate(employeeId, Number(amount), from),
    onSuccess: async () => {
      setAmount('')
      await queryClient.invalidateQueries({ queryKey: ['employees'] })
      await queryClient.invalidateQueries({ queryKey: ['wages'] })
      toast.success(`New wage ${formatMoney(Number(amount))} from ${formatDay(from)}`)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not change the wage.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    add.mutate()
  }

  const current = rates.data?.find((rate) => rate.isCurrent)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Daily wage</CardTitle>
        <CardDescription>
          A change starts from its date. Earlier days keep the rate they were worked at, and paid weeks never change.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        <div>
          <div className="text-xs text-muted-foreground">Current wage</div>
          <div className="font-heading text-2xl font-semibold tabular-nums">
            {rates.isPending ? <Skeleton className="h-8 w-24" /> : formatMoney(current?.dailyWage ?? null)}
          </div>
          {current && <div className="text-xs text-muted-foreground">since {formatDay(current.effectiveFrom)}</div>}
        </div>

        <form className="grid gap-3 rounded-lg border bg-muted/40 p-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end" onSubmit={handleSubmit}>
          <div className="grid gap-1.5">
            <Label htmlFor="new-wage">New daily wage (₹)</Label>
            <Input
              id="new-wage"
              type="number"
              inputMode="decimal"
              min="0.01"
              step="0.01"
              required
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
            />
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="new-wage-from">From</Label>
            <Input id="new-wage-from" type="date" required value={from} onChange={(event) => setFrom(event.target.value)} />
          </div>
          <Button type="submit" disabled={add.isPending}>
            {add.isPending && <Loader2 className="size-4 animate-spin" />}
            Change wage
          </Button>
        </form>

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {rates.isError ? (
          <ErrorState error={rates.error} />
        ) : (
          <div>
            <div className="mb-2 text-sm font-medium">Wage history</div>
            {rates.isPending ? (
              <TableSkeleton rows={3} columns={3} />
            ) : (
              <ul className="divide-y rounded-lg border">
                {rates.data.map((rate) => (
                  <li key={rate.id} className="flex items-center gap-3 px-3 py-2 text-sm">
                    <div className="min-w-0 flex-1">
                      <div className={rate.isSuperseded ? 'text-muted-foreground line-through' : 'font-medium'}>
                        {formatMoney(rate.dailyWage)} / day
                      </div>
                      <div className="text-xs text-muted-foreground">
                        Set {formatDateTime(rate.setAt)}
                        {rate.setByName && ` by ${rate.setByName}`}
                      </div>
                    </div>
                    <div className="text-right text-xs text-muted-foreground">
                      from {formatDay(rate.effectiveFrom)}
                      <div className="mt-0.5">
                        {rate.isCurrent && <Badge variant="success">Current</Badge>}
                        {rate.isUpcoming && !rate.isSuperseded && <Badge variant="info">Upcoming</Badge>}
                        {rate.isSuperseded && <Badge variant="outline">Corrected</Badge>}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function PaymentsCard({ employeeId }: { employeeId: string }) {
  const payments = useQuery({
    queryKey: ['wages', 'payments', { employeeId }],
    queryFn: () => wagesApi.payments({ employeeId }),
  })

  const rows = payments.data ?? []
  const totalPaid = rows.filter((p) => p.status === 'Paid').reduce((sum, p) => sum + p.amount, 0)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Wage payments</CardTitle>
        <CardDescription>
          {payments.isPending ? 'Loading…' : `${rows.length} week${rows.length === 1 ? '' : 's'} · ${formatMoney(totalPaid)} paid`}
        </CardDescription>
      </CardHeader>
      <CardContent className="px-0">
        {payments.isPending ? (
          <TableSkeleton columns={3} />
        ) : payments.isError ? (
          <ErrorState error={payments.error} />
        ) : rows.length === 0 ? (
          <EmptyState
            icon={HandCoins}
            title="Nothing paid yet"
            description="Wages are paid from the Weekly wages screen every Saturday."
            action={
              <Button asChild variant="outline">
                <Link to="/wages">
                  <IndianRupee className="size-4" />
                  Weekly wages
                </Link>
              </Button>
            }
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Week</TableHead>
                <TableHead className="hidden text-right sm:table-cell">Days</TableHead>
                <TableHead className="text-right">Paid</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((payment) => (
                <TableRow key={payment.id} className={payment.status === 'Cancelled' ? 'opacity-60' : undefined}>
                  <TableCell>
                    <Link
                      className="font-medium underline-offset-4 hover:underline"
                      to={`/wages?date=${payment.periodStart}`}
                    >
                      {payment.periodLabel}
                    </Link>
                    <div className="mt-0.5 text-xs text-muted-foreground">
                      {formatDay(payment.paymentDate)} · {formatPaymentMethod(payment.method)}
                      {payment.adjustmentAmount !== 0 && ` · adjusted ${formatMoney(payment.adjustmentAmount)}`}
                    </div>
                    {payment.status === 'Cancelled' && (
                      <div className="mt-1 flex items-center gap-2 text-xs text-destructive">
                        <Badge variant="destructive">Cancelled</Badge>
                        {payment.cancellationReason}
                      </div>
                    )}
                  </TableCell>
                  <TableCell className="hidden text-right tabular-nums sm:table-cell">
                    {formatQuantity(payment.daysWorked)}
                  </TableCell>
                  <TableCell className="text-right font-medium tabular-nums">{formatMoney(payment.amount)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}
