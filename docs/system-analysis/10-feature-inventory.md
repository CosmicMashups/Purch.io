# Purch.io — Exhaustive Implemented Feature Inventory

| Feature ID | Module | Feature Name | Entry Point (UI / Client) | Backend Controller / Route | Primary Entities / Tables | Required Roles | Confidence & Implementation Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **AUTH-01** | Auth | Counter Staff PIN Login | `/login` (Device PIN pad) | `POST /auth/login` | `Device`, `User`, `RefreshToken` | Anonymous (Rate limited) | **CONFIRMED** (`AuthEndpoints.cs`, `LoginService.cs`) |
| **AUTH-02** | Auth | Web Admin Login | `/login` (Email + Password tab) | `POST /auth/admin-login` | `User`, `RefreshToken` | Anonymous (Rate limited) | **CONFIRMED** (`AuthEndpoints.cs`, `LoginService.cs`) |
| **AUTH-03** | Auth | Token Refresh & Rotation | Axios interceptor / Riverpod | `POST /auth/refresh` | `RefreshToken` | Anonymous | **CONFIRMED** (`TokenRefreshService.cs`, SHA-256 hashed) |
| **AUTH-04** | Auth | User Logout | App header / Settings | `POST /auth/logout` | `RefreshToken` | Anonymous | **CONFIRMED** (Revokes token hash) |
| **AUTH-05** | Auth | Password Reset Request | `/login` (Forgot Password) | `POST /auth/password-reset/request` | `PasswordResetToken`, `User` | Anonymous | **CONFIRMED** (`PasswordResetService.cs`, 204 constant time) |
| **AUTH-06** | Auth | Password Reset Confirm | Reset link token landing | `POST /auth/password-reset/confirm` | `PasswordResetToken`, `User` | Anonymous | **CONFIRMED** (`PasswordResetService.cs`, BCrypt password update) |
| **ONBD-01** | Onboarding | Public Tenant Bootstrap | `/onboarding` (Wizard) | `POST /onboarding/bootstrap` | `Tenant`, `Branch`, `Device`, `User` | Anonymous | **CONFIRMED** (`BootstrapTenantService.cs`) |
| **ONBD-02** | Staff | Staff Account List | `/business/staff` | `GET /staff` | `User` | Admin, Manager | **CONFIRMED** (`StaffService.cs`, Tenant/Branch scope filtered) |
| **ONBD-03** | Staff | Create Staff Account | `/business/staff` (Add Modal) | `POST /staff` | `User` | Admin | **CONFIRMED** (`StaffService.cs`, BCrypt PIN hashing) |
| **ONBD-04** | Staff | Update Staff Account | `/business/staff` (Edit Modal) | `PUT /staff/{id}` | `User` | Admin | **CONFIRMED** (`StaffService.cs`, Role & Branch scope updates) |
| **ONBD-05** | Branch | Branch Directory List | `/business/branches` | `GET /branches` | `Branch`, `Device` | Any Staff | **CONFIRMED** (`BranchService.cs`) |
| **ONBD-06** | Branch | Create Branch | `/business/branches` (New) | `POST /branches` | `Branch` | Admin | **CONFIRMED** (`BranchService.cs`) |
| **ONBD-07** | Branch | Hardware Settings Update | `/business/branches` (HW) | `PUT /branches/{id}/hardware-settings`| `Branch` | Admin | **CONFIRMED** (Receipt printer, scale, drawer config) |
| **ONBD-08** | Branch | Manual GCash QR Upload | `/business/branches` (QR) | `PUT /branches/{id}/manual-gcash-qr` | `Branch` | Admin | **CONFIRMED** (Countertop QR image URL) |
| **ONBD-09** | Branch | Branch Departments | `/business/branches` (Depts) | `GET/POST /branches/{id}/departments`| `Department` | Admin, Manager | **CONFIRMED** (`DepartmentService.cs`) |
| **ONBD-10** | Device | Device Provisioning List | `/business/devices` | `GET /devices` | `Device` | Admin | **CONFIRMED** (`DeviceManagementService.cs`) |
| **ONBD-11** | Device | Create New Device | `/business/devices` (Add) | `POST /devices` | `Device` | Admin | **CONFIRMED** (`PairingCodeGenerator.cs`) |
| **ONBD-12** | Device | Reset Pairing Code | `/business/devices` (Regen) | `POST /devices/{id}/reset-pairing-code`| `Device` | Admin | **CONFIRMED** (Invalidates active sessions) |
| **ONBD-13** | Device | Reset Device PIN | `/business/devices` (Reset) | `POST /devices/{id}/reset-pairing-pin` | `Device` | Admin | **CONFIRMED** (Updates BCrypt PairingPinHash) |
| **ONBD-14** | Settings | Tenant Settings Query | `/business/settings` | `GET /tenant/settings` | `Tenant` | Admin | **CONFIRMED** (`TenantSettingsService.cs`) |
| **ONBD-15** | Settings | Update Theme & Branding | `/business/settings` (Branding)| `PUT /tenant/settings/branding` | `Tenant` | Admin | **CONFIRMED** (Colors, logo URL, font family) |
| **ONBD-16** | Settings | Update BIR Compliance | `/business/settings` (BIR) | `PUT /tenant/settings/bir` | `Tenant` | Admin | **CONFIRMED** (TIN, registered name & address) |
| **ONBD-17** | Settings | Toggle Barcode Rule | `/business/settings` (Barcode)| `PUT /tenant/settings/barcode` | `Tenant` | Admin | **CONFIRMED** (RequiresBarcodePerItem toggle) |
| **ONBD-18** | Settings | Credit Ledger Retention | `/business/settings` (Credit)| `PUT /tenant/settings/credit-ledger` | `Tenant` | Admin | **CONFIRMED** (Retention days & enable toggle) |
| **ONBD-19** | Settings | Inventory Tracking Mode | `/business/settings` (Recipe)| `PUT /tenant/settings/inventory-tracking`| `Tenant` | Admin | **CONFIRMED** (UseSeparateInventoryTracking) |
| **ONBD-20** | Audit | Audit Log Search | `/business/audit-log` | `GET /audit-logs` | `AuditLog`, `User` | Admin, Manager | **CONFIRMED** (`AuditLogQueryService.cs`, Cursor paging) |
| **CAT-01** | Catalog | List Categories | `/catalog/categories` | `GET /categories` | `Category` | Any Staff | **CONFIRMED** (Supports ETag / 304 caching) |
| **CAT-02** | Catalog | Create Category | `/catalog/categories` (Add) | `POST /categories` | `Category` | Admin, Manager | **CONFIRMED** (`CategoryService.cs`) |
| **CAT-03** | Catalog | Update Category | `/catalog/categories` (Edit)| `PUT /categories/{id}` | `Category` | Admin, Manager | **CONFIRMED** (`CategoryService.cs`) |
| **CAT-04** | Catalog | List Master Items | `/catalog/items` | `GET /items` | `Item`, `Category`, `ItemVariant` | Any Staff | **CONFIRMED** (`ItemService.cs`, Search, Category filter) |
| **CAT-05** | Catalog | Create Item | `/catalog/items/new` | `POST /items` | `Item` | Admin, Manager | **CONFIRMED** (Enforces barcode policy) |
| **CAT-06** | Catalog | Update Item | `/catalog/items/{id}/edit` | `PUT /items/{id}` | `Item` | Admin, Manager | **CONFIRMED** (`ItemService.cs`) |
| **CAT-07** | Catalog | Modifier Groups List | `/catalog/modifier-groups` | `GET /modifier-groups` | `ModifierGroup`, `ItemModifier` | Any Staff | **CONFIRMED** (`ModifierGroupService.cs`) |
| **CAT-08** | Catalog | Create Modifier Group | `/catalog/modifier-groups` | `POST /modifier-groups` | `ModifierGroup` | Admin, Manager | **CONFIRMED** (AllowMultiple, IsRequired) |
| **CAT-09** | Catalog | Add Item Modifier Option| `/catalog/modifier-groups` | `POST /modifier-groups/{id}/modifiers`| `ItemModifier` | Admin, Manager | **CONFIRMED** (Name, PriceDelta) |
| **CAT-10** | Catalog | Link Modifiers to Item | `/catalog/items/{id}/modifier-groups`| `POST /items/{id}/modifier-groups` | `ItemModifierGroup` | Admin, Manager | **CONFIRMED** (`ItemModifierGroupService.cs`) |
| **CAT-11** | Catalog | Tingi / Sachet Config | `/catalog/items/{id}/tingi-config`| `PUT /items/{id}/tingi-config` | `Item` | Admin, Manager | **CONFIRMED** (TingiMode, UnitsPerPack) |
| **CAT-12** | Catalog | Service Duration Config| `/catalog/items/{id}/service-duration`| `PUT /items/{id}/service-duration`| `Item` | Admin, Manager | **CONFIRMED** (ServiceDurationMinutes) |
| **CAT-13** | Catalog | Assign Item Department | `/catalog/items/{id}/department` | `PUT /items/{id}/department` | `Item`, `Department` | Admin, Manager | **CONFIRMED** (DepartmentId linkage) |
| **CAT-14** | Catalog | Low Stock Threshold | `/catalog/items/{id}/low-stock-threshold`| `PUT /items/{id}/low-stock-threshold`| `Item` | Admin, Manager | **CONFIRMED** (Per-item trigger quantity) |
| **CAT-15** | Catalog | Item Batches / Lots | `/catalog/items/{id}/batches` | `GET/POST /items/{id}/batches` | `ItemBatch` | Admin, Manager | **CONFIRMED** (`ItemBatchService.cs`, LotNumber, Expiry) |
| **CAT-16** | Catalog | Bundle Promo Rules | `/catalog/items/{id}/bundle-rules` | `GET/POST /items/{id}/bundle-rules` | `BundlePromoRule` | Admin, Manager | **CONFIRMED** (`BundlePromoRuleService.cs`) |
| **CAT-17** | Catalog | Variant Matrix | `/catalog/items/{id}/variants` | `GET/POST /items/{id}/variants` | `ItemVariant` | Admin, Manager | **CONFIRMED** (`ItemVariantService.cs`, Attributes JSON) |
| **CAT-18** | Catalog | Combo Meal Slots | `/catalog/items/{id}/combo-components`| `GET/POST /items/{id}/combo-components`| `ItemComboComponent` | Admin, Manager | **CONFIRMED** (SlotLabel, Upcharges) |
| **PROMO-01**| Promos | Promo Codes CRUD | `/business/promotions` (Codes)| `GET/POST /promo-codes` | `PromoCode` | Admin, Manager | **CONFIRMED** (`PromoCodeService.cs`, DiscountType, Value) |
| **PROMO-02**| Promos | Buy-1-Take-1 (BOGO) Rules| `/business/promotions` (BOGO) | `GET/POST/PUT /promos/bogo` | `BogoPromoRule` | Admin, Manager | **CONFIRMED** (`BogoPromoRuleService.cs`) |
| **PROMO-03**| Promos | Combo Deals CRUD | `/business/promotions` (Combos)| `GET/POST/PUT /promos/combos` | `ComboPromoRule` | Admin, Manager | **CONFIRMED** (`ComboPromoRuleService.cs`, 2-item flat price) |
| **PROMO-04**| Promos | Item Discount Rules | `/business/promotions` (Discounts)| `GET/POST/PUT /promos/item-discounts`| `ItemDiscountPromoRule` | Admin, Manager | **CONFIRMED** (Percentage, Fixed, or Override) |
| **POS-01** | POS | Device Cart Engine | `/sell` (Cashier Grid / Cart)| `GET /transactions/cart` | `Transaction`, `TransactionLine`| Cashier, Manager, Admin | **CONFIRMED** (`TransactionService.cs`, 1 cart/device) |
| **POS-02** | POS | Add Item Line to Cart | `/sell` (Tap Item / Scan Barcode)| `POST /transactions/cart/lines` | `TransactionLine` | Cashier, Manager, Admin | **CONFIRMED** (Handles variants, combos, modifiers) |
| **POS-03** | POS | Update Line Quantity | `/sell` (Qty adjust in Cart) | `PUT /transactions/cart/lines/{id}` | `TransactionLine` | Cashier, Manager, Admin | **CONFIRMED** (Recalculates totals & promos) |
| **POS-04** | POS | Remove Cart Line | `/sell` (Trash icon in Cart) | `DELETE /transactions/cart/lines/{id}`| `TransactionLine` | Cashier, Manager, Admin | **CONFIRMED** (`RemoveLineAsync`) |
| **POS-05** | POS | Apply Promo Code | `/sell` (Promo input) | `PUT /transactions/cart/promo-code` | `Transaction`, `PromoCode` | Cashier, Manager, Admin | **CONFIRMED** (Validates expiry & status) |
| **POS-06** | POS | Senior / PWD Discount | `/sell` (Senior/PWD button) | `PUT /transactions/cart/senior-pwd-discount`| `Transaction` | Manager, Admin | **CONFIRMED** (BIR mandated 20% + VAT exempt) |
| **POS-07** | POS | Set Order Type | `/sell` (Dine In / Take Out)| `PUT /transactions/cart/order-type` | `Transaction` | Cashier, Manager, Admin | **CONFIRMED** (OrderType string) |
| **POS-08** | POS | Void Open Cart | `/sell` (Void Cart button) | `POST /transactions/cart/void` | `Transaction`, `AuditLog` | Manager, Admin | **CONFIRMED** (Supervisor authorization required) |
| **POS-09** | POS | Record Cart Payment | `/sell/payment` (Tender) | `POST /transactions/cart/payments` | `Payment` | Cashier, Manager, Admin | **CONFIRMED** (Cash, GCash, Bank, Utang) |
| **POS-10** | POS | Receipt Number Sequence | `/sell` (Header sequence) | `GET /transactions/receipt-sequence`| `ReceiptSequence` | Cashier, Manager, Admin | **CONFIRMED** (Branch & Device scoped counter) |
| **POS-11** | POS | Idempotent Checkout | `/sell/payment` (Complete Sale)| `POST /transactions/checkout` | `Transaction`, `Payment`, `Inventory`| Cashier, Manager, Admin | **CONFIRMED** (`ClientSaleId` prevents double charge) |
| **POS-12** | POS | Post-Sale Refund | Sales History / Audit | `POST /transactions/{id}/refund` | `Transaction`, `Inventory`, `Audit` | Manager, Admin | **CONFIRMED** (Restores stock, supervisor PIN) |
| **POS-13** | POS | List Pending Kiosk Orders| `/sell/kiosk-orders` | `GET /transactions/kiosk-pending` | `Transaction` | Cashier, Manager, Admin | **CONFIRMED** (Branch-filtered queue) |
| **POS-14** | POS | Claim Kiosk Order | `/sell/kiosk-orders` (Claim)| `POST /transactions/kiosk-pending/{id}/claim`| `Transaction` | Cashier, Manager, Admin | **CONFIRMED** (Binds kiosk order to POS cart) |
| **SHIFT-01**| Shift | Query Current Shift | `/sell/shift` | `GET /shifts/current` | `Shift` | Cashier, Manager, Admin | **CONFIRMED** (`ShiftService.cs`) |
| **SHIFT-02**| Shift | Open Cash Shift | `/sell/shift` (Open Drawer) | `POST /shifts/open` | `Shift`, `AuditLog` | Cashier, Manager, Admin | **CONFIRMED** (Opening cash float declaration) |
| **SHIFT-03**| Shift | Close Cash Shift | `/sell/shift` (End Shift) | `POST /shifts/close` | `Shift`, `AuditLog` | Cashier, Manager, Admin | **CONFIRMED** (Rate limited, Manager PIN on variance) |
| **SHIFT-04**| Shift | Manual Drawer Open | `/sell/shift` (Pop Drawer) | `POST /shifts/manual-drawer-open` | `AuditLog` | Cashier, Manager, Admin | **CONFIRMED** (Logs reason, triggers RJ11 kick) |
| **INV-01** | Inventory | Stock Dashboard Summary | `/inventory` | `GET /inventory/dashboard` | `Item`, `InventoryItem` | Warehouse, Manager, Admin | **CONFIRMED** (SKUs, low stock, out of stock) |
| **INV-02** | Inventory | Stock Movement Log | `/inventory/movements` | `GET /inventory/movements` | `InventoryMovement` | Warehouse, Manager, Admin | **CONFIRMED** (Cursor paging, type & branch filter) |
| **INV-03** | Inventory | Record Stock Adjustment | `/inventory/movements/new` | `POST /inventory/movements` | `InventoryMovement`, `Stock`| Warehouse, Manager, Admin | **CONFIRMED** (Damage, Waste, Loss, Count) |
| **INV-04** | Inventory | Raw Ingredients Master | `/inventory/ingredients` | `GET/POST /inventory-items` | `InventoryItem` | Warehouse, Manager, Admin | **CONFIRMED** (`InventoryItemService.cs`) |
| **INV-05** | Inventory | Physical Count Recount | `/inventory/ingredients` (Count)| `POST /inventory-items/{id}/physical-count`| `InventoryItem`, `Movement` | Warehouse, Manager, Admin | **CONFIRMED** (Direct stock correction) |
| **INV-06** | Inventory | Receive Ingredient Delivery| `/inventory/ingredients` (Receive)| `POST /inventory-items/{id}/receive` | `InventoryItem`, `Movement` | Warehouse, Manager, Admin | **CONFIRMED** (Packaging quantity conversion) |
| **INV-07** | Inventory | Recipe / BOM Linkage | `/catalog/items/{id}/recipe` | `GET/PUT /items/{id}/recipe` | `ItemRecipeLine` | Warehouse, Manager, Admin | **CONFIRMED** (QuantityPerOrder definition) |
| **INV-08** | Inventory | Supplier Directory | `/inventory/suppliers` | `GET/POST /suppliers` | `Supplier` | Warehouse, Manager, Admin | **CONFIRMED** (`SupplierService.cs`) |
| **INV-09** | Inventory | Purchase Order Directory| `/inventory/purchase-orders` | `GET/POST /purchase-orders` | `PurchaseOrder`, `POLine` | Warehouse, Manager, Admin | **CONFIRMED** (Status lifecycle management) |
| **INV-10** | Inventory | Mark PO Sent | `/inventory/purchase-orders` (Send)| `POST /purchase-orders/{id}/mark-sent`| `PurchaseOrder` | Warehouse, Manager, Admin | **CONFIRMED** (Draft -> Sent) |
| **INV-11** | Inventory | Cancel Purchase Order | `/inventory/purchase-orders` (Cancel)| `POST /purchase-orders/{id}/cancel` | `PurchaseOrder` | Warehouse, Manager, Admin | **CONFIRMED** (Terminal state) |
| **INV-12** | Inventory | Receive PO Stock Lines | `/inventory/purchase-orders` (Receive)| `POST /purchase-orders/{id}/receive` | `PurchaseOrder`, `Movement`| Warehouse, Manager, Admin | **CONFIRMED** (Partial & full receipt) |
| **INV-13** | Inventory | Branch Transfer List | `/inventory/transfers` | `GET/POST /branch-transfers` | `BranchTransfer`, `Lines` | Warehouse, Manager, Admin | **CONFIRMED** (`BranchTransferService.cs`) |
| **INV-14** | Inventory | Dispatch Transfer | `/inventory/transfers` (Dispatch)| `POST /branch-transfers/{id}/mark-in-transit`| `BranchTransfer` | Warehouse, Manager, Admin | **CONFIRMED** (Decrements origin branch stock) |
| **INV-15** | Inventory | Receive Transfer Stock | `/inventory/transfers` (Receive)| `POST /branch-transfers/{id}/mark-received` | `BranchTransfer` | Warehouse, Manager, Admin | **CONFIRMED** (Increments dest branch stock) |
| **INV-16** | Inventory | Cancel Branch Transfer | `/inventory/transfers` (Cancel)| `POST /branch-transfers/{id}/cancel` | `BranchTransfer` | Warehouse, Manager, Admin | **CONFIRMED** (Reverts in-transit stock) |
| **CRED-01** | Credit | Customer Accounts List | `/business/customers` | `GET /credit-ledger` | `CustomerCreditLedger` | Cashier, Manager, Admin | **CONFIRMED** (`CustomerCreditLedgerService.cs`) |
| **CRED-02** | Credit | Create Customer Account | `/business/customers` (New) | `POST /credit-ledger` | `CustomerCreditLedger` | Manager, Admin | **CONFIRMED** (Name, Phone, CreditLimit) |
| **CRED-03** | Credit | Record Repayment | `/business/customers` (Repay)| `POST /credit-ledger/{id}/payments` | `CreditTransaction` | Cashier, Manager, Admin | **CONFIRMED** (Deducts balance, updates date) |
| **CRED-04** | Credit | Due Date Reminders | `/business/customers` (Remind)| `GET /credit-ledger/reminders` | `CustomerCreditLedger` | Manager, Admin | **CONFIRMED** (Days threshold query) |
| **CRED-05** | Credit | Adjust Credit Limit | `/business/customers` (Limit)| `PUT /credit-ledger/{id}/credit-limit` | `CustomerCreditLedger` | Manager, Admin | **CONFIRMED** (Requires supervisor role) |
| **CRED-06** | Credit | NPC DPA Anonymize | `/business/customers` (Erase)| `POST /credit-ledger/{id}/anonymize` | `CustomerCreditLedger` | Manager, Admin | **CONFIRMED** (Blocked if balance > 0) |
| **REP-01** | Reports | Generate BIR X-Reading | `/business/reports` (X-Reading)| `POST /reports/x-reading` | `Transaction`, `ReceiptSequence`| Manager, Admin | **CONFIRMED** (Mid-shift fiscal summary) |
| **REP-02** | Reports | Generate BIR Z-Reading | `/business/reports` (Z-Reading)| `POST /reports/z-reading` | `ReceiptSequence`, `Shift`| Manager, Admin | **CONFIRMED** (Daily fiscal reset counter) |
| **REP-03** | Reports | Sales Performance Dash | `/` & `/business/reports` | `GET /reports/sales-dashboard` | `Transaction` | Manager, Admin | **CONFIRMED** (Total sales, top items, trends) |
| **REP-04** | Reports | Inventory Movement Summ| `/business/reports` (Movements)| `GET /reports/inventory/movement-summary`| `InventoryMovement` | Manager, Admin | **CONFIRMED** (`InventoryReportService.cs`) |
| **REP-05** | Reports | Low Stock Reorder Export| `/business/reports` (Export) | `GET /reports/inventory/low-stock-export.csv`| `Item`, `InventoryItem` | Manager, Admin | **CONFIRMED** (Direct CSV streaming) |
| **REP-06** | Reports | Sales Transactions Export| `/business/reports` (Sales CSV)| `GET /reports/sales/transactions-export.csv`| `Transaction` | Admin | **CONFIRMED** (Admin-only raw CSV download) |
| **REP-07** | Reports | Staff Performance Metrics| `/business/reports` (Staff) | `GET /reports/staff-performance` | `Transaction`, `User` | Manager, Admin | **CONFIRMED** (Volume & average transaction value) |
| **REP-08** | Reports | Department Sales Attrib | `/business/reports` (Depts) | `GET /reports/department-sales` | `TransactionLine`, `Item` | Manager, Admin | **CONFIRMED** (Concessionaire breakdown) |
| **KIOSK-01**| Kiosk | Kiosk Device Session | `/kiosk/pair` | `POST /kiosk/session` | `Device` | Anonymous | **CONFIRMED** (`KioskSessionService.cs`, 24h JWT) |
| **KIOSK-02**| Kiosk | Kiosk Cart Management | `/kiosk/cart` | `GET/POST/PUT/DELETE /kiosk/cart/*`| `Transaction`, `Lines` | Kiosk Role | **CONFIRMED** (Server-backed pricing engine) |
| **KIOSK-03**| Kiosk | Submit Kiosk Prep Ticket| `/kiosk/done` | `POST /kiosk/cart/submit` | `Transaction`, `KioskPrepSeq`| Kiosk Role | **CONFIRMED** (Allocates prep number) |
| **KIOSK-04**| Kiosk | Kiosk Branding & Posters| `/kiosk` | `GET /kiosk/branding` | `Tenant` | Kiosk Role | **CONFIRMED** (16:9 hero poster & theme) |
| **DISP-01** | Displays | Order Board Session | `/order-board/pair` | `POST /order-board/session` | `Device` | Anonymous | **CONFIRMED** (`UnattendedSessionService.cs`) |
| **DISP-02** | Displays | Order Board Pending List| `/order-board` | `GET /order-board/pending` | `Transaction` | OrderBoard Role | **CONFIRMED** (5s polling queue) |
| **DISP-03** | Displays | Kitchen Display Session | `/kitchen/pair` | `POST /kitchen-display/session` | `Device` | Anonymous | **CONFIRMED** (24h Unattended JWT) |
| **DISP-04** | Displays | Kitchen Pending Orders | `/kitchen` | `GET /kitchen-display/pending` | `Transaction`, `Lines` | KitchenDisplay Role | **CONFIRMED** (Shows modifiers & combo slots) |
| **DISP-05** | Displays | Advance Kitchen Status | `/kitchen` (Status button) | `PUT /kitchen-display/orders/{id}/status`| `Transaction` | KitchenDisplay Role | **CONFIRMED** (Queued -> Preparing -> Ready) |
| **SYNC-01** | Sync | Batch Mutation Ingestion| Client background coordinator| `POST /sync` | `SyncedRecord` | Any Staff | **CONFIRMED** (`SyncService.cs`, Idempotency key) |
| **SYNC-02** | Sync | Query Flagged Conflicts | `/business/sync-conflicts` | `GET /sync/flagged` | `SyncedRecord` | Manager, Admin | **CONFIRMED** (Timestamp collision audit) |
| **SYNC-03** | Sync | Acknowledge Conflict | `/business/sync-conflicts` (Ack) | `POST /sync/flagged/{id}/acknowledge`| `SyncedRecord` | Manager, Admin | **CONFIRMED** (Sets `ReviewedAt = UtcNow`) |
| **UPLD-01** | Uploads | Upload Tenant Image | Form file picker (Item/Brand)| `POST /uploads/image` | File System (`wwwroot/uploads`) | Admin, Manager, Warehouse | **CONFIRMED** (5MB limit, PNG/JPEG/WEBP/GIF) |
