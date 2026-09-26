import {
  Boxes,
  CalendarClock,
  ClipboardList,
  ChartNoAxesColumn,
  FileText,
  HandCoins,
  Hourglass,
  LayoutDashboard,
  LogOut,
  Menu,
  Package,
  PackageOpen,
  PackagePlus,
  Route,
  Settings,
  Store,
  Truck,
  Wallet,
  Warehouse,
  type LucideIcon,
} from 'lucide-react'
import { Dialog as DialogPrimitive } from 'radix-ui'
import { useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/auth-context'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'

type NavItem = { to: string; label: string; icon: LucideIcon; end?: boolean }
type NavGroup = { label: string | null; items: NavItem[] }

/** Only routes that exist. Grouped the way the work is grouped. */
const navigation: NavGroup[] = [
  {
    label: null,
    items: [{ to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true }],
  },
  {
    label: 'Sales',
    items: [
      { to: '/invoices', label: 'Bills', icon: FileText },
      { to: '/payments', label: 'Payments', icon: Wallet },
      { to: '/customers', label: 'Customers', icon: Store },
    ],
  },
  {
    label: 'Inventory',
    items: [
      { to: '/stock', label: 'Stock', icon: Boxes },
      { to: '/stock-age', label: 'Stock age', icon: CalendarClock },
      { to: '/products', label: 'Products', icon: Package },
      { to: '/packing', label: 'Packing', icon: PackagePlus },
      { to: '/repacking', label: 'Repacking', icon: PackageOpen },
    ],
  },
  {
    label: 'Field sales',
    items: [
      { to: '/field-sales', label: 'Today on the road', icon: Route },
      { to: '/van', label: 'Van', icon: Truck },
      { to: '/stock-requests', label: 'Stock requests', icon: ClipboardList },
    ],
  },
  {
    label: 'Reports',
    items: [
      { to: '/reports/sales', label: 'Sales', icon: ChartNoAxesColumn },
      { to: '/reports/collections', label: 'Collections', icon: HandCoins },
      { to: '/reports/outstanding', label: 'Outstanding', icon: Hourglass },
      { to: '/reports/stock', label: 'Stock movement', icon: Warehouse },
    ],
  },
  {
    label: null,
    items: [{ to: '/settings', label: 'Settings', icon: Settings }],
  },
]

function Logo({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" aria-hidden="true" className={cn('size-7 shrink-0', className)}>
      <rect width="32" height="32" rx="7" className="fill-primary" />
      <circle cx="16" cy="16" r="9" className="fill-brand" />
      <circle cx="12.6" cy="13.6" r="1.1" className="fill-brand-deep" />
      <circle cx="18.8" cy="15.2" r="0.9" className="fill-brand-deep" />
      <circle cx="15.1" cy="19" r="1" className="fill-brand-deep" />
    </svg>
  )
}

function Brand() {
  return (
    <div className="flex items-center gap-2.5">
      <Logo />
      <span className="font-heading text-sm font-semibold tracking-tight">Golden Pappadam</span>
    </div>
  )
}

function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <nav className="flex flex-col gap-5" aria-label="Main">
      {navigation.map((group, index) => (
        <div key={group.label ?? `group-${index}`} className="flex flex-col gap-0.5">
          {group.label && (
            <div className="mb-1 px-3 text-xs font-medium tracking-wide text-muted-foreground">{group.label}</div>
          )}
          {group.items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              onClick={onNavigate}
              className={({ isActive }) =>
                cn(
                  'relative flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                  'focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none',
                  isActive
                    ? 'bg-secondary text-foreground'
                    : 'text-muted-foreground hover:bg-muted/70 hover:text-foreground',
                )
              }
            >
              {({ isActive }) => (
                <>
                  <span
                    aria-hidden="true"
                    className={cn(
                      'absolute top-1.5 bottom-1.5 -left-2 w-0.5 rounded-full bg-primary transition-opacity',
                      isActive ? 'opacity-100' : 'opacity-0',
                    )}
                  />
                  <item.icon className={cn('size-4 shrink-0', isActive && 'text-foreground')} />
                  {item.label}
                </>
              )}
            </NavLink>
          ))}
        </div>
      ))}
    </nav>
  )
}

function AccountMenu({ align = 'end' }: { align?: 'start' | 'end' }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const initials = (user?.fullName ?? user?.email ?? '?')
    .split(' ')
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-auto w-full justify-start gap-2.5 px-2 py-1.5">
          <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-secondary text-xs font-medium">
            {initials}
          </span>
          <span className="min-w-0 truncate text-sm font-medium">{user?.fullName ?? 'Account'}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align={align} className="w-56">
        <DropdownMenuLabel className="font-normal text-muted-foreground">{user?.email}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onClick={async () => {
            await logout()
            navigate('/login', { replace: true })
          }}
        >
          <LogOut className="size-4" />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

export function AppLayout() {
  const { pathname } = useLocation()
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[15rem_1fr]">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-3 focus:left-3 focus:z-50 focus:rounded-md focus:bg-card focus:px-3 focus:py-2 focus:text-sm focus:ring-2 focus:ring-ring"
      >
        Skip to content
      </a>

      {/* Desktop and landscape-tablet: a fixed sidebar. */}
      <aside className="sticky top-0 hidden h-dvh flex-col border-r bg-card lg:flex">
        <div className="flex h-14 items-center px-5">
          <Brand />
        </div>
        <div className="flex-1 overflow-y-auto px-4 py-4">
          <NavLinks />
        </div>
        <div className="border-t p-2">
          <AccountMenu align="start" />
        </div>
      </aside>

      {/* Phone and portrait-tablet: a bar plus a drawer. */}
      <header className="sticky top-0 z-40 flex h-14 items-center gap-2 border-b bg-card/95 px-3 backdrop-blur-sm lg:hidden">
        <DialogPrimitive.Root open={isDrawerOpen} onOpenChange={setIsDrawerOpen}>
          <DialogPrimitive.Trigger asChild>
            <Button variant="ghost" size="icon" aria-label="Open menu">
              <Menu className="size-5" />
            </Button>
          </DialogPrimitive.Trigger>
          <DialogPrimitive.Portal>
            <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-foreground/20 duration-150 data-open:animate-in data-open:fade-in-0 data-closed:animate-out data-closed:fade-out-0" />
            <DialogPrimitive.Content className="fixed inset-y-0 left-0 z-50 flex w-72 max-w-[85vw] flex-col bg-card shadow-xl duration-200 outline-none data-open:animate-in data-open:slide-in-from-left data-closed:animate-out data-closed:slide-out-to-left">
              <DialogPrimitive.Title className="sr-only">Menu</DialogPrimitive.Title>
              <div className="flex h-14 items-center px-5">
                <Brand />
              </div>
              <div className="flex-1 overflow-y-auto px-4 py-4">
                <NavLinks onNavigate={() => setIsDrawerOpen(false)} />
              </div>
              <div className="border-t p-2">
                <AccountMenu align="start" />
              </div>
            </DialogPrimitive.Content>
          </DialogPrimitive.Portal>
        </DialogPrimitive.Root>

        <Brand />
      </header>

      <main id="main" className="min-w-0 px-4 py-5 sm:px-6 sm:py-6 lg:px-8">
        <div key={pathname} className="mx-auto max-w-[80rem] animate-enter">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
