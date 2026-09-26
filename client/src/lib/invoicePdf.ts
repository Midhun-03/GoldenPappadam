import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { toast } from 'sonner'
import { invoicesApi } from '@/api/sales'
import type { InvoiceDocumentType } from '@/api/types'
import { ApiError } from '@/lib/api'
import { openInNewTab, saveFile } from '@/lib/files'

/**
 * Viewing, saving and printing all use the stored PDF, so what is printed in the office is the
 * same file the customer is emailed. There is no separate print layout to drift out of step.
 */

/** Opens the stored PDF in a new tab, where the browser's viewer can print or save it. */
export const viewInvoicePdf = (invoiceId: string) => openInNewTab(() => invoicesApi.pdf(invoiceId))

export const downloadInvoicePdf = (invoiceId: string, invoiceNumber: string) =>
  saveFile(() => invoicesApi.pdf(invoiceId), `Invoice-${invoiceNumber.replaceAll('/', '-')}.pdf`)

/**
 * Loads the PDF into a hidden frame and opens the browser's print dialog for it - A4, exactly as
 * stored. Where a browser cannot print a PDF from a frame, it opens the PDF in a tab instead,
 * where the viewer's own print button does the job.
 */
export async function printInvoicePdf(invoiceId: string) {
  const url = URL.createObjectURL(await invoicesApi.pdf(invoiceId))
  const frame = document.createElement('iframe')
  frame.style.position = 'fixed'
  frame.style.right = '0'
  frame.style.bottom = '0'
  frame.style.width = '0'
  frame.style.height = '0'
  frame.style.border = '0'
  frame.src = url

  const cleanUp = () => {
    frame.remove()
    URL.revokeObjectURL(url)
  }

  frame.onload = () => {
    try {
      frame.contentWindow?.focus()
      frame.contentWindow?.print()
      // The print dialog blocks until it is closed in most browsers; give it time either way.
      setTimeout(cleanUp, 60_000)
    } catch {
      cleanUp()
      void viewInvoicePdf(invoiceId)
    }
  }

  document.body.append(frame)
}

/**
 * View, save and print, each from the stored PDF. The first request for a phone's sale is what
 * makes its PDF, so the invoice is refreshed afterwards to show that it now has one.
 */
export function useInvoicePdf() {
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState<string | null>(null)

  async function run(invoiceId: string, key: string, action: () => Promise<void>, failure: string) {
    setBusy(`${invoiceId}:${key}`)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
    } catch (caught) {
      toast.error(caught instanceof ApiError ? caught.message : failure)
    } finally {
      setBusy(null)
    }
  }

  return {
    isBusy: (invoiceId: string, key: 'view' | 'download' | 'print') => busy === `${invoiceId}:${key}`,
    view: (invoiceId: string) => run(invoiceId, 'view', () => viewInvoicePdf(invoiceId), 'Could not open the PDF.'),
    download: (invoiceId: string, invoiceNumber: string) =>
      run(invoiceId, 'download', () => downloadInvoicePdf(invoiceId, invoiceNumber), 'Could not download the PDF.'),
    print: (invoiceId: string) => run(invoiceId, 'print', () => printInvoicePdf(invoiceId), 'Could not print the invoice.'),
  }
}

/** A GST bill is a tax invoice, or a bill of supply when every item is exempt; anything else is a normal bill. */
export const documentTitle = (type: InvoiceDocumentType) =>
  type === 'TaxInvoice' ? 'GST bill (tax invoice)' : type === 'BillOfSupply' ? 'GST bill (bill of supply)' : 'Normal bill'
