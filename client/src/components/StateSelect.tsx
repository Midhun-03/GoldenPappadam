import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { useIndianStates } from '@/lib/indianStates'

const NONE = 'none'

/** A GST state picker. `allowNone` offers "Not set", for a shop whose state nobody has entered yet. */
export function StateSelect({
  id,
  value,
  onChange,
  allowNone = false,
}: {
  id: string
  value: string
  onChange: (code: string) => void
  allowNone?: boolean
}) {
  const states = useIndianStates()

  return (
    <Select value={value === '' ? NONE : value} onValueChange={(next) => onChange(next === NONE ? '' : next)}>
      <SelectTrigger id={id} className="w-full">
        <SelectValue placeholder="Choose a state" />
      </SelectTrigger>
      <SelectContent>
        {allowNone && <SelectItem value={NONE}>Not set</SelectItem>}
        {(states.data ?? []).map((state) => (
          <SelectItem key={state.code} value={state.code}>
            {state.name} ({state.code})
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
