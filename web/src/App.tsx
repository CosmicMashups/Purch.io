import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './layouts/AppShell/AppShell';
import { RequireAuth } from './features/auth/RequireAuth';
import { RequireTab } from './features/auth/RequireTab';
import { LoginPage } from './features/auth/LoginPage';
import { InventoryHomePage } from './features/inventory/pages/InventoryHomePage';
import { IngredientsPage } from './features/inventory/pages/IngredientsPage';
import { MovementLogPage } from './features/inventory/pages/MovementLogPage';
import { PurchaseOrdersPage } from './features/inventory/pages/PurchaseOrdersPage';
import { RecordMovementPage } from './features/inventory/pages/RecordMovementPage';
import { SuppliersPage } from './features/inventory/pages/SuppliersPage';
import { TransfersPage } from './features/inventory/pages/TransfersPage';
import { PromotionsPage } from './features/promotions/PromotionsPage';
import { CustomersPage } from './features/credit/CustomersPage';
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
import { ModifierGroupsPage } from './features/catalog/pages/ModifierGroupsPage';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>

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
            <Route path="/business/promotions" element={<PromotionsPage />} />
            <Route path="/business/customers" element={<CustomersPage />} />
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
