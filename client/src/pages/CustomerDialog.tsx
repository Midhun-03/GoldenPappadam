import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { customersApi } from '@/api/sales'
import type { Customer, SaveCustomer } from '@/api/types'
import { StateSelect } from '@/components/StateSelect'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
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

const empty = {
  name: '',
  contactPerson: '',
  phone: '',
  address: '',
  openingBalance: '',
  notes: '',
  hasMultipleBranches: false,
  email: '',
  isGstRegistered: false,
  gstin: '',
  stateCode: '',
}

export function CustomerDialog({
  open,
  onOpenChange,
  customer,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  customer: Customer | null
}) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [form, setForm] = useState(empty)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return

    setError(null)
    setForm(
      customer
        ? {
            name: customer.name,
            contactPerson: customer.contactPerson ?? '',
            phone: customer.phone ?? '',
            address: customer.address ?? '',
            openingBalance: customer.openingBalance ? String(customer.openingBalance) : '',
            notes: customer.notes ?? '',
            hasMultipleBranches: customer.hasMultipleBranches,
            email: customer.email ?? '',
            isGstRegistered: customer.isGstRegistered,
            gstin: customer.gstin ?? '',
            stateCode: customer.stateCode ?? '',
          }
        : empty,
    )
  }, [open, customer])

  const save = useMutation({
    mutationFn: (payload: SaveCustomer) =>
      customer ? customersApi.update(customer.id, payload) : customersApi.create(payload),
    onSuccess: async (saved) => {
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      toast.success(customer ? `Updated ${saved.name}` : `Added ${saved.name}`)
      onOpenChange(false)

      // A brand-new multi-branch customer has no branches yet: take the admin straight to where
      // "+ Add Branch" lives, instead of a customer they cannot yet bill.
      if (!customer && saved.hasMultipleBranches) {
        navigate(`/customers/${saved.id}`)
      }
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the customer.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    save.mutate({
      name: form.name,
      contactPerson: form.contactPerson.trim() || null,
      phone: form.phone.trim() || null,
      address: form.address.trim() || null,
      openingBalance: form.openingBalance.trim() === '' ? 0 : Number(form.openingBalance),
      notes: form.notes.trim() || null,
      hasMultipleBranches: form.hasMultipleBranches,
      email: form.email.trim() || null,
      isGstRegistered: form.isGstRegistered,
      gstin: form.isGstRegistered ? form.gstin.trim().toUpperCase() || null : null,
      stateCode: form.stateCode || null,
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{customer ? 'Edit customer' : 'New customer'}</DialogTitle>
          <DialogDescription>
            The opening balance is what this shop already owed before you started using the app.
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-1.5">
            <Label htmlFor="name">Shop name <span className="text-destructive">*</span></Label>
            <Input
              id="name"
              required
              maxLength={150}
              value={form.name}
              onChange={(event) => setForm({ ...form, name: event.target.value })}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="contactPerson">Contact person</Label>
              <Input
                id="contactPerson"
                maxLength={100}
                value={form.contactPerson}
                onChange={(event) => setForm({ ...form, contactPerson: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="phone">Phone</Label>
              <Input
                id="phone"
                maxLength={20}
                value={form.phone}
                onChange={(event) => setForm({ ...form, phone: event.target.value })}
              />
            </div>
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="address">Address</Label>
            <Input
              id="address"
              maxLength={300}
              value={form.address}
              onChange={(event) => setForm({ ...form, address: event.target.value })}
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="email">Email for invoices</Label>
            <Input
              id="email"
              type="email"
              maxLength={256}
              placeholder="accounts@shop.com"
              value={form.email}
              onChange={(event) => setForm({ ...form, email: event.target.value })}
            />
          </div>

          <div className="grid gap-3 rounded-lg border bg-muted/40 p-3">
            <div className="flex items-start gap-2.5">
              <Checkbox
                id="isGstRegistered"
                checked={form.isGstRegistered}
                onCheckedChange={(checked) => setForm({ ...form, isGstRegistered: checked === true })}
              />
              <div className="grid gap-0.5">
                <Label htmlFor="isGstRegistered" className="font-normal">
                  GST registered
                </Label>
                <p className="text-xs text-muted-foreground">
                  {form.isGstRegistered
                    ? 'This shop gets GST bills, showing its GSTIN and ours.'
                    : 'This shop gets normal bills. Tick this if it has a GSTIN and needs GST bills.'}
                </p>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              {form.isGstRegistered && (
                <div className="grid gap-1.5">
                  <Label htmlFor="gstin">
                    GSTIN <span className="text-destructive">*</span>
                  </Label>
                  <Input
                    id="gstin"
                    required
                    maxLength={30}
                    className="font-mono uppercase placeholder:font-sans placeholder:normal-case"
                    placeholder="32ABCDE1234F1Z5"
                    value={form.gstin}
                    onChange={(event) => setForm({ ...form, gstin: event.target.value })}
                  />
                </div>
              )}

              <div className="grid gap-1.5">
                <Label htmlFor="stateCode">State</Label>
                <StateSelect
                  id="stateCode"
                  allowNone
                  value={form.stateCode}
                  onChange={(stateCode) => setForm({ ...form, stateCode })}
                />
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              {form.isGstRegistered
                ? 'Leave the state as "Not set" to take it from the GSTIN.'
                : 'Optional. Only needed if a product ever becomes taxable.'}
              {form.hasMultipleBranches && ' A branch in another state sets its own.'}
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-1.5">
              <Label htmlFor="openingBalance">Opening balance</Label>
              <Input
                id="openingBalance"
                type="number"
                step="0.01"
                placeholder="0"
                value={form.openingBalance}
                onChange={(event) => setForm({ ...form, openingBalance: event.target.value })}
              />
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="notes">Notes</Label>
              <Input
                id="notes"
                maxLength={300}
                value={form.notes}
                onChange={(event) => setForm({ ...form, notes: event.target.value })}
              />
            </div>
          </div>

          <div className="group/field-label flex items-start gap-2.5">
            <Checkbox
              id="hasMultipleBranches"
              checked={form.hasMultipleBranches}
              onCheckedChange={(checked) => setForm({ ...form, hasMultipleBranches: checked === true })}
              disabled={Boolean(customer && customer.hasMultipleBranches && customer.activeBranchCount > 0)}
            />
            <div className="grid gap-0.5">
              <Label htmlFor="hasMultipleBranches" className="font-normal">
                This customer has multiple branches
              </Label>
              <p className="text-xs text-muted-foreground">
                {customer && customer.hasMultipleBranches && customer.activeBranchCount > 0
                  ? 'Deactivate its branches first to turn this off.'
                  : 'Each shop, like Kundara or Coimbatore, is added as a branch after saving.'}
              </p>
            </div>
          </div>

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
