import { api } from '@/lib/api'
import type { CurrentUser } from './types'

export const authApi = {
  me: () => api.get<CurrentUser>('/api/auth/me'),
  login: (email: string, password: string) =>
    api.post<CurrentUser>('/api/auth/login', { email, password, rememberMe: true }),
  logout: () => api.post<void>('/api/auth/logout'),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<void>('/api/auth/change-password', { currentPassword, newPassword }),
}
