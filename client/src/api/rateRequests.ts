import { api } from '@/lib/api'
import { formatMoney } from '@/lib/format'

export type RateRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'

/**
 * A salesperson asking for a shop's rate to change. `currentPrice` is the shop's agreed rate now (null
 * = the standard price, `standardPrice`); it may differ from `priceWhenRequested` if it moved since.
 */
export type RateRequest = {
  id: string
  customerId: string
  customerName: string
  productId: string
  productName: string
  unitCode: string
  priceWhenRequested: number | null
  currentPrice: number | null
  standardPrice: number | null
  requestedPrice: number
  reason: string | null
  requestedAt: string
  requestedBy: string | null
  status: RateRequestStatus
  decidedAt: string | null
  decidedBy: string | null
  decisionNote: string | null
}

const base = '/api/sales/rate-requests'

function query(filters: { status?: RateRequestStatus; customerId?: string }) {
  const search = new URLSearchParams()
  if (filters.status) search.set('status', filters.status)
  if (filters.customerId) search.set('customerId', filters.customerId)
  const text = search.toString()
  return text ? `?${text}` : ''
}

export const rateRequestsApi = {
  list: (filters: { status?: RateRequestStatus; customerId?: string } = {}) =>
    api.get<RateRequest[]>(`${base}${query(filters)}`),
  approve: (id: string, note?: string) => api.post<RateRequest>(`${base}/${id}/approve`, { note: note || null }),
  reject: (id: string, note?: string) => api.post<RateRequest>(`${base}/${id}/reject`, { note: note || null }),
}

/** "₹35.00", or "₹45.00 (standard)" when the shop has no agreed rate. */
export function describeRate(price: number | null, standard: number | null) {
  return price === null ? `${formatMoney(standard)} (standard)` : formatMoney(price)
}
