import { api } from '@/lib/api'
import type {
  CreateVanLoadResponse,
  FieldSalesDay,
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
