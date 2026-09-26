import { useQuery } from '@tanstack/react-query'
import { stockApi } from '@/api/inventory'
import { settlementLabels, type ReturnSettlement, type SettlementState } from '@/api/returns'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { formatMoney } from '@/lib/format'

const hints: Record<ReturnSettlement, string> = {
  Pending: 'Record what came back now and decide later from the returns list.',
  Replacement: 'Fresh packets of the same products go to the shop free. Stock goes down; the bill stays the same.',
  Credit: "Takes the amount off what the shop owes, settling its oldest bills first. It is not money received.",
  NoCompensation: 'Only the record. The shop owes the same.',
}

/**
 * The office's decision for a return: replace, credit or nothing, or leave it to decide later.
 * Shared by the new-return form and the settle dialog so both ask the same way.
 */
export function ReturnSettlementFields({
  value,
  onChange,
  packetsValue,
  allowPending,
}: {
  value: SettlementState
  onChange: (next: SettlementState) => void
  /** The value of the returned packets, which a credit defaults to. */
  packetsValue: number
  allowPending: boolean
}) {
  const locations = useQuery({ queryKey: ['stock', 'locations'], queryFn: stockApi.locations })
  const options = (Object.keys(settlementLabels) as ReturnSettlement[]).filter((s) => allowPending || s !== 'Pending')

  return (
    <div className="grid gap-3">
      <div className="grid grid-cols-2 gap-2">
        {options.map((option) => (
          <Button
            key={option}
            type="button"
            variant={value.settlement === option ? 'default' : 'outline'}
            aria-pressed={value.settlement === option}
            onClick={() => onChange({ ...value, settlement: option })}
          >
            {settlementLabels[option]}
          </Button>
        ))}
      </div>
      <p className="text-xs text-muted-foreground">{hints[value.settlement]}</p>

      {value.settlement === 'Credit' && (
        <div className="grid gap-1.5">
          <Label htmlFor="return-credit">Credit amount</Label>
          <Input
            id="return-credit"
            type="number"
            inputMode="decimal"
            step="0.01"
            min="0.01"
            className="text-right"
            placeholder={packetsValue > 0 ? String(packetsValue) : 'Enter an amount'}
            value={value.creditAmount}
            onChange={(event) => onChange({ ...value, creditAmount: event.target.value })}
          />
          <p className="text-xs text-muted-foreground">
            Leave empty to credit the packets' value, {formatMoney(packetsValue)}.
          </p>
        </div>
      )}

      {value.settlement === 'Replacement' && (
        <div className="grid gap-1.5">
          <Label htmlFor="return-location">Fresh packets from</Label>
          <Select value={value.locationId} onValueChange={(locationId) => onChange({ ...value, locationId })}>
            <SelectTrigger id="return-location" className="w-full">
              <SelectValue placeholder="Main warehouse" />
            </SelectTrigger>
            <SelectContent>
              {(locations.data ?? [])
                .filter((location) => location.isActive)
                .map((location) => (
                  <SelectItem key={location.id} value={location.id}>
                    {location.name}
                  </SelectItem>
                ))}
            </SelectContent>
          </Select>
        </div>
      )}
    </div>
  )
}
