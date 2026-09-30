import { api } from '@/lib/api'
import type {
  Category,
  PackingEntry,
  PackingPlan,
  PackingResponse,
  Product,
  RepackEntry,
  RepackPlan,
  StockAgeRow,
  ProductKind,
  SaveProduct,
  StockEntryResponse,
  StockLocation,
  StockMovement,
  StockMovementType,
  StockOnHand,
  Unit,
} from './types'

const base = '/api/inventory'

function query(params: Record<string, string | boolean | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '' && value !== false) search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

export const categoriesApi = {
  list: (includeInactive = false) => api.get<Category[]>(`${base}/categories${query({ includeInactive })}`),
  create: (name: string) => api.post<Category>(`${base}/categories`, { name }),
  rename: (id: string, name: string) => api.put<Category>(`${base}/categories/${id}`, { name }),
  setActive: (id: string, isActive: boolean) =>
    api.post<Category>(`${base}/categories/${id}/active${query({ isActive: String(isActive) })}`),
}

export const unitsApi = {
  list: (includeInactive = false) => api.get<Unit[]>(`${base}/units${query({ includeInactive })}`),
  create: (code: string, name: string) => api.post<Unit>(`${base}/units`, { code, name }),
  update: (id: string, code: string, name: string) => api.put<Unit>(`${base}/units/${id}`, { code, name }),
  setActive: (id: string, isActive: boolean) =>
    api.post<Unit>(`${base}/units/${id}/active${query({ isActive: String(isActive) })}`),
}

export type ProductFilters = {
  categoryId?: string
  kind?: ProductKind
  search?: string
  includeInactive?: boolean
}

export const productsApi = {
  list: (filters: ProductFilters = {}) => api.get<Product[]>(`${base}/products${query(filters)}`),
  get: (id: string) => api.get<Product>(`${base}/products/${id}`),
  create: (product: SaveProduct) => api.post<Product>(`${base}/products`, product),
  update: (id: string, product: SaveProduct) => api.put<Product>(`${base}/products/${id}`, product),
  setActive: (id: string, isActive: boolean) =>
    api.post<Product>(`${base}/products/${id}/active${query({ isActive: String(isActive) })}`),
}

export type StockEntry = {
  productId: string
  movementType: Extract<StockMovementType, 'Opening' | 'Production' | 'Damage'>
  quantity: number
  occurredAt?: string
  notes?: string
  /** Left out means the main warehouse, which is where all of this used to happen. */
  locationId?: string
}

export const stockApi = {
  /** Defaults to the main warehouse; pass a location for the van, or allLocations for the total. */
  onHand: (
    filters: {
      categoryId?: string
      lowStockOnly?: boolean
      includeInactive?: boolean
      locationId?: string
      allLocations?: boolean
    } = {},
  ) => api.get<StockOnHand[]>(`${base}/stock${query(filters)}`),
  movements: (productId: string) => api.get<StockMovement[]>(`${base}/stock/${productId}/movements`),
  locations: () => api.get<StockLocation[]>(`${base}/stock/locations`),
  addEntry: (entry: StockEntry) => api.post<StockEntryResponse>(`${base}/stock/entries`, entry),
  adjust: (adjustment: {
    productId: string
    countedQuantity: number
    notes: string
    locationId?: string
  }) => api.post<StockEntryResponse>(`${base}/stock/adjustments`, adjustment),
}

export const packingApi = {
  history: () => api.get<PackingEntry[]>(`${base}/packing`),
  /** What the loose loses is worked out by the server, never sent. */
  create: (request: { packedProductId: string; packsProduced: number; notes?: string; clientRequestId: string }) =>
    api.post<PackingResponse>(`${base}/packing`, request),
  preview: (request: { packedProductId: string; packsProduced: number }) =>
    api.post<PackingPlan>(`${base}/packing/preview`, request),
}

export type RepackRequest = { fromProductId: string; fromQuantity: number; toProductId: string; notes?: string }

export const stockAgeApi = {
  list: () => api.get<StockAgeRow[]>(`${base}/stock/age`),
  /** Records what has expired in one place as damage. */
  writeOff: (productId: string, locationId: string) =>
    api.post<StockEntryResponse>(`${base}/stock/age/write-off`, { productId, locationId }),
}

export const repackingApi = {
  /** What a repack would make, worked out by the server; nothing is saved. */
  preview: (request: RepackRequest) => api.post<RepackPlan>(`${base}/repacking/preview`, request),
  create: (request: RepackRequest) =>
    api.post<{ repackEntryId: string; result: RepackPlan }>(`${base}/repacking`, request),
  history: () => api.get<RepackEntry[]>(`${base}/repacking`),
}
