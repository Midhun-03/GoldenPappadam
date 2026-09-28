import { api } from '@/lib/api'
import type {
  AttendanceSheet,
  AttendanceStatus,
  Employee,
  PayWages,
  SaveAttendanceResponse,
  SaveEmployee,
  WagePayment,
  WageRate,
  WageWeek,
} from './types'

const base = '/api/staff'

function query(params: Record<string, string | boolean | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '' && value !== false) search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

export const employeesApi = {
  list: (filters: { search?: string; includeInactive?: boolean } = {}) =>
    api.get<Employee[]>(`${base}/employees${query(filters)}`),
  get: (id: string) => api.get<Employee>(`${base}/employees/${id}`),
  create: (employee: SaveEmployee) => api.post<Employee>(`${base}/employees`, employee),
  update: (id: string, employee: SaveEmployee) => api.put<Employee>(`${base}/employees/${id}`, employee),
  setActive: (id: string, isActive: boolean) =>
    api.post<Employee>(`${base}/employees/${id}/active${query({ isActive: String(isActive) })}`),
  wageRates: (id: string) => api.get<WageRate[]>(`${base}/employees/${id}/wage-rates`),
  /** Adds to the history; the earlier rate is never overwritten. */
  addWageRate: (id: string, dailyWage: number, effectiveFrom: string) =>
    api.post<WageRate[]>(`${base}/employees/${id}/wage-rates`, { dailyWage, effectiveFrom }),
}

export const attendanceApi = {
  sheet: (date: string) => api.get<AttendanceSheet>(`${base}/attendance${query({ date })}`),
  save: (date: string, entries: { employeeId: string; statusId: string | null }[]) =>
    api.put<SaveAttendanceResponse>(`${base}/attendance`, { date, entries }),
  statuses: () => api.get<AttendanceStatus[]>(`${base}/attendance-statuses`),
  updateStatus: (id: string, dayFraction: number) =>
    api.put<AttendanceStatus>(`${base}/attendance-statuses/${id}`, { dayFraction }),
}

export const wagesApi = {
  week: (date: string) => api.get<WageWeek>(`${base}/wages/week${query({ date })}`),
  payments: (filters: { employeeId?: string; from?: string; to?: string } = {}) =>
    api.get<WagePayment[]>(`${base}/wage-payments${query(filters)}`),
  payment: (id: string) => api.get<WagePayment>(`${base}/wage-payments/${id}`),
  pay: (request: PayWages) => api.post<{ payments: WagePayment[]; total: number }>(`${base}/wage-payments`, request),
  cancel: (id: string, reason: string) => api.post<WagePayment>(`${base}/wage-payments/${id}/cancel`, { reason }),
}
