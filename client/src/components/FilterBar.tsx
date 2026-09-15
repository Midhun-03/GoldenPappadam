import type { ReactNode } from 'react'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

/**
 * The filter row above a list. Two columns on a phone, one flowing row from
 * small screens up, so filters never turn into a tall ragged stack.
 */
export function FilterBar({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn(
        'mb-4 grid grid-cols-2 gap-x-3 gap-y-3 rounded-lg border bg-card p-3 sm:flex sm:flex-wrap sm:items-end',
        className,
      )}
    >
      {children}
    </div>
  )
}

/** A labelled filter control. `htmlFor` must match the control's own id. */
export function FilterField({
  label,
  htmlFor,
  children,
  className,
}: {
  label: string
  htmlFor: string
  children: ReactNode
  className?: string
}) {
  return (
    <div className={cn('grid gap-1.5', className)}>
      <Label htmlFor={htmlFor} className="text-xs text-muted-foreground">
        {label}
      </Label>
      {children}
    </div>
  )
}

/** An on/off filter, pressed state announced to screen readers. */
export function FilterToggle({
  pressed,
  onPressedChange,
  children,
  className,
}: {
  pressed: boolean
  onPressedChange: (pressed: boolean) => void
  children: ReactNode
  className?: string
}) {
  return (
    <Button
      type="button"
      variant={pressed ? 'secondary' : 'outline'}
      aria-pressed={pressed}
      onClick={() => onPressedChange(!pressed)}
      className={cn(pressed && 'ring-1 ring-foreground/15', className)}
    >
      {children}
    </Button>
  )
}
