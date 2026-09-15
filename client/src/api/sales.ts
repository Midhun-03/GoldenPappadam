import { api } from '@/lib/api'
import type {
  CreateInvoiceResponse,
  CreatePaymentResponse,
  Customer,
  CustomerPrice,
  InvoiceDetail,
  InvoiceListItem,
  InvoiceStatus,
  LedgerEntry,
  OutstandingInvoice,
  Payment,
  PaymentMethod,
  SaveCustomer,
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
  list: (filters: { search?: string; includeInactive?: boolean; withBalanceOnly?: boolean } = {}) =>
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

/** Prices are admin-only on the server; the salesperson's app can only read its own snapshot. */
export const customerPricesApi = {
  list: (customerId: string, filters: { agreedOnly?: boolean } = {}) =>
    api.get<CustomerPrice[]>(`${base}/customers/${customerId}/prices${query(filters)}`),
  set: (customerId: string, productId: string, unitPrice: number) =>
    api.put<CustomerPrice>(`${base}/customers/${customerId}/prices/${productId}`, { unitPrice }),
  remove: (customerId: string, productId: string) =>
    api.del<void>(`${base}/customers/${customerId}/prices/${productId}`),
}

export type InvoiceFilters = {
  customerId?: string
  from?: string
  to?: string
  status?: InvoiceStatus
  unpaidOnly?: boolean
}

export type CreateInvoice = {
  customerId: string
  invoiceDate?: string
  discountAmount: number
  notes?: string
  lines: { productId: string; quantity: number; unitPrice?: number }[]
}

export const invoicesApi = {
  list: (filters: InvoiceFilters = {}) => api.get<InvoiceListItem[]>(`${base}/invoices${query(filters)}`),
  get: (id: string) => api.get<InvoiceDetail>(`${base}/invoices/${id}`),
  create: (invoice: CreateInvoice) => api.post<CreateInvoiceResponse>(`${base}/invoices`, invoice),
  cancel: (id: string, reason: string) => api.post<InvoiceDetail>(`${base}/invoices/${id}/cancel`, { reason }),
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
