import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MoreHorizontal, Pencil, Plus, Search, Store, Wallet } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { customersApi } from '@/api/sales'
import type { Customer } from '@/api/types'
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
import { formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import { CustomerDialog } from './CustomerDialog'

export function CustomersPage() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [withBalanceOnly, setWithBalanceOnly] = useState(false)
  const [includeInactive, setIncludeInactive] = useState(false)
  const [editing, setEditing] = useState<Customer | null>(null)
  const [isDialogOpen, setIsDialogOpen] = useState(false)

  const filters = { search: search || undefined, withBalanceOnly, includeInactive }
  const customers = useQuery({ queryKey: ['customers', filters], queryFn: () => customersApi.list(filters) })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => customersApi.setActive(id, isActive),
    onSuccess: async (customer) => {
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      toast.success(`${customer.name} is now ${customer.isActive ? 'active' : 'inactive'}`)
    },
    onError: (caught) =>
      toast.error(caught instanceof ApiError ? caught.message : 'Could not change the customer.'),
  })

  const rows = customers.data ?? []
  const totalOwed = rows.reduce((sum, customer) => sum + Math.max(customer.balance, 0), 0)
  const isFiltered = search !== '' || withBalanceOnly

  function openDialog(customer: Customer | null) {
    setEditing(customer)
    setIsDialogOpen(true)
  }

  return (
    <>
      <PageHeader
        title="Customers"
        description={
          customers.isPending ? undefined : (
            <span className="tabular-nums">
              {rows.length} shown ·{' '}
              <span className={cn(totalOwed > 0 && 'font-medium text-destructive')}>
                {formatMoney(totalOwed)} outstanding
              </span>
            </span>
          )
        }
        action={
          <Button onClick={() => openDialog(null)}>
            <Plus className="size-4" />
            New customer
          </Button>
        }
      />

      <FilterBar>
        <FilterField label="Search" htmlFor="customer-search" className="col-span-2 sm:w-64">
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="customer-search"
              className="pl-8"
              placeholder="Shop name or phone"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
        </FilterField>

        <FilterToggle pressed={withBalanceOnly} onPressedChange={setWithBalanceOnly}>
          Owing money only
        </FilterToggle>

        <FilterToggle pressed={includeInactive} onPressedChange={setIncludeInactive}>
          Show inactive
        </FilterToggle>
      </FilterBar>

      <Card>
        <CardContent className="px-0">
          {customers.isPending ? (
            <TableSkeleton columns={4} />
          ) : customers.isError ? (
            <ErrorState error={customers.error} />
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Store}
              title={isFiltered ? 'No customers match' : 'No customers yet'}
              description={
                isFiltered
                  ? 'Try a different name or phone number.'
                  : 'Add the shops you deliver to, and their bills and payments will build up here.'
              }
              action={
                isFiltered ? undefined : (
                  <Button onClick={() => openDialog(null)}>
                    <Plus className="size-4" />
                    New customer
                  </Button>
                )
              }
            />
          ) : (
            <Table>
              <TableHeader sticky>
                <TableRow>
                  <TableHead>Shop</TableHead>
                  <TableHead className="hidden lg:table-cell">Contact</TableHead>
                  <TableHead className="hidden md:table-cell">Phone</TableHead>
                  <TableHead className="text-right">Balance</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((customer) => (
                  <TableRow key={customer.id} className={customer.isActive ? undefined : 'opacity-60'}>
                    <TableCell className="max-w-[16rem]">
                      <div className="flex items-center gap-2">
                        <Link
                          className="truncate font-medium underline-offset-4 hover:underline"
                          to={`/customers/${customer.id}`}
                        >
                          {customer.name}
                        </Link>
                        {!customer.isActive && <Badge variant="outline">Inactive</Badge>}
                      </div>
                      {(customer.address || customer.phone) && (
                        <div className="mt-0.5 truncate text-xs text-muted-foreground">
                          {customer.address}
                          <span className="md:hidden">
                            {customer.address && customer.phone ? ' · ' : ''}
                            {customer.phone}
                          </span>
                        </div>
                      )}
                    </TableCell>

                    <TableCell className="hidden text-muted-foreground lg:table-cell">
                      {customer.contactPerson ?? '—'}
                    </TableCell>

                    <TableCell className="hidden text-muted-foreground md:table-cell">
                      {customer.phone ? (
                        <a href={`tel:${customer.phone}`} className="hover:text-foreground hover:underline">
                          {customer.phone}
                        </a>
                      ) : (
                        '—'
                      )}
                    </TableCell>

                    <TableCell
                      className={cn(
                        'text-right font-medium tabular-nums',
                        customer.balance > 0 && 'text-destructive',
                        customer.balance < 0 && 'text-success',
                      )}
                    >
                      {formatMoney(customer.balance)}
                      {customer.balance < 0 && (
                        <div className="text-xs font-normal text-muted-foreground">in advance</div>
                      )}
                    </TableCell>

                    <TableCell className="text-right">
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${customer.name}`}>
                            <MoreHorizontal className="size-4" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem asChild>
                            <Link to={`/customers/${customer.id}`}>
                              <Wallet className="size-4" />
                              Ledger
                            </Link>
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => openDialog(customer)}>
                            <Pencil className="size-4" />
                            Edit
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onClick={() => setActive.mutate({ id: customer.id, isActive: !customer.isActive })}
                          >
                            {customer.isActive ? 'Deactivate' : 'Activate'}
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

      <CustomerDialog open={isDialogOpen} onOpenChange={setIsDialogOpen} customer={editing} />
    </>
  )
}
