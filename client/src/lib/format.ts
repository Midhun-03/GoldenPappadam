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

export const formatQuantity = (value: number) => quantityFormat.format(value)

export const formatMoney = (value: number | null) => (value === null ? '—' : moneyFormat.format(value))

export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso))

export const formatDate = (iso: string) => dateFormat.format(new Date(iso))
