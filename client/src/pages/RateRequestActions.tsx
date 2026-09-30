import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Check, Loader2, X } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import { describeRate, rateRequestsApi, type RateRequest } from '@/api/rateRequests'
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
import { formatMoney } from '@/lib/format'

/**
 * Approve or reject one request, with an optional note the salesperson sees on the phone. Approving
 * changes the shop's rate from its next bill; bills already made keep their price.
 */
export function RateRequestActions({ request }: { request: RateRequest }) {
  const queryClient = useQueryClient()
  const [decision, setDecision] = useState<'approve' | 'reject' | null>(null)
  const [note, setNote] = useState('')

  const decide = useMutation({
    mutationFn: () =>
      decision === 'approve' ? rateRequestsApi.approve(request.id, note) : rateRequestsApi.reject(request.id, note),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['rate-requests'] })
      await queryClient.invalidateQueries({ queryKey: ['customers', request.customerId, 'prices'] })
      toast.success(
        decision === 'approve'
          ? `${request.customerName} now pays ${formatMoney(request.requestedPrice)} for ${request.productName}.`
          : `Rejected. ${request.customerName} keeps its rate.`,
      )
      setDecision(null)
      setNote('')
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : 'Could not save the decision.'),
  })

  const current = describeRate(request.currentPrice, request.standardPrice)

  return (
    <>
      <div className="flex justify-end gap-1.5">
        <Button size="sm" onClick={() => setDecision('approve')}>
          <Check className="size-4" />
          Approve
        </Button>
        <Button size="sm" variant="outline" onClick={() => setDecision('reject')}>
          <X className="size-4" />
          Reject
        </Button>
      </div>

      <Dialog open={decision !== null} onOpenChange={(open) => !open && setDecision(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{decision === 'approve' ? 'Approve this rate?' : 'Reject this rate?'}</DialogTitle>
            <DialogDescription>
              {request.customerName} · {request.productName}: {current} → {formatMoney(request.requestedPrice)}.{' '}
              {decision === 'approve'
                ? 'It applies from the next bill, at every branch.'
                : 'The shop keeps paying what it pays now.'}
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor={`decision-note-${request.id}`}>Note to {request.requestedBy ?? 'the salesperson'}</Label>
            <Input
              id={`decision-note-${request.id}`}
              maxLength={300}
              placeholder="Optional"
              value={note}
              onChange={(event) => setNote(event.target.value)}
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setDecision(null)}>
              Not now
            </Button>
            <Button
              variant={decision === 'approve' ? 'default' : 'destructive'}
              disabled={decide.isPending}
              onClick={() => decide.mutate()}
            >
              {decide.isPending && <Loader2 className="size-4 animate-spin" />}
              {decision === 'approve' ? 'Approve' : 'Reject'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
