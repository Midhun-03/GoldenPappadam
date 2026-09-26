import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Plus, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { returnsApi, settlementRequest, type ReturnReason, type SettlementState } from '@/api/returns'
import { customerBranchesApi, customerPricesApi, customersApi } from '@/api/sales'
import { PageHeader } from '@/components/PageHeader'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ApiError } from '@/lib/api'
import { formatMoney, todayInIndia } from '@/lib/format'
import { ReturnSettlementFields } from './ReturnSettlementFields'

type Line = { key: number; productId: string; quantity: string; reason: ReturnReason; unitRate: string }

const emptyLine = (key: number): Line => ({ key, productId: '', quantity: '', reason: 'Expired', unitRate: '' })

/**
 * Packets a shop gave back. Only expired or damaged packets come back and they are never resold, so
 * nothing here adds to stock: the office only decides what the shop gets for them.
 */
export function NewReturnPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()

  const [customerId, setCustomerId] = useState(searchParams.get('customerId') ?? '')
  const [branchId, setBranchId] = useState('')
  const [returnDate, setReturnDate] = useState(todayInIndia())
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<Line[]>([emptyLine(1)])
  const [settlement, setSettlement] = useState<SettlementState>({ settlement: 'Pending', creditAmount: '', locationId: '' })
  const [error, setError] = useState<string | null>(null)

  const customers = useQuery({ queryKey: ['customers', {}], queryFn: () => customersApi.list() })
  const customer = customers.data?.find((candidate) => candidate.id === customerId)
  const needsBranch = customer?.hasMultipleBranches === true

  const branches = useQuery({
    queryKey: ['customers', customerId, 'branches'],
    queryFn: () => customerBranchesApi.list(customerId),
    enabled: needsBranch,
  })

  // Every product with this shop's rate, which is what its returned packets are worth.
  const prices = useQuery({
    queryKey: ['customers', customerId, 'prices'],
    queryFn: () => customerPricesApi.list(customerId),
    enabled: customerId !== '',
  })

  function selectCustomer(next: string) {
    setCustomerId(next)
    setBranchId('')
  }

  const rows = lines.map((line) => {
    const price = prices.data?.find((candidate) => candidate.productId === line.productId)
    const quantity = line.quantity.trim() === '' ? 0 : Number(line.quantity)
    const rate = line.unitRate.trim() !== '' ? Number(line.unitRate) : (price?.effectivePrice ?? 0)
    return { line, price, quantity, rate, value: Math.round(quantity * rate * 100) / 100 }
  })
  const filled = rows.filter((row) => row.line.productId !== '' && row.quantity > 0)
  const total = filled.reduce((sum, row) => sum + row.value, 0)

  const create = useMutation({
    mutationFn: () =>
      returnsApi.create({
        customerId,
        branchId: needsBranch ? branchId : undefined,
        returnDate,
        notes: notes.trim() || undefined,
        lines: filled.map((row) => ({
          productId: row.line.productId,
          quantity: row.quantity,
          reason: row.line.reason,
          unitRate: row.line.unitRate.trim() === '' ? undefined : Number(row.line.unitRate),
        })),
        ...settlementRequest(settlement),
      }),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: ['returns'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      await queryClient.invalidateQueries({ queryKey: ['customers'] })
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      await queryClient.invalidateQueries({ queryKey: ['payments'] })

      toast.success(`${result.return.returnNumber} recorded`)
      result.warnings.forEach((warning) => toast.warning(warning))
      navigate(`/returns/${result.return.id}`)
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not record the return.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    if (filled.length === 0) {
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

  return (
    <>
      <PageHeader
        back={{ to: '/returns', label: 'Returns' }}
        title="New return"
        description="Expired or damaged packets a shop gave back. They never go back into stock."
      />

      <form onSubmit={handleSubmit} className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-5">
        <div className="grid gap-4 lg:gap-5">
          <Card>
            <CardContent className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_11rem]">
              <div className="grid gap-1.5">
                <Label htmlFor="return-customer">
                  Customer <span className="text-destructive">*</span>
                </Label>
                <Select value={customerId} onValueChange={selectCustomer}>
                  <SelectTrigger id="return-customer" className="w-full">
                    <SelectValue placeholder="Choose a shop" />
                  </SelectTrigger>
                  <SelectContent>
                    {(customers.data ?? []).map((option) => (
                      <SelectItem key={option.id} value={option.id}>
                        {option.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-1.5">
                <Label htmlFor="returnDate">
                  Date <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="returnDate"
                  type="date"
                  required
                  max={todayInIndia()}
                  value={returnDate}
                  onChange={(event) => setReturnDate(event.target.value)}
                />
              </div>

              {needsBranch && (
                <div className="grid gap-1.5 sm:col-span-2">
                  <Label htmlFor="return-branch">
                    Branch <span className="text-destructive">*</span>
                  </Label>
                  <Select value={branchId} onValueChange={setBranchId}>
                    <SelectTrigger id="return-branch" className="w-full sm:w-[calc(50%-0.5rem)]">
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
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Packets returned</CardTitle>
            </CardHeader>
            <CardContent className="@container grid gap-3">
              <div className="hidden gap-3 px-1 text-xs font-medium text-muted-foreground @xl:grid @xl:grid-cols-[minmax(0,1fr)_6rem_7rem_6rem_6rem_2.25rem]">
                <span>Product</span>
                <span>Quantity</span>
                <span>Reason</span>
                <span>Rate</span>
                <span className="text-right">Value</span>
                <span className="sr-only">Remove</span>
              </div>

              {rows.map((row, index) => (
                <div
                  key={row.line.key}
                  className="grid gap-3 rounded-lg border p-3 @xl:grid-cols-[minmax(0,1fr)_6rem_7rem_6rem_6rem_2.25rem] @xl:items-start @xl:rounded-none @xl:border-0 @xl:border-b @xl:p-0 @xl:pb-3 @xl:last:border-0"
                >
                  <div className="grid gap-1.5">
                    <Label htmlFor={`return-product-${row.line.key}`} className="text-xs @xl:sr-only">
                      Product
                    </Label>
                    <Select
                      value={row.line.productId}
                      disabled={customerId === ''}
                      onValueChange={(value) => updateLine(row.line.key, { productId: value })}
                    >
                      <SelectTrigger id={`return-product-${row.line.key}`} className="w-full">
                        <SelectValue placeholder={customerId === '' ? 'Choose the shop first' : 'Choose a product'} />
                      </SelectTrigger>
                      <SelectContent>
                        {(prices.data ?? []).map((product) => (
                          <SelectItem key={product.productId} value={product.productId}>
                            {product.productName} ({product.unitCode})
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="grid grid-cols-3 gap-3 @xl:contents">
                    <div className="grid gap-1.5">
                      <Label htmlFor={`return-quantity-${row.line.key}`} className="text-xs @xl:sr-only">
                        Quantity
                      </Label>
                      <Input
                        id={`return-quantity-${row.line.key}`}
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
                      <Label htmlFor={`return-reason-${row.line.key}`} className="text-xs @xl:sr-only">
                        Reason
                      </Label>
                      <Select
                        value={row.line.reason}
                        onValueChange={(value) => updateLine(row.line.key, { reason: value as ReturnReason })}
                      >
                        <SelectTrigger id={`return-reason-${row.line.key}`} className="w-full">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Expired">Expired</SelectItem>
                          <SelectItem value="Damaged">Damaged</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    <div className="grid gap-1.5">
                      <Label htmlFor={`return-rate-${row.line.key}`} className="text-xs @xl:sr-only">
                        Rate
                      </Label>
                      <Input
                        id={`return-rate-${row.line.key}`}
                        type="number"
                        inputMode="decimal"
                        step="0.01"
                        min="0"
                        className="text-right"
                        placeholder={row.price?.effectivePrice != null ? String(row.price.effectivePrice) : '0'}
                        value={row.line.unitRate}
                        onChange={(event) => updateLine(row.line.key, { unitRate: event.target.value })}
                      />
                    </div>
                  </div>

                  <div className="flex items-center justify-between gap-3 @xl:h-9 @xl:justify-end">
                    <span className="text-xs text-muted-foreground @xl:hidden">Value</span>
                    <span className="font-medium tabular-nums">{formatMoney(row.value)}</span>
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
              ))}

              <Button
                type="button"
                variant="outline"
                className="justify-self-start"
                onClick={() => setLines([...lines, emptyLine(Math.max(...lines.map((line) => line.key)) + 1)])}
              >
                <Plus className="size-4" />
                Add line
              </Button>
              <p className="text-xs text-muted-foreground">
                The rate is what this shop pays for the product, and can be changed.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="grid gap-1.5">
              <Label htmlFor="return-notes">Notes</Label>
              <Input
                id="return-notes"
                maxLength={300}
                placeholder="Collected by, condition of the packets…"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            </CardContent>
          </Card>
        </div>

        <Card className="lg:sticky lg:top-6">
          <CardHeader>
            <CardTitle>What the shop gets</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Value of packets</span>
              <span className="font-medium tabular-nums">{formatMoney(total)}</span>
            </div>

            <ReturnSettlementFields value={settlement} onChange={setSettlement} packetsValue={total} allowPending />

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <Button type="submit" disabled={create.isPending || customerId === ''}>
              {create.isPending && <Loader2 className="size-4 animate-spin" />}
              {create.isPending ? 'Recording…' : 'Record return'}
            </Button>
          </CardContent>
        </Card>
      </form>
    </>
  )
}
