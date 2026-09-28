import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, Plus, Tag, Ruler, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { categoriesApi, unitsApi } from '@/api/inventory'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { PageHeader } from '@/components/PageHeader'
import { InvoiceSettingsCard } from './InvoiceSettingsCard'
import { AttendanceValuesCard, ExpenseCategoriesCard } from './StaffSettingsCards'
import { UsersCard } from './UsersCard'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { ApiError } from '@/lib/api'

const showError = (caught: unknown, fallback: string) =>
  toast.error(caught instanceof ApiError ? caught.message : fallback)

function ListSkeleton() {
  return (
    <div className="grid gap-2">
      {Array.from({ length: 4 }, (_, index) => (
        <Skeleton key={index} className="h-9" />
      ))}
    </div>
  )
}

export function SettingsPage() {
  return (
    <>
      <PageHeader
        title="Settings"
        description="The business on its invoices, who can sign in, the categories and units that products are built from, and how expenses and attendance are counted."
      />
      <div className="grid gap-4 lg:grid-cols-2 lg:gap-5">
        <InvoiceSettingsCard />
        <UsersCard />
        <CategoriesCard />
        <UnitsCard />
        <ExpenseCategoriesCard />
        <AttendanceValuesCard />
      </div>
    </>
  )
}

function CategoriesCard() {
  const queryClient = useQueryClient()
  const [newName, setNewName] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')

  const categories = useQuery({ queryKey: ['categories', 'all'], queryFn: () => categoriesApi.list(true) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['categories'] })

  const create = useMutation({
    mutationFn: () => categoriesApi.create(newName.trim()),
    onSuccess: async () => {
      setNewName('')
      await refresh()
      toast.success('Category added')
    },
    onError: (caught) => showError(caught, 'Could not add the category.'),
  })

  const rename = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => categoriesApi.rename(id, name),
    onSuccess: async () => {
      setEditingId(null)
      await refresh()
      toast.success('Category renamed')
    },
    onError: (caught) => showError(caught, 'Could not rename the category.'),
  })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => categoriesApi.setActive(id, isActive),
    onSuccess: refresh,
    onError: (caught) => showError(caught, 'Could not change the category.'),
  })

  function handleAdd(event: FormEvent) {
    event.preventDefault()
    create.mutate()
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Categories</CardTitle>
        <CardDescription>Deactivate instead of deleting: products keep pointing at them.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        <form className="flex gap-2" onSubmit={handleAdd}>
          <Label htmlFor="new-category" className="sr-only">
            New category
          </Label>
          <Input
            id="new-category"
            placeholder="New category"
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
          <ListSkeleton />
        ) : categories.isError ? (
          <ErrorState error={categories.error} />
        ) : categories.data.length === 0 ? (
          <EmptyState
            icon={Tag}
            title="No categories yet"
            description="Add one above, then products can be grouped by it."
          />
        ) : (
          <ul className="divide-y rounded-lg border">
            {categories.data.map((category) => (
              <li key={category.id} className="flex items-center gap-2 px-3 py-2">
                {editingId === category.id ? (
                  <>
                    <Label htmlFor={`rename-${category.id}`} className="sr-only">
                      Rename {category.name}
                    </Label>
                    <Input
                      id={`rename-${category.id}`}
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
                        category.isActive ? 'min-w-0 flex-1 truncate font-medium' : 'min-w-0 flex-1 truncate text-muted-foreground'
                      }
                    >
                      {category.name}
                    </span>
                    {!category.isActive && <Badge variant="outline">Inactive</Badge>}
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
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

function UnitsCard() {
  const queryClient = useQueryClient()
  const [code, setCode] = useState('')
  const [name, setName] = useState('')

  const units = useQuery({ queryKey: ['units', 'all'], queryFn: () => unitsApi.list(true) })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['units'] })

  const create = useMutation({
    mutationFn: () => unitsApi.create(code.trim(), name.trim()),
    onSuccess: async () => {
      setCode('')
      setName('')
      await refresh()
      toast.success('Unit added')
    },
    onError: (caught) => showError(caught, 'Could not add the unit.'),
  })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => unitsApi.setActive(id, isActive),
    onSuccess: refresh,
    onError: (caught) => showError(caught, 'Could not change the unit.'),
  })

  function handleAdd(event: FormEvent) {
    event.preventDefault()
    create.mutate()
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Units</CardTitle>
        <CardDescription>How stock is counted: kilograms, pieces, packets, boxes.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        <form className="flex gap-2" onSubmit={handleAdd}>
          <Label htmlFor="new-unit-code" className="sr-only">
            Unit code
          </Label>
          <Input
            id="new-unit-code"
            className="w-24 shrink-0"
            placeholder="Code"
            maxLength={10}
            required
            value={code}
            onChange={(event) => setCode(event.target.value)}
          />
          <Label htmlFor="new-unit-name" className="sr-only">
            Unit name
          </Label>
          <Input
            id="new-unit-name"
            placeholder="Name"
            maxLength={50}
            required
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
          <Button type="submit" disabled={create.isPending}>
            <Plus className="size-4" />
            Add
          </Button>
        </form>

        {units.isPending ? (
          <ListSkeleton />
        ) : units.isError ? (
          <ErrorState error={units.error} />
        ) : units.data.length === 0 ? (
          <EmptyState icon={Ruler} title="No units yet" description="Add one above before creating products." />
        ) : (
          <ul className="divide-y rounded-lg border">
            {units.data.map((unit) => (
              <li key={unit.id} className="flex items-center gap-3 px-3 py-2">
                <span className="w-14 shrink-0 font-mono text-xs font-medium">{unit.code}</span>
                <span className={unit.isActive ? 'min-w-0 flex-1 truncate' : 'min-w-0 flex-1 truncate text-muted-foreground'}>
                  {unit.name}
                </span>
                {!unit.isActive && <Badge variant="outline">Inactive</Badge>}
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setActive.mutate({ id: unit.id, isActive: !unit.isActive })}
                >
                  {unit.isActive ? 'Deactivate' : 'Activate'}
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
