import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { customersApi } from '@/api/sales'
import type { Customer } from '@/api/types'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
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
    onError: (caught) => toast.error(caught instanceof ApiError ? caught.message : 'Could not change the customer.'),
  })

  const rows = customers.data ?? []
  const totalOwed = rows.reduce((sum, customer) => sum + Math.max(customer.balance, 0), 0)

  return (
    <>
      <PageHeader
        title="Customers"
        description={`${rows.length} shown · ${formatMoney(totalOwed)} outstanding`}
        action={
          <Button
            onClick={() => {
              setEditing(null)
              setIsDialogOpen(true)
            }}
          >
            <Plus className="size-4" />
            New customer
          </Button>
        }
      />

      <Card className="mb-4">
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="grid gap-2">
            <Label htmlFor="search">Search</Label>
            <Input
              id="search"
              className="w-56"
              placeholder="Shop name or phone"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>

          <Button variant={withBalanceOnly ? 'default' : 'outline'} onClick={() => setWithBalanceOnly(!withBalanceOnly)}>
            Owing money only
          </Button>

          <Button variant="ghost" size="sm" onClick={() => setIncludeInactive(!includeInactive)}>
            {includeInactive ? 'Hide inactive' : 'Show inactive'}
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Shop</TableHead>
                <TableHead>Contact</TableHead>
                <TableHead>Phone</TableHead>
                <TableHead className="text-right">Balance</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((customer) => (
                <TableRow key={customer.id} className={customer.isActive ? undefined : 'opacity-60'}>
                  <TableCell>
                    <Link className="font-medium hover:underline" to={`/customers/${customer.id}`}>
                      {customer.name}
                    </Link>
                    {!customer.isActive && (
                      <Badge variant="outline" className="ml-2">
                        Inactive
                      </Badge>
                    )}
                    {customer.address && (
                      <div className="text-xs text-muted-foreground">{customer.address}</div>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{customer.contactPerson ?? '—'}</TableCell>
                  <TableCell className="text-muted-foreground">{customer.phone ?? '—'}</TableCell>
                  <TableCell
                    className={cn(
                      'text-right font-medium tabular-nums',
                      customer.balance > 0 && 'text-destructive',
                      customer.balance < 0 && 'text-emerald-600',
                    )}
                  >
                    {formatMoney(customer.balance)}
                    {customer.balance < 0 && <div className="text-xs font-normal">in advance</div>}
                  </TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    <Button variant="ghost" size="sm" asChild>
                      <Link to={`/customers/${customer.id}`}>Ledger</Link>
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setEditing(customer)
                        setIsDialogOpen(true)
                      }}
                    >
                      Edit
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setActive.mutate({ id: customer.id, isActive: !customer.isActive })}
                    >
                      {customer.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                  </TableCell>
                </TableRow>
              ))}

              {rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="py-10 text-center text-muted-foreground">
                    No customers yet. Add the shops you deliver to.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <CustomerDialog open={isDialogOpen} onOpenChange={setIsDialogOpen} customer={editing} />
    </>
  )
}
