import { api } from '@/lib/api'

export type ReportColumnKind = 'Text' | 'Money' | 'Quantity' | 'Count' | 'Date'

export type ReportColumn = { key: string; title: string; kind: ReportColumnKind; total: boolean }

/** A cell: text, a number, or a plain date ("2026-09-23"). */
export type ReportValue = string | number | null

export type ReportSection = {
  title: string
  columns: ReportColumn[]
  rows: Record<string, ReportValue>[]
  /** Worked out by the server, so the screen, the PDF and Excel show the same totals. */
  totals: Record<string, ReportValue> | null
  note: string | null
}

export type ReportDocument = {
  name: string
  title: string
  /** Written for people: "23 Sep 2026" or "1 Sep – 23 Sep 2026". */
  period: string
  from: string
  to: string
  subtitle: string | null
  summary: { label: string; value: number; kind: ReportColumnKind }[]
  sections: ReportSection[]
  generatedAt: string
}

export type ReportFormat = 'pdf' | 'xlsx'

/** Report paths under /api/reports, with the query each one takes. */
export type ReportRequest = { path: string; params: Record<string, string | undefined> }

function url({ path, params }: ReportRequest, format?: ReportFormat) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value)
  }
  if (format) search.set('format', format)
  const text = search.toString()
  return `/api/reports/${path}${text ? `?${text}` : ''}`
}

export const reportsApi = {
  get: (request: ReportRequest) => api.get<ReportDocument>(url(request)),
  file: (request: ReportRequest, format: ReportFormat) => api.blob(url(request, format)),
}
