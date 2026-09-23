export type ProductKind = 'Loose' | 'Packed'

/** How GST applies to a product. Null on a product means nobody has decided yet. */
export type TaxTreatment = 'Taxable' | 'NilRated' | 'Exempt' | 'NonGst'

export type StockMovementType =
  | 'Opening'
  | 'Production'
  | 'Packing'
  | 'Transfer'
  | 'Sale'
  | 'SaleReversal'
  | 'Damage'
  | 'Adjustment'

export type Role = 'Admin' | 'Salesperson'

export type UserAccount = {
  id: string
  email: string
  fullName: string
  role: Role
  isActive: boolean
}

export type CurrentUser = {
  id: string
  email: string
  fullName: string
  /** What to show, never what is allowed — the API decides that. */
  role: Role
}

/** What one shop pays for one product. `effectivePrice` is what a bill would actually charge. */
export type CustomerPrice = {
  productId: string
  productCode: string
  productName: string
  unitCode: string
  defaultPrice: number | null
  agreedPrice: number | null
  effectivePrice: number | null
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
  hsnCode: string | null
  taxTreatment: TaxTreatment | null
  /** Full GST rate in percent; only for a taxable product. */
  gstRate: number | null
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
  hsnCode: string | null
  taxTreatment: TaxTreatment | null
  gstRate: number | null
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

export type StockLocationKind = 'Warehouse' | 'Van'

export type StockLocation = {
  id: string
  code: string
  name: string
  kind: StockLocationKind
  isActive: boolean
}

/** One product's stock in one place. */
export type LocationStock = {
  locationId: string
  code: string
  name: string
  kind: StockLocationKind
  quantityOnHand: number
}

export type StockMovement = {
  id: string
  occurredAt: string
  movementType: StockMovementType
  quantity: number
  runningBalance: number
  locationId: string
  locationCode: string
  referenceType: string | null
  referenceId: string | null
  notes: string | null
}

export type StockEntryResponse = {
  movementId: string
  productId: string
  locationId: string
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
  /** True for a parent company with several physical shops, e.g. Danya Supermarket. */
  hasMultipleBranches: boolean
  activeBranchCount: number
  createdByName: string | null
  /** A salesperson found this shop and added it from the phone. */
  addedBySalesperson: boolean
  email: string | null
  gstin: string | null
  /** GST state code, "32" for Kerala. */
  stateCode: string | null
}

/**
 * One rate change. `previousPrice` null means the shop was on the standard price before;
 * `newPrice` null means the agreed rate was removed.
 */
export type CustomerPriceChange = {
  id: string
  customerId: string
  customerName: string
  productId: string
  productName: string
  unitCode: string
  previousPrice: number | null
  newPrice: number | null
  changedAt: string
  changedBy: string | null
  changedBySalesperson: boolean
}

export type SaveCustomer = {
  name: string
  contactPerson: string | null
  phone: string | null
  address: string | null
  openingBalance: number
  notes: string | null
  hasMultipleBranches: boolean
  email: string | null
  gstin: string | null
  stateCode: string | null
}

/** One physical shop under a multi-branch customer, e.g. "Kundara" under Danya Supermarket. */
export type CustomerBranch = {
  id: string
  customerId: string
  name: string
  location: string | null
  address: string | null
  phone: string | null
  contactPerson: string | null
  isActive: boolean
  gstin: string | null
  stateCode: string | null
}

export type SaveCustomerBranch = {
  name: string
  location: string | null
  address: string | null
  phone: string | null
  contactPerson: string | null
  gstin: string | null
  stateCode: string | null
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

/** What the printed document is called: decided by GST law, not chosen. */
export type InvoiceDocumentType = 'Invoice' | 'TaxInvoice' | 'BillOfSupply'

export type InvoiceEmailStatus = 'Sent' | 'Failed'

export type InvoiceLine = {
  id: string
  productId: string
  description: string
  unitCode: string
  hsnCode: string | null
  quantity: number
  unitPrice: number
  /** Quantity × rate, before discount. */
  lineTotal: number
  discountAmount: number
  taxTreatment: TaxTreatment | null
  gstRate: number
  taxableValue: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  cessAmount: number
  /** What the line adds to the grand total. */
  amount: number
}

export type InvoiceListItem = {
  id: string
  invoiceNumber: string
  customerId: string
  customerName: string
  branchId: string | null
  branchName: string | null
  invoiceDate: string
  status: InvoiceStatus
  totalAmount: number
  amountPaid: number
  outstanding: number
  documentType: InvoiceDocumentType
  hasPdf: boolean
  lastEmailStatus: InvoiceEmailStatus | null
}

/** A supplier, customer or branch exactly as printed on the invoice. */
export type InvoiceParty = {
  name: string
  address: string | null
  phone: string | null
  gstin: string | null
  stateCode: string | null
}

export type InvoiceDocumentInfo = {
  fileName: string
  sizeBytes: number
  sha256: string
  generatedAt: string
}

export type InvoiceTotals = {
  subTotal: number
  discountAmount: number
  taxableAmount: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  cessAmount: number
  roundOff: number
  totalAmount: number
}

export type InvoiceDetail = InvoiceListItem &
  InvoiceTotals & {
    seriesCode: string
    financialYear: string
    supplier: InvoiceParty
    customer: InvoiceParty
    branch: InvoiceParty | null
    placeOfSupplyStateCode: string | null
    isInterState: boolean
    reverseCharge: boolean
    pricesIncludeTax: boolean
    notes: string | null
    finalizedAt: string
    finalizedByName: string | null
    /** The phone a synced sale came from. */
    recordedOnDevice: string | null
    cancelledAt: string | null
    cancelledByName: string | null
    cancellationReason: string | null
    /** Null until the PDF has been made. */
    document: InvoiceDocumentInfo | null
    customerEmail: string | null
    lines: InvoiceLine[]
  }

/** What a bill would come to, worked out by the server exactly as it will finalize it. */
export type InvoicePreview = InvoiceTotals & {
  documentType: InvoiceDocumentType
  placeOfSupplyStateCode: string | null
  isInterState: boolean
  pricesIncludeTax: boolean
  lines: InvoiceLine[]
}

export type InvoiceEmailLog = {
  id: string
  attemptedAt: string
  recipient: string
  subject: string
  status: InvoiceEmailStatus
  errorMessage: string | null
  attemptNumber: number
  sentByName: string | null
}

export type InvoiceSettings = {
  legalName: string
  address: string | null
  phone: string | null
  email: string | null
  /** Null means GST is off: bills are plain invoices with no tax. */
  gstin: string | null
  stateCode: string
  seriesCode: string
  pricesIncludeTax: boolean
  roundToNearestRupee: boolean
  paymentTerms: string | null
  bankDetails: string | null
  termsAndConditions: string | null
  gstEnabled: boolean
  /** What the next bill dated today would be called. Shown, never reserved. */
  nextInvoiceNumber: string
  updatedAt: string | null
}

export type SaveInvoiceSettings = Omit<InvoiceSettings, 'gstEnabled' | 'nextInvoiceNumber' | 'updatedAt'>

export type IndianState = { code: string; name: string }

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

export type VanLoadDirection = 'Loading' | 'Return'

export type VanLoadLine = {
  productId: string
  productName: string
  unitCode: string
  quantity: number
}

export type VanLoad = {
  id: string
  vanLocationId: string
  vanCode: string
  direction: VanLoadDirection
  /** The phone that recorded it. Null means the office did. */
  deviceName: string | null
  occurredAt: string
  businessDate: string
  notes: string | null
  lines: VanLoadLine[]
}

export type SaveVanLoad = {
  vanLocationId: string
  direction: VanLoadDirection
  occurredAt?: string
  notes?: string
  lines: { productId: string; quantity: number }[]
}

export type CreateVanLoadResponse = {
  vanLoad: VanLoad
  warnings: string[]
}

/**
 * One product's day on the van. `unaccounted` is what the van is still holding after the evening
 * return: it should be zero, and when it is not, a person decides why rather than the system
 * quietly adjusting it away.
 */
export type VanReconciliationLine = {
  productId: string
  productName: string
  unitCode: string
  opening: number
  loaded: number
  sold: number
  returned: number
  other: number
  unaccounted: number
}

export type VanReconciliation = {
  vanLocationId: string
  vanCode: string
  businessDate: string
  isSettled: boolean
  lines: VanReconciliationLine[]
}

export type VisitOutcome = 'Sold' | 'NoOrder' | 'Closed' | 'Skipped'

/** `recordedAt` is when the phone saved it; `receivedAt` is when the server heard about it. */
export type FieldSaleRow = {
  invoiceId: string
  invoiceNumber: string
  customerId: string
  customerName: string
  products: string
  totalAmount: number
  amountPaid: number
  salesperson: string
  deviceName: string
  recordedAt: string
  receivedAt: string
  priceMismatch: boolean
  status: InvoiceStatus
}

export type FieldVisitRow = {
  customerId: string
  customerName: string
  outcome: VisitOutcome
  visitedAt: string
  salesperson: string
  notes: string | null
}

export type FieldSalesDay = {
  businessDate: string
  totalSales: number
  saleCount: number
  shopsVisited: number
  cashCollected: number
  creditSales: number
  outstandingCreatedToday: number
  priceMismatchCount: number
  slowestSyncMinutes: number
  sales: FieldSaleRow[]
  visits: FieldVisitRow[]
}

export type StockRequestStatus = 'Requested' | 'Fulfilled' | 'Cancelled'

export type StockRequestLine = {
  productId: string
  productName: string
  unitCode: string
  quantity: number
}

/** What a salesperson has asked the packing unit to pack, and when they need it. */
export type StockRequest = {
  id: string
  requiredDate: string
  status: StockRequestStatus
  requestedBy: string
  deviceName: string | null
  createdAt: string
  notes: string | null
  lines: StockRequestLine[]
}

/** The same requests added up: one figure per product per day, for the packing table. */
export type PackingNeed = {
  requiredDate: string
  productId: string
  productName: string
  unitCode: string
  quantity: number
  requestCount: number
}

/**
 * A phone the sales team carries. `locationId` is the van it rides in — until the office sets it,
 * that phone cannot record a load at all, because the server reads the van off the device rather
 * than trusting what the phone sends.
 */
export type FieldDevice = {
  id: string
  name: string
  platform: string
  salesperson: string
  locationId: string | null
  vanName: string | null
  lastSeenAt: string
  isActive: boolean
}
