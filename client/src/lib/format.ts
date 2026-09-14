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

const dateFormat = new Intl.DateTimeFormat('en-IN', {
  timeZone: 'Asia/Kolkata',
  dateStyle: 'medium',
})

/** Plain dates from the API ("2026-09-14") carry no time zone, so they are formatted as they are. */
const dayFormat = new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' })

export const formatQuantity = (value: number) => quantityFormat.format(value)

/** Formats a plain date string such as an invoice date. */
export const formatDay = (value: string) => dayFormat.format(new Date(`${value}T00:00:00`))

/** Today in IST as "2026-09-14", for date inputs. */
export const todayInIndia = () =>
  new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata' }).format(new Date())

export const formatMoney = (value: number | null) => (value === null ? '—' : moneyFormat.format(value))

export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso))

export const formatDate = (iso: string) => dateFormat.format(new Date(iso))
