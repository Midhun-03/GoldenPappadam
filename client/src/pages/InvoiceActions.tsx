import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Loader2, Send } from 'lucide-react'
import { useEffect, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { invoicesApi } from '@/api/sales'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api'

const describe = (caught: unknown, fallback: string) => (caught instanceof ApiError ? caught.message : fallback)

/**
 * Sends the stored PDF by email. A failure is not an error here: the attempt is recorded, the
 * invoice is untouched, and the office can send it again whenever the problem is fixed.
 */
export function EmailInvoiceDialog({
  invoice,
  open,
  onOpenChange,
}: {
  invoice: { id: string; invoiceNumber: string; customerEmail?: string | null } | null
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const queryClient = useQueryClient()
  const [recipient, setRecipient] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    setError(null)
    setRecipient(invoice?.customerEmail ?? '')
  }, [open, invoice])

  const send = useMutation({
    mutationFn: () => invoicesApi.email(invoice!.id, recipient.trim()),
    onSuccess: async (attempt) => {
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })

      if (attempt.status === 'Sent') {
        toast.success(`${invoice!.invoiceNumber} emailed to ${attempt.recipient}`)
        onOpenChange(false)
      } else {
        setError(`Not sent: ${attempt.errorMessage ?? 'the mail server refused it'}. The invoice is fine - try again once that is sorted.`)
      }
    },
    onError: (caught) => setError(describe(caught, 'Could not send the invoice.')),
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    send.mutate()
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Email {invoice?.invoiceNumber}</DialogTitle>
          <DialogDescription>
            The stored PDF is attached - the same file you print. Leave the address empty to use the one on the
            customer.
          </DialogDescription>
        </DialogHeader>

        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-1.5">
            <Label htmlFor="email-recipient">Send to</Label>
            <Input
              id="email-recipient"
              type="email"
              maxLength={256}
              placeholder="The customer's email on file"
              value={recipient}
              onChange={(event) => setRecipient(event.target.value)}
            />
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Close
            </Button>
            <Button type="submit" disabled={send.isPending}>
              {send.isPending ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
              {send.isPending ? 'Sending…' : error ? 'Try again' : 'Send'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
