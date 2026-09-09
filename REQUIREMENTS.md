# Purch.io — Functional & Non-Functional Requirements

## Functional Requirements (FR)

### Onboarding & Configuration
- FR1: Admin can select business type, which pre-configures relevant modules
- FR2: Admin can upload logo, set theme color, select font — applied globally to POS and receipts
- FR3: Admin can add/manage branches and pair devices
- FR4: Admin can create staff accounts with role assignment (Admin/Manager/Cashier/Warehouse)

### Catalog Management
- FR5: Admin can add/edit/deactivate items with name, price, image, category
- FR6: System supports multiple pricing types per item: unit, weight/volume, bundle, service, combo, variant-matrix
- FR7: Admin can define combo components (restaurant/café mode) or size/color variants (retail mode)
- FR8: Admin can create modifier groups (add-ons, extras) reusable across items
- FR9: Admin can scan barcodes to populate/find items

### Point of Sale
- FR10: Cashier can browse items by category and add to cart via tap
- FR11: Cashier can customize an item at point of sale (combo slot selection, variant selection, modifiers)
- FR12: Cashier can apply discounts, promo codes, and Senior Citizen/PWD discount with correct recalculation
- FR13: Cashier can accept payment via cash (with change calculation), card, GCash/Maya, QR Ph, bank transfer, split payment
- FR14: System generates and prints/sends a sequential, BIR-compliant receipt per transaction
- FR15: Manager can void a transaction or apply a post-sale discount/refund (permissioned, logged)

### Inventory
- FR16: Storekeeper can record stock-in, stock-out, consumption, spoiled, damaged, for-return, and transfer movements, each with reason/note
- FR17: System auto-decrements stock on sale completion
- FR18: System flags low-stock and out-of-stock items
- FR19: Admin/storekeeper can transfer stock between branches with status tracking (multi-branch, phase 2)
- FR20: System supports batch/lot + expiry tracking for perishable items (grocery/convenience, phase 2)

### Offline & Sync
- FR21: System continues to process sales when offline, queuing transactions locally
- FR22: System syncs queued transactions automatically on reconnect
- FR23: System resolves sync conflicts by auto-cancelling the later-timestamped conflicting record and flagging it for review

### Reporting
- FR24: Manager can view daily/weekly/monthly sales summaries and top-selling items
- FR25: Manager can generate shift/cash-drawer reconciliation reports
- FR26: Manager can generate Z-reading/X-reading reports in BIR format
- FR27: Manager can view inventory movement reports filtered by type/date/branch
- FR28: Manager can view staff sales performance reports

### Access & Audit
- FR29: System enforces role-based permissions server-side on every action
- FR30: System logs every void, refund, discount override, price override, and inventory adjustment with actor and timestamp

### Kiosk (phase 2)
- FR31: Customer can self-order via kiosk (browse, customize, pay) without cashier involvement
- FR32: Kiosk displays queue/order number after payment

---

## Non-Functional Requirements (NFR)

### Performance
- NFR1: A sale transaction (item tap to cart update) must complete in under ~300ms on low/mid-spec Android tablets
- NFR2: Sync of a full day's queued offline transactions must complete within a few seconds of reconnect on typical PH mobile data speeds, not just Wi-Fi

### Reliability & Offline Resilience
- NFR3: No transaction may be silently lost — every write is durable locally before being considered "complete," even offline
- NFR4: System must remain fully usable for core sales functions with zero internet connectivity for a full shift, not just a few minutes

### Security
- NFR5: All data in transit encrypted (HTTPS/TLS)
- NFR6: All transaction/payment data at rest on-device encrypted
- NFR7: Server-side authorization on every endpoint — client-side role hiding is not a security boundary
- NFR8: Session auto-expiry/auto-logout after configurable idle period on shared devices

### Compliance
- NFR9: Receipt numbering must be sequential and gap-detectable per branch/device (BIR requirement)
- NFR10: Data retention and handling must comply with the Data Privacy Act (NPC) — non-optional once the utang/credit ledger or any customer PII is stored
- NFR11: Senior Citizen/PWD discount calculation must match legally mandated formatting, not just a generic percentage-off

### Usability
- NFR12: Core sales flow must be operable by a first-time, non-technical user within minutes, with no training manual required — testable, not assumed
- NFR13: UI must meet minimum touch-target size (~48dp) and contrast standards for fast, error-resistant tapping in a busy counter environment

### Scalability
- NFR14: Backend must support multi-tenant data isolation without cross-tenant data leakage, on a shared-database architecture
- NFR15: System must handle multiple devices per branch writing concurrently without data corruption

### Maintainability/Portability
- NFR16: Vertical-specific behavior must be addable via configuration (`business_type`, `pricing_type`, `feature_flags`) without forking the codebase
- NFR17: Hosting must be swappable from free-tier to paid-tier providers without application-level rework (config-only migration)

### Availability
- NFR18: Backend API uptime target — undefined until a client is signed and an SLA exists to hold; flagged as an open gap

---

**Status note:** Several NFRs (concurrent-device load, real sync latency on PH mobile networks, actual usability with a non-technical tester) can only be verified with real devices and a real user, not on paper.
