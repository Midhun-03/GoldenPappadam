import { api } from '@/lib/api'
import type {
  CreateVanLoadResponse,
  FieldSalesDay,
  PackingNeed,
  StockRequest,
  StockRequestStatus,
  SaveVanLoad,
  VanLoad,
  VanLoadLine,
  VanReconciliation,
} from './types'

const base = '/api/fieldsales'

function query(params: Record<string, string | boolean | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '' && value !== false) search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

export const vanLoadsApi = {
  list: (filters: { vanLocationId?: string; from?: string; to?: string } = {}) =>
    api.get<VanLoad[]>(`${base}/van-loads${query(filters)}`),
  create: (load: SaveVanLoad) => api.post<CreateVanLoadResponse>(`${base}/van-loads`, load),
  /** What the van is holding right now — used to fill in the evening return. */
  onVan: (vanLocationId: string) =>
    api.get<VanLoadLine[]>(`${base}/van-loads/on-van/${vanLocationId}`),
  reconciliation: (vanLocationId: string, businessDate: string) =>
    api.get<VanReconciliation>(
      `${base}/van-loads/reconciliation${query({ vanLocationId, businessDate })}`,
    ),
}

export const fieldSalesApi = {
  day: (businessDate: string) => api.get<FieldSalesDay>(`${base}/day${query({ businessDate })}`),
}

/** Admin-only on the server: the salesperson raises requests, the office answers them. */
export const stockRequestsApi = {
  list: (filters: { from?: string; to?: string; status?: StockRequestStatus } = {}) =>
    api.get<StockRequest[]>(`${base}/stock-requests${query(filters)}`),
  packingNeeds: (filters: { from?: string; to?: string } = {}) =>
    api.get<PackingNeed[]>(`${base}/stock-requests/packing-needs${query(filters)}`),
  setStatus: (id: string, status: StockRequestStatus) =>
    api.post<StockRequest>(`${base}/stock-requests/${id}/status${query({ status })}`),
}
