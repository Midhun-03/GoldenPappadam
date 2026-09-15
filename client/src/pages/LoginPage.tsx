import { Loader2 } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '@/auth/auth-context'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api'

export function LoginPage() {
  const { user, isLoading, login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (!isLoading && user) return <Navigate to="/" replace />

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      await login(email, password)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not sign in. Is the server running?')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-4 py-10">
      <div className="w-full max-w-sm animate-enter">
        <div className="mb-7 flex flex-col items-center text-center">
          <svg viewBox="0 0 32 32" aria-hidden="true" className="size-11">
            <rect width="32" height="32" rx="7" className="fill-primary" />
            <circle cx="16" cy="16" r="9" className="fill-brand" />
            <circle cx="12.6" cy="13.6" r="1.1" className="fill-brand-deep" />
            <circle cx="18.8" cy="15.2" r="0.9" className="fill-brand-deep" />
            <circle cx="15.1" cy="19" r="1" className="fill-brand-deep" />
          </svg>
          <h1 className="mt-4 font-heading text-xl font-semibold tracking-tight">Golden Pappadam</h1>
          <p className="mt-1 text-sm text-muted-foreground">Stock, bills and customer payments.</p>
        </div>

        <form className="grid gap-4 rounded-xl border bg-card p-5" onSubmit={handleSubmit}>
          <div className="grid gap-1.5">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              autoComplete="username"
              autoFocus
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          </div>

          <div className="grid gap-1.5">
            <Label htmlFor="password">Password</Label>
            <Input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <Button type="submit" size="lg" className="mt-1" disabled={isSubmitting}>
            {isSubmitting && <Loader2 className="size-4 animate-spin" />}
            {isSubmitting ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
      </div>
    </div>
  )
}
