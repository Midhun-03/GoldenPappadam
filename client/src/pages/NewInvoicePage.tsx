import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, Plus, Trash2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { productsApi, stockApi } from '@/api/inventory'
import { customersApi, invoicesApi } from '@/api/sales'
import { PageHeader } from '@/components/PageHeader'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api'
import { formatMoney, formatQuantity, todayInIndia } from '@/lib/format'

type Line = { key: number; productId: string; quantity: string; unitPrice: string }

const emptyLine = (key: number): Line => ({ key, productId: '', quantity: '', unitPrice: '' })

export function NewInvoicePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [searchParams] = useSearchParams()

  const [customerId, setCustomerId] = useState(searchParams.get('customerId') ?? '')
  const [invoiceDate, setInvoiceDate] = useState(todayInIndia())
  const [discount, setDiscount] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<Line[]>([emptyLine(1)])
  const [error, setError] = useState<string | null>(null)

  const customers = useQuery({ queryKey: ['customers', {}], queryFn: () => customersApi.list() })
  const products = useQuery({ queryKey: ['products', {}], queryFn: () => productsApi.list() })
  const stock = useQuery({ queryKey: ['stock', {}], queryFn: () => stockApi.onHand() })

  const priced = lines.map((line) => {
    const product = products.data?.find((p) => p.id === line.productId)
    const quantity = line.quantity.trim() === '' ? 0 : Number(line.quantity)
    const unitPrice =
      line.unitPrice.trim() !== '' ? Number(line.unitPrice) : (product?.sellingPrice ?? 0)

    return { line, product, quantity, unitPrice, total: Math.round(quantity * unitPrice * 100) / 100 }
  })

  const subTotal = priced.reduce((sum, row) => sum + row.total, 0)
  const discountValue = discount.trim() === '' ? 0 : Number(discount)
  const total = subTotal - discountValue

  const create = useMutation({
    mutationFn: () =>
      invoicesApi.create({
        customerId,
        invoiceDate,
        discountAmount: discountValue,
        notes: notes.trim() || undefined,
        lines: priced
          .filter((row) => row.line.productId !== '' && row.quantity > 0)
          .map((row) => ({
            productId: row.line.productId,
            quantity: row.quantity,
            unitPrice: row.line.unitPrice.trim() === '' ? undefined : Number(row.line.unitPrice),
          })),
      }),
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

    create.mutate()
  }

  const onHandFor = (productId: string) => stock.data?.find((row) => row.productId === productId)

  return (
    <>
      <PageHeader
        title="New bill"
        description="Prices come from the product and can be changed per line."
        action={
          <Button variant="outline" asChild>
            <Link to="/invoices">
              <ArrowLeft className="size-4" />
              All bills
            </Link>
          </Button>
        }
      />

      <form onSubmit={handleSubmit} className="grid gap-6">
        <Card>
          <CardContent className="flex flex-wrap items-end gap-4">
            <div className="grid gap-2">
              <Label>Customer</Label>
              <Select value={customerId} onValueChange={setCustomerId}>
                <SelectTrigger className="w-72">
                  <SelectValue placeholder="Choose a shop" />
                </SelectTrigger>
                <SelectContent>
                  {(customers.data ?? []).map((customer) => (
                    <SelectItem key={customer.id} value={customer.id}>
                      {customer.name}
                      {customer.balance > 0 ? ` · owes ${formatMoney(customer.balance)}` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="invoiceDate">Date</Label>
              <Input
                id="invoiceDate"
                type="date"
                required
                value={invoiceDate}
                onChange={(event) => setInvoiceDate(event.target.value)}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Products</CardTitle>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[45%]">Product</TableHead>
                  <TableHead className="w-32">Quantity</TableHead>
                  <TableHead className="w-32">Price</TableHead>
                  <TableHead className="text-right">Line total</TableHead>
                  <TableHead className="w-12" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {priced.map((row) => {
                  const available = onHandFor(row.line.productId)

                  return (
                    <TableRow key={row.line.key}>
                      <TableCell>
                        <Select
                          value={row.line.productId}
                          onValueChange={(value) =>
                            setLines(
                              lines.map((l) => (l.key === row.line.key ? { ...l, productId: value } : l)),
                            )
                          }
                        >
                          <SelectTrigger>
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
                          <div className="mt-1 text-xs text-muted-foreground">
                            {formatQuantity(available.quantityOnHand)} {available.unitCode} in stock
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        <Input
                          type="number"
                          step="0.001"
                          min="0"
                          value={row.line.quantity}
                          onChange={(event) =>
                            setLines(
                              lines.map((l) =>
                                l.key === row.line.key ? { ...l, quantity: event.target.value } : l,
                              ),
                            )
                          }
                        />
                      </TableCell>
                      <TableCell>
                        <Input
                          type="number"
                          step="0.01"
                          min="0"
                          placeholder={row.product?.sellingPrice ? String(row.product.sellingPrice) : '0'}
                          value={row.line.unitPrice}
                          onChange={(event) =>
                            setLines(
                              lines.map((l) =>
                                l.key === row.line.key ? { ...l, unitPrice: event.target.value } : l,
                              ),
                            )
                          }
                        />
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{formatMoney(row.total)}</TableCell>
                      <TableCell>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          disabled={lines.length === 1}
                          onClick={() => setLines(lines.filter((l) => l.key !== row.line.key))}
                        >
                          <Trash2 className="size-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  )
                })}
              </TableBody>
            </Table>

            <Button
              type="button"
              variant="outline"
              size="sm"
              className="mt-3"
              onClick={() => setLines([...lines, emptyLine(Math.max(...lines.map((l) => l.key)) + 1)])}
            >
              <Plus className="size-4" />
              Add line
            </Button>
          </CardContent>
        </Card>

        <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
          <Card>
            <CardContent>
              <div className="grid gap-2">
                <Label htmlFor="invoice-notes">Notes</Label>
                <Input
                  id="invoice-notes"
                  maxLength={300}
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="grid gap-3">
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Subtotal</span>
                <span className="tabular-nums">{formatMoney(subTotal)}</span>
              </div>

              <div className="flex items-center justify-between gap-3 text-sm">
                <Label htmlFor="discount" className="text-muted-foreground">
                  Discount
                </Label>
                <Input
                  id="discount"
                  className="w-32"
                  type="number"
                  step="0.01"
                  min="0"
                  placeholder="0"
                  value={discount}
                  onChange={(event) => setDiscount(event.target.value)}
                />
              </div>

              <div className="flex items-center justify-between border-t pt-3">
                <span className="font-medium">Total</span>
                <span className="font-heading text-xl font-semibold tabular-nums">{formatMoney(total)}</span>
              </div>

              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <Button type="submit" disabled={create.isPending || customerId === ''}>
                {create.isPending ? 'Saving…' : 'Create bill'}
              </Button>
              <p className="text-xs text-muted-foreground">
                Stock is taken off when the bill is saved. Short stock warns but never blocks.
              </p>
            </CardContent>
          </Card>
        </div>
      </form>
    </>
  )
}
