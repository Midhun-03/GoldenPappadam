import type { ReportColumn, ReportColumnKind, ReportDocument, ReportValue } from '@/api/reports'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDay, formatMoney, formatQuantity } from '@/lib/format'
import { cn } from '@/lib/utils'

const isNumber = (kind: ReportColumnKind) => kind === 'Money' || kind === 'Quantity' || kind === 'Count'

function formatReportValue(value: ReportValue, kind: ReportColumnKind): string {
  if (value === null || value === undefined || value === '') return ''
  if (typeof value === 'number') {
    if (kind === 'Money') return formatMoney(value)
    if (kind === 'Count') return value.toLocaleString('en-IN')
    return formatQuantity(value)
  }
  return kind === 'Date' ? formatDay(value) : value
}

/**
 * Any report the server builds, on screen: the summary figures, then each table with the totals
 * row the server worked out. The same document prints as the PDF and downloads as Excel.
 */
export function ReportView({ report }: { report: ReportDocument }) {
  return (
    <div className="grid gap-4 lg:gap-5">
      {report.subtitle && <p className="text-sm text-muted-foreground">{report.subtitle}</p>}

      {report.summary.length > 0 && (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          {report.summary.map((figure) => (
            <Card key={figure.label} className="gap-1 py-4">
              <CardContent className="grid gap-1 px-4">
                <span className="text-xs text-muted-foreground">{figure.label}</span>
                <span className="font-heading text-xl font-semibold tabular-nums">
                  {formatReportValue(figure.value, figure.kind)}
                </span>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {report.sections.map((section) => (
        <Card key={section.title}>
          <CardHeader>
            <CardTitle>{section.title}</CardTitle>
            {section.note && <CardDescription>{section.note}</CardDescription>}
          </CardHeader>
          <CardContent className="px-0">
            {section.rows.length === 0 ? (
              <p className="px-4 text-sm text-muted-foreground">Nothing in this period.</p>
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      {section.columns.map((column) => (
                        <TableHead key={column.key} className={cn(isNumber(column.kind) && 'text-right')}>
                          {column.title}
                        </TableHead>
                      ))}
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {section.rows.map((row, index) => (
                      <TableRow key={index}>
                        {section.columns.map((column) => (
                          <Cell key={column.key} column={column} value={row[column.key] ?? null} />
                        ))}
                      </TableRow>
                    ))}
                  </TableBody>
                  {section.totals && (
                    <TableFooter>
                      <TableRow>
                        {section.columns.map((column, index) => (
                          <TableCell
                            key={column.key}
                            className={cn('font-semibold tabular-nums', isNumber(column.kind) && 'text-right')}
                          >
                            {column.key in section.totals!
                              ? formatReportValue(section.totals![column.key], column.kind)
                              : index === 0
                                ? 'Total'
                                : ''}
                          </TableCell>
                        ))}
                      </TableRow>
                    </TableFooter>
                  )}
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

function Cell({ column, value }: { column: ReportColumn; value: ReportValue }) {
  const negative = typeof value === 'number' && value < 0

  return (
    <TableCell
      className={cn(
        isNumber(column.kind) && 'text-right tabular-nums',
        column.kind === 'Date' && 'whitespace-nowrap',
        negative && 'text-success',
      )}
    >
      {formatReportValue(value, column.kind)}
    </TableCell>
  )
}
