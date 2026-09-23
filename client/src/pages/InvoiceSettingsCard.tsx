import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { invoiceSettingsApi } from '@/api/sales'
import type { InvoiceSettings } from '@/api/types'
import { ErrorState } from '@/components/EmptyState'
import { StateSelect } from '@/components/StateSelect'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { ApiError } from '@/lib/api'

type Form = {
  legalName: string
  address: string
  phone: string
  email: string
  gstin: string
  stateCode: string
  seriesCode: string
  pricesIncludeTax: boolean
  roundToNearestRupee: boolean
  paymentTerms: string
  bankDetails: string
  termsAndConditions: string
}

const toForm = (settings: InvoiceSettings): Form => ({
  legalName: settings.legalName,
  address: settings.address ?? '',
  phone: settings.phone ?? '',
  email: settings.email ?? '',
  gstin: settings.gstin ?? '',
  stateCode: settings.stateCode,
  seriesCode: settings.seriesCode,
  pricesIncludeTax: settings.pricesIncludeTax,
  roundToNearestRupee: settings.roundToNearestRupee,
  paymentTerms: settings.paymentTerms ?? '',
  bankDetails: settings.bankDetails ?? '',
  termsAndConditions: settings.termsAndConditions ?? '',
})

/**
 * The business as it appears on its invoices, how bills are numbered, and the switch that turns
 * GST on: entering the GSTIN. Changes apply to bills made afterwards; every invoice keeps what it
 * printed.
 */
export function InvoiceSettingsCard() {
  const queryClient = useQueryClient()
  const settings = useQuery({ queryKey: ['invoice-settings'], queryFn: invoiceSettingsApi.get })
  const [form, setForm] = useState<Form | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (settings.data) setForm(toForm(settings.data))
  }, [settings.data])

  const save = useMutation({
    mutationFn: (value: Form) =>
      invoiceSettingsApi.save({
        legalName: value.legalName,
        address: value.address.trim() || null,
        phone: value.phone.trim() || null,
        email: value.email.trim() || null,
        gstin: value.gstin.trim().toUpperCase() || null,
        stateCode: value.stateCode,
        seriesCode: value.seriesCode.trim().toUpperCase(),
        pricesIncludeTax: value.pricesIncludeTax,
        roundToNearestRupee: value.roundToNearestRupee,
        paymentTerms: value.paymentTerms.trim() || null,
        bankDetails: value.bankDetails.trim() || null,
        termsAndConditions: value.termsAndConditions.trim() || null,
      }),
    onSuccess: (saved) => {
      queryClient.setQueryData(['invoice-settings'], saved)
      toast.success(saved.gstEnabled ? 'Invoice settings saved. GST is on.' : 'Invoice settings saved')
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not save the settings.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (form) save.mutate(form)
  }

  const field = <K extends keyof Form>(key: K, value: Form[K]) => form && setForm({ ...form, [key]: value })

  return (
    <Card className="lg:col-span-2">
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          Invoices and GST
          {settings.data && (
            <Badge variant={settings.data.gstEnabled ? 'success' : 'outline'}>
              {settings.data.gstEnabled ? 'GST on' : 'GST off'}
            </Badge>
          )}
        </CardTitle>
        <CardDescription>
          What every invoice prints about the business. Changes apply to bills made from now on - an invoice already
          made keeps what it printed.
          {settings.data && (
            <>
              {' '}
              Next number: <span className="font-mono text-foreground">{settings.data.nextInvoiceNumber}</span>
            </>
          )}
        </CardDescription>
      </CardHeader>

      <CardContent>
        {settings.isError ? (
          <ErrorState error={settings.error} />
        ) : !form ? (
          <div className="grid gap-3">
            {Array.from({ length: 4 }, (_, index) => (
              <Skeleton key={index} className="h-9" />
            ))}
          </div>
        ) : (
          <form className="grid gap-4" onSubmit={handleSubmit}>
            <div className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-1.5">
                <Label htmlFor="legalName">
                  Legal name <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="legalName"
                  required
                  maxLength={150}
                  value={form.legalName}
                  onChange={(event) => field('legalName', event.target.value)}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="business-address">Address</Label>
                <Input
                  id="business-address"
                  maxLength={300}
                  value={form.address}
                  onChange={(event) => field('address', event.target.value)}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="business-phone">Phone</Label>
                <Input
                  id="business-phone"
                  maxLength={20}
                  value={form.phone}
                  onChange={(event) => field('phone', event.target.value)}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="business-email">Email</Label>
                <Input
                  id="business-email"
                  type="email"
                  maxLength={256}
                  placeholder="Customers' replies go here"
                  value={form.email}
                  onChange={(event) => field('email', event.target.value)}
                />
              </div>
            </div>

            <div className="grid gap-4 rounded-lg border bg-muted/40 p-3 md:grid-cols-3">
              <div className="grid gap-1.5">
                <Label htmlFor="business-state">
                  State <span className="text-destructive">*</span>
                </Label>
                <StateSelect id="business-state" value={form.stateCode} onChange={(code) => field('stateCode', code)} />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="business-gstin">GSTIN</Label>
                <Input
                  id="business-gstin"
                  maxLength={15}
                  className="font-mono uppercase placeholder:font-sans placeholder:normal-case"
                  placeholder="Not registered"
                  value={form.gstin}
                  onChange={(event) => field('gstin', event.target.value)}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="seriesCode">
                  Invoice prefix <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="seriesCode"
                  required
                  maxLength={3}
                  className="font-mono uppercase placeholder:font-sans placeholder:normal-case"
                  value={form.seriesCode}
                  onChange={(event) => field('seriesCode', event.target.value)}
                />
              </div>

              <p className="text-xs text-muted-foreground md:col-span-3">
                Entering the GSTIN switches GST on: every product then needs its GST treatment, and a taxed sale needs
                the shop's state. Leave it empty until the accountant confirms the registration and the rates. Numbers
                restart at 1 each financial year on their own; changing the prefix starts a new series.
              </p>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <label className="flex items-start gap-2.5" htmlFor="pricesIncludeTax">
                <Checkbox
                  id="pricesIncludeTax"
                  checked={form.pricesIncludeTax}
                  onCheckedChange={(checked) => field('pricesIncludeTax', checked === true)}
                />
                <span className="grid gap-0.5">
                  <span className="text-sm">Rates already include GST</span>
                  <span className="text-xs text-muted-foreground">
                    Like an MRP: the tax is worked out of the agreed rate, so the shop pays exactly that rate.
                  </span>
                </span>
              </label>

              <label className="flex items-start gap-2.5" htmlFor="roundToNearestRupee">
                <Checkbox
                  id="roundToNearestRupee"
                  checked={form.roundToNearestRupee}
                  onCheckedChange={(checked) => field('roundToNearestRupee', checked === true)}
                />
                <span className="grid gap-0.5">
                  <span className="text-sm">Round totals to the rupee</span>
                  <span className="text-xs text-muted-foreground">The difference is printed as the round-off.</span>
                </span>
              </label>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
              <div className="grid gap-1.5">
                <Label htmlFor="paymentTerms">Payment terms</Label>
                <Textarea
                  id="paymentTerms"
                  maxLength={200}
                  placeholder="Payable on next delivery"
                  value={form.paymentTerms}
                  onChange={(event) => field('paymentTerms', event.target.value)}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="bankDetails">Bank and UPI details</Label>
                <Textarea
                  id="bankDetails"
                  maxLength={500}
                  placeholder="Bank, account number, IFSC, UPI id"
                  value={form.bankDetails}
                  onChange={(event) => field('bankDetails', event.target.value)}
                />
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="termsAndConditions">Terms and conditions</Label>
                <Textarea
                  id="termsAndConditions"
                  maxLength={1000}
                  value={form.termsAndConditions}
                  onChange={(event) => field('termsAndConditions', event.target.value)}
                />
              </div>
            </div>

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <div>
              <Button type="submit" disabled={save.isPending}>
                {save.isPending && <Loader2 className="size-4 animate-spin" />}
                {save.isPending ? 'Saving…' : 'Save invoice settings'}
              </Button>
            </div>
          </form>
        )}
      </CardContent>
    </Card>
  )
}
