/** Pieces: the own shop's pappadam by the piece, one per loose variety, sold only over its counter. */
export type ProductKind = 'Loose' | 'Packed' | 'Pieces'

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
  | 'Repacking'
  | 'Replacement'
  | 'ShopTransfer'

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
  /** Days the product stays good from packing; null means it does not expire. */
  shelfLifeDays: number | null
  /** A count-based packet from loose kg: the pieces it holds. Null otherwise. */
  piecesPerPack: number | null
  /** A loose variety counted in kg: its average pieces per kg (200 for the standard pappadam). */
  piecesPerKg: number | null
  /** What one pack uses of its source, worked out by the server: 0.1 kg for 20 pieces at 200/kg. */
  sourcePerPack: number | null
  /** Own-shop pieces products only: the lowest rate per piece; null means no lower than sellingPrice. */
  minimumSellingPrice: number | null
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
  shelfLifeDays: number | null
  piecesPerPack: number | null
  piecesPerKg: number | null
  minimumSellingPrice: number | null
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

export type StockLocationKind = 'Warehouse' | 'Van' | 'Shop'

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
  plan: PackingPlan
}

/**
 * A packing worked out by the server - the same code that saves it. The pieces fields are set for a
 * count-based packet only. `shortfall` is why it cannot be packed, or null when it can.
 */
export type PackingPlan = {
  packedProductId: string
  packedProductName: string
  packsProduced: number
  packedUnitCode: string
  sourceProductId: string
  sourceProductName: string
  sourceUnitCode: string
  piecesPerPack: number | null
  piecesPerKg: number | null
  sourcePerPack: number
  sourceUsed: number
  piecesUsed: number | null
  sourceOnHandBefore: number
  sourceOnHandAfter: number
  shortfall: string | null
}

export type InvoiceStatus = 'Issued' | 'Cancelled'

/** ReturnCredit is never chosen: only a return note creates one. */
export type PaymentMethod = 'Cash' | 'UPI' | 'BankTransfer' | 'Cheque' | 'Other' | 'ReturnCredit'

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
  /** A GST customer gets GST bills; every other shop gets a normal bill. True exactly when it has a GSTIN. */
  isGstRegistered: boolean
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
  /** The salesperson whose request the office approved, when the change came from one. */
  requestedBy: string | null
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
  /** Ticked: the GSTIN is required. Unticked: the shop gets normal bills and has no GSTIN. */
  isGstRegistered: boolean
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
  entryType: 'Opening' | 'Invoice' | 'Payment' | 'Return credit'
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
  stockAge: StockAgeAlerts
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
  sourceUnitCode: string
  /** The conversion the entry used. Null on entries made before 30 Sep 2026, which never recorded it. */
  piecesPerPack: number | null
  piecesPerKg: number | null
  sourcePerPack: number | null
  sourceOnHandBefore: number | null
  sourceOnHandAfter: number | null
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
  /** Fresh packets handed to shops in place of expired or damaged ones. */
  replaced: number
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

/** One product in one place, split into age bands relative to its shelf life. */
export type StockAgeRow = {
  productId: string
  productCode: string
  productName: string
  unitCode: string
  locationId: string
  locationCode: string
  locationName: string
  shelfLifeDays: number
  fresh: number
  repackWindow: number
  ageing: number
  expiringSoon: number
  expired: number
  total: number
  /** Stock that expires within the next few days (the dashboard's "expiring soon"). */
  expiringWithinDays: number
  oldestPackedOn: string | null
  freshLabel: string
  repackLabel: string
  ageingLabel: string
  expiringLabel: string
  layers: { packedOn: string; ageDays: number; quantity: number }[]
}

export type StockAgeAlerts = { toRepack: number; expiringSoon: number; expired: number }

export type RepackPlan = {
  fromProductId: string
  fromProductName: string
  fromQuantity: number
  fromUnitCode: string
  toProductId: string
  toProductName: string
  toQuantity: number
  toUnitCode: string
  leftoverProductId: string | null
  leftoverProductName: string | null
  leftoverQuantity: number
  looseUnitCode: string
  fromOnHand: number
  warning: string | null
}

export type RepackEntry = {
  id: string
  occurredAt: string
  fromProductName: string
  fromQuantity: number
  toProductName: string
  toQuantity: number
  leftoverProductName: string | null
  leftoverQuantity: number
  notes: string | null
}

/** Someone paid a daily wage. Not a login account. */
export type Employee = {
  id: string
  name: string
  designation: string | null
  phone: string | null
  address: string | null
  joinedOn: string | null
  isActive: boolean
  /** The wage in effect today. */
  currentDailyWage: number | null
  currentWageFrom: string | null
  /** A raise already entered for a later date. */
  upcomingDailyWage: number | null
  upcomingWageFrom: string | null
  createdAt: string
}

/** Details only. The wage is given once on creation, then changed through the wage history. */
export type SaveEmployee = {
  name: string
  designation: string | null
  phone: string | null
  address: string | null
  joinedOn: string | null
  dailyWage?: number | null
  wageEffectiveFrom?: string | null
}

/** One entry in the wage history. `isSuperseded`: replaced by a later entry with the same start date. */
export type WageRate = {
  id: string
  dailyWage: number
  effectiveFrom: string
  setAt: string
  setByName: string | null
  isCurrent: boolean
  isUpcoming: boolean
  isSuperseded: boolean
}

export type AttendanceStatus = {
  id: string
  code: string
  name: string
  /** Share of a day's wage the status earns: 1, 0.5, 0. */
  dayFraction: number
  sortOrder: number
}

export type AttendanceRow = {
  employeeId: string
  name: string
  designation: string | null
  isActive: boolean
  /** Null: not recorded. */
  statusId: string | null
  /** The employee's week is already paid: a change is settled in their next unpaid week. */
  weekPaid: boolean
  changedAt: string | null
  changedByName: string | null
}

export type AttendanceSheet = {
  date: string
  weekStart: string
  weekEnd: string
  weekLabel: string
  isFuture: boolean
  statuses: AttendanceStatus[]
  rows: AttendanceRow[]
}

export type SaveAttendanceResponse = {
  sheet: AttendanceSheet
  changed: number
  warnings: string[]
}

export type WageStatus = 'Paid' | 'Pending' | 'NothingToPay'

export type WageDay = {
  date: string
  statusId: string | null
  statusName: string
  dayFraction: number
  dailyWage: number | null
  amount: number
}

export type WageAdjustment = {
  kind: 'Correction' | 'CarriedBalance'
  date: string
  description: string
  amount: number
}

export type EmployeeWage = {
  employeeId: string
  name: string
  designation: string | null
  isActive: boolean
  status: WageStatus
  /** The rates the worked days were paid at: two when a raise fell mid-week. */
  dailyWages: number[]
  days: WageDay[]
  statusCounts: { name: string; days: number }[]
  notRecordedDays: number
  daysWorked: number
  workAmount: number
  adjustments: WageAdjustment[]
  adjustmentAmount: number
  /** What to hand over; for a paid week, what was handed over. */
  payable: number
  /** An overpayment larger than this week's pay, carried on to the next week. */
  carriedForward: number
  missingWageDates: string[]
  payment: {
    id: string
    paymentDate: string
    method: PaymentMethod
    reference: string | null
    amount: number
    paidAt: string
    paidByName: string | null
  } | null
  /** Attendance corrected after this week was paid, to be settled in the next unpaid week. */
  changedSincePaid: WageAdjustment[]
  changedSincePaidAmount: number
}

export type WageWeek = {
  weekStart: string
  weekEnd: string
  label: string
  /** False until the week's Saturday. */
  canPay: boolean
  employees: EmployeeWage[]
  paidTotal: number
  pendingTotal: number
  paidCount: number
  pendingCount: number
}

export type WagePaymentStatus = 'Paid' | 'Cancelled'

export type WagePaymentLine = {
  lineType: 'Attendance' | 'Correction' | 'CarriedBalance'
  workDate: string
  statusName: string | null
  dayFraction: number
  previousStatusName: string | null
  previousDayFraction: number | null
  dailyWage: number
  amount: number
}

export type WagePayment = {
  id: string
  employeeId: string
  employeeName: string
  periodStart: string
  periodEnd: string
  periodLabel: string
  paymentDate: string
  method: PaymentMethod
  reference: string | null
  notes: string | null
  daysWorked: number
  workAmount: number
  adjustmentAmount: number
  amount: number
  carriedForward: number
  status: WagePaymentStatus
  paidAt: string
  paidByName: string | null
  cancelledAt: string | null
  cancelledByName: string | null
  cancellationReason: string | null
  expenseId: string | null
  lines: WagePaymentLine[]
}

export type PayWages = {
  weekStart: string
  paymentDate: string
  method: PaymentMethod
  reference: string | null
  notes: string | null
  /** `expectedAmount` is the figure the office was shown; the server refuses if it has changed since. */
  employees: { employeeId: string; expectedAmount: number }[]
}

/** `isSystem`: Employee wages, filled only by wage payments. */
export type ExpenseCategory = {
  id: string
  name: string
  isActive: boolean
  isSystem: boolean
}

export type ExpenseStatus = 'Recorded' | 'Cancelled'

export type Expense = {
  id: string
  categoryId: string
  categoryName: string
  expenseDate: string
  amount: number
  description: string | null
  paymentMethod: PaymentMethod | null
  reference: string | null
  status: ExpenseStatus
  /** Made by a wage payment: changed only by cancelling that payment. */
  isWageExpense: boolean
  wagePaymentId: string | null
  employeeId: string | null
  createdAt: string
  createdByName: string | null
  updatedAt: string | null
  updatedByName: string | null
  /** How many earlier versions edits and cancellation have kept. */
  versionCount: number
}

export type SaveExpense = {
  categoryId: string
  expenseDate: string
  amount: number
  description: string | null
  paymentMethod: PaymentMethod | null
  reference: string | null
  reason?: string | null
}

export type ExpenseVersion = {
  change: 'Recorded' | 'Edited' | 'Cancelled'
  categoryName: string
  expenseDate: string
  amount: number
  description: string | null
  paymentMethod: PaymentMethod | null
  reference: string | null
  changedAt: string
  changedByName: string | null
  reason: string | null
}

export type ExpenseSummary = {
  from: string | null
  to: string | null
  total: number
  count: number
  categories: { categoryId: string; name: string; total: number; count: number }[]
}
