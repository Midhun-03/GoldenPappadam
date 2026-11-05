import type { CSSProperties } from 'react'
import { cn } from '@/lib/utils'

/**
 * The Golden Pappadam mark: a pappadam on a dark tile, its three blisters in the deeper gold.
 *
 * `arrive` fries it once - the disc puffs up and the blisters rise one after another - and
 * `frying` keeps the blisters bubbling while something is genuinely in progress. Both are
 * styled in index.css (`.gp-mark`) and stand still when the system asks for less motion.
 */
export function Logo({
  className,
  arrive,
  frying,
}: {
  className?: string
  arrive?: boolean
  frying?: boolean
}) {
  return (
    <svg
      viewBox="0 0 32 32"
      aria-hidden="true"
      className={cn('gp-mark size-7 shrink-0', className)}
      data-arrive={arrive || undefined}
      data-frying={frying || undefined}
    >
      <rect width="32" height="32" rx="7" className="fill-primary" />
      <circle cx="16" cy="16" r="9" className="gp-mark-disc fill-brand" />
      <circle cx="12.6" cy="13.6" r="1.1" className="gp-mark-blister fill-brand-deep" style={{ '--i': 0 } as CSSProperties} />
      <circle cx="18.8" cy="15.2" r="0.9" className="gp-mark-blister fill-brand-deep" style={{ '--i': 1 } as CSSProperties} />
      <circle cx="15.1" cy="19" r="1" className="gp-mark-blister fill-brand-deep" style={{ '--i': 2 } as CSSProperties} />
    </svg>
  )
}
