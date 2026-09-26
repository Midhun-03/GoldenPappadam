import { api } from '@/lib/api'

const base = '/api/sales/returns'

/** Pending means "office to decide": recorded, nothing given yet. */
export type ReturnSettlement = 'Pending' | 'Replacement' | 'Credit' | 'NoCompensation'
export type ReturnReason = 'Expired' | 'Damaged'
export type ReturnStatus = 'Recorded' | 'Cancelled'

export type ReturnListItem = {
  id: string
  returnNumber: string
  returnDate: string
  customerId: string
  customerName: string
  branchName: string | null
  status: ReturnStatus
  settlement: ReturnSettlement
  value: number
  creditAmount: number
}

export type ReturnLine = {
  id: string
  lineNumber: number
  productId: string
  description: string
  unitCode: string
  quantity: number
  reason: ReturnReason
  unitRate: number
  value: number
}

export type ReturnDetail = {
  id: string
  returnNumber: string
  returnDate: string
  customerId: string
  customerName: string
  branchId: string | null
  branchName: string | null
  status: ReturnStatus
  settlement: ReturnSettlement
  value: number
  creditAmount: number
  /** Where a replacement's fresh packets came from. */
  replacementFrom: string | null
  notes: string | null
  recordedAt: string
  recordedByName: string | null
  settledAt: string | null
  cancelledAt: string | null
  cancelledByName: string | null
  cancellationReason: string | null
  lines: ReturnLine[]
}

export type ReturnResponse = { return: ReturnDetail; warnings: string[] }

/** How a return is settled. creditAmount defaults to the packets' value; location to the warehouse. */
export type SettleReturn = {
  settlement: ReturnSettlement
  creditAmount?: number
  replacementLocationId?: string
}

export type CreateReturn = SettleReturn & {
  customerId: string
  branchId?: string
  returnDate?: string
  notes?: string
  /** unitRate left out values the packets at the shop's agreed rate. */
  lines: { productId: string; quantity: number; reason: ReturnReason; unitRate?: number }[]
}

export type ReturnFilters = { customerId?: string; from?: string; to?: string; pendingOnly?: boolean }

function query(filters: ReturnFilters) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (value) search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

export const returnsApi = {
  list: (filters: ReturnFilters = {}) => api.get<ReturnListItem[]>(`${base}${query(filters)}`),
  get: (id: string) => api.get<ReturnDetail>(`${base}/${id}`),
  create: (request: CreateReturn) => api.post<ReturnResponse>(base, request),
  settle: (id: string, request: SettleReturn) => api.post<ReturnResponse>(`${base}/${id}/settle`, request),
  cancel: (id: string, reason: string) => api.post<ReturnDetail>(`${base}/${id}/cancel`, { reason }),
  pdf: (id: string) => api.blob(`${base}/${id}/pdf`),
}

export const settlementLabels: Record<ReturnSettlement, string> = {
  Pending: 'Office to decide',
  Replacement: 'Replaced free',
  Credit: 'Credit',
  NoCompensation: 'Nothing given',
}

/** The settlement as the form holds it: typed text, empty meaning "use the default". */
export type SettlementState = { settlement: ReturnSettlement; creditAmount: string; locationId: string }

/** The request fields for a settlement, leaving out what does not apply. */
export const settlementRequest = (state: SettlementState): SettleReturn => ({
  settlement: state.settlement,
  creditAmount:
    state.settlement === 'Credit' && state.creditAmount.trim() !== '' ? Number(state.creditAmount) : undefined,
  replacementLocationId: state.settlement === 'Replacement' && state.locationId ? state.locationId : undefined,
})
