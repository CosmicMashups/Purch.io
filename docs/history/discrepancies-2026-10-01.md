# Purch.io — Code vs. Documentation Discrepancy Report

An exhaustive audit cross-referencing markdown documentation (`README.md`, `PRODUCT.md`, `PAGES.md`, `REQUIREMENTS.md`, `ARCHITECTURE.md`, `docs/adr/*`) against the actual executable codebase (`Purch.Api`, `Purch.Application`, `Purch.Domain`, `Purch.Infrastructure`, `client/`, `web/`) identified the following key discrepancies.

---

## 1. Five-Tab Navigation vs. Four-Tab Implementation

* **Documentation Claim (`README.md`, `PAGES.md`)**:
  `README.md` states: *"Landscape Staff Shell: A role-filtered, five-tab bottom-navigation dashboard (Home, Sell, Reports, Inventory, Business)..."*
* **Actual Implementation (`app_router.dart`, `AppShellScreen.dart`)**:
  In `client/lib/core/routing/app_router.dart` (lines 53-57), the codebase explicitly documents and implements:
  > *"The Reports tab was removed: its four screens were flat text reports, and Home now renders the same data as charts. The reports domain/data layer is unchanged — Home consumes it directly."*
  The staff shell contains exactly **four** branches: `/home`, `/cashier`, `/inventory`, and `/business`. The web client (`AppShell.tsx`) similarly groups reports under Business (`/business/reports`) and charts on Home (`/`).
* **Verdict**: **IMPLEMENTED CODE IS AUTHORITATIVE**. The five-tab claim in the root README is outdated.

---

## 2. Offline Sales on Web Client

* **Documentation Claim (`REQUIREMENTS.md` FR21/NFR4)**:
  Specifies continuous offline sales and queuing across all clients.
* **Actual Implementation (`REACT-MIGRATION.md` Decision 1)**:
  Offline sales are **deliberately blocked** on the web client (`web/`). The web app does not contain a TypeScript port of `pricing_engine.dart` and depends strictly on the server-backed cart API (`/transactions/cart/*`). The native Flutter client (`client/`) alone implements client-side Drift SQLite persistence and the full offline pricing engine mirror.
* **Verdict**: **DOCUMENTED EXCEPTION / PLATFORM BOUNDARY**. As established in ADR 0003 and `REACT-MIGRATION.md`, the web client is designed for online administrative and counter use.

---

## 3. Scope of Live Offline Sync Integration (ADR 0003 Boundary)

* **Documentation Claim (`REQUIREMENTS.md` FR21–FR23)**:
  Suggests that all POS sales, inventory movements, and cashier transactions seamlessly write through an offline sync queue.
* **Actual Implementation (`backend/src/Purch.Api/Endpoints/SyncEndpoints.cs`, ADR 0003)**:
  ADR 0003 clarifies that while the server-side `SyncedRecord` conflict-resolution engine (`POST /sync`) and the client-side `SyncQueueDao`/`SyncCoordinator` are fully implemented, **existing live feature write calls (POS checkout, inventory movements) write directly to the backend over HTTP**. Retrofitting all write endpoints to write through the queue first remains an unstarted follow-up.
* **Verdict**: **CODE EVIDENCE IS AUTHORITATIVE**. The sync infrastructure is built and tested, but live checkout endpoints interact directly with the backend.

---

## 4. Bill Payment & E-Load Provider Integration (ADR 0004)

* **Documentation Claim (`PAGES.md` D5 Convenience Store Mode)**:
  Lists over-the-counter bill payments and telco prepaid e-load tabs as a cashier capability.
* **Actual Implementation**:
  ADR 0004 documents the selection of **Dragonpay (a Xendit company)** for bill payments and telco top-ups. However, in the executable backend (`Purch.Api`), there are no live Dragonpay HTTP client connectors; only the architectural decision and merchant sign-up requirement are tracked.
* **Verdict**: **DOCUMENTED_ONLY / BUSINESS REQUIREMENT**. The feature is designed and vendor-selected in ADR 0004, but no active endpoint exists in `backend/src/Purch.Api/Endpoints`.

---

## 5. BIR Compliance Readiness vs. Accreditation Review (ADR 0005)

* **Documentation Claim (`README.md`, `PRODUCT.md`)**:
  Promotes the system as "strict Bureau of Internal Revenue (BIR) compliance readiness."
* **Actual Implementation (`BirReadingService.cs`, ADR 0005)**:
  `BirReadingService.cs` generates X-readings and Z-readings with sequential invoice numbering, VAT calculations, and grand accumulators. However, ADR 0005 explicitly records:
  > *"Implemented as best-effort, NOT final... Every field mapping in the report-generation code carries a `// TODO(BIR-ACCREDITATION):` marker. Action required before any real/live deployment: Verify against actual BIR accreditation paperwork or an accountant/BIR consultant's input."*
* **Verdict**: **BEST-EFFORT IMPLEMENTATION PENDING OFFICIAL ACCREDITATION**.

---

## 6. NPC Registration & Credit Ledger PII (ADR 0006)

* **Documentation Claim (`REQUIREMENTS.md` NFR10)**:
  Requires full National Privacy Commission (NPC) compliance for customer credit ledger (*utang*) PII.
* **Actual Implementation (`CustomerCreditLedgerService.cs`, ADR 0006)**:
  Code-level protections are confirmed: minimal PII collection (Name and Phone only; no sensitive personal IDs), configurable retention auto-purge, and data erasure (`POST /credit-ledger/{id}/anonymize` blocked if balance > 0). ADR 0006 notes that legal organizational registration with the NPC is tracked externally.
* **Verdict**: **CONFIRMED & COMPLIANT AT CODE LEVEL**.
