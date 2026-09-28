import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CalendarCheck, ChevronLeft, ChevronRight, Loader2, Undo2, Users } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { attendanceApi } from '@/api/staff'
import type { AttendanceStatus } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { ApiError } from '@/lib/api'
import { formatDay, formatWeekday, shiftDay, todayInIndia } from '@/lib/format'
import { cn } from '@/lib/utils'

/** Colour carries meaning only: worked, part-worked, not worked, excused. */
const tone: Record<string, string> = {
  Present: 'border-success/40 bg-success-surface text-success',
  HalfDay: 'border-warning/40 bg-warning-surface text-warning',
  Absent: 'border-destructive/40 bg-destructive-surface text-destructive',
  Leave: 'border-info/40 bg-info-surface text-info',
}

const shortName = (status: AttendanceStatus) => (status.code === 'HalfDay' ? '½' : status.name.charAt(0))

/**
 * The daily register. Pick a day, tap each person's status, save once. "Everyone present" fills the
 * rows not yet marked, so a normal day is one tap and a save. Tapping a status that is already chosen
 * clears it back to "not recorded" - the usual state for someone whose Sunday it is not.
 */
export function AttendancePage() {
  const queryClient = useQueryClient()
  const today = todayInIndia()
  const [date, setDate] = useState(today)
  const [draft, setDraft] = useState<Record<string, string | null>>({})

  const sheet = useQuery({ queryKey: ['attendance', date], queryFn: () => attendanceApi.sheet(date) })

  // A fresh register every time the day (or the saved data) changes.
  useEffect(() => {
    if (!sheet.data) return
    setDraft(Object.fromEntries(sheet.data.rows.map((row) => [row.employeeId, row.statusId])))
  }, [sheet.data])

  const rows = useMemo(() => sheet.data?.rows ?? [], [sheet.data])
  const statuses = sheet.data?.statuses ?? []
  const changed = rows.filter((row) => (draft[row.employeeId] ?? null) !== row.statusId)
  const isDirty = changed.length > 0
  const unmarked = rows.filter((row) => row.isActive && !draft[row.employeeId]).length

  const save = useMutation({
    mutationFn: () =>
      attendanceApi.save(
        date,
        changed.map((row) => ({ employeeId: row.employeeId, statusId: draft[row.employeeId] ?? null })),
      ),
    onSuccess: async (response) => {
      queryClient.setQueryData(['attendance', date], response.sheet)
      await queryClient.invalidateQueries({ queryKey: ['wages'] })
      toast.success(`Attendance saved for ${formatDay(date)}`)
      response.warnings.forEach((warning) => toast.warning(warning, { duration: 8000 }))
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not save attendance.'),
  })

  function goTo(next: string) {
    if (next === date) return
    if (isDirty && !window.confirm('You have unsaved attendance for this day. Leave it without saving?')) return
    setDate(next)
  }

  function mark(employeeId: string, statusId: string) {
    setDraft((current) => ({ ...current, [employeeId]: current[employeeId] === statusId ? null : statusId }))
  }

  function markEveryonePresent() {
    const present = statuses.find((status) => status.code === 'Present')
    if (!present) return

    setDraft((current) => {
      const next = { ...current }
      rows.filter((row) => row.isActive && !next[row.employeeId]).forEach((row) => (next[row.employeeId] = present.id))
      return next
    })
  }

  const counts = statuses.map((status) => ({
    status,
    count: rows.filter((row) => draft[row.employeeId] === status.id).length,
  }))

  return (
    <>
      <PageHeader
        title="Attendance"
        description={
          sheet.data ? (
            <span>
              {formatWeekday(date)} {formatDay(date)} · wage week {sheet.data.weekLabel}
            </span>
          ) : undefined
        }
        action={
          <Button asChild variant="outline">
            <Link to={`/wages?date=${date}`}>Weekly wages</Link>
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Day" htmlFor="attendance-date" className="col-span-2 sm:col-span-1">
          <div className="flex items-center gap-1">
            <Button variant="outline" size="icon" aria-label="Previous day" onClick={() => goTo(shiftDay(date, -1))}>
              <ChevronLeft className="size-4" />
            </Button>
            <Input
              id="attendance-date"
              type="date"
              className="w-40"
              max={today}
              value={date}
              onChange={(event) => event.target.value && goTo(event.target.value)}
            />
            <Button
              variant="outline"
              size="icon"
              aria-label="Next day"
              disabled={date >= today}
              onClick={() => goTo(shiftDay(date, 1))}
            >
              <ChevronRight className="size-4" />
            </Button>
          </div>
        </FilterField>
        <Button variant="outline" disabled={date === today} onClick={() => goTo(today)}>
          Today
        </Button>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {sheet.isPending ? (
            <TableSkeleton columns={2} />
          ) : sheet.isError ? (
            <ErrorState error={sheet.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Users}
              title="No employees to mark"
              description="Add employees first; everyone active on this day appears here."
              action={
                <Button asChild>
                  <Link to="/employees">Employees</Link>
                </Button>
              }
            />
          ) : (
            <>
              <div className="flex flex-wrap items-center gap-x-4 gap-y-2 border-b px-4 pb-3 text-sm">
                {counts.map(({ status, count }) => (
                  <span key={status.id} className="tabular-nums text-muted-foreground">
                    <span className="font-medium text-foreground">{count}</span> {status.name.toLowerCase()}
                  </span>
                ))}
                <span className="tabular-nums text-muted-foreground">
                  <span className="font-medium text-foreground">{unmarked}</span> not marked
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  className="ml-auto"
                  disabled={unmarked === 0}
                  onClick={markEveryonePresent}
                >
                  <CalendarCheck className="size-4" />
                  Everyone else present
                </Button>
              </div>

              <ul className="divide-y">
                {rows.map((row) => {
                  const selected = draft[row.employeeId] ?? null
                  const isChanged = selected !== row.statusId

                  return (
                    <li
                      key={row.employeeId}
                      className={cn(
                        'flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:gap-4',
                        isChanged && 'bg-muted/50',
                      )}
                    >
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="truncate font-medium">{row.name}</span>
                          {!row.isActive && <Badge variant="outline">Inactive</Badge>}
                          {row.weekPaid && (
                            <Badge variant="info" title="A change is settled in the next unpaid week's wages">
                              Week paid
                            </Badge>
                          )}
                        </div>
                        <div className="truncate text-xs text-muted-foreground">
                          {row.designation ?? ''}
                          {isChanged
                            ? `${row.designation ? ' · ' : ''}not saved yet`
                            : row.changedByName && `${row.designation ? ' · ' : ''}marked by ${row.changedByName}`}
                        </div>
                      </div>

                      <div role="group" aria-label={`Attendance for ${row.name}`} className="grid grid-cols-4 gap-1.5 sm:flex">
                        {statuses.map((status) => {
                          const isSelected = selected === status.id
                          return (
                            <Button
                              key={status.id}
                              type="button"
                              variant="outline"
                              size="sm"
                              aria-pressed={isSelected}
                              disabled={!row.isActive && !isSelected}
                              title={status.name}
                              onClick={() => mark(row.employeeId, status.id)}
                              className={cn('min-w-0 sm:min-w-20', isSelected && tone[status.code])}
                            >
                              <span className="sm:hidden">{shortName(status)}</span>
                              <span className="hidden sm:inline">{status.name}</span>
                            </Button>
                          )
                        })}
                      </div>
                    </li>
                  )
                })}
              </ul>

              <div className="sticky bottom-0 flex flex-wrap items-center gap-3 border-t bg-card px-4 pt-3">
                <p className="text-xs text-muted-foreground">
                  {isDirty
                    ? `${changed.length} change${changed.length === 1 ? '' : 's'} not saved.`
                    : 'Tap a chosen status again to clear it.'}
                </p>
                <div className="ml-auto flex gap-2">
                  <Button
                    variant="ghost"
                    disabled={!isDirty || save.isPending}
                    onClick={() =>
                      setDraft(Object.fromEntries(rows.map((row) => [row.employeeId, row.statusId])))
                    }
                  >
                    <Undo2 className="size-4" />
                    Undo
                  </Button>
                  <Button disabled={!isDirty || save.isPending} onClick={() => save.mutate()}>
                    {save.isPending && <Loader2 className="size-4 animate-spin" />}
                    Save attendance
                  </Button>
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </>
  )
}
