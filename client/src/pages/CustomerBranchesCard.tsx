import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Plus, Store } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { customerBranchesApi } from '@/api/sales'
import type { CustomerBranch } from '@/api/types'
import { EmptyState, ErrorState } from '@/components/EmptyState'
import { StateSelect } from '@/components/StateSelect'
import { TableSkeleton } from '@/components/TableSkeleton'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'

/**
 * The physical shops under a multi-branch customer, e.g. Kundara and Coimbatore under Danya
 * Supermarket. Only shown once the customer is marked multi-branch - a plain shop has nothing
 * to manage here.
 */
export function CustomerBranchesCard({ customerId }: { customerId: string }) {
  const [isOpen, setIsOpen] = useState(false)
  const [editing, setEditing] = useState<CustomerBranch | null>(null)
  const queryClient = useQueryClient()

  const branches = useQuery({
    queryKey: ['customers', customerId, 'branches'],
    queryFn: () => customerBranchesApi.list(customerId, { includeInactive: true }),
  })

  const setActive = useMutation({
    mutationFn: ({ branchId, isActive }: { branchId: string; isActive: boolean }) =>
      customerBranchesApi.setActive(customerId, branchId, isActive),
    onSuccess: async (branch) => {
      await queryClient.invalidateQueries({ queryKey: ['customers', customerId, 'branches'] })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      toast.success(`${branch.name} is now ${branch.isActive ? 'active' : 'inactive'}`)
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not change the branch.'),
  })

  const rows = branches.data ?? []

  function openDialog(branch: CustomerBranch | null) {
    setEditing(branch)
    setIsOpen(true)
  }

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between gap-3 space-y-0">
        <CardTitle>Branches</CardTitle>
        <Button size="sm" variant="outline" onClick={() => openDialog(null)}>
          <Plus className="size-4" />
          Add branch
        </Button>
      </CardHeader>

      <CardContent className="px-0">
        {branches.isPending ? (
          <TableSkeleton columns={3} />
        ) : branches.isError ? (
          <ErrorState error={branches.error} />
        ) : rows.length === 0 ? (
          <EmptyState
            icon={Store}
            title="No branches yet"
            description="Add each shop, like Kundara or Coimbatore, as its own branch."
            action={
              <Button onClick={() => openDialog(null)}>
                <Plus className="size-4" />
                Add branch
              </Button>
            }
          />
        ) : (
          <Table>
            <TableHeader sticky>
              <TableRow>
                <TableHead>Branch</TableHead>
                <TableHead className="hidden sm:table-cell">Contact</TableHead>
                <TableHead className="w-32" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((branch) => (
                <TableRow key={branch.id} className={branch.isActive ? undefined : 'opacity-60'}>
                  <TableCell className="max-w-[12rem]">
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        className="truncate font-medium underline-offset-4 hover:underline"
                        onClick={() => openDialog(branch)}
                      >
                        {branch.name}
                      </button>
                      {!branch.isActive && <Badge variant="outline">Inactive</Badge>}
                    </div>
                    {(branch.location || branch.address) && (
                      <div className="mt-0.5 truncate text-xs text-muted-foreground">
                        {branch.location}
                        {branch.location && branch.address ? ' · ' : ''}
                        {branch.address}
                      </div>
                    )}
                  </TableCell>

                  <TableCell className="hidden text-muted-foreground sm:table-cell">
                    {[branch.contactPerson, branch.phone].filter(Boolean).join(' · ') || '—'}
                  </TableCell>

                  <TableCell className="text-right">
                    <div className="flex justify-end gap-1">
                      <Button size="sm" variant="ghost" onClick={() => openDialog(branch)}>
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={setActive.isPending}
                        onClick={() => setActive.mutate({ branchId: branch.id, isActive: !branch.isActive })}
                      >
                        {branch.isActive ? 'Deactivate' : 'Activate'}
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <BranchDialog open={isOpen} onOpenChange={setIsOpen} customerId={customerId} branch={editing} />
    </Card>
  )
}

const emptyBranch = { name: '', location: '', address: '', phone: '', contactPerson: '', gstin: '', stateCode: '' }

function BranchDialog({
  open,
  onOpenChange,
  customerId,
  branch,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  customerId: string
  /** Set when editing an existing branch; null when adding a new one. */
  branch: CustomerBranch | null
}) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState(emptyBranch)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return

    setError(null)
    setForm(
      branch
        ? {
            name: branch.name,
            location: branch.location ?? '',
            address: branch.address ?? '',
            phone: branch.phone ?? '',
            contactPerson: branch.contactPerson ?? '',
            gstin: branch.gstin ?? '',
            stateCode: branch.stateCode ?? '',
          }
        : emptyBranch,
    )
  }, [open, branch])

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        name: form.name,
        location: form.location.trim() || null,
        address: form.address.trim() || null,
        phone: form.phone.trim() || null,
        contactPerson: form.contactPerson.trim() || null,
        gstin: form.gstin.trim().toUpperCase() || null,
        stateCode: form.stateCode || null,
      }

      return branch
        ? customerBranchesApi.update(customerId, branch.id, payload)
        : customerBranchesApi.create(customerId, payload)
    },
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['customers', customerId, 'branches'] })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      toast.success(branch ? `Updated ${saved.name}` : `Added ${saved.name}`)
      onOpenChange(false)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the branch.'),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    if (form.name.trim() === '') {
      setError('Enter a branch name.')
      return
    }

    save.mutate()
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>{branch ? 'Edit branch' : 'Add branch'}</DialogTitle>
            <DialogDescription>
              A branch is one physical shop under this customer, like Kundara or Coimbatore.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="grid gap-1.5">
              <Label htmlFor="branch-name">
                Branch name <span className="text-destructive">*</span>
              </Label>
              <Input
                id="branch-name"
                required
                autoFocus
                maxLength={150}
                placeholder="Kundara"
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label htmlFor="branch-location">Location / city</Label>
                <Input
                  id="branch-location"
                  maxLength={100}
                  value={form.location}
                  onChange={(event) => setForm({ ...form, location: event.target.value })}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="branch-phone">Phone</Label>
                <Input
                  id="branch-phone"
                  maxLength={20}
                  value={form.phone}
                  onChange={(event) => setForm({ ...form, phone: event.target.value })}
                />
              </div>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="branch-address">Address</Label>
              <Input
                id="branch-address"
                maxLength={300}
                value={form.address}
                onChange={(event) => setForm({ ...form, address: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="branch-contact">Contact person</Label>
              <Input
                id="branch-contact"
                maxLength={100}
                value={form.contactPerson}
                onChange={(event) => setForm({ ...form, contactPerson: event.target.value })}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label htmlFor="branch-state">State</Label>
                <StateSelect
                  id="branch-state"
                  allowNone
                  value={form.stateCode}
                  onChange={(stateCode) => setForm({ ...form, stateCode })}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="branch-gstin">GSTIN</Label>
                <Input
                  id="branch-gstin"
                  maxLength={15}
                  className="font-mono uppercase placeholder:font-sans placeholder:normal-case"
                  placeholder="If registered separately"
                  value={form.gstin}
                  onChange={(event) => setForm({ ...form, gstin: event.target.value })}
                />
              </div>
            </div>
            <p className="-mt-2 text-xs text-muted-foreground">
              A branch bill's place of supply is the branch itself, so a Coimbatore branch is billed IGST even when
              the head office is in Kerala.
            </p>

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              Save branch
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
