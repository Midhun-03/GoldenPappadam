import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { categoriesApi, unitsApi } from '@/api/inventory'
import { PageHeader } from '@/components/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'

const showError = (caught: unknown, fallback: string) =>
  toast.error(caught instanceof ApiError ? caught.message : fallback)

export function SettingsPage() {
  return (
    <>
      <PageHeader title="Settings" description="Categories and units used by products." />
      <div className="grid gap-6 lg:grid-cols-2">
        <CategoriesCard />
        <UnitsCard />
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
    onSuccess: async () => {
      await refresh()
    },
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
          <Input
            placeholder="New category"
            maxLength={100}
            required
            value={newName}
            onChange={(event) => setNewName(event.target.value)}
          />
          <Button type="submit" disabled={create.isPending}>
            Add
          </Button>
        </form>

        <Table>
          <TableBody>
            {(categories.data ?? []).map((category) => (
              <TableRow key={category.id}>
                <TableCell>
                  {editingId === category.id ? (
                    <Input
                      autoFocus
                      value={editingName}
                      onChange={(event) => setEditingName(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter') rename.mutate({ id: category.id, name: editingName.trim() })
                        if (event.key === 'Escape') setEditingId(null)
                      }}
                    />
                  ) : (
                    <span className={category.isActive ? 'font-medium' : 'text-muted-foreground'}>
                      {category.name}
                      {!category.isActive && (
                        <Badge variant="outline" className="ml-2">
                          Inactive
                        </Badge>
                      )}
                    </span>
                  )}
                </TableCell>
                <TableCell className="text-right whitespace-nowrap">
                  {editingId === category.id ? (
                    <>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => rename.mutate({ id: category.id, name: editingName.trim() })}
                      >
                        Save
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => setEditingId(null)}>
                        Cancel
                      </Button>
                    </>
                  ) : (
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
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
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
    onSuccess: async () => {
      await refresh()
    },
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
          <Input
            className="w-24"
            placeholder="Code"
            maxLength={10}
            required
            value={code}
            onChange={(event) => setCode(event.target.value)}
          />
          <Input
            placeholder="Name"
            maxLength={50}
            required
            value={name}
            onChange={(event) => setName(event.target.value)}
          />
          <Button type="submit" disabled={create.isPending}>
            Add
          </Button>
        </form>

        <Table>
          <TableBody>
            {(units.data ?? []).map((unit) => (
              <TableRow key={unit.id}>
                <TableCell className="w-24 font-mono text-xs">{unit.code}</TableCell>
                <TableCell className={unit.isActive ? undefined : 'text-muted-foreground'}>
                  {unit.name}
                  {!unit.isActive && (
                    <Badge variant="outline" className="ml-2">
                      Inactive
                    </Badge>
                  )}
                </TableCell>
                <TableCell className="text-right">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setActive.mutate({ id: unit.id, isActive: !unit.isActive })}
                  >
                    {unit.isActive ? 'Deactivate' : 'Activate'}
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  )
}
