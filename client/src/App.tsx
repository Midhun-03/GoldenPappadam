import { Loader2 } from 'lucide-react'
import { Suspense, lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { RequireAuth } from './auth/RequireAuth'
import { CustomerLedgerPage } from './pages/CustomerLedgerPage'
import { CustomersPage } from './pages/CustomersPage'
import { InvoiceDetailPage } from './pages/InvoiceDetailPage'
import { InvoicesPage } from './pages/InvoicesPage'
import { LoginPage } from './pages/LoginPage'
import { NewInvoicePage } from './pages/NewInvoicePage'
import { NewReturnPage } from './pages/NewReturnPage'
import { ReturnDetailPage } from './pages/ReturnDetailPage'
import { ReturnsPage } from './pages/ReturnsPage'
import { PackingPage } from './pages/PackingPage'
import { RepackingPage } from './pages/RepackingPage'
import { StockAgePage } from './pages/StockAgePage'
import { PaymentsPage } from './pages/PaymentsPage'
import { ProductsPage } from './pages/ProductsPage'
import { SettingsPage } from './pages/SettingsPage'
import { StockHistoryPage } from './pages/StockHistoryPage'
import { StockPage } from './pages/StockPage'
import { StockRequestsPage } from './pages/StockRequestsPage'
import { FieldSalesDayPage } from './pages/FieldSalesDayPage'
import { VanPage } from './pages/VanPage'
import {
  CollectionsReportPage,
  OutstandingReportPage,
  ReturnsReportPage,
  SalesReportPage,
  StockReportPage,
} from './pages/reports/ReportPage'

// The dashboard is the only screen that draws charts, so its charting library loads with it
// rather than with every other page.
const DashboardPage = lazy(() =>
  import('./pages/DashboardPage').then((module) => ({ default: module.DashboardPage })),
)

function PageLoading() {
  return (
    <div className="flex min-h-64 items-center justify-center">
      <Loader2 className="size-5 animate-spin text-muted-foreground" />
      <span className="sr-only">Loading</span>
    </div>
  )
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route
            index
            element={
              <Suspense fallback={<PageLoading />}>
                <DashboardPage />
              </Suspense>
            }
          />

          <Route path="/stock" element={<StockPage />} />
          <Route path="/stock-age" element={<StockAgePage />} />
          <Route path="/stock/:productId" element={<StockHistoryPage />} />
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/packing" element={<PackingPage />} />
          <Route path="/repacking" element={<RepackingPage />} />
          <Route path="/van" element={<VanPage />} />
          <Route path="/field-sales" element={<FieldSalesDayPage />} />
          <Route path="/stock-requests" element={<StockRequestsPage />} />

          <Route path="/customers" element={<CustomersPage />} />
          <Route path="/customers/:customerId" element={<CustomerLedgerPage />} />
          <Route path="/invoices" element={<InvoicesPage />} />
          <Route path="/invoices/new" element={<NewInvoicePage />} />
          <Route path="/invoices/:invoiceId" element={<InvoiceDetailPage />} />
          <Route path="/payments" element={<PaymentsPage />} />
          <Route path="/returns" element={<ReturnsPage />} />
          <Route path="/returns/new" element={<NewReturnPage />} />
          <Route path="/returns/:returnId" element={<ReturnDetailPage />} />
          <Route path="/reports/sales" element={<SalesReportPage />} />
          <Route path="/reports/collections" element={<CollectionsReportPage />} />
          <Route path="/reports/outstanding" element={<OutstandingReportPage />} />
          <Route path="/reports/stock" element={<StockReportPage />} />
          <Route path="/reports/returns" element={<ReturnsReportPage />} />

          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
