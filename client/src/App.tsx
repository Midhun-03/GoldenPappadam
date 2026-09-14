import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/AppLayout'
import { RequireAuth } from './auth/RequireAuth'
import { LoginPage } from './pages/LoginPage'
import { PackingPage } from './pages/PackingPage'
import { ProductsPage } from './pages/ProductsPage'
import { SettingsPage } from './pages/SettingsPage'
import { StockHistoryPage } from './pages/StockHistoryPage'
import { StockPage } from './pages/StockPage'

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<Navigate to="/stock" replace />} />
          <Route path="/stock" element={<StockPage />} />
          <Route path="/stock/:productId" element={<StockHistoryPage />} />
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/packing" element={<PackingPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/stock" replace />} />
    </Routes>
  )
}
