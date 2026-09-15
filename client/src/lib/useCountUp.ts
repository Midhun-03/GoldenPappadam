import { useEffect, useRef, useState } from 'react'
import { prefersReducedMotion } from './motion'

/**
 * Counts a headline figure up to its value once, so a dashboard that has just
 * loaded reads as live. Short enough that the number stays legible, and skipped
 * entirely when the system asks for less motion.
 */
export function useCountUp(value: number, duration = 450) {
  const [shown, setShown] = useState(value)
  const from = useRef(value)

  useEffect(() => {
    if (prefersReducedMotion() || from.current === value) {
      from.current = value
      setShown(value)
      return
    }

    const start = performance.now()
    const origin = from.current
    from.current = value
    let frame = 0

    const step = (now: number) => {
      // rAF's timestamp can precede `start`, so clamp both ends.
      const progress = Math.min(Math.max((now - start) / duration, 0), 1)
      // Ease out, so the number settles rather than stopping dead.
      const eased = 1 - (1 - progress) ** 3
      setShown(origin + (value - origin) * eased)
      if (progress < 1) frame = requestAnimationFrame(step)
    }

    frame = requestAnimationFrame(step)
    return () => cancelAnimationFrame(frame)
  }, [value, duration])

  return shown
}
