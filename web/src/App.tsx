import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './layouts/AppShell/AppShell';
import { RequireAuth } from './features/auth/RequireAuth';
import { RequireTab } from './features/auth/RequireTab';
import { LoginPage } from './features/auth/LoginPage';
import { LegalPage } from './features/onboarding/LegalPage';
import { OnboardingPage } from './features/onboarding/OnboardingPage';
import { HomePage } from './features/dashboard/HomePage';
import { InventoryHomePage } from './features/inventory/pages/InventoryHomePage';
import { IngredientsPage } from './features/inventory/pages/IngredientsPage';
import { MovementLogPage } from './features/inventory/pages/MovementLogPage';
import { PurchaseOrdersPage } from './features/inventory/pages/PurchaseOrdersPage';
import { RecordMovementPage } from './features/inventory/pages/RecordMovementPage';
import { SuppliersPage } from './features/inventory/pages/SuppliersPage';
import { TransfersPage } from './features/inventory/pages/TransfersPage';
import { ReportsPage } from './features/reports/ReportsPage';
import { PromotionsPage } from './features/promotions/PromotionsPage';
import { BusinessPage } from './features/business/BusinessPage';
import { CustomersPage } from './features/credit/CustomersPage';
import { SyncConflictsPage } from './features/sync/SyncConflictsPage';
import { AuditLogPage } from './features/business/AuditLogPage';
import { BranchesPage } from './features/business/BranchesPage';
import { DevicesPage } from './features/business/DevicesPage';
import { SettingsPage } from './features/business/SettingsPage';
import { StaffPage } from './features/business/StaffPage';
import { RequireRole } from './features/auth/RequireRole';
import { KioskOrdersPage } from './features/pos/KioskOrdersPage';
import { ShiftPage } from './features/shifts/ShiftPage';
import { PaymentPage } from './features/pos/PaymentPage';
import { ReceiptPage } from './features/pos/ReceiptPage';
import { SellPage } from './features/pos/SellPage';
import { ItemListPage } from './features/catalog/pages/ItemListPage';
import { AddItemPage } from './features/catalog/pages/AddItemPage';
import { EditItemPage } from './features/catalog/pages/EditItemPage';
import { BatchesPage } from './features/catalog/pages/BatchesPage';
import { TingiConfigPage } from './features/catalog/pages/TingiConfigPage';
import { BundleRulesPage } from './features/catalog/pages/BundleRulesPage';
import { ServiceDurationPage } from './features/catalog/pages/ServiceDurationPage';
import { VariantsPage } from './features/catalog/pages/VariantsPage';
import { ComboComponentsPage } from './features/catalog/pages/ComboComponentsPage';
import { ItemModifierGroupsPage } from './features/catalog/pages/ItemModifierGroupsPage';
import { AssignDepartmentPage } from './features/catalog/pages/AssignDepartmentPage';
import { LowStockThresholdPage } from './features/catalog/pages/LowStockThresholdPage';
import { RecipePage } from './features/catalog/pages/RecipePage';
import { CategoriesPage } from './features/catalog/pages/CategoriesPage';
import { DevicePairPage } from './features/kiosk/DevicePairPage';
import { KioskCartPage } from './features/kiosk/KioskCartPage';
import { KioskDonePage } from './features/kiosk/KioskDonePage';
import { KioskLandingPage } from './features/kiosk/KioskLandingPage';
import { KioskLayout } from './features/kiosk/KioskLayout';
import { KioskMenuPage } from './features/kiosk/KioskMenuPage';
import { KioskOrderTypePage } from './features/kiosk/KioskOrderTypePage';
import { KitchenDisplayPage } from './features/kiosk/KitchenDisplayPage';
import { OrderBoardPage } from './features/kiosk/OrderBoardPage';
import { RequireDevice } from './features/kiosk/RequireDevice';
import { ModifierGroupsPage } from './features/catalog/pages/ModifierGroupsPage';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/onboarding" element={<OnboardingPage />} />
      <Route path="/legal/:document" element={<LegalPage />} />

      <Route path="/kiosk/pair" element={<DevicePairPage role="Kiosk" />} />
      <Route path="/kitchen/pair" element={<DevicePairPage role="KitchenDisplay" />} />
      <Route path="/order-board/pair" element={<DevicePairPage role="OrderBoard" />} />

      <Route element={<RequireDevice role="Kiosk" />}>
        <Route element={<KioskLayout />}>
          <Route path="/kiosk" element={<KioskLandingPage />} />
          <Route path="/kiosk/menu" element={<KioskMenuPage />} />
          <Route path="/kiosk/cart" element={<KioskCartPage />} />
          <Route path="/kiosk/order-type" element={<KioskOrderTypePage />} />
          <Route path="/kiosk/done" element={<KioskDonePage />} />
          <Route path="/kiosk/*" element={<Navigate to="/kiosk" replace />} />
        </Route>
      </Route>
      <Route element={<RequireDevice role="KitchenDisplay" />}>
        <Route path="/kitchen" element={<KitchenDisplayPage />} />
      </Route>
      <Route element={<RequireDevice role="OrderBoard" />}>
        <Route path="/order-board" element={<OrderBoardPage />} />
      </Route>

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<HomePage />} />

          <Route element={<RequireTab tab="sell" />}>
            <Route path="/sell" element={<SellPage />} />
            <Route path="/sell/shift" element={<ShiftPage />} />
            <Route path="/sell/kiosk-orders" element={<KioskOrdersPage />} />
            <Route path="/sell/payment" element={<PaymentPage />} />
            <Route path="/sell/receipt" element={<ReceiptPage />} />
            <Route path="/sell/*" element={<Navigate to="/sell" replace />} />
          </Route>

          <Route element={<RequireTab tab="inventory" />}>
            <Route path="/inventory" element={<InventoryHomePage />} />
            <Route path="/inventory/movements" element={<MovementLogPage />} />
            <Route path="/inventory/movements/new" element={<RecordMovementPage />} />
            <Route path="/inventory/ingredients" element={<IngredientsPage />} />
            <Route path="/inventory/suppliers" element={<SuppliersPage />} />
            <Route path="/inventory/purchase-orders" element={<PurchaseOrdersPage />} />
            <Route path="/inventory/transfers" element={<TransfersPage />} />
            <Route path="/inventory/*" element={<Navigate to="/inventory" replace />} />
          </Route>

          <Route element={<RequireTab tab="business" />}>
            <Route path="/business" element={<BusinessPage />} />
            <Route path="/business/promotions" element={<PromotionsPage />} />
            <Route path="/business/reports" element={<ReportsPage />} />
            <Route path="/business/customers" element={<CustomersPage />} />
            <Route path="/business/sync-conflicts" element={<SyncConflictsPage />} />
            <Route path="/business/staff" element={<StaffPage />} />
            <Route path="/business/branches" element={<BranchesPage />} />
            <Route path="/business/audit-log" element={<AuditLogPage />} />
            <Route element={<RequireRole allow={['Admin']} />}>
              <Route path="/business/devices" element={<DevicesPage />} />
              <Route path="/business/settings" element={<SettingsPage />} />
            </Route>
            <Route path="/catalog/items" element={<ItemListPage />} />
            <Route path="/catalog/items/new" element={<AddItemPage />} />
            <Route path="/catalog/items/:itemId/edit" element={<EditItemPage />} />
            <Route path="/catalog/items/:itemId/batches" element={<BatchesPage />} />
            <Route path="/catalog/items/:itemId/tingi-config" element={<TingiConfigPage />} />
            <Route path="/catalog/items/:itemId/bundle-rules" element={<BundleRulesPage />} />
            <Route path="/catalog/items/:itemId/service-duration" element={<ServiceDurationPage />} />
            <Route path="/catalog/items/:itemId/variants" element={<VariantsPage />} />
            <Route path="/catalog/items/:itemId/combo-components" element={<ComboComponentsPage />} />
            <Route path="/catalog/items/:itemId/modifier-groups" element={<ItemModifierGroupsPage />} />
            <Route path="/catalog/items/:itemId/department" element={<AssignDepartmentPage />} />
            <Route path="/catalog/items/:itemId/low-stock-threshold" element={<LowStockThresholdPage />} />
            <Route path="/catalog/items/:itemId/recipe" element={<RecipePage />} />
            <Route path="/catalog/categories" element={<CategoriesPage />} />
            <Route path="/catalog/modifier-groups" element={<ModifierGroupsPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
