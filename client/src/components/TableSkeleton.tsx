import { Skeleton } from '@/components/ui/skeleton'

/**
 * Placeholder rows while a list loads, so the first paint is never an empty
 * table that reads as "nothing here".
 */
export function TableSkeleton({ rows = 5, columns = 4 }: { rows?: number; columns?: number }) {
  return (
    <div className="divide-y">
      {Array.from({ length: rows }, (_, row) => (
        <div key={row} className="flex items-center gap-4 px-3 py-3">
          {Array.from({ length: columns }, (_, column) => (
            <Skeleton
              key={column}
              className="h-4"
              style={{
                // The first column is the name, so it gets the width; the rest are figures.
                width: column === 0 ? '30%' : `${Math.max(56, 120 - column * 16)}px`,
                marginLeft: column === columns - 1 ? 'auto' : undefined,
              }}
            />
          ))}
        </div>
      ))}
    </div>
  )
}
