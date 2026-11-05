import { Loader2 } from 'lucide-react'
import { useRef, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '@/auth/auth-context'
import { Logo } from '@/components/Logo'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api'
import { prefersReducedMotion } from '@/lib/motion'

export function LoginPage() {
  const { user, isLoading, login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)
  // The arrival plays once; after the first attempt the mark only bubbles while signing in.
  const [hasTried, setHasTried] = useState(false)

  if (!isLoading && user) return <Navigate to="/" replace />

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setHasTried(true)
    setIsSubmitting(true)

    try {
      await login(email, password)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not sign in. Is the server running?')
      // A short head-shake says "not that" before the message is read. Every failed attempt gets one.
      if (!prefersReducedMotion()) {
        formRef.current?.animate(
          [
            { transform: 'none' },
            { transform: 'translateX(-6px)' },
            { transform: 'translateX(5px)' },
            { transform: 'translateX(-3px)' },
            { transform: 'translateX(2px)' },
            { transform: 'none' },
          ],
          { duration: 340, easing: 'ease-out' },
        )
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-4 py-10">
      <div className="w-full max-w-sm animate-enter">
        <div className="mb-7 flex flex-col items-center text-center">
          {/* The pappadam fries once on arrival, and its blisters bubble while the sign-in is in flight. */}
          <Logo className="size-11" arrive={!hasTried} frying={isSubmitting} />
          <h1 className="mt-4 font-heading text-xl font-semibold tracking-tight">Golden Pappadam</h1>
          <p className="mt-1 text-sm text-muted-foreground">Stock, bills and customer payments.</p>
        </div>

        <form ref={formRef} className="grid gap-4 rounded-xl border bg-card p-5" onSubmit={handleSubmit}>
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
            <Alert variant="destructive" className="animate-arrive">
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
