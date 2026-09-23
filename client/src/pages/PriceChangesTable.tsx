import { Link } from 'react-router-dom'
import type { CustomerPriceChange } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime, formatMoney } from '@/lib/format'

function describe(change: CustomerPriceChange) {
  if (change.newPrice === null) return `Removed ${formatMoney(change.previousPrice)} — back to standard`
  if (change.previousPrice === null) return `Agreed ${formatMoney(change.newPrice)}`
  return `${formatMoney(change.previousPrice)} → ${formatMoney(change.newPrice)}`
}

/**
 * Rate changes, newest first. Salesmen set rates from the phone, so each row says who made the
 * change - which is what lets the office spot one and correct it.
 */
export function PriceChangesTable({
  changes,
  showCustomer = false,
}: {
  changes: CustomerPriceChange[]
  showCustomer?: boolean
}) {
  return (
    <Table>
      <TableHeader sticky>
        <TableRow>
          <TableHead>{showCustomer ? 'Shop and product' : 'Product'}</TableHead>
          <TableHead>Change</TableHead>
          <TableHead className="hidden sm:table-cell">By</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {changes.map((change) => (
          <TableRow key={change.id}>
            <TableCell className="max-w-[12rem]">
              {showCustomer && (
                <Link
                  to={`/customers/${change.customerId}`}
                  className="block truncate font-medium underline-offset-4 hover:underline"
                >
                  {change.customerName}
                </Link>
              )}
              <div className={showCustomer ? 'truncate text-xs text-muted-foreground' : 'truncate'}>
                {change.productName}
              </div>
              <div className="text-xs text-muted-foreground">{formatDateTime(change.changedAt)}</div>
            </TableCell>
            <TableCell className="tabular-nums">
              {describe(change)}
              <div className="text-xs text-muted-foreground sm:hidden">{change.changedBy ?? 'Unknown'}</div>
            </TableCell>
            <TableCell className="hidden sm:table-cell">
              <div className="flex items-center gap-2">
                <span className="truncate text-muted-foreground">{change.changedBy ?? 'Unknown'}</span>
                {change.changedBySalesperson && <Badge variant="secondary">Sales team</Badge>}
              </div>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
