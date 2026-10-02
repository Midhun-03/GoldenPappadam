import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, Loader2, Plus, ReceiptText, Trash2 } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { shopSalesApi, shopStockApi, type ShopSaleDetail, type ShopStock } from '@/api/ownShop'
import { customersApi } from '@/api/sales'
import type { PaymentMethod } from '@/api/types'
import { PageHeader } from '@/components/PageHeader'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { ApiError } from '@/lib/api'
import { formatMoney, formatPaymentMethod, formatQuantity } from '@/lib/format'

const WALK_IN = 'walk-in'

/** The bundles the shop makes up ahead: a tap fills the pieces. They are still pieces, not products. */
const QUICK_PIECES = [15, 30, 50, 100]

const paymentMethods: PaymentMethod[] = ['Cash', 'UPI', 'BankTransfer', 'Cheque', 'Other']

type Line = {
  key: string
  productId: string
  quantity: string
  rate: string
  /** Typed by hand, so a change of customer does not overwrite it. */
  rateTouched: boolean
}

const newLine = (productId = '', rate = ''): Line => ({
  key: crypto.randomUUID(),
  productId,
  quantity: '',
  rate,
  rateTouched: false,
})

const money = (value: number) => Math.round(value * 100) / 100

/** What is wrong with a line as typed, or null. The server checks all of it again. */
function problemOf(line: Line, row: ShopStock | undefined): string | null {
  if (!row) return null
  const pieces = Number(line.quantity)
  const rate = Number(line.rate)

  if (line.quantity !== '' && (!Number.isInteger(pieces) || pieces <= 0)) return 'Pieces are counted whole.'
  if (pieces > row.shopPieces) return `The shop has ${formatQuantity(row.shopPieces)} pieces.`
  if (line.rate !== '' && row.minimumRate !== null && rate < row.minimumRate)
    return `No lower than ${formatMoney(row.minimumRate)} a piece.`
  if (line.rate !== '' && row.defaultRate !== null && rate > row.defaultRate)
    return `No higher than ${formatMoney(row.defaultRate)} a piece.`
  return null
}

/**
 * A sale over the own shop's counter, by the piece, paid in full now - to a walk-in customer or a
 * known one such as a caterer. The rate starts at the customer's agreed rate or the standard rate and
 * may be lowered to the variety's minimum, never raised.
 */
export function NewShopSalePage() {
  const queryClient = useQueryClient()
  const [customerId, setCustomerId] = useState(WALK_IN)
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<Line[]>([newLine()])
  const [error, setError] = useState<string | null>(null)
  const [lastSale, setLastSale] = useState<ShopSaleDetail | null>(null)

  // One id per sale on this screen, so a second press of Save sells once. A new one after each sale.
  const [clientRequestId, setClientRequestId] = useState(() => crypto.randomUUID())

  const customers = useQuery({ queryKey: ['customers', {}], queryFn: () => customersApi.list() })
  const buyer = customerId === WALK_IN ? undefined : customerId
  const stock = useQuery({
    queryKey: ['own-shop', 'stock', { customerId: buyer }],
    queryFn: () => shopStockApi.list(buyer),
  })

  const varieties = (stock.data ?? []).filter((row) => row.isActive)
  const rowOf = (productId: string) => varieties.find((row) => row.productId === productId)

  // A new customer brings their own agreed rates; lines whose rate nobody typed follow them. With a
  // single variety there is nothing to choose, so an empty line takes it.
  useEffect(() => {
    if (!stock.data) return
    const active = stock.data.filter((row) => row.isActive)
    setLines((current) =>
      current.map((line) => {
        const row =
          active.find((candidate) => candidate.productId === line.productId) ??
          (line.productId === '' && active.length === 1 ? active[0] : undefined)
        return row && !line.rateTouched
          ? { ...line, productId: row.productId, rate: row.rate?.toString() ?? '' }
          : line
      }),
    )
  }, [stock.data])

  function update(key: string, change: Partial<Line>) {
    setError(null)
    setLines((current) => current.map((line) => (line.key === key ? { ...line, ...change } : line)))
  }

  const priced = lines.map((line) => {
    const row = rowOf(line.productId)
    const total = line.quantity !== '' && line.rate !== '' ? money(Number(line.quantity) * Number(line.rate)) : 0
    return { line, row, total, problem: problemOf(line, row) }
  })
  const total = money(priced.reduce((sum, item) => sum + item.total, 0))
  const pieces = priced.reduce((sum, item) => sum + (Number(item.line.quantity) || 0), 0)
  const complete = priced.every(
    (item) => item.row && item.line.quantity !== '' && item.line.rate !== '' && item.problem === null,
  )
  const duplicate = new Set(lines.map((line) => line.productId)).size !== lines.length

  const sell = useMutation({
    mutationFn: () =>
      shopSalesApi.create({
        customerId: buyer ?? null,
        paymentMethod,
        lines: lines.map((line) => ({
          productId: line.productId,
          quantity: Number(line.quantity),
          unitPrice: Number(line.rate),
        })),
        notes: notes.trim() || undefined,
        clientRequestId,
      }),
    onSuccess: async (sale) => {
      await queryClient.invalidateQueries({ queryKey: ['own-shop'] })
      await queryClient.invalidateQueries({ queryKey: ['stock'] })
      toast.success(`${sale.saleNumber}: ${formatMoney(sale.totalAmount)} received`)
      setLastSale(sale)
      setCustomerId(WALK_IN)
      setPaymentMethod('Cash')
      setNotes('')
      setLines([varieties.length === 1 ? newLine(varieties[0].productId, varieties[0].defaultRate?.toString()) : newLine()])
      setClientRequestId(crypto.randomUUID())
    },
    onError: (caught) => setError(caught instanceof ApiError ? caught.message : 'Could not record the sale.'),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    sell.mutate()
  }

  return (
    <>
      <PageHeader
        back={{ to: '/own-shop', label: 'Own shop' }}
        title="New shop sale"
        description="Pieces sold over the counter and paid for now."
        action={
          <Button variant="outline" asChild>
            <Link to="/own-shop/sales">
              <ReceiptText className="size-4" />
              Shop sales
            </Link>
          </Button>
        }
      />

      {lastSale && (
        <Alert className="mb-4">
          <CheckCircle2 className="size-4 text-success" />
          <AlertDescription className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span>
              {lastSale.saleNumber} · {lastSale.customerName ?? 'Walk-in customer'} ·{' '}
              <span className="font-medium tabular-nums">{formatMoney(lastSale.totalAmount)}</span> by{' '}
              {formatPaymentMethod(lastSale.paymentMethod)}
            </span>
            <Link to={`/own-shop/sales/${lastSale.id}`} className="font-medium underline-offset-4 hover:underline">
              View
            </Link>
          </AlertDescription>
        </Alert>
      )}

      <form className="grid gap-4 lg:grid-cols-[1fr_minmax(0,20rem)] lg:gap-5" onSubmit={handleSubmit}>
        <Card className="min-w-0">
          <CardHeader>
            <CardTitle>Pieces</CardTitle>
          </CardHeader>
          <CardContent className="grid grid-cols-1 gap-3">
            {priced.map(({ line, row, total: lineTotal, problem }, index) => (
              <div key={line.key} className="grid min-w-0 grid-cols-1 gap-3 rounded-lg border p-3">
                <div className="flex items-end gap-2">
                  <div className="grid min-w-0 flex-1 grid-cols-1 gap-1.5">
                    <Label htmlFor={`sale-product-${line.key}`}>Pappadam</Label>
                    <Select
                      value={line.productId}
                      onValueChange={(value) =>
                        update(line.key, { productId: value, rate: rowOf(value)?.rate?.toString() ?? '', rateTouched: false })
                      }
                    >
                      <SelectTrigger id={`sale-product-${line.key}`} className="w-full">
                        <SelectValue placeholder="Choose" />
                      </SelectTrigger>
                      <SelectContent>
                        {varieties.map((option) => (
                          <SelectItem key={option.productId} value={option.productId}>
                            {option.name} · {formatQuantity(option.shopPieces)} pcs
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  {lines.length > 1 && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={`Remove line ${index + 1}`}
                      onClick={() => setLines((current) => current.filter((candidate) => candidate.key !== line.key))}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  )}
                </div>

                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="grid gap-1.5">
                    <Label htmlFor={`sale-pieces-${line.key}`}>Pieces</Label>
                    <Input
                      id={`sale-pieces-${line.key}`}
                      type="number"
                      inputMode="numeric"
                      step="1"
                      min="1"
                      required
                      value={line.quantity}
                      onChange={(event) => update(line.key, { quantity: event.target.value })}
                    />
                    <div className="flex flex-wrap gap-1.5">
                      {QUICK_PIECES.map((count) => (
                        <Button
                          key={count}
                          type="button"
                          variant="outline"
                          size="sm"
                          className="tabular-nums"
                          onClick={() => update(line.key, { quantity: String(count) })}
                        >
                          {count}
                        </Button>
                      ))}
                    </div>
                  </div>

                  <div className="grid content-start gap-1.5">
                    <Label htmlFor={`sale-rate-${line.key}`}>Rate per piece</Label>
                    <Input
                      id={`sale-rate-${line.key}`}
                      type="number"
                      inputMode="decimal"
                      step="0.01"
                      min={row?.minimumRate ?? 0}
                      max={row?.defaultRate ?? undefined}
                      required
                      value={line.rate}
                      onChange={(event) => update(line.key, { rate: event.target.value, rateTouched: true })}
                    />
                    {row && (
                      <p className="text-xs text-muted-foreground">
                        {formatMoney(row.minimumRate)} to {formatMoney(row.defaultRate)}
                      </p>
                    )}
                  </div>
                </div>

                <div className="flex items-baseline justify-between gap-3 text-sm">
                  <span className={problem ? 'text-destructive' : 'text-muted-foreground'}>
                    {problem ??
                      (line.quantity && line.rate
                        ? `${formatQuantity(Number(line.quantity))} × ${formatMoney(Number(line.rate))}`
                        : '')}
                  </span>
                  <span className="font-medium tabular-nums">{formatMoney(lineTotal)}</span>
                </div>
              </div>
            ))}

            <Button
              type="button"
              variant="outline"
              className="justify-self-start"
              disabled={lines.length >= varieties.length}
              onClick={() => setLines((current) => [...current, newLine()])}
            >
              <Plus className="size-4" />
              Add another pappadam
            </Button>
            {duplicate && <p className="text-xs text-destructive">A pappadam is listed twice. Put the pieces on one line.</p>}
          </CardContent>
        </Card>

        <Card className="min-w-0 lg:sticky lg:top-6 lg:self-start">
          <CardHeader>
            <CardTitle>Payment</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <div className="grid gap-1.5">
              <Label htmlFor="sale-customer">Customer</Label>
              <Select value={customerId} onValueChange={setCustomerId}>
                <SelectTrigger id="sale-customer" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={WALK_IN}>Walk-in customer</SelectItem>
                  {(customers.data ?? [])
                    .filter((customer) => customer.isActive)
                    .map((customer) => (
                      <SelectItem key={customer.id} value={customer.id}>
                        {customer.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="sale-method">Paid by</Label>
              <Select value={paymentMethod} onValueChange={(value) => setPaymentMethod(value as PaymentMethod)}>
                <SelectTrigger id="sale-method" className="w-full">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {paymentMethods.map((method) => (
                    <SelectItem key={method} value={method}>
                      {formatPaymentMethod(method)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-1.5">
              <Label htmlFor="sale-notes">Note</Label>
              <Input
                id="sale-notes"
                maxLength={300}
                placeholder="Optional"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
              />
            </div>

            <div className="flex items-baseline justify-between border-t pt-3">
              <span className="text-muted-foreground">
                Total · <span className="tabular-nums">{formatQuantity(pieces)}</span> pieces
              </span>
              <span className="text-xl font-semibold tabular-nums">{formatMoney(total)}</span>
            </div>

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <Button type="submit" size="lg" disabled={!complete || duplicate || sell.isPending}>
              {sell.isPending && <Loader2 className="size-4 animate-spin" />}
              {sell.isPending ? 'Saving…' : `Record sale · ${formatMoney(total)}`}
            </Button>
          </CardContent>
        </Card>
      </form>
    </>
  )
}
