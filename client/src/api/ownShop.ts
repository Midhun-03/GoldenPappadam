import { api } from '@/lib/api'
import type { PaymentMethod } from './types'

const base = '/api/own-shop'

function query(params: Record<string, string | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, value)
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

/**
 * One variety as the own shop holds it. `defaultRate` is the rate per piece and the most a piece sells
 * for; `minimumRate` the lowest allowed; `rate` is where a sale starts - the customer's agreed rate
 * when one was asked for.
 */
export type ShopStock = {
  productId: string
  productCode: string
  name: string
  sourceProductId: string
  sourceProductName: string
  piecesPerKg: number | null
  shopPieces: number
  factoryKg: number
  defaultRate: number | null
  minimumRate: number | null
  rate: number | null
  lowStockThreshold: number | null
  isLowStock: boolean
  isActive: boolean
}

export type ShopTransferPlan = {
  sourceProductId: string
  sourceProductName: string
  piecesProductId: string
  piecesProductName: string
  quantityKg: number
  piecesPerKg: number
  pieces: number
  factoryOnHandBefore: number
  factoryOnHandAfter: number
  shopOnHandBefore: number
  shopOnHandAfter: number
  /** Why it cannot be sent, or null when it can. */
  shortfall: string | null
}

export type ShopTransfer = {
  id: string
  occurredAt: string
  sourceProductId: string
  sourceProductName: string
  piecesProductId: string
  piecesProductName: string
  fromLocationName: string
  toLocationName: string
  quantityKg: number
  piecesPerKg: number
  piecesReceived: number
  sourceOnHandBefore: number
  sourceOnHandAfter: number
  shopOnHandBefore: number
  shopOnHandAfter: number
  createdByName: string | null
  notes: string | null
}

export type ShopSaleStatus = 'Completed' | 'Cancelled'

export type ShopSaleLine = {
  lineNumber: number
  productId: string
  description: string
  quantity: number
  unitPrice: number
  defaultPrice: number
  minimumPrice: number
  lineTotal: number
}

export type ShopSaleListItem = {
  id: string
  saleNumber: string
  saleDate: string
  createdAt: string
  customerId: string | null
  /** Null for a walk-in customer. */
  customerName: string | null
  paymentMethod: PaymentMethod
  pieces: number
  totalAmount: number
  status: ShopSaleStatus
  /** Some line was sold under the standard rate: a wholesale or catering price. */
  belowStandardRate: boolean
}

export type ShopSaleDetail = {
  id: string
  saleNumber: string
  saleDate: string
  createdAt: string
  locationName: string
  customerId: string | null
  customerName: string | null
  paymentMethod: PaymentMethod
  totalAmount: number
  status: ShopSaleStatus
  notes: string | null
  createdByName: string | null
  cancelledAt: string | null
  cancellationReason: string | null
  cancelledByName: string | null
  lines: ShopSaleLine[]
}

export type CreateShopSale = {
  customerId: string | null
  paymentMethod: PaymentMethod
  lines: { productId: string; quantity: number; unitPrice: number }[]
  notes?: string
  clientRequestId: string
}

export const shopStockApi = {
  /** With a customer, `rate` is their agreed rate per piece where they have one. */
  list: (customerId?: string) => api.get<ShopStock[]>(`${base}/stock${query({ customerId })}`),
}

export const shopTransfersApi = {
  history: (filters: { from?: string; to?: string } = {}) =>
    api.get<ShopTransfer[]>(`${base}/transfers${query(filters)}`),
  /** The pieces are worked out by the server from the variety's pieces per kg, never sent. */
  preview: (request: { sourceProductId: string; quantityKg: number }) =>
    api.post<ShopTransferPlan>(`${base}/transfers/preview`, request),
  create: (request: { sourceProductId: string; quantityKg: number; notes?: string; clientRequestId: string }) =>
    api.post<ShopTransfer>(`${base}/transfers`, request),
}

export const shopSalesApi = {
  list: (filters: { from?: string; to?: string; customerId?: string; productId?: string } = {}) =>
    api.get<ShopSaleListItem[]>(`${base}/sales${query(filters)}`),
  get: (id: string) => api.get<ShopSaleDetail>(`${base}/sales/${id}`),
  create: (sale: CreateShopSale) => api.post<ShopSaleDetail>(`${base}/sales`, sale),
  cancel: (id: string, reason: string) => api.post<ShopSaleDetail>(`${base}/sales/${id}/cancel`, { reason }),
}
