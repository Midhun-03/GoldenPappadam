import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Plus, Trash2 } from 'lucide-react'
import { useDeferredValue, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { productsApi, stockApi } from '@/api/inventory'
import { customerBranchesApi, customersApi, invoicesApi, type CreateInvoice } from '@/api/sales'
import { PageHeader } from '@/components/PageHeader'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ApiError } from '@/lib/api'
import { formatMoney, formatQuantity, todayInIndia } from '@/lib/format'
import { cn } from '@/lib/utils'

type Line = { key: number; productId: string; quantity: string; unitPrice: string }

const emptyLine = (key: number): Line => ({ key, productId: '', quantity: '', unitPrice: '' })

export function NewInvoicePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()

  const [customerId, setCustomerId] = useState(searchParams.get('customerId') ?? '')
  const [branchId, setBranchId] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(todayInIndia())
  const [discount, setDiscount] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<Line[]>([emptyLine(1)])
  const [error, setError] = useState<string | null>(null)

  const customers = useQuery({ queryKey: ['customers', {}], queryFn: () => customersApi.list() })
  const products = useQuery({ queryKey: ['products', {}], queryFn: () => productsApi.list() })
  const stock = useQuery({ queryKey: ['stock', {}], queryFn: () => stockApi.onHand() })

  const customer = customers.data?.find((candidate) => candidate.id === customerId)
  const needsBranch = customer?.hasMultipleBranches === true

  const branches = useQuery({
    queryKey: ['customers', customerId, 'branches'],
    queryFn: () => customerBranchesApi.list(customerId),
    enabled: needsBranch,
  })

  function selectCustomer(nextCustomerId: string) {
    setCustomerId(nextCustomerId)
    // A branch chosen for one shop must never silently attach to a different one.
    setBranchId('')
  }

  const discountValue = discount.trim() === '' ? 0 : Number(discount)
  const filled = lines
    .map((line) => ({ line, quantity: line.quantity.trim() === '' ? 0 : Number(line.quantity) }))
    .filter((row) => row.line.productId !== '' && row.quantity > 0)

  const request: CreateInvoice = {
    customerId,
    branchId: needsBranch ? branchId : undefined,
    invoiceDate,
    discountAmount: discountValue,
    notes: notes.trim() || undefined,
    lines: filled.map((row) => ({
      productId: row.line.productId,
      quantity: row.quantity,
      unitPrice: row.line.unitPrice.trim() === '' ? undefined : Number(row.line.unitPrice),
    })),
  }

  // The totals on this screen are the server's, worked out by the same code that finalizes the
  // bill: the shop's agreed rate, the discount, GST and the round-off. The request is deferred so
  // typing a quantity does not fire a request per keystroke.
  const deferredRequest = useDeferredValue(request)
  const previewKey = useMemo(() => JSON.stringify(deferredRequest), [deferredRequest])
  const canPreview =
    deferredRequest.customerId !== '' &&
    (!needsBranch || deferredRequest.branchId !== '') &&
    deferredRequest.lines.length > 0 &&
    discountValue >= 0

  const preview = useQuery({
    queryKey: ['invoice-preview', previewKey],
    queryFn: () => invoicesApi.preview(deferredRequest),
    enabled: canPreview,
    placeholderData: keepPreviousData,
    retry: false,
  })

  const shown = canPreview ? preview.data : undefined

  const priced = lines.map((line) => {
    const product = products.data?.find((candidate) => candidate.id === line.productId)
    const quantity = line.quantity.trim() === '' ? 0 : Number(line.quantity)
    const position = filled.findIndex((row) => row.line.key === line.key)
    const fromServer = position >= 0 ? shown?.lines[position] : undefined
    const unitPrice =
      line.unitPrice.trim() !== '' ? Number(line.unitPrice) : (fromServer?.unitPrice ?? product?.sellingPrice ?? 0)

    return {
      line,
      product,
      quantity,
      /** The rate the server will charge when none is typed - the shop's agreed rate if it has one. */
      rate: fromServer?.unitPrice ?? product?.sellingPrice ?? null,
      total: fromServer ? fromServer.lineTotal : Math.round(quantity * unitPrice * 100) / 100,
    }
  })

  const subTotal = shown?.subTotal ?? priced.reduce((sum, row) => sum + row.total, 0)
  const total = shown?.totalAmount ?? subTotal - discountValue
  const taxed = shown?.documentType === 'TaxInvoice'

  const create = useMutation({
    mutationFn: () => invoicesApi.create(request),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] })

      toast.success(`${result.invoice.invoiceNumber} created · ${formatMoney(result.invoice.totalAmount)}`)
      result.warnings.forEach((warning) => toast.warning(warning))

      navigate(`/invoices/${result.invoice.id}`)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not create the bill.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    if (priced.every((row) => row.line.productId === '' || row.quantity <= 0)) {
      setError('Add at least one product with a quantity.')
      return
    }

    if (needsBranch && branchId === '') {
      setError('Please select a branch before continuing.')
      return
    }

    create.mutate()
  }

  const updateLine = (key: number, change: Partial<Line>) =>
    setLines(lines.map((line) => (line.key === key ? { ...line, ...change } : line)))

  const onHandFor = (productId: string) => stock.data?.find((row) => row.productId === productId)

  return (
    <>
      <PageHeader
        back={{ to: '/invoices', label: 'Bills' }}
        title="New bill"
        description="Each line charges the shop's agreed rate, else the product price, and can be changed."
      />

      <form onSubmit={handleSubmit} className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-5">
        <div className="grid gap-4 lg:gap-5">
          <Card>
            <CardContent className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_11rem]">
              <div className="grid gap-1.5">
                <Label htmlFor="bill-customer">
                  Customer <span className="text-destructive">*</span>
                </Label>
                <Select value={customerId} onValueChange={selectCustomer}>
                  <SelectTrigger id="bill-customer" className="w-full">
                    <SelectValue placeholder="Choose a shop" />
                  </SelectTrigger>
                  <SelectContent>
                    {(customers.data ?? []).map((option) => (
                      <SelectItem key={option.id} value={option.id}>
                        {option.name}
                        {option.balance > 0 ? ` · owes ${formatMoney(option.balance)}` : ''}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {customer && customer.balance > 0 && (
                  <p className="text-xs text-warning">
                    This shop already owes {formatMoney(customer.balance)}.
                  </p>
                )}
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="invoiceDate">
                  Date <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="invoiceDate"
                  type="date"
                  required
                  value={invoiceDate}
                  onChange={(event) => setInvoiceDate(event.target.value)}
                />
              </div>

              {needsBranch && (
                <div className="grid gap-1.5 sm:col-span-2">
                  <Label htmlFor="bill-branch">
                    Branch <span className="text-destructive">*</span>
                  </Label>
                  <Select value={branchId} onValueChange={setBranchId}>
                    <SelectTrigger id="bill-branch" className="w-full sm:w-[calc(50%-0.5rem)]">
                      <SelectValue placeholder="Choose a branch" />
                    </SelectTrigger>
                    <SelectContent>
                      {(branches.data ?? []).map((option) => (
                        <SelectItem key={option.id} value={option.id}>
                          {option.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {branches.isSuccess && branches.data.length === 0 && (
                    <p className="text-xs text-warning">
                      This customer has no branches yet. Add one from the customer page first.
                    </p>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Products</CardTitle>
            </CardHeader>
            <CardContent className="@container grid gap-3">
              {/* Column headings for the wide layout; each row repeats them on a phone. */}
              <div className="hidden gap-3 px-1 text-xs font-medium text-muted-foreground @xl:grid @xl:grid-cols-[minmax(0,1fr)_6rem_6rem_6rem_2.25rem]">
                <span>Product</span>
                <span>Quantity</span>
                <span>Price</span>
                <span className="text-right">Line total</span>
                <span className="sr-only">Remove</span>
              </div>

              {priced.map((row, index) => {
                const available = onHandFor(row.line.productId)
                const isShort = available !== undefined && row.quantity > available.quantityOnHand

                return (
                  <div
                    key={row.line.key}
                    className="grid gap-3 rounded-lg border p-3 @xl:grid-cols-[minmax(0,1fr)_6rem_6rem_6rem_2.25rem] @xl:items-start @xl:rounded-none @xl:border-0 @xl:border-b @xl:p-0 @xl:pb-3 @xl:last:border-0"
                  >
                    <div className="grid gap-1.5">
                      <Label htmlFor={`line-product-${row.line.key}`} className="text-xs @xl:sr-only">
                        Product
                      </Label>
                      <Select
                        value={row.line.productId}
                        onValueChange={(value) => updateLine(row.line.key, { productId: value })}
                      >
                        <SelectTrigger id={`line-product-${row.line.key}`} className="w-full">
                          <SelectValue placeholder="Choose a product" />
                        </SelectTrigger>
                        <SelectContent>
                          {(products.data ?? []).map((product) => (
                            <SelectItem key={product.id} value={product.id}>
                              {product.name} ({product.unitCode})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      {available && (
                        <p className={cn('text-xs', isShort ? 'text-warning' : 'text-muted-foreground')}>
                          {formatQuantity(available.quantityOnHand)} {available.unitCode} in stock
                          {isShort && ' — this bill takes it below zero'}
                        </p>
                      )}
                    </div>

                    <div className="grid grid-cols-2 gap-3 @xl:contents">
                      <div className="grid gap-1.5">
                        <Label htmlFor={`line-quantity-${row.line.key}`} className="text-xs @xl:sr-only">
                          Quantity
                        </Label>
                        <Input
                          id={`line-quantity-${row.line.key}`}
                          type="number"
                          inputMode="decimal"
                          step="0.001"
                          min="0"
                          className="text-right"
                          value={row.line.quantity}
                          onChange={(event) => updateLine(row.line.key, { quantity: event.target.value })}
                        />
                      </div>

                      <div className="grid gap-1.5">
                        <Label htmlFor={`line-price-${row.line.key}`} className="text-xs @xl:sr-only">
                          Price
                        </Label>
                        <Input
                          id={`line-price-${row.line.key}`}
                          type="number"
                          inputMode="decimal"
                          step="0.01"
                          min="0"
                          className="text-right"
                          placeholder={row.rate !== null ? String(row.rate) : '0'}
                          value={row.line.unitPrice}
                          onChange={(event) => updateLine(row.line.key, { unitPrice: event.target.value })}
                        />
                      </div>
                    </div>

                    <div className="flex items-center justify-between gap-3 @xl:h-9 @xl:justify-end">
                      <span className="text-xs text-muted-foreground @xl:hidden">Line total</span>
                      <span className="font-medium tabular-nums">{formatMoney(row.total)}</span>
                    </div>

                    <div className="flex justify-end @xl:h-9 @xl:items-center">
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-sm"
                        disabled={lines.length === 1}
                        aria-label={`Remove line ${index + 1}`}
                        onClick={() => setLines(lines.filter((other) => other.key !== row.line.key))}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  </div>
                )
              })}

              <Button
                type="button"
                variant="outline"
                className="justify-self-start"
                onClick={() => setLines([...lines, emptyLine(Math.max(...lines.map((line) => line.key)) + 1)])}
              >
                <Plus className="size-4" />
                Add line
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="grid gap-1.5">
              <Label htmlFor="invoice-notes">Notes</Label>
              <Input
                id="invoice-notes"
                maxLength={300}
                placeholder="Anything worth remembering about this delivery"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            </CardContent>
          </Card>
        </div>

        <Card className="lg:sticky lg:top-6">
          <CardContent className="grid gap-3">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Subtotal</span>
              <span className="tabular-nums">{formatMoney(subTotal)}</span>
            </div>

            <div className="flex items-center justify-between gap-3">
              <Label htmlFor="discount" className="text-sm font-normal text-muted-foreground">
                Discount
              </Label>
              <Input
                id="discount"
                className="w-28 text-right"
                type="number"
                inputMode="decimal"
                step="0.01"
                min="0"
                placeholder="0"
                value={discount}
                onChange={(event) => setDiscount(event.target.value)}
              />
            </div>

            {taxed && shown && (
              <div className="grid gap-1.5 text-sm">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Taxable value</span>
                  <span className="tabular-nums">{formatMoney(shown.taxableAmount)}</span>
                </div>
                {shown.isInterState ? (
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">IGST</span>
                    <span className="tabular-nums">{formatMoney(shown.igstAmount)}</span>
                  </div>
                ) : (
                  <>
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">CGST</span>
                      <span className="tabular-nums">{formatMoney(shown.cgstAmount)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">SGST</span>
                      <span className="tabular-nums">{formatMoney(shown.sgstAmount)}</span>
                    </div>
                  </>
                )}
              </div>
            )}

            {shown && shown.roundOff !== 0 && (
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Round off</span>
                <span className="tabular-nums">
                  {shown.roundOff > 0 ? '+' : '−'} {formatMoney(Math.abs(shown.roundOff))}
                </span>
              </div>
            )}

            <div className="mt-1 flex items-baseline justify-between border-t pt-3">
              <span className="text-sm font-medium">Total</span>
              <span className={cn('font-heading text-2xl font-semibold tabular-nums', total < 0 && 'text-destructive')}>
                {formatMoney(total)}
              </span>
            </div>

            {canPreview && preview.isError && !error && (
              <Alert variant="destructive">
                <AlertDescription>
                  {preview.error instanceof ApiError ? preview.error.message : 'Could not work out the bill.'}
                </AlertDescription>
              </Alert>
            )}

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <Button
              type="submit"
              size="lg"
              className="mt-1"
              disabled={create.isPending || customerId === '' || (needsBranch && branchId === '')}
            >
              {create.isPending && <Loader2 className="size-4 animate-spin" />}
              {create.isPending ? 'Saving…' : 'Create bill'}
            </Button>

            <p className="text-xs text-muted-foreground">
              The bill is numbered and stock comes off when it is saved; its PDF is made straight after. Short stock
              warns but never blocks.
            </p>
          </CardContent>
        </Card>
      </form>
    </>
  )
}
