import { useQuery } from '@tanstack/react-query'
import { invoiceSettingsApi } from '@/api/sales'

/** States and their GST codes come from the server, so there is one list, not one per screen. */
export function useIndianStates() {
  return useQuery({ queryKey: ['states'], queryFn: invoiceSettingsApi.states, staleTime: Infinity })
}

/** "Kerala (32)", or the bare code while the list loads. */
export function useStateName() {
  const states = useIndianStates()

  return (code: string | null | undefined) => {
    if (!code) return null
    const state = states.data?.find((candidate) => candidate.code === code)
    return state ? `${state.name} (${state.code})` : code
  }
}
