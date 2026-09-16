import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, UserPlus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { usersApi } from '@/api/auth'
import type { Role } from '@/api/types'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { useAuth } from '@/auth/auth-context'
import { ApiError } from '@/lib/api'

/**
 * The accounts. An admin can do everything here; a salesperson can reach only the phone's own
 * endpoints, which is enforced by the API rather than by this screen hiding anything.
 */
export function UsersCard() {
  const [isAdding, setIsAdding] = useState(false)
  const queryClient = useQueryClient()
  const { user } = useAuth()

  const users = useQuery({ queryKey: ['users'], queryFn: usersApi.list })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => usersApi.setActive(id, isActive),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['users'] })
      toast.success('Account updated')
    },
    onError: (caught) =>
      toast.error(caught instanceof ApiError ? caught.message : 'Could not change the account.'),
  })

  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-3 space-y-0">
        <div>
          <CardTitle>People</CardTitle>
          <CardDescription>
            Who can sign in, and what they may do. Accounts are deactivated, never deleted, because
            their id is on everything they recorded.
          </CardDescription>
        </div>
        <Button size="sm" variant="outline" className="shrink-0" onClick={() => setIsAdding(true)}>
          <UserPlus className="size-4" />
          Add
        </Button>
      </CardHeader>

      <CardContent className="px-0">
        {users.isPending ? (
          <p className="px-6 text-sm text-muted-foreground">Loading…</p>
        ) : users.isError ? (
          <p className="px-6 text-sm text-destructive">
            {users.error instanceof ApiError ? users.error.message : 'Could not load the accounts.'}
          </p>
        ) : (
          <Table>
            <TableHeader sticky>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Role</TableHead>
                <TableHead className="w-28 text-right">Active</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.data.map((account) => (
                <TableRow key={account.id} className={account.isActive ? undefined : 'opacity-60'}>
                  <TableCell className="max-w-[12rem]">
                    <div className="truncate font-medium">{account.fullName}</div>
                    <div className="truncate text-xs text-muted-foreground">{account.email}</div>
                  </TableCell>
                  <TableCell>
                    <Badge variant={account.role === 'Admin' ? 'secondary' : 'outline'}>
                      {account.role === 'Salesperson' ? 'Salesperson' : 'Admin'}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right">
                    {account.id === user?.id ? (
                      <span className="text-xs text-muted-foreground">you</span>
                    ) : (
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={setActive.isPending}
                        onClick={() =>
                          setActive.mutate({ id: account.id, isActive: !account.isActive })
                        }
                      >
                        {account.isActive ? 'Deactivate' : 'Reactivate'}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <AddUserDialog open={isAdding} onOpenChange={setIsAdding} />
    </Card>
  )
}

function AddUserDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [fullName, setFullName] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<Role>('Salesperson')
  const [error, setError] = useState<string | null>(null)

  const create = useMutation({
    mutationFn: () =>
      usersApi.create({ email: email.trim(), fullName: fullName.trim(), password, role }),
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ['users'] })
      toast.success(`${created.fullName} can now sign in.`)
      onOpenChange(false)
      setEmail('')
      setFullName('')
      setPassword('')
      setRole('Salesperson')
      setError(null)
    },
    onError: (caught) =>
      setError(caught instanceof ApiError ? caught.message : 'Could not add the account.'),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    if (password.length < 8) {
      setError('The password needs at least 8 characters.')
      return
    }

    create.mutate()
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>Add someone</DialogTitle>
            <DialogDescription>
              They sign in with this email and password. Tell them the password yourself — it is not
              shown again.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="grid gap-2">
              <Label htmlFor="user-name">Name</Label>
              <Input
                id="user-name"
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
                required
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="user-email">Email</Label>
              <Input
                id="user-email"
                type="email"
                autoComplete="off"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="user-password">Password</Label>
              <Input
                id="user-password"
                type="password"
                autoComplete="new-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="user-role">Role</Label>
              <Select value={role} onValueChange={(value) => setRole(value as Role)}>
                <SelectTrigger id="user-role">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Salesperson">Salesperson — the phone only</SelectItem>
                  <SelectItem value="Admin">Admin — everything</SelectItem>
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {role === 'Salesperson'
                  ? 'Records deliveries, payments and visits on the road. Cannot set prices, move stock or cancel a bill.'
                  : 'Full access to the office system, including prices and stock.'}
              </p>
            </div>

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Loader2 className="size-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
