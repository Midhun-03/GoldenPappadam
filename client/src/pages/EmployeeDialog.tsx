import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { employeesApi } from '@/api/staff'
import type { Employee, SaveEmployee } from '@/api/types'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
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
import { ApiError } from '@/lib/api'
import { todayInIndia } from '@/lib/format'

const empty = {
  name: '',
  designation: '',
  phone: '',
  address: '',
  joinedOn: '',
  dailyWage: '',
  wageEffectiveFrom: '',
}

/**
 * Adds or edits an employee. A new employee is given a daily wage here; an existing one's wage is
 * changed on their page, where it is added to the history instead of overwriting it.
 */
export function EmployeeDialog({
  open,
  onOpenChange,
  employee,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  employee: Employee | null
}) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState(empty)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return

    setError(null)
    setForm(
      employee
        ? {
            ...empty,
            name: employee.name,
            designation: employee.designation ?? '',
            phone: employee.phone ?? '',
            address: employee.address ?? '',
            joinedOn: employee.joinedOn ?? '',
          }
        : { ...empty, wageEffectiveFrom: todayInIndia() },
    )
  }, [open, employee])

  const save = useMutation({
    mutationFn: (payload: SaveEmployee) =>
      employee ? employeesApi.update(employee.id, payload) : employeesApi.create(payload),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['employees'] })
      await queryClient.invalidateQueries({ queryKey: ['attendance'] })
      await queryClient.invalidateQueries({ queryKey: ['wages'] })
      toast.success(employee ? `Updated ${saved.name}` : `Added ${saved.name}`)
      onOpenChange(false)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the employee.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    save.mutate({
      name: form.name,
      designation: form.designation.trim() || null,
      phone: form.phone.trim() || null,
      address: form.address.trim() || null,
      joinedOn: form.joinedOn || null,
      ...(employee
        ? {}
        : {
            dailyWage: form.dailyWage === '' ? null : Number(form.dailyWage),
            wageEffectiveFrom: form.wageEffectiveFrom || form.joinedOn || null,
          }),
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{employee ? 'Edit employee' : 'New employee'}</DialogTitle>
          <DialogDescription>
            {employee
              ? 'To change the daily wage, use "Change wage" on the employee’s page, so the old rate is kept.'
              : 'Employees are paid a daily wage every Saturday, for the attendance marked that week.'}
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="employee-name">
                Name <span className="text-destructive">*</span>
              </Label>
              <Input
                id="employee-name"
                required
                maxLength={150}
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="employee-designation">Role</Label>
              <Input
                id="employee-designation"
                maxLength={100}
                placeholder="Packing, Driver…"
                value={form.designation}
                onChange={(event) => setForm({ ...form, designation: event.target.value })}
              />
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="employee-phone">Phone</Label>
              <Input
                id="employee-phone"
                type="tel"
                maxLength={20}
                value={form.phone}
                onChange={(event) => setForm({ ...form, phone: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="employee-joined">Joined on</Label>
              <Input
                id="employee-joined"
                type="date"
                max={todayInIndia()}
                value={form.joinedOn}
                onChange={(event) => setForm({ ...form, joinedOn: event.target.value })}
              />
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="employee-address">Address</Label>
            <Input
              id="employee-address"
              maxLength={300}
              value={form.address}
              onChange={(event) => setForm({ ...form, address: event.target.value })}
            />
          </div>

          {!employee && (
            <div className="grid gap-4 rounded-lg border bg-muted/40 p-3 sm:grid-cols-2">
              <div className="grid gap-1.5">
                <Label htmlFor="employee-wage">
                  Daily wage (₹) <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="employee-wage"
                  type="number"
                  inputMode="decimal"
                  min="0.01"
                  step="0.01"
                  required
                  value={form.dailyWage}
                  onChange={(event) => setForm({ ...form, dailyWage: event.target.value })}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="employee-wage-from">Wage from</Label>
                <Input
                  id="employee-wage-from"
                  type="date"
                  value={form.wageEffectiveFrom}
                  onChange={(event) => setForm({ ...form, wageEffectiveFrom: event.target.value })}
                />
              </div>
            </div>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && <Loader2 className="size-4 animate-spin" />}
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
