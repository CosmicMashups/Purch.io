# Purch.io — Executive System Overview & Architecture Summary

## 1. System Identity and Stated Purpose

**Purch.io** is an enterprise-grade, configuration-driven Point-of-Sale (POS), back-office inventory, customer credit ledger (*utang*), and self-service kiosk system designed specifically for the Philippine SME (Small and Medium Enterprises) and retail ecosystem. 

Rather than maintaining separate vertical forks or fragmented codebases for convenience stores, quick-service restaurants (QSR) / cafés, retail apparel boutiques, groceries, or appointment-based service salons, Purch.io implements a **unified, multi-vertical pricing and transaction engine** coupled with an opt-in Recipe/BOM (Bill of Materials) ingredient tracking model.

## 2. Primary Users & Personas

1. **Cashier / Floor Staff**: Operates standard POS cash registers, scans items via barcode or touch grid, applies discounts, executes shift drawer operations, and tenders multi-method payments.
2. **Manager / Supervisor**: Authorizes privileged operations (voids, post-sale refunds, manual drawer opens, Senior/PWD discounts), performs shift reconciliation, monitors X/Z readings, and oversees credit limits.
3. **Admin / Business Owner**: Configures tenant settings (branding, theme, fonts, BIR details, barcode enforcement, credit ledger retention), manages branches, provisions hardware/devices, creates staff, and oversees multi-branch sales exports.
4. **Warehouse Officer / Storekeeper**: Manages raw ingredient catalogs, registers supplier Purchase Orders, performs receiving against open POs, logs stock movements (damages, spoilage, adjustments), and initiates/receives branch transfers.
5. **Customer (Walk-up)**: Interacts with the portrait self-service kiosk terminal to browse menu categories, customize items, select order types (Dine In / Take Out), and dispatch prep tickets.
6. **Kitchen / Counter Staff**: Monitors unattended Kitchen Displays and Order Boards to transition orders from Queued to Preparing, Ready, and Picked Up.

## 3. High-Level Architectural Style & Technology Stack

The system follows a clean, decoupled, layered service architecture:

* **Backend**:
  * **Framework**: ASP.NET Core 9 Minimal APIs running on C# 12/.NET 9.
  * **Architecture**: Clean Architecture / Domain-Driven Design (DDD) with `Purch.Domain`, `Purch.Application`, `Purch.Infrastructure`, and `Purch.Api`.
  * **Persistence**: Entity Framework Core 9 (`PurchDbContext`) targeting PostgreSQL 16+.
  * **Multi-Tenancy**: Tenant isolation enforced at the data layer via JWT claim inspection (`ICurrentTenantProvider`) and EF Core Global Query Filters (`e.TenantId == CurrentTenantId`).
  * **Authentication**: Stateless HMAC-SHA256 JWT bearer tokens (Staff/Admin = 30-min expiry; Unattended Kiosk/Display = 24-hr expiry), paired with cryptographic SHA-256 hashed refresh tokens and BCrypt hashed PINs/passwords.
  * **Security & Rate Limiting**: ASP.NET Core built-in concurrency and fixed-window rate limiting on authentication, refresh, password reset, and shift close operations.
* **Clients**:
  * **Flutter Native Client** (`client/`): Multi-platform client (Windows Desktop, Android Tablet, Linux/macOS) built on Flutter 3.24+, Riverpod state management, `go_router`, and an embedded Drift SQLite database with background sync coordination for zero-latency offline resilience.
  * **Web Client** (`web/`): Modern web application built on React 19, TypeScript, Vite, Tailwind CSS 4, react-router 7, TanStack Query 5, and Zustand stores. Operates primarily online against the server-backed cart pipeline.

## 4. Key Deployment Modes

* **Cloud Mode (`DeploymentMode.Cloud`)**:
  * Multi-tenant shared backend hosted on containerized infrastructure (Render / Vercel), backed by managed PostgreSQL (Supabase / Render Postgres).
  * Tenant isolation strictly enforced through JWT claims and EF Core global query filters.
* **Local Mode (`DeploymentMode.Local`)**:
  * Dedicated, single-tenant installation running on an on-premises LAN server (Docker or Windows Service).
  * Runs automatic migrations upon boot, allowing POS terminals on the local subnet to operate even if outside internet connectivity is severed.

## 5. Major Modules

1. **Authentication & Identity**: Dual-login pipeline (Device Pairing Code + Numeric PIN for counter staff; Email + Password for web administrators).
2. **Onboarding & Multi-Tenant Management**: Business onboarding bootstrap wizard, branch management, device provisioning, dynamic branding, and BIR registration setup.
3. **Unified Catalog & Pricing**: Support for standard unit items, variant matrices, fractional tingi/weight/volume items, combo meal slots with substitution upcharges, bundles, and timed services.
4. **Promotions & Discounts**: Code-based promo rules and automated no-code promotions (BOGO, Combo Bundle, Item Discounts) alongside BIR-mandated Senior Citizen / PWD discounts.
5. **POS & Cashier Engine**: Device-bound cart lifecycle, item customization, one-call checkout, payment processing (Cash, Bank Transfer, QR Ph, Utang), receipt sequencing, and kiosk order claiming.
6. **Shift Management & Cash Drawer**: Shift opening, mid-shift manual drawer pop logging, closing cash reconciliation, and automated variance calculation.
7. **Inventory & Recipe / BOM**: Ingredient-level tracking, recipe bill-of-materials consumption, supplier purchase orders, stock movement audits, and inter-branch transfers.
8. **Customer Credit Ledger (*Utang*)**: Philippine SME informal credit tracking, payment schedules, credit limits, reminder notifications, and NPC Data Privacy Act compliance (anonymization).
9. **BIR Compliance & Fiscal Reporting**: Sequential invoice tracking, tamper-evident reset counters, X-Readings (mid-shift summary), and Z-Readings (daily fiscal reset).
10. **Unattended Displays & Kiosk**: Customer self-service ordering kiosk, Kitchen Display System (KDS), and public Order Status Board.
11. **Offline Synchronization Engine**: `SyncedRecord` idempotency ledger, client mutation queue, and conflict auto-resolution (later timestamp auto-cancels and flags for supervisor review).
