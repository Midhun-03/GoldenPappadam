import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, type ReactNode } from 'react'
import { authApi } from '@/api/auth'
import type { CurrentUser } from '@/api/types'
import { AuthContext } from './auth-context'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()

  // A 401 here simply means nobody is signed in, so it must not be retried.
  const { data, isLoading } = useQuery({
    queryKey: ['me'],
    queryFn: authApi.me,
    retry: false,
    staleTime: Infinity,
  })

  const value = useMemo(
    () => ({
      user: data ?? null,
      isLoading,
      login: async (email: string, password: string) => {
        const user = await authApi.login(email, password)
        queryClient.setQueryData<CurrentUser>(['me'], user)
      },
      logout: async () => {
        await authApi.logout()
        queryClient.clear()
        queryClient.setQueryData<CurrentUser | undefined>(['me'], undefined)
      },
    }),
    [data, isLoading, queryClient],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
