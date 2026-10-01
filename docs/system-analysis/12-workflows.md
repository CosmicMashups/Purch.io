# Purch.io — End-to-End Workflow Specifications

---

## Workflow 1: Public Business Onboarding & Device Provisioning

```
Unregistered Merchant
        ↓
Navigates to /onboarding
        ↓
Step 1: Business Profile (Name, BusinessType enum, optional BIR TIN & Registered Name)
        ↓
Step 2: Initial Branch (Branch Name, Address)
        ↓
Step 3: Administrator Account (Admin Name, 4-6 digit Numeric PIN, Optional Email & Password)
        ↓
Accept Terms of Service & Privacy Policy checkbox
        ↓
Submit Form -> POST /onboarding/bootstrap
        ↓
Backend: Validate PIN policy (4-6 digits, not trivial like 1234 or 0000)
Backend: Validate Password policy (min 8 chars, 1 uppercase, 1 digit)
Backend: Create Tenant (DeploymentMode derived from server environment)
Backend: Create Branch
Backend: Create Device (Generate 8-char PairingCode)
Backend: Create User (Role = Admin, ScopeType = Tenant, BCrypt PinHash & PasswordHash)
        ↓
200 OK Response { tenantId, branchId, deviceId, pairingCode, adminId }
        ↓
UI Displays Success Dialog with Device Pairing Code in large monospace font
        ↓
Redirects to /login
```

---

## Workflow 2: Cashier Shift Opening, Sale Ring-up & Multi-Tender Checkout

```
Cashier Staff
        ↓
Selects Device Pairing Code & enters PIN on /login
        ↓
POST /auth/login -> Returns JWT Token with Role: Cashier, DeviceId, BranchId
        ↓
Redirected to /sell (Landscape POS Shell)
        ↓
System checks GET /shifts/current:
  [Case A: No Open Shift]
    UI prompts "Shift Required to Ring Up Sales"
    Cashier inputs Opening Cash Float (e.g. ₱1,000.00)
    POST /shifts/open -> Shift entity created (Status = Open, OpenedByUserId = Cashier)
  [Case B: Shift Active]
    Proceeds directly to Catalog Grid
        ↓
Cashier scans barcode OR taps category tile and item card
        ↓
[Optional Customization Modal]:
  - If Item has Modifiers: select single/multiple modifier options
  - If Item has Variants: select Size/Color attribute combination
  - If Item is Combo: select required slot items (e.g. Drink, Side) with upcharge deltas
  - If Item is Tingi / Fractional: enter weight or portion quantity
        ↓
Item added to Cart -> POST /transactions/cart/lines
        ↓
Pricing Engine runs:
  1. Base Line Total: Qty * (UnitPrice + Modifiers + Upcharges)
  2. Evaluates Automatic Promos (BOGO, 2-Item Combos, Item Discounts)
  3. Updates Cart Summary (Subtotal, ItemPromoDiscountAmount, Net Total)
        ↓
[Optional Discounts]:
  - Enter alphanumeric promo code -> PUT /transactions/cart/promo-code
  - Senior Citizen / PWD toggle -> PUT /transactions/cart/senior-pwd-discount (Requires Supervisor)
        ↓
Cashier taps "Proceed to Payment" -> Navigates to /sell/payment
        ↓
Select Payment Method(s):
  - Cash: Enter Amount Tendered -> UI calculates Change Due
  - Static GCash / QR Ph: Displays branch QR code -> Cashier confirms reference code
  - Bank Transfer: Cashier verifies receipt slip
  - Utang (Customer Credit): Selects customer from list -> Verifies Balance + Total <= Limit
        ↓
Cashier taps "Complete Checkout"
        ↓
POST /transactions/checkout:
  - Generates atomic sequential ReceiptNumber for (BranchId, DeviceId)
  - Commits Transaction (Status = Completed)
  - Records Payment rows
  - Checks Tenant.UseSeparateInventoryTracking:
      IF TRUE: Decrements linked InventoryItems via ItemRecipeLine quantities
      IF FALSE: Decrements ItemVariant or Item.StockOnHand directly
  - If Utang: Appends CreditTransaction record & increments customer balance
        ↓
Cashier terminal renders BIR receipt & sends ESC/POS print job to thermal printer
        ↓
UI resets cart state for Next Sale
```

---

## Workflow 3: Customer Self-Service Kiosk Ordering & Cashier Claim

```
Customer at Portrait Kiosk Terminal
        ↓
Terminal running in Kiosk Mode (/kiosk) with 24-hr Device Token
        ↓
Customer taps screen to start order -> Navigates to /kiosk/menu
        ↓
Customer browses visual categories and taps items
        ↓
Customer configures modifiers & meal slots in tactile dialog
        ↓
POST /kiosk/cart/lines -> Server calculates priced total
        ↓
Customer navigates to /kiosk/cart to review items
        ↓
Customer proceeds to /kiosk/order-type -> Selects "Dine In" or "Take Out"
        ↓
PUT /kiosk/cart/order-type
        ↓
Customer taps "Submit Order" -> POST /kiosk/cart/submit
        ↓
Backend:
  - Allocates next KioskPrepNumber from KioskPrepSequence table
  - Sets Transaction.OriginatedFromKiosk = true
  - Sets Transaction.KitchenStatus = Queued
  - Leaves Transaction.Status = Open (Awaiting counter payment)
        ↓
Kiosk screen displays /kiosk/done with large Prep Number (e.g. #042)
Kiosk terminal counts down 30 seconds and auto-resets to landing screen
        ↓
[Cashier Counter Claim Flow]:
Cashier on POS navigates to /sell/kiosk-orders (or sees badge notification)
        ↓
GET /transactions/kiosk-pending queries open kiosk orders for branch
        ↓
Cashier matches customer's prep ticket #042
        ↓
Cashier taps "Claim Order" -> POST /transactions/kiosk-pending/{id}/claim
        ↓
Backend transfers transaction ownership to cashier's active device cart
        ↓
Cashier collects payment (Cash/Card/GCash) and completes checkout
```

---

## Workflow 4: Kitchen Expediting & Order Board Flow

```
Completed Sale or Submitted Kiosk Ticket
        ↓
Kitchen Display System polling GET /kitchen-display/pending every 5 seconds
        ↓
New ticket appears in "Queued" column with prep number, order type, and item customizations
        ↓
Cook / Barista taps "Start Preparing"
        ↓
PUT /kitchen-display/orders/{id}/status { status: "Preparing" }
        ↓
Customer Order Board polling GET /order-board/pending every 5 seconds
        ↓
Order Board renders ticket number under "Preparing" header
        ↓
Cook finishes preparation and taps "Order Ready"
        ↓
PUT /kitchen-display/orders/{id}/status { status: "Ready" }
        ↓
Order Board moves ticket number to "Now Ready" column with chime notification
        ↓
Customer presents receipt / ticket at pickup counter
        ↓
Expediter taps "Order Picked Up"
        ↓
PUT /kitchen-display/orders/{id}/status { status: "PickedUp" }
        ↓
Ticket is archived and cleared from display queues
```

---

## Workflow 5: Purchase Order Receiving & Raw Ingredient Stock-In

```
Warehouse Officer / Storekeeper
        ↓
Navigates to /inventory/purchase-orders
        ↓
Taps "New Purchase Order" -> Selects Supplier, Destination Branch, and Line Items
        ↓
POST /purchase-orders -> Status: Draft
        ↓
Officer reviews quantities and expected unit cost -> Taps "Send to Supplier"
        ↓
POST /purchase-orders/{id}/mark-sent -> Status: Sent
        ↓
Supplier delivers physical shipment to warehouse
        ↓
Officer taps "Receive Delivery" on purchase order
        ↓
Modal opens with ordered items and input fields for "Quantity Received":
  - Officer enters actual counted quantities received
        ↓
POST /purchase-orders/{id}/receive:
  - Updates PurchaseOrderLine.QuantityReceived
  - For each line: Increments linked InventoryItem.StockOnHand
  - Creates InventoryMovement record (MovementType.StockIn, Reference: PO#)
  - Checks if all lines fully received:
      IF All Lines Received: Status = Received
      IF Partial Lines Received: Status = PartiallyReceived
        ↓
Inventory dashboard and recipe-derived stock levels automatically update
```

---

## Workflow 6: Shift Drawer Reconciliation & BIR Z-Reading Fiscal Reset

```
Shift Cashier / Store Manager
        ↓
Navigates to /sell/shift -> Taps "End Shift & Close Drawer"
        ↓
Blind Cash Count Modal:
  Cashier counts physical cash bills and coins in drawer
  Enters total counted cash (e.g. ₱5,450.00) and optional handover notes
        ↓
POST /shifts/close:
  - System sums opening float + all cash payments during shift - cash refunds
  - Computes Variance: CountedCash - ExpectedCash
  - IF Variance != 0:
      Requires Supervisor / Manager PIN verification (ApprovedByUserId)
  - Sets Shift.Status = Closed, Shift.ClosedAt = UtcNow
        ↓
[End-of-Day Fiscal Reset - BIR Z-Reading]:
Store Manager navigates to /business/reports -> Selects "BIR Readings"
        ↓
Manager taps "Generate Z-Reading" (Prompted with warning: "This resets the fiscal day")
        ↓
POST /reports/z-reading:
  - Fetches all transactions since LastZReadingReceiptNumber
  - Aggregates Gross Sales, Net Sales, 12% VAT, VAT Exempt, Senior/PWD Discounts, Voids, Returns
  - Updates ReceiptSequence:
      GrandAccumulatedSales += TodayGrossSales
      ZReadingResetCounter += 1
      LastZReadingReceiptNumber = CurrentLastReceiptNumber
      LastZReadingAt = UtcNow
        ↓
Generates official BIR Z-Reading document format
Manager prints physical copy to be filed in store fiscal logbook
```
