import { api } from '@/lib/api'
import type {
  Category,
  PackingEntry,
  PackingResponse,
  Product,
  ProductKind,
  SaveProduct,
  StockEntryResponse,
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
}

export const stockApi = {
  onHand: (filters: { categoryId?: string; lowStockOnly?: boolean; includeInactive?: boolean } = {}) =>
    api.get<StockOnHand[]>(`${base}/stock${query(filters)}`),
  movements: (productId: string) => api.get<StockMovement[]>(`${base}/stock/${productId}/movements`),
  addEntry: (entry: StockEntry) => api.post<StockEntryResponse>(`${base}/stock/entries`, entry),
  adjust: (adjustment: { productId: string; countedQuantity: number; notes: string }) =>
    api.post<StockEntryResponse>(`${base}/stock/adjustments`, adjustment),
}

export const packingApi = {
  history: () => api.get<PackingEntry[]>(`${base}/packing`),
  create: (request: {
    packedProductId: string
    packsProduced: number
    sourceQuantityUsed?: number
    notes?: string
  }) => api.post<PackingResponse>(`${base}/packing`, request),
}
