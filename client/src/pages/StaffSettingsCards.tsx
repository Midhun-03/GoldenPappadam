import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Plus, ReceiptIndianRupee, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { expenseCategoriesApi } from '@/api/accounting'
import { attendanceApi } from '@/api/staff'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/lib/api'

const showError = (caught: unknown, fallback: string) =>
  toast.error(caught instanceof ApiError ? caught.message : fallback)

/** Expense categories are data: new ones are added here, with no code change. */
export function ExpenseCategoriesCard() {
  const queryClient = useQueryClient()
  const [newName, setNewName] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')

  const categories = useQuery({ queryKey: ['expense-categories', 'all'], queryFn: () => expenseCategoriesApi.list(true) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['expense-categories'] })

  const create = useMutation({
    mutationFn: () => expenseCategoriesApi.create(newName.trim()),
    onSuccess: async () => {
      setNewName('')
      await refresh()
      toast.success('Expense category added')
    },
    onError: (caught) => showError(caught, 'Could not add the category.'),
  })

  const rename = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => expenseCategoriesApi.rename(id, name),
    onSuccess: async () => {
      setEditingId(null)
      await refresh()
      await queryClient.invalidateQueries({ queryKey: ['expenses'] })
      toast.success('Expense category renamed')
    },
    onError: (caught) => showError(caught, 'Could not rename the category.'),
  })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => expenseCategoriesApi.setActive(id, isActive),
    onSuccess: refresh,
    onError: (caught) => showError(caught, 'Could not change the category.'),
  })

  function handleAdd(event: FormEvent) {
    event.preventDefault()
    create.mutate()
  }

  return (
    <Card id="expense-categories">
      <CardHeader>
        <CardTitle>Expense categories</CardTitle>
        <CardDescription>
          Deactivate instead of deleting: past expenses keep their category. Employee wages is filled by wage
          payments and cannot be changed.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        <form className="flex gap-2" onSubmit={handleAdd}>
          <Label htmlFor="new-expense-category" className="sr-only">
            New expense category
          </Label>
          <Input
            id="new-expense-category"
            placeholder="New expense category"
            maxLength={100}
            required
            value={newName}
            onChange={(event) => setNewName(event.target.value)}
          />
          <Button type="submit" disabled={create.isPending}>
            <Plus className="size-4" />
            Add
          </Button>
        </form>

        {categories.isPending ? (
          <div className="grid gap-2">
            {Array.from({ length: 4 }, (_, index) => (
              <Skeleton key={index} className="h-9" />
            ))}
          </div>
        ) : categories.isError ? (
          <ErrorState error={categories.error} />
        ) : categories.data.length === 0 ? (
          <EmptyState icon={ReceiptIndianRupee} title="No expense categories" />
        ) : (
          <ul className="divide-y rounded-lg border">
            {categories.data.map((category) => (
              <li key={category.id} className="flex items-center gap-2 px-3 py-2">
                {editingId === category.id ? (
                  <>
                    <Label htmlFor={`rename-expense-${category.id}`} className="sr-only">
                      Rename {category.name}
                    </Label>
                    <Input
                      id={`rename-expense-${category.id}`}
                      autoFocus
                      value={editingName}
                      onChange={(event) => setEditingName(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter') rename.mutate({ id: category.id, name: editingName.trim() })
                        if (event.key === 'Escape') setEditingId(null)
                      }}
                    />
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      aria-label="Save name"
                      disabled={rename.isPending}
                      onClick={() => rename.mutate({ id: category.id, name: editingName.trim() })}
                    >
                      <Check className="size-4" />
                    </Button>
                    <Button variant="ghost" size="icon-sm" aria-label="Cancel" onClick={() => setEditingId(null)}>
                      <X className="size-4" />
                    </Button>
                  </>
                ) : (
                  <>
                    <span
                      className={
                        category.isActive
                          ? 'min-w-0 flex-1 truncate font-medium'
                          : 'min-w-0 flex-1 truncate text-muted-foreground'
                      }
                    >
                      {category.name}
                    </span>
                    {category.isSystem && <Badge variant="secondary">From wages</Badge>}
                    {!category.isActive && <Badge variant="outline">Inactive</Badge>}
                    {!category.isSystem && (
                      <>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            setEditingId(category.id)
                            setEditingName(category.name)
                          }}
                        >
                          Rename
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setActive.mutate({ id: category.id, isActive: !category.isActive })}
                        >
                          {category.isActive ? 'Deactivate' : 'Activate'}
                        </Button>
                      </>
                    )}
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * What each attendance status is worth, as a share of a day's wage. A change applies to weeks not yet
 * paid; paid weeks keep the value they were paid at.
 */
export function AttendanceValuesCard() {
  const queryClient = useQueryClient()
  const [values, setValues] = useState<Record<string, string>>({})

  const statuses = useQuery({ queryKey: ['attendance-statuses'], queryFn: attendanceApi.statuses })

  const update = useMutation({
    mutationFn: ({ id, dayFraction }: { id: string; dayFraction: number }) => attendanceApi.updateStatus(id, dayFraction),
    onSuccess: async (saved) => {
      setValues((current) => {
        const next = { ...current }
        delete next[saved.id]
        return next
      })
      await queryClient.invalidateQueries({ queryKey: ['attendance-statuses'] })
      await queryClient.invalidateQueries({ queryKey: ['attendance'] })
      await queryClient.invalidateQueries({ queryKey: ['wages'] })
      toast.success(`${saved.name} now counts as ${saved.dayFraction} of a day`)
    },
    onError: (caught) => showError(caught, 'Could not change the value.'),
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Attendance day values</CardTitle>
        <CardDescription>
          How much of a day’s wage each status earns. A change applies to weeks not yet paid; paid weeks keep the
          value they were paid at.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {statuses.isPending ? (
          <Skeleton className="h-36" />
        ) : statuses.isError ? (
          <ErrorState error={statuses.error} />
        ) : (
          <ul className="divide-y rounded-lg border">
            {statuses.data.map((status) => {
              const value = values[status.id] ?? String(status.dayFraction)
              const isChanged = values[status.id] !== undefined && Number(value) !== status.dayFraction

              return (
                <li key={status.id} className="flex items-center gap-3 px-3 py-2">
                  <Label htmlFor={`fraction-${status.id}`} className="min-w-0 flex-1 font-medium">
                    {status.name}
                  </Label>
                  <Input
                    id={`fraction-${status.id}`}
                    type="number"
                    inputMode="decimal"
                    min="0"
                    max="1"
                    step="0.05"
                    className="w-24 text-right tabular-nums"
                    value={value}
                    onChange={(event) => setValues({ ...values, [status.id]: event.target.value })}
                  />
                  <span className="w-10 text-xs text-muted-foreground">day</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={!isChanged || update.isPending || value === ''}
                    onClick={() => update.mutate({ id: status.id, dayFraction: Number(value) })}
                  >
                    Save
                  </Button>
                </li>
              )
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
