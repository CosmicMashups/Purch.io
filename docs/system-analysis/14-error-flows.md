# Purch.io — Explicit Error, Exception & Failure Flow Specifications

The Purch.io backend handles exceptions globally through `ExceptionHandlingMiddleware`, converting failures into RFC 7807 `ProblemDetails` JSON responses with consistent HTTP status codes.

---

## 1. Authentication & Security Exceptions

### 1.1 Invalid Device Pairing Code or Staff PIN
* **Trigger**: Cashier attempts login with an unassigned device pairing code or incorrect numeric PIN.
* **HTTP Status**: `401 Unauthorized`.
* **Payload**:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
    "title": "Invalid credentials.",
    "status": 401,
    "detail": "The device pairing code or PIN was not recognized."
  }
  ```
* **Recovery / Mitigation**: User re-enters PIN or uses the Device Provisioning screen to reset the pairing PIN.

### 1.2 Rate Limit Exceeded
* **Trigger**: More than allowable auth attempts within the fixed window (`RateLimiterPolicies.AuthSensitive`).
* **HTTP Status**: `429 Too Many Requests`.
* **Payload**: `ProblemDetails` with `Retry-After` header.

### 1.3 Expired or Revoked Refresh Token
* **Trigger**: Single-flight token refresh request with an expired token or already revoked token hash.
* **HTTP Status**: `401 Unauthorized`.
* **Client Behavior**: Clears `localStorage` / SecureStorage auth state and forces navigation to `/login`.

---

## 2. Authorization & RBAC Exceptions

### 2.1 Role Privilege Insufficient (`ForbiddenException`)
* **Trigger**: Cashier attempts privileged operation (e.g. `POST /transactions/cart/void`, `POST /transactions/{id}/refund`, `PUT /transactions/cart/senior-pwd-discount`, or `GET /reports/sales/transactions-export.csv`).
* **HTTP Status**: `403 Forbidden`.
* **Payload**:
  ```json
  {
    "title": "Forbidden",
    "status": 403,
    "detail": "Only a manager or admin can perform this action."
  }
  ```
* **UI Behavior**: Prompts for Manager/Supervisor PIN overlay to authorize the operation in-line.

### 2.2 Cross-Branch Scope Violation (`BranchScopeGuard`)
* **Trigger**: A user scoped to Branch A attempts to read or mutate inventory or shifts for Branch B.
* **HTTP Status**: `403 Forbidden`.
* **Detail**: User does not have access to the target branch scope.

---

## 3. POS & Cashier Transaction Failures

### 3.1 Cashier Checkout Without Active Shift
* **Trigger**: Cashier attempts `POST /transactions/checkout` when no active open shift exists for the terminal.
* **HTTP Status**: `400 Bad Request` or `ValidationException`.
* **Detail**: "An active shift must be open before processing transactions."
* **Recovery**: Redirects or displays modal to input opening float and call `POST /shifts/open`.

### 3.2 Customer Credit Limit Exceeded
* **Trigger**: Cashier selects `PaymentMethod.Credit` (*Utang*) when customer balance + transaction total exceeds `CreditLimit`.
* **HTTP Status**: `400 Bad Request` / `ValidationException`.
* **Detail**: "The customer's credit limit of ₱X,XXX.XX would be exceeded by this transaction."
* **UI Behavior**: Displays warning badge; allows customer to tender partial cash or select alternative payment.

### 3.3 Network Dropout During Checkout (Offline Resilience)
* **Trigger**: Internet or LAN connection drops while cashier taps "Complete Sale".
* **Flutter Client Behavior**:
  1. Transaction write fails network call.
  2. Client writes completed transaction to Drift SQLite local `pending_sync_queue`.
  3. UI generates local offline receipt using cached sequence counter.
  4. Background `SyncCoordinator` queues mutation and retries with exponential backoff.
* **Web Client Behavior**:
  1. Offline checkout is explicitly blocked (see ADR 0003 and `REACT-MIGRATION.md`).
  2. UI displays retry modal alerting user that internet connection is required.

---

## 4. Inventory & Supply Chain Exceptions

### 4.1 Modifying Terminal State Purchase Order
* **Trigger**: User calls `mark-sent`, `receive`, or `cancel` on a PO already in `PurchaseOrderStatus.Received` or `Cancelled`.
* **HTTP Status**: `409 Conflict` (`ConflictException`).
* **Detail**: "Cannot alter purchase order in its terminal status."

### 4.2 Branch Transfer Stock Depletion
* **Trigger**: Origin branch attempts `mark-in-transit` on stock transfer exceeding available `StockOnHand`.
* **HTTP Status**: `400 Bad Request`.
* **Detail**: "Insufficient stock on hand at origin branch to dispatch transfer."

---

## 5. Customer Privacy & Anonymization Exceptions

### 5.1 Anonymizing Customer with Outstanding Balance
* **Trigger**: Administrator attempts `POST /credit-ledger/{id}/anonymize` for a customer account with `CurrentBalance > 0`.
* **HTTP Status**: `409 Conflict` (`ConflictException`).
* **Detail**: "Customer cannot be anonymized while an outstanding balance remains."
* **Recovery**: Full repayment must be logged via `POST /credit-ledger/{id}/payments` before PII erasure can proceed.

---

## 6. Offline Sync Conflict Failures

### 6.1 Concurrent Device Update Collision
* **Trigger**: Device A and Device B mutate the same entity while offline. Device A syncs first with timestamp T2; Device B syncs later with timestamp T1 (where T1 < T2).
* **Server Action**:
  1. Device B's change is auto-cancelled to preserve the later state.
  2. Record is saved to `SyncedRecords` with `FlaggedForReview = true`.
* **Supervisor Action**:
  1. Supervisor navigates to `/business/sync-conflicts`.
  2. Reviews collision diff.
  3. Calls `POST /sync/flagged/{id}/acknowledge` to clear flag from review queue.
