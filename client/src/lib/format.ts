const quantityFormat = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 3 })
const moneyFormat = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  maximumFractionDigits: 2,
})

/** The API sends UTC; the business works in IST. */
const dateTimeFormat = new Intl.DateTimeFormat('en-IN', {
  timeZone: 'Asia/Kolkata',
  dateStyle: 'medium',
  timeStyle: 'short',
})

/** Plain dates from the API ("2026-09-14") carry no time zone, so they are formatted as they are. */
const dayFormat = new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' })

export const formatQuantity = (value: number) => quantityFormat.format(value)

/** Formats a plain date string such as an invoice date. */
export const formatDay = (value: string) => dayFormat.format(new Date(`${value}T00:00:00`))

/** The hour of the day in IST, 0-23, for a time-of-day greeting. */
export const hourInIndia = () =>
  Number(
    new Intl.DateTimeFormat('en-GB', {
      timeZone: 'Asia/Kolkata',
      hour: '2-digit',
      hour12: false,
    }).format(new Date()),
  )

/** Today in IST as "2026-09-14", for date inputs. */
export const todayInIndia = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata' }).format(new Date())

export const formatMoney = (value: number | null) => (value === null ? '—' : moneyFormat.format(value))

export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso))

const paymentMethodNames: Record<string, string> = { BankTransfer: 'Bank transfer', ReturnCredit: 'Return credit' }

/** "BankTransfer" is a stored enum value; people read "Bank transfer". */
export const formatPaymentMethod = (method: string) => paymentMethodNames[method] ?? method

/** Short label for a chart axis, e.g. "14 Sep". */
const dayShortFormat = new Intl.DateTimeFormat('en-IN', { day: 'numeric', month: 'short' })

export const formatDayShort = (value: string) => dayShortFormat.format(new Date(`${value}T00:00:00`))

/** Moves a plain date string such as "2026-09-14" by whole days. */
export const shiftDay = (value: string, days: number) => {
  const date = new Date(`${value}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

/**
 * Money with the digits dropped, for chart axes where the exact figure is in the tooltip.
 * Keeps one decimal so neighbouring axis ticks never round to the same label.
 */
export const formatMoneyCompact = (value: number) => {
  const short = (divisor: number, suffix: string) => {
    const scaled = value / divisor
    // 1000 reads as "1k", 1200 as "1.2k".
    const digits = scaled % 1 === 0 ? 0 : 1
    return `₹${scaled.toFixed(digits)}${suffix}`
  }

  if (value >= 10_000_000) return short(10_000_000, 'Cr')
  if (value >= 100_000) return short(100_000, 'L')
  if (value >= 1_000) return short(1_000, 'k')
  return `₹${Math.round(value)}`
}
