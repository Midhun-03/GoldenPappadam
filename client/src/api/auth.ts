import { api } from '@/lib/api'
import type { CurrentUser, Role, UserAccount } from './types'

export const authApi = {
  me: () => api.get<CurrentUser>('/api/auth/me'),
  login: (email: string, password: string) =>
    api.post<CurrentUser>('/api/auth/login', { email, password, rememberMe: true }),
  logout: () => api.post<void>('/api/auth/logout'),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<void>('/api/auth/change-password', { currentPassword, newPassword }),
}

/** Admin-only on the server; the salesperson's token gets 403 on every one of these. */
export const usersApi = {
  list: () => api.get<UserAccount[]>('/api/admin/users'),
  create: (user: { email: string; fullName: string; password: string; role: Role }) =>
    api.post<UserAccount>('/api/admin/users', user),
  setActive: (id: string, isActive: boolean) =>
    api.post<UserAccount>(`/api/admin/users/${id}/active?isActive=${isActive}`),
}
