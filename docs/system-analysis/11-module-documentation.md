# Purch.io — Detailed Module Technical Documentation

---

## 1. Authentication & Identity Management

### Module Purpose
Provides secure token issuance, session maintenance, privilege verification, and device binding for both attended point-of-sale registers and unattended self-service screens.

### Actors
* **Cashier / Floor Staff**: Authenticates via physical terminal pairing code and personal 4-6 digit numeric PIN.
* **Store Manager / Supervisor**: Authenticates via PIN on terminals or credentials on web.
* **Business Admin**: Authenticates via email and password on web; manages accounts and password resets.
* **Unattended Terminals**: Kiosk, Kitchen Display, and Order Board devices authenticate using device pairing credentials.

### Screens / Routes
* Web: `/login` (Dual mode: PIN keypad or Email/Password tab), `/legal/:document`.
* Flutter: `/login` (Riverpod `authGateProvider` redirecting to `/home` or `/cashier`).

### Key API Endpoints
* `POST /auth/login` (DevicePairingCode, Pin) -> `{ accessToken, refreshToken }`
* `POST /auth/admin-login` (Email, Password) -> `{ accessToken, refreshToken }`
* `POST /auth/refresh` (RefreshToken) -> `{ accessToken, refreshToken }`
* `POST /auth/logout` (RefreshToken) -> `204 NoContent`
* `POST /auth/password-reset/request` (Email) -> `204 NoContent` (Constant time, prevents enumeration)
* `POST /auth/password-reset/confirm` (Token, NewPassword) -> `204 NoContent`

### Business & Validation Rules
* **Device Scoping**: PIN authentication is scoped to the tenant owning the physical device. PINs are stored as BCrypt hashes.
* **Rate Limiting**: `RateLimiterPolicies.AuthSensitive` enforces a fixed window rate limit on login attempts to mitigate brute-force attacks.
* **Security Responses**: Ambiguous credential failure responses (`401 Unauthorized` with identical message) to prevent device enumeration.
* **Token Lifecycles**:
  * Staff & Admin Access Tokens: 30 minutes.
  * Unattended Kiosk / Display Tokens: 24 hours.
  * Refresh Tokens: Cryptographic random base64 string stored as a SHA-256 hash in the database, invalidated immediately upon use (single-flight rotation).

---

## 2. Onboarding & Multi-Tenant Administration

### Module Purpose
Handles public business registration (tenant bootstrap), hierarchical branch creation, department attribution, device provisioning, and global tenant-level configuration.

### Actors
* **Business Owner / Admin**: Has complete authority over tenant settings, branding, branch configuration, and hardware assignment.
* **Store Manager**: Views branches, staff accounts, and audit history.

### Screens / Routes
* Web: `/onboarding` (Public 3-step wizard), `/business`, `/business/settings`, `/business/branches`, `/business/staff`, `/business/devices`, `/business/audit-log`.
* Flutter: `/business`, `/business/branches`, `/business/staff`, `/business/devices`, `/business/settings`, `/business/audit-log`.

### Key API Endpoints
* `POST /onboarding/bootstrap`: Creates `Tenant`, initial `Branch`, primary `Device`, and root `User` (Admin) atomically.
* `GET/POST /staff`, `PUT /staff/{id}`: Staff directory and scope assignment (Tenant, Branch, or Department).
* `GET/POST /branches`, `PUT /branches/{id}/hardware-settings`, `PUT /branches/{id}/manual-gcash-qr`.
* `GET/POST /devices`, `POST /devices/{id}/reset-pairing-code`, `POST /devices/{id}/reset-pairing-pin`.
* `GET/PUT /tenant/settings`: Granular update endpoints for branding, BIR compliance details, barcode enforcement, credit ledger, and recipe tracking.
* `GET /audit-logs`: Cursor-paginated audit trail.

### Business & Validation Rules
* **Data Isolation**: Multi-tenancy is enforced through `PurchDbContext` global query filters (`e.TenantId == _currentTenantProvider.TenantId`).
* **Deployment Mode Stamp**: Tenants inherit the deployment mode (`Cloud` or `Local`) of the running host environment (`IDeploymentContext`).
* **Hardware Setting Storage**: Hardware configuration records peripheral profiles (e.g. ReceiptPrinterProfile, Scale unit mode) without executing low-level native drivers server-side.
* **Pairing Invalidation**: Resetting a device pairing code or PIN immediately revokes all issued refresh tokens associated with that `DeviceId`.

---

## 3. Catalog & Multi-Vertical Pricing Engine

### Module Purpose
Enables unified multi-vertical merchandise and service modeling across retail, grocery, hospitality, and salons without codebase fragmentation.

### Supported Pricing Models
1. **Standard Retail Unit Pricing**: Base price per SKU with barcode lookup.
2. **Variant Matrices**: Multi-attribute matrix (Size, Color, Material) stored in `VariantAttributesJson` with independent stock counts and price overrides.
3. **Tingi / Fractional Packaging**: Sachet pricing vs bulk unit packaging (e.g., selling 1 piece from a 24-piece box) with automated inventory conversion.
4. **Combo Meals / Slot Builder**: Predefined meal slots (e.g., Main, Side, Drink) with category restrictions, default items, and substitution upcharge deltas.
5. **Mix-and-Match Bundles**: Multi-buy volume pricing (e.g., "Buy any 3 for ₱100").
6. **Timed Services**: Duration-based pricing (in minutes) with resource/staff assignment.
7. **Perishable Batches / Lots**: Lot number and expiry date logging with FIFO stock deduction.

### Screens / Routes
* Web: `/catalog/items`, `/catalog/items/new`, `/catalog/items/:id/edit`, `/catalog/categories`, `/catalog/modifier-groups`, and sub-pages for batches, tingi, bundles, service-duration, variants, combos, and recipes.
* Flutter: `/inventory/items`, `/inventory/categories`, `/inventory/modifier-groups`.

### Key API Endpoints
* `GET /items`, `POST /items`, `PUT /items/{id}`
* `GET /categories`, `POST /categories`, `PUT /categories/{id}`
* `GET /modifier-groups`, `POST /modifier-groups`, `POST /modifier-groups/{id}/modifiers`
* `GET/POST /items/{id}/variants`, `GET/POST /items/{id}/combo-components`, `GET/POST /items/{id}/batches`
* `PUT /items/{id}/tingi-config`, `PUT /items/{id}/service-duration`, `PUT /items/{id}/department`, `PUT /items/{id}/low-stock-threshold`

---

## 4. Point of Sale (POS) & Checkout Engine

### Module Purpose
Provides low-latency cashier checkout, cart manipulation, discount stacking, multi-tender payment recording, sequential receipt issuance, and supervisor overrides.

### Actors
* **Cashier**: Conducts sales, scans barcodes, applies customer discounts, tenders cash/GCash.
* **Supervisor / Manager**: Authorizes voiding open carts, post-sale refunds, and Senior Citizen / PWD discounts.

### Key API Endpoints
* `GET /transactions/cart`: Retrieves or creates the single open transaction associated with the calling device.
* `POST /transactions/cart/lines`: Appends an item line with modifier selections and combo components.
* `PUT /transactions/cart/lines/{id}`: Adjusts quantity or price.
* `DELETE /transactions/cart/lines/{id}`: Removes a line.
* `POST /transactions/cart/void`: Voids the open cart (Requires Admin/Manager).
* `POST /transactions/checkout`: Atomic, one-call checkout endpoint. Receives full transaction state, lines, and payment list. Idempotent based on `ClientSaleId`.
* `POST /transactions/{id}/refund`: Reverses a completed sale, decrements cash totals, and restores stockOnHand.
* `GET /transactions/receipt-sequence`: Fetches current sequence state for the terminal.
* `GET /transactions/kiosk-pending`, `POST /transactions/kiosk-pending/{id}/claim`: Allows cashiers to pull orders submitted from self-service kiosks.

### Business Rules & Calculation Formulas
1. **Line Total Calculation**:
   $$\text{LineTotal} = \text{Quantity} \times (\text{UnitPrice} + \sum \text{ModifierPriceDeltas} + \sum \text{ComboUpcharges})$$
2. **Promotions Application Pass**:
   Evaluates active Buy-1-Take-1 (BOGO), combo bundle deals, and item discount rules. Uses a claimed-quantity mechanism so that units discounted by one rule are not double-discounted by another.
3. **BIR Mandated Senior Citizen / PWD Discount**:
   $$\text{VatableSales} = \frac{\text{GrossAmount}}{1.12}$$
   $$\text{DiscountAmount} = \text{VatableSales} \times 0.20$$
   $$\text{NetPayable} = \text{VatableSales} - \text{DiscountAmount}$$
   *Requires supervisor authorization if applied directly by a cashier.*
4. **Idempotent Checkout**: Every checkout request includes a unique `ClientSaleId` UUID. If the server receives a duplicate submission due to a retried network request, it returns the existing completed transaction without creating duplicate lines, deducting inventory twice, or incrementing the receipt counter.

---

## 5. Shift Management & Cash Drawer Reconciliation

### Module Purpose
Enforces cash control, audit-logged manual drawer pops, opening float declaration, and blind-count closing cash reconciliation.

### Workflows & Endpoints
* `GET /shifts/current`: Returns active open shift for current device.
* `POST /shifts/open`: Creates open shift with declared `OpeningCashAmount`.
* `POST /shifts/manual-drawer-open`: Logs reason for manual drawer kickout (`AuditActionType.ManualDrawerOpen`).
* `POST /shifts/close`: Closes shift. Computes:
  $$\text{ExpectedCash} = \text{OpeningCash} + \sum \text{CashPayments} - \sum \text{CashRefunds}$$
  $$\text{Variance} = \text{ClosingCashAmount} - \text{ExpectedCash}$$
  If variance is non-zero, requiring supervisor approval via `ApprovedByUserId`.

---

## 6. Inventory & Recipe / BOM (Bill of Materials)

### Module Purpose
Tracks raw ingredients and resale items, manages supplier purchase orders, processes physical counts, and executes multi-branch stock transfers.

### Key Workflows
* **Raw Ingredient Tracking**: Separate catalog of raw items (`InventoryItem`) measured in bulk units (e.g. grams, milliliters, packaging boxes).
* **Automated Sale Consumption**: When `UseSeparateInventoryTracking` is enabled for the tenant, completing a sale does not decrement the menu item, but iterates through linked `ItemRecipeLines` and decrements each ingredient's `StockOnHand` by:
  $$\text{Deduction} = \text{SaleQuantity} \times \text{QuantityPerOrder}$$
* **Purchase Order Lifecycle**:
  `Draft` $\rightarrow$ `Sent` $\rightarrow$ `PartiallyReceived` / `Received` / `Cancelled`. Receiving updates stock and creates `InventoryMovement` of type `StockIn`.
* **Branch Transfers**:
  Dispatches stock from origin branch (`MovementType.TransferOut`) and receives into destination branch (`MovementType.TransferIn`).

---

## 7. Customer Credit Ledger (*Utang*)

### Module Purpose
Enables sari-sari stores and neighborhood groceries to maintain credit tabs for trusted customers in compliance with Philippine SME customs and the National Privacy Commission (NPC) Data Privacy Act.

### Workflows & Security
* **Credit Limit Verification**: POS checkout blocks `PaymentMethod.Credit` if $\text{CurrentBalance} + \text{SaleAmount} > \text{CreditLimit}$.
* **Aging & Reminders**: Tracks `NextPaymentDue` and generates reminder lists via `GET /credit-ledger/reminders`.
* **NPC Anonymization**: Under ADR 0006 and DPA regulations, customers can request data erasure (`POST /credit-ledger/{id}/anonymize`). The system enforces that customer PII (Name, Phone) can only be scrubbed if $\text{CurrentBalance} == 0$.

---

## 8. BIR Compliance & Fiscal Reporting

### Module Purpose
Provides continuous compliance with Bureau of Internal Revenue (BIR) POS/CRM regulations regarding sequential invoice generation, machine identification numbers, and non-resettable fiscal accumulators.

### Implemented Reports
* **X-Reading (`POST /reports/x-reading`)**: Generates mid-shift fiscal summary including gross sales, VAT-exempt sales, 12% VAT amount, Senior/PWD discounts, returns, voids, and payment method breakdown without resetting sequence counters.
* **Z-Reading (`POST /reports/z-reading`)**: Generates end-of-day fiscal report. Commits closing daily sales into `GrandAccumulatedSales`, increments `ZReadingResetCounter`, and resets the daily active receipt window.
* **Data Exports**:
  * Low stock reorder list as CSV (`GET /reports/inventory/low-stock-export.csv`).
  * Raw transactions sales ledger as CSV (`GET /reports/sales/transactions-export.csv`, Admin only).

---

## 9. Unattended Displays & Kiosk Subsystem

### Module Purpose
Drives customer self-service terminals, kitchen display boards, and customer queue boards using dedicated unattended device tokens.

### Screens & Endpoints
* **Portrait Self-Service Kiosk**:
  * Session Init: `POST /kiosk/session` (device pairing code + PIN).
  * Cart Pipeline: `POST /kiosk/cart/lines`, `PUT /kiosk/cart/order-type`, `POST /kiosk/cart/submit`.
  * Completion: Displays large prep sequence number (`KioskPrepNumber`), auto-resets after 30 seconds.
* **Kitchen Display System (KDS)**:
  * Session Init: `POST /kitchen-display/session`.
  * Polling: `GET /kitchen-display/pending` (every 5s).
  * Status Lifecycle: `PUT /kitchen-display/orders/{id}/status` (`Queued` $\rightarrow$ `Preparing` $\rightarrow$ `Ready` $\rightarrow$ `PickedUp`).
* **Order Status Board**:
  * Session Init: `POST /order-board/session`.
  * Display: `GET /order-board/pending` renders "Now Preparing" and "Now Ready" tickets.

---

## 10. Offline Synchronization Engine

### Module Purpose
Guarantees uninterrupted cashier operations during network or internet outages through client-side SQLite queuing and server-side conflict resolution.

### Sync Pipeline (`POST /sync`)
* **Mutation Batch**: Clients submit arrays of queued operations containing `IdempotencyKey`, `EntityType`, `EntityId`, and `ClientTimestamp`.
* **Idempotency Protection**: If an idempotency key already exists in `SyncedRecords`, the server acknowledges the record without re-executing business logic.
* **Conflict Auto-Resolution**: When a mutation modifies an entity that was already modified by another device with a later timestamp, the server auto-cancels the outdated operation and flags the record (`FlaggedForReview = true`).
* **Supervisor Review**: Flagged conflicts appear on `/business/sync-conflicts` for manager acknowledgment via `POST /sync/flagged/{id}/acknowledge`.
