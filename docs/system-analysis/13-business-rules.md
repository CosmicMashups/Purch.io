# Purch.io — Implemented Business Rules Specification

---

## 1. Multi-Tenancy & Data Security Rules

1. **Global Query Isolation**: Every tenant query in EF Core automatically evaluates `e.TenantId == CurrentTenantProvider.TenantId`. A request lacking a valid `tenant_id` claim is aborted with `401 Unauthorized` or `403 Forbidden`.
2. **Deployment Mode Immutability**: A tenant created in Cloud mode can never declare itself Local, and vice-versa. Stamped from server configuration upon bootstrap (`BootstrapTenantService`).
3. **Audit Immutability**: `AuditLog` entries are append-only. There is no update or delete endpoint for audit records in the entire API.

---

## 2. Authentication & Credential Policies

4. **PIN Complexity Rule**: Numeric PINs must be between 4 and 6 digits. Obvious sequential combinations (`1234`, `12345`, `123456`) and uniform repeated digits (`0000`, `1111`, `9999`) are strictly rejected (`PinPolicy.Validate`).
5. **Password Complexity Rule**: Passwords must be at least 8 characters long, contain at least one uppercase letter, and contain at least one numeric digit (`PasswordPolicy.Validate`).
6. **Credential Enumeration Defense**: Device code and PIN validation failures return identical HTTP status codes and error messages (`401 Unauthorized: The device pairing code or PIN was not recognized`) regardless of whether the device code or the PIN was incorrect.
7. **Single-Use Refresh Tokens**: Redeeming a refresh token immediately records its `RevokedAt` timestamp. Re-presenting an already revoked refresh token invalidates the entire session chain.

---

## 3. Pricing, Discounts & Promotions Rules

8. **Claimed-Quantity Promo Pass**: Automatic promotion rules (BOGO, Combo Bundles, Item Discounts) claim quantities sequentially to prevent a single item unit from receiving multiple promotional discounts.
9. **Senior Citizen / PWD BIR Mandated Calculation**:
   * Gross transaction total is first divided by 1.12 to strip the 12% Value Added Tax (VAT exemption).
   * A 20% discount is applied to the vatable base amount.
   * Total net payable = $\text{VatableBase} \times 0.80$.
10. **Discount Authorization Boundary**: Only users with `Admin` or `Manager` roles can apply Senior Citizen / PWD discounts or promo code overrides directly to an active cashier cart.
11. **Tingi Sachet Decoupling**: Tingi packaging calculates unit retail price based on `UnitsPerPack`. Stock decrements can deduct either whole packaging boxes or fractional counts based on configured `TingiMode`.

---

## 4. Point of Sale & Cashiering Rules

12. **Single Open Cart per Device**: At any given time, a physical device (`DeviceId`) can have at most one transaction with `TransactionStatus.Open`. Attempting to create a second cart returns the existing open cart.
13. **Idempotent Checkout**: `POST /transactions/checkout` accepts a client-generated GUID `SaleId`. If a checkout request is re-sent with the same `SaleId`, the backend returns the existing transaction without executing duplicate stock deductions, payments, or receipt sequence increments.
14. **Cart Voiding Authority**: Only users in `Admin` or `Manager` roles can execute `POST /transactions/cart/void`. Cashiers attempting to void a cart receive `403 Forbidden`.
15. **Post-Sale Refund Authority**: Refunding a completed transaction (`POST /transactions/{id}/refund`) requires supervisor authorization, logs a mandatory reason into `AuditLog`, returns the tender to cash totals, and restores item inventory quantities.
16. **Sequential Receipt Integrity**: Receipt numbers are strictly sequential per branch and device. Numbering never skips digits and cannot be decremented or manually overridden.

---

## 5. Cash Drawer & Shift Management Rules

17. **Shift Gate for Cashiers**: Cashiers cannot ring up transactions without an active open shift (`ShiftStatus.Open`) tied to their device.
18. **Blind Cash Audit**: When closing a shift, the cashier must declare the physically counted drawer cash without the system disclosing the calculated expected cash upfront.
19. **Variance Supervisor Approval**: If $\text{ClosingCash} \ne \text{ExpectedCash}$, the system mandates an `ApprovedByUserId` supervisor PIN to commit the shift closure.
20. **Audit-Logged Manual Drawer Pops**: Any manual cash drawer kickout outside of a cash sale must record an explicit reason (e.g., "Change Dispense", "Manager Audit") and write an `AuditLog` entry.

---

## 6. Inventory & Recipe / BOM Rules

21. **Decoupled Recipe Consumption**: If `Tenant.UseSeparateInventoryTracking` is enabled:
    * Selling a menu item does not alter the menu item's inventory.
    * The system resolves all linked `ItemRecipeLine` records and auto-decrements raw `InventoryItem.StockOnHand`.
22. **Purchase Order State Transitions**:
    * `Draft` $\rightarrow$ `Sent` or `Cancelled`.
    * `Sent` $\rightarrow$ `PartiallyReceived`, `Received`, or `Cancelled`.
    * Once `Received` or `Cancelled`, a purchase order enters a terminal state and cannot be modified.
23. **Branch Transfer State Transitions**:
    * Dispatching a transfer (`mark-in-transit`) decrements stock from the source branch.
    * Receiving a transfer (`mark-received`) increments stock in the destination branch.
    * Cancelling an in-transit transfer restores stock back to the origin branch.

---

## 7. Customer Credit Ledger (*Utang*) Rules

24. **Credit Limit Hard Stop**: A customer credit sale is blocked if:
    $$\text{CurrentBalance} + \text{TransactionAmount} > \text{CreditLimit}$$
25. **Data Privacy Act (NPC) Anonymization Gate**: Under ADR 0006 and Republic Act 10173, a customer account can only be anonymized/erased if:
    $$\text{CurrentBalance} == 0$$
    Attempting to anonymize an account with an outstanding balance throws a `ConflictException`.

---

## 8. Offline Synchronization & Conflict Resolution Rules

26. **Idempotency Key Deduplication**: All synced records are logged in `SyncedRecords` by `IdempotencyKey`. Duplicate keys are acknowledged without re-applying mutations.
27. **Later Timestamp Auto-Cancellation**: If a synced record arrives with an entity mutation whose `ClientTimestamp` is earlier than an already committed change for that entity, the later-timestamped record wins; the out-of-order record is auto-cancelled and flagged for manager review (`FlaggedForReview = true`).
28. **Review Acknowledgment**: Flagged conflict records can only be cleared by a Manager or Admin via `POST /sync/flagged/{id}/acknowledge`.
