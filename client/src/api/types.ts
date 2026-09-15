export type ProductKind = 'Loose' | 'Packed'

export type StockMovementType =
  | 'Opening'
  | 'Production'
  | 'Packing'
  | 'Sale'
  | 'SaleReversal'
  | 'Damage'
  | 'Adjustment'

export type CurrentUser = {
  id: string
  email: string
  fullName: string
}

export type Category = {
  id: string
  name: string
  isActive: boolean
}

export type Unit = {
  id: string
  code: string
  name: string
  isActive: boolean
}

export type Product = {
  id: string
  productCode: string
  name: string
  categoryId: string
  categoryName: string
  kind: ProductKind
  unitOfMeasureId: string
  unitCode: string
  sourceProductId: string | null
  sourceProductName: string | null
  sourceQuantityPerPack: number | null
  sellingPrice: number | null
  lowStockThreshold: number | null
  isActive: boolean
}

export type SaveProduct = {
  productCode: string
  name: string
  categoryId: string
  kind: ProductKind
  unitOfMeasureId: string
  sourceProductId: string | null
  sourceQuantityPerPack: number | null
  sellingPrice: number | null
  lowStockThreshold: number | null
}

export type StockOnHand = {
  productId: string
  productCode: string
  name: string
  kind: ProductKind
  unitCode: string
  quantityOnHand: number
  lowStockThreshold: number | null
  isLowStock: boolean
  isActive: boolean
}

export type StockMovement = {
  id: string
  occurredAt: string
  movementType: StockMovementType
  quantity: number
  runningBalance: number
  referenceType: string | null
  referenceId: string | null
  notes: string | null
}

export type StockEntryResponse = {
  movementId: string
  productId: string
  quantityOnHand: number
  warning: string | null
}

export type PackingResponse = {
  packingEntryId: string
  packedProductId: string
  packsProduced: number
  packedQuantityOnHand: number
  sourceProductId: string
  sourceQuantityUsed: number
  sourceQuantityOnHand: number
  warning: string | null
}

export type InvoiceStatus = 'Issued' | 'Cancelled'

export type PaymentMethod = 'Cash' | 'UPI' | 'BankTransfer' | 'Cheque' | 'Other'

export type Customer = {
  id: string
  name: string
  contactPerson: string | null
  phone: string | null
  address: string | null
  openingBalance: number
  /** What the shop owes right now: opening balance + bills − payments. */
  balance: number
  notes: string | null
  isActive: boolean
}

export type SaveCustomer = {
  name: string
  contactPerson: string | null
  phone: string | null
  address: string | null
  openingBalance: number
  notes: string | null
}

export type LedgerEntry = {
  /** A plain date, "2026-09-14". */
  date: string
  entryType: 'Opening' | 'Invoice' | 'Payment'
  reference: string
  description: string | null
  billed: number
  paid: number
  balance: number
}

export type OutstandingInvoice = {
  invoiceId: string
  invoiceNumber: string
  invoiceDate: string
  totalAmount: number
  amountPaid: number
  outstanding: number
}

export type InvoiceLine = {
  id: string
  productId: string
  description: string
  unitCode: string
  quantity: number
  unitPrice: number
  lineTotal: number
}

export type InvoiceListItem = {
  id: string
  invoiceNumber: string
  customerId: string
  customerName: string
  invoiceDate: string
  status: InvoiceStatus
  totalAmount: number
  amountPaid: number
  outstanding: number
}

export type InvoiceDetail = InvoiceListItem & {
  subTotal: number
  discountAmount: number
  notes: string | null
  cancelledAt: string | null
  cancellationReason: string | null
  lines: InvoiceLine[]
}

export type CreateInvoiceResponse = {
  invoice: InvoiceDetail
  warnings: string[]
}

export type PaymentAllocationView = {
  invoiceId: string
  invoiceNumber: string
  amount: number
}

export type Payment = {
  id: string
  customerId: string
  customerName: string
  paymentDate: string
  amount: number
  method: PaymentMethod
  reference: string | null
  notes: string | null
  allocatedAmount: number
  unallocatedAmount: number
  allocations: PaymentAllocationView[]
}

export type CreatePaymentResponse = {
  payment: Payment
  customerBalance: number
}

export type CustomerBalance = {
  customerId: string
  name: string
  balance: number
}

export type DashboardSummary = {
  today: string
  todaySales: number
  todayInvoiceCount: number
  monthSales: number
  monthInvoiceCount: number
  activeCustomers: number
  outstandingTotal: number
  lowStockCount: number
  recentInvoices: InvoiceListItem[]
  topOutstanding: CustomerBalance[]
  lowStockProducts: StockOnHand[]
}

export type PackingEntry = {
  id: string
  occurredAt: string
  packedProductId: string
  packedProductName: string
  packsProduced: number
  sourceProductId: string
  sourceProductName: string
  sourceQuantityUsed: number
  notes: string | null
}

/**
 * Sales for one product over a date range. `salesValue` is the sum of the line totals, so it
 * is gross of any bill-level discount and will not tie exactly to the sales headline figures.
 */
export type ProductSales = {
  productId: string
  productName: string
  unitCode: string
  categoryName: string
  quantitySold: number
  salesValue: number
}
