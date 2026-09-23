import { api } from '@/lib/api'
import type {
  CreateInvoiceResponse,
  CreatePaymentResponse,
  Customer,
  CustomerBranch,
  CustomerPrice,
  CustomerPriceChange,
  IndianState,
  InvoiceDetail,
  InvoiceEmailLog,
  InvoiceEmailStatus,
  InvoiceListItem,
  InvoicePreview,
  InvoiceSettings,
  InvoiceStatus,
  LedgerEntry,
  OutstandingInvoice,
  Payment,
  PaymentMethod,
  SaveCustomer,
  SaveCustomerBranch,
  SaveInvoiceSettings,
} from './types'

const base = '/api/sales'

function query(params: Record<string, string | boolean | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '' && value !== false) search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

export const customersApi = {
  list: (
    filters: { search?: string; includeInactive?: boolean; withBalanceOnly?: boolean; addedBySales?: boolean } = {},
  ) =>
    api.get<Customer[]>(`${base}/customers${query(filters)}`),
  get: (id: string) => api.get<Customer>(`${base}/customers/${id}`),
  create: (customer: SaveCustomer) => api.post<Customer>(`${base}/customers`, customer),
  update: (id: string, customer: SaveCustomer) => api.put<Customer>(`${base}/customers/${id}`, customer),
  setActive: (id: string, isActive: boolean) =>
    api.post<Customer>(`${base}/customers/${id}/active${query({ isActive: String(isActive) })}`),
  ledger: (id: string) => api.get<LedgerEntry[]>(`${base}/customers/${id}/ledger`),
  outstandingInvoices: (id: string) =>
    api.get<OutstandingInvoice[]>(`${base}/customers/${id}/outstanding-invoices`),
}

export const customerBranchesApi = {
  list: (customerId: string, filters: { includeInactive?: boolean } = {}) =>
    api.get<CustomerBranch[]>(`${base}/customers/${customerId}/branches${query(filters)}`),
  create: (customerId: string, branch: SaveCustomerBranch) =>
    api.post<CustomerBranch>(`${base}/customers/${customerId}/branches`, branch),
  update: (customerId: string, branchId: string, branch: SaveCustomerBranch) =>
    api.put<CustomerBranch>(`${base}/customers/${customerId}/branches/${branchId}`, branch),
  setActive: (customerId: string, branchId: string, isActive: boolean) =>
    api.post<CustomerBranch>(
      `${base}/customers/${customerId}/branches/${branchId}/active${query({ isActive: String(isActive) })}`,
    ),
}

/** Prices are admin-only on the server; the salesperson's app can only read its own snapshot. */
export const customerPricesApi = {
  list: (customerId: string, filters: { agreedOnly?: boolean } = {}) =>
    api.get<CustomerPrice[]>(`${base}/customers/${customerId}/prices${query(filters)}`),
  set: (customerId: string, productId: string, unitPrice: number) =>
    api.put<CustomerPrice>(`${base}/customers/${customerId}/prices/${productId}`, { unitPrice }),
  remove: (customerId: string, productId: string) =>
    api.del<void>(`${base}/customers/${customerId}/prices/${productId}`),
}

/** Every rate change, by the office or the sales team. Dates are IST business days. */
export const customerPriceChangesApi = {
  list: (filters: { customerId?: string; salespersonOnly?: boolean; from?: string; to?: string } = {}) =>
    api.get<CustomerPriceChange[]>(`${base}/customer-price-changes${query(filters)}`),
}

export type InvoiceFilters = {
  customerId?: string
  from?: string
  to?: string
  status?: InvoiceStatus
  unpaidOnly?: boolean
  /** Matches the invoice number, customer or branch as printed. */
  search?: string
  /** Failed finds invoices whose last email did not go. */
  emailStatus?: InvoiceEmailStatus
}

export type CreateInvoice = {
  customerId: string
  branchId?: string
  invoiceDate?: string
  discountAmount: number
  notes?: string
  lines: { productId: string; quantity: number; unitPrice?: number }[]
}

export const invoicesApi = {
  list: (filters: InvoiceFilters = {}) => api.get<InvoiceListItem[]>(`${base}/invoices${query(filters)}`),
  get: (id: string) => api.get<InvoiceDetail>(`${base}/invoices/${id}`),
  create: (invoice: CreateInvoice) => api.post<CreateInvoiceResponse>(`${base}/invoices`, invoice),
  /** The server's own calculation of a bill that has not been saved: agreed rates, tax, round-off. */
  preview: (invoice: CreateInvoice) => api.post<InvoicePreview>(`${base}/invoices/preview`, invoice),
  cancel: (id: string, reason: string) => api.post<InvoiceDetail>(`${base}/invoices/${id}/cancel`, { reason }),
  /** The stored PDF - the same file every time it is asked for. */
  pdf: (id: string) => api.blob(`${base}/invoices/${id}/pdf`),
  generatePdf: (id: string) => api.post<InvoiceDetail>(`${base}/invoices/${id}/pdf`),
  /** Leave the recipient empty to use the customer's address. A failed send comes back as a Failed attempt. */
  email: (id: string, recipient?: string) =>
    api.post<InvoiceEmailLog>(`${base}/invoices/${id}/email`, { recipient: recipient || null }),
  emails: (id: string) => api.get<InvoiceEmailLog[]>(`${base}/invoices/${id}/emails`),
}

export const invoiceSettingsApi = {
  get: () => api.get<InvoiceSettings>(`${base}/invoice-settings`),
  save: (settings: SaveInvoiceSettings) => api.put<InvoiceSettings>(`${base}/invoice-settings`, settings),
  states: () => api.get<IndianState[]>(`${base}/states`),
}

export type CreatePayment = {
  customerId: string
  paymentDate?: string
  amount: number
  method: PaymentMethod
  reference?: string
  notes?: string
  /** Leave empty to settle the oldest bills first. */
  allocations?: { invoiceId: string; amount: number }[]
}

export const paymentsApi = {
  list: (filters: { customerId?: string; from?: string; to?: string } = {}) =>
    api.get<Payment[]>(`${base}/payments${query(filters)}`),
  create: (payment: CreatePayment) => api.post<CreatePaymentResponse>(`${base}/payments`, payment),
}
