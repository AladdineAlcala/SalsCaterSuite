import { Navigate, Route, Routes } from 'react-router-dom'
import { RouteErrorBoundary } from '@/components/feedback/RouteErrorBoundary'
import { AuthLayout } from '@/layouts/AuthLayout'
import { AppLayout } from '@/layouts/AppLayout'
import { ProtectedRoute } from './ProtectedRoute'
import { useAuth } from '@/context/AuthContext'
import { LoginPage } from '@/features/auth/LoginPage'
import { BookingsEventsPage } from '@/features/bookings/BookingsEventsPage'
import { BookingEditPage } from '@/features/bookings/BookingEditPage'
import { BookingMenuPage } from '@/features/bookings/BookingMenuPage'
import { BookingWorkbenchPage } from '@/features/bookings/BookingWorkbenchPage'
import { CustomerMasterPage } from '@/features/customers/CustomerMasterPage'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { MenuCostingMasterPage } from '@/features/menus/MenuCostingMasterPage'
import { MenuRecipeFormPage } from '@/features/menus/MenuRecipeFormPage'
import { PackageCataloguePage } from '@/features/packages/PackageCataloguePage'
import { PackageCourseRulesPage } from '@/features/packages/PackageCourseRulesPage'
import { PackageLocationsPage } from '@/features/packages/PackageLocationsPage'
import { PackageTierFormPage } from '@/features/packages/PackageTierFormPage'
import { NotImplementedPage } from '@/features/placeholders/NotImplementedPage'
import { SettingsPage } from '@/features/settings/SettingsPage'
import { AddonMasterPage } from '@/features/settings/addons/AddonMasterPage'

/**
 * Route table.
 *
 * The Dashboard, the Customer Master, the Menu & Food Costing screens, the
 * Packages & Buffets screens — including the Package Configuration Workbench
 * and its two workbenches — and the Bookings & Events Register with its
 * Reservation Workbench and its Menu editor are the implemented ones. Every
 * other navigation entry resolves to a labelled "not yet implemented" page
 * (AC-F13) — no dead links and no fabricated data.
 */
export function AppRoutes() {
  const { status } = useAuth()

  return (
    /*
     * The last-resort boundary. A failure in the application shell itself —
     * or in the route table — is caught here and shown as a message instead
     * of a blank white page.
     */
    <RouteErrorBoundary area="the application" className="p-gutter-mobile md:p-margin">
      <Routes>
        <Route
          path="/login"
          element={
            status === 'authenticated' ? (
              <Navigate to="/" replace />
            ) : (
              <AuthLayout>
                <LoginPage />
              </AuthLayout>
            )
          }
        />

        <Route
          element={
            <ProtectedRoute>
              <AppLayout />
            </ProtectedRoute>
          }
        >
          <Route index element={<DashboardPage />} />
          <Route path="bookings" element={<BookingsEventsPage />} />
          <Route path="bookings/new" element={<BookingWorkbenchPage />} />
          <Route path="bookings/:transactionId/edit" element={<BookingEditPage />} />
          <Route path="bookings/:transactionId/menu" element={<BookingMenuPage />} />
          <Route path="menus" element={<MenuCostingMasterPage />} />
          <Route path="menus/new" element={<MenuRecipeFormPage />} />
          <Route path="menus/:menuId/edit" element={<MenuRecipeFormPage />} />
          <Route path="packages" element={<PackageCataloguePage />} />
          <Route path="packages/new" element={<PackageTierFormPage />} />
          <Route path="packages/:packageId/edit" element={<PackageTierFormPage />} />
          <Route path="packages/:packageId/rules" element={<PackageCourseRulesPage />} />
          <Route path="packages/:packageId/locations" element={<PackageLocationsPage />} />
          <Route
            path="payments"
            element={<NotImplementedPage moduleName="Payments & Ledger" />}
          />
          <Route
            path="receivables"
            element={<NotImplementedPage moduleName="Accounts Receivable & SOA" />}
          />
          <Route
            path="calendar"
            element={<NotImplementedPage moduleName="Banquet Calendar" />}
          />
          <Route path="customers" element={<CustomerMasterPage />} />
          <Route path="reports" element={<NotImplementedPage moduleName="Reports & BEO" />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="settings/addons" element={<AddonMasterPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </RouteErrorBoundary>
  )
}
