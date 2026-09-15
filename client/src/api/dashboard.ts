import { api } from '@/lib/api'
import type { DashboardSummary, ProductSales } from './types'

export const dashboardApi = {
  summary: () => api.get<DashboardSummary>('/api/dashboard/summary'),
  /** Plain business dates, "2026-09-14", inclusive at both ends. */
  productSales: (from: string, to: string) =>
    api.get<ProductSales[]>(`/api/dashboard/product-sales?from=${from}&to=${to}`),
}
