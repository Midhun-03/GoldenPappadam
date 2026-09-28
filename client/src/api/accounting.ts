import { api } from '@/lib/api'
import type { Expense, ExpenseCategory, ExpenseSummary, ExpenseVersion, SaveExpense } from './types'

const base = '/api/accounting'

function query(params: Record<string, string | boolean | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '' && value !== false) search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

/** Dates are IST business days, both ends included. */
export type ExpenseFilters = {
  from?: string
  to?: string
  categoryId?: string
  search?: string
}

export const expensesApi = {
  list: (filters: ExpenseFilters & { includeCancelled?: boolean } = {}) =>
    api.get<Expense[]>(`${base}/expenses${query(filters)}`),
  /** Totals per category for the same filters, cancelled expenses left out. */
  summary: (filters: ExpenseFilters = {}) => api.get<ExpenseSummary>(`${base}/expenses/summary${query(filters)}`),
  history: (id: string) => api.get<ExpenseVersion[]>(`${base}/expenses/${id}/history`),
  create: (expense: SaveExpense) => api.post<Expense>(`${base}/expenses`, expense),
  update: (id: string, expense: SaveExpense) => api.put<Expense>(`${base}/expenses/${id}`, expense),
  cancel: (id: string, reason: string) => api.post<Expense>(`${base}/expenses/${id}/cancel`, { reason }),
}

export const expenseCategoriesApi = {
  list: (includeInactive = false) =>
    api.get<ExpenseCategory[]>(`${base}/expense-categories${query({ includeInactive })}`),
  create: (name: string) => api.post<ExpenseCategory>(`${base}/expense-categories`, { name }),
  rename: (id: string, name: string) => api.put<ExpenseCategory>(`${base}/expense-categories/${id}`, { name }),
  setActive: (id: string, isActive: boolean) =>
    api.post<ExpenseCategory>(`${base}/expense-categories/${id}/active${query({ isActive: String(isActive) })}`),
}
