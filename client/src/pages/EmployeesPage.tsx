import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MoreHorizontal, Pencil, Plus, Search, UserRound, Users } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { employeesApi } from '@/api/staff'
import type { Employee } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { FilterBar, FilterField, FilterToggle } from '@/components/FilterBar'
import { PageHeader } from '@/components/PageHeader'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatDay, formatMoney } from '@/lib/format'
import { EmployeeDialog } from './EmployeeDialog'

export function EmployeesPage() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const [editing, setEditing] = useState<Employee | null>(null)
  const [isDialogOpen, setIsDialogOpen] = useState(false)

  const filters = { search: search || undefined, includeInactive }
  const employees = useQuery({ queryKey: ['employees', filters], queryFn: () => employeesApi.list(filters) })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => employeesApi.setActive(id, isActive),
    onSuccess: async (employee) => {
      await queryClient.invalidateQueries({ queryKey: ['employees'] })
      await queryClient.invalidateQueries({ queryKey: ['attendance'] })
      toast.success(`${employee.name} is now ${employee.isActive ? 'active' : 'inactive'}`)
    },
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not change the employee.'),
  })

  const rows = employees.data ?? []
  const activeCount = rows.filter((employee) => employee.isActive).length
  const isFiltered = search !== ''

  function openDialog(employee: Employee | null) {
    setEditing(employee)
    setIsDialogOpen(true)
  }

  return (
    <>
      <PageHeader
        title="Employees"
        description={employees.isPending ? undefined : `${activeCount} active`}
        action={
          <Button onClick={() => openDialog(null)}>
            <Plus className="size-4" />
            New employee
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Search" htmlFor="employee-search" className="col-span-2 sm:w-64">
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="employee-search"
              className="pl-8"
              placeholder="Name, role or phone"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
        </FilterField>

        <FilterToggle pressed={includeInactive} onPressedChange={setIncludeInactive}>
          Show inactive
        </FilterToggle>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {employees.isPending ? (
            <TableSkeleton columns={4} />
          ) : employees.isError ? (
            <ErrorState error={employees.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Users}
              title={isFiltered ? 'No employees match' : 'No employees yet'}
              description={
                isFiltered
                  ? 'Try a different name, role or phone number.'
                  : 'Add the people you pay a daily wage, then mark their attendance each day.'
              }
              action={
                isFiltered ? undefined : (
                  <Button onClick={() => openDialog(null)}>
                    <Plus className="size-4" />
                    New employee
                  </Button>
                )
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead className="hidden md:table-cell">Phone</TableHead>
                  <TableHead className="hidden lg:table-cell">Joined</TableHead>
                  <TableHead className="text-right">Daily wage</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((employee) => (
                  <TableRow key={employee.id} className={employee.isActive ? undefined : 'opacity-60'}>
                    <TableCell className="max-w-[16rem]">
                      <div className="flex items-center gap-2">
                        <Link
                          className="truncate font-medium underline-offset-4 hover:underline"
                          to={`/employees/${employee.id}`}
                        >
                          {employee.name}
                        </Link>
                        {!employee.isActive && <Badge variant="outline">Inactive</Badge>}
                      </div>
                      <div className="mt-0.5 truncate text-xs text-muted-foreground">
                        {employee.designation ?? 'No role set'}
                        {employee.phone && <span className="md:hidden"> · {employee.phone}</span>}
                      </div>
                    </TableCell>

                    <TableCell className="hidden text-muted-foreground md:table-cell">
                      {employee.phone ? (
                        <a href={`tel:${employee.phone}`} className="hover:text-foreground hover:underline">
                          {employee.phone}
                        </a>
                      ) : (
                        '—'
                      )}
                    </TableCell>

                    <TableCell className="hidden text-muted-foreground lg:table-cell">
                      {employee.joinedOn ? formatDay(employee.joinedOn) : '—'}
                    </TableCell>

                    <TableCell className="text-right tabular-nums">
                      <span className="font-medium">{formatMoney(employee.currentDailyWage)}</span>
                      {employee.upcomingDailyWage !== null && employee.upcomingWageFrom && (
                        <div className="text-xs text-muted-foreground">
                          {formatMoney(employee.upcomingDailyWage)} from {formatDay(employee.upcomingWageFrom)}
                        </div>
                      )}
                    </TableCell>

                    <TableCell className="text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${employee.name}`}>
                            <MoreHorizontal className="size-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem asChild>
                            <Link to={`/employees/${employee.id}`}>
                              <UserRound className="size-4" />
                              View details
                            </Link>
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => openDialog(employee)}>
                            <Pencil className="size-4" />
                            Edit
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onClick={() => setActive.mutate({ id: employee.id, isActive: !employee.isActive })}
                          >
                            {employee.isActive ? 'Deactivate' : 'Activate'}
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <EmployeeDialog open={isDialogOpen} onOpenChange={setIsDialogOpen} employee={editing} />
    </>
  )
}
