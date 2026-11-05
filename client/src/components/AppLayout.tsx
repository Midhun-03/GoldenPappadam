import {
  Boxes,
  CalendarCheck,
  CalendarClock,
  ClipboardList,
  ChartNoAxesColumn,
  FileText,
  HandCoins,
  IdCard,
  IndianRupee,
  Hourglass,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  Package,
  PackageOpen,
  PackagePlus,
  Scale,
  ReceiptIndianRupee,
  Route,
  Settings,
  ShoppingBasket,
  ShoppingCart,
  Store,
  Truck,
  Undo2,
  Wallet,
  Warehouse,
  type LucideIcon,
} from 'lucide-react'
import { Dialog as DialogPrimitive } from 'radix-ui'
import { useQuery } from '@tanstack/react-query'
import { useLayoutEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { rateRequestsApi } from '@/api/rateRequests'
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
import { ChangePasswordDialog } from './ChangePasswordDialog'
import { Logo } from './Logo'

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
      { to: '/returns', label: 'Returns', icon: Undo2 },
      { to: '/rate-requests', label: 'Rate requests', icon: Scale },
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
    label: 'Own shop',
    items: [
      { to: '/own-shop', label: 'Shop stock', icon: ShoppingBasket, end: true },
      { to: '/own-shop/sales/new', label: 'New shop sale', icon: ShoppingCart },
      { to: '/own-shop/sales', label: 'Shop sales', icon: ReceiptIndianRupee, end: true },
      { to: '/own-shop/transfers', label: 'Receive from factory', icon: Truck },
    ],
  },
  {
    label: 'Staff',
    items: [
      { to: '/attendance', label: 'Attendance', icon: CalendarCheck },
      { to: '/wages', label: 'Weekly wages', icon: IndianRupee },
      { to: '/employees', label: 'Employees', icon: IdCard },
    ],
  },
  {
    label: 'Accounts',
    items: [{ to: '/expenses', label: 'Expenses', icon: ReceiptIndianRupee }],
  },
  {
    label: 'Reports',
    items: [
      { to: '/reports/sales', label: 'Sales', icon: ChartNoAxesColumn },
      { to: '/reports/collections', label: 'Collections', icon: HandCoins },
      { to: '/reports/outstanding', label: 'Outstanding', icon: Hourglass },
      { to: '/reports/stock', label: 'Stock movement', icon: Warehouse },
      { to: '/reports/returns', label: 'Returns and expiry', icon: Undo2 },
    ],
  },
  {
    label: null,
    items: [{ to: '/settings', label: 'Settings', icon: Settings }],
  },
]

/** The name and mark. Also the way home: it leads to the dashboard, and the pappadam puffs as you reach for it. */
function Brand({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <Link
      to="/"
      onClick={onNavigate}
      aria-label="Golden Pappadam, go to the dashboard"
      className="gp-home -mx-1.5 flex items-center gap-2.5 rounded-lg px-1.5 py-1 focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none"
    >
      <Logo />
      <span className="font-heading text-sm font-semibold tracking-tight">Golden Pappadam</span>
    </Link>
  )
}

function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const { pathname } = useLocation()
  const navRef = useRef<HTMLElement>(null)
  const markerRef = useRef<HTMLSpanElement>(null)
  const hasPlaced = useRef(false)

  // Shares its cache with the Rate requests page's default view, so approving one there updates this count.
  const pendingRates = useQuery({
    queryKey: ['rate-requests', { status: 'Pending' }],
    queryFn: () => rateRequestsApi.list({ status: 'Pending' }),
  })
  const waiting: Record<string, number | undefined> = { '/rate-requests': pendingRates.data?.length }

  // One marker for the whole list, moved to the active link, so changing page shows where you went
  // from and to. It is placed without travel the first time, so a fresh load does not sweep.
  useLayoutEffect(() => {
    const nav = navRef.current
    const marker = markerRef.current
    if (!nav || !marker) return

    const place = (travel: boolean) => {
      const active = nav.querySelector<HTMLElement>('a[aria-current="page"]')
      // No active link, or the list is hidden (the desktop sidebar on a phone): nothing to point at.
      if (!active || active.offsetHeight === 0) {
        marker.style.opacity = '0'
        hasPlaced.current = false
        return
      }

      const top = active.getBoundingClientRect().top - nav.getBoundingClientRect().top
      marker.style.transition = travel && hasPlaced.current ? '' : 'none'
      marker.style.transform = `translateY(${top + 6}px)`
      marker.style.height = `${active.offsetHeight - 12}px`
      marker.style.opacity = '1'
      hasPlaced.current = true
    }

    place(true)

    // The list can appear or reflow without a page change (a window crossing the sidebar
    // breakpoint, a font loading late); the marker follows without travelling.
    // Its first call only reports the size just measured, and would cut the travel short.
    let isFirst = true
    const observer = new ResizeObserver(() => {
      if (isFirst) {
        isFirst = false
        return
      }
      place(false)
    })
    observer.observe(nav)
    return () => observer.disconnect()
  }, [pathname])

  return (
    <nav ref={navRef} className="relative flex flex-col gap-5" aria-label="Main">
      <span
        ref={markerRef}
        aria-hidden="true"
        className="nav-marker pointer-events-none absolute top-0 -left-2 w-0.5 rounded-full bg-primary opacity-0"
      />
      {navigation.map((group, index) => (
        <div key={group.label ?? `group-${index}`} className="flex flex-col gap-0.5">
          {group.label && (
            <div className="mb-1 px-3 text-xs font-medium tracking-wide text-muted-foreground">{group.label}</div>
          )}
          {group.items.map((item) => {
            const count = waiting[item.to]

            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'group/nav flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                    'focus-visible:ring-2 focus-visible:ring-ring/50 focus-visible:outline-none',
                    isActive
                      ? 'bg-secondary text-foreground'
                      : 'text-muted-foreground hover:bg-muted/70 hover:text-foreground',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      className={cn(
                        'size-4 shrink-0 transition-transform duration-150 group-hover/nav:-translate-y-px',
                        isActive && 'text-foreground',
                      )}
                    />
                    {item.label}
                    {count !== undefined && count > 0 && (
                      <span
                        key={count}
                        className="ml-auto animate-pop rounded-full bg-primary px-1.5 py-px text-[0.6875rem] leading-4 font-semibold text-primary-foreground tabular-nums"
                      >
                        {count}
                        <span className="sr-only"> waiting</span>
                      </span>
                    )}
                  </>
                )}
              </NavLink>
            )
          })}
        </div>
      ))}
    </nav>
  )
}

function AccountMenu({ align = 'end' }: { align?: 'start' | 'end' }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [isPasswordOpen, setIsPasswordOpen] = useState(false)

  const initials = (user?.fullName ?? user?.email ?? '?')
    .split(' ')
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase()

  return (
    <>
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
          <DropdownMenuItem onSelect={() => setIsPasswordOpen(true)}>
            <KeyRound className="size-4" />
            Change password
          </DropdownMenuItem>
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
      <ChangePasswordDialog open={isPasswordOpen} onOpenChange={setIsPasswordOpen} />
    </>
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
                <Brand onNavigate={() => setIsDrawerOpen(false)} />
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
