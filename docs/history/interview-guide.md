# Purch.io — Full-Stack Flutter Developer Interview Guide & Project Overview

This guide is designed as an end-to-end reference for job interviews discussing **Purch.io**. It is structured to help you articulate architectural decisions, technical trade-offs, state management strategies, offline resilience, and full-stack integration clearly and confidently.

---

## 1. Executive Summary & Project Pitch

> **The 30-Second "Elevator Pitch":**  
> *"Purch.io is an enterprise-grade, config-driven POS and self-service kiosk platform built for the Philippine retail and service ecosystem. Instead of maintaining fragmented codebases or forks for supermarkets, cafés, and salons, Purch.io uses a unified data model driven by an item-level `pricing_type` and merchant `business_type`. The frontend is built in Flutter using Riverpod, Drift (SQLite), and GoRouter with role-aware dual form-factors (landscape staff tablet and portrait kiosk). The backend is built with ASP.NET Core (.NET 9) Minimal APIs following Clean Architecture with EF Core and PostgreSQL. It features offline-first local write queuing, server-enforced role and scope security, and Philippine-specific fiscal workflows like BIR X/Z-readings, Senior/PWD discounts, and Utang credit ledgers."*

---

## 2. High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      FRONTEND: Flutter (Dart 3.7+)                      │
├────────────────────────────────────┬────────────────────────────────────┤
│ Landscape Staff Shell (POS/Admin)  │ Portrait Customer Kiosk Terminal   │
│ - Role-filtered GoRouter Tabs      │ - Visual Category Carousels        │
│ - Fast-action touch targets (48dp+)| - Restricted, order-only flow      │
│ - Cashier, Inventory, Settings     │ - Claimed by Cashier at counter    │
├────────────────────────────────────┴────────────────────────────────────┤
│ State: Riverpod 2.x (Notifier / AsyncNotifier / CodeGen)                │
│ Offline Store: Drift (SQLite) with typed pending_sync_queue             │
│ Networking: Dio with Bearer interceptors & connectivity_plus            │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ JSON / HTTPS
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                   BACKEND: ASP.NET Core 9 (Clean Architecture)          │
├─────────────────────────────────────────────────────────────────────────┤
│ Purch.Api: Minimal API Route Groups, JWT Auth Middleware, Static Uploads│
│ Purch.Application: CQRS Commands/Queries, Domain Services, DTOs         │
│ Purch.Domain: Entities, Enums, Value Objects, Pricing Engine Logic      │
│ Purch.Infrastructure: EF Core 9 / Npgsql, Global Tenant Filters, Auth   │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     DATABASE & DEPLOYMENT ARCHITECTURE                  │
├────────────────────────────────────┬────────────────────────────────────┤
│ Cloud Mode                         │ Local Mode (Air-Gapped / On-Prem)  │
│ - Managed PostgreSQL (Supabase)    │ - Dedicated Postgres on Store LAN  │
│ - Shared DB with tenant_id filters │ - Self-migrating Windows / Docker  │
│ - Deployed on Render               │ - Zero internet dependency for POS │
└────────────────────────────────────┴────────────────────────────────────┘
```

---

## 3. Flutter & Client-Side Deep Dive

### 3.1 State Management: Riverpod 2.x
- **Why Riverpod over Bloc/Provider:** Compile-safe dependency injection, testability without widget tree context, automatic caching/disposal (`autoDispose`), and cleaner syntax using `NotifierProvider` and `AsyncNotifierProvider`.
- **Cart & POS State:** `cartNotifierProvider` manages the live order state in memory. Cashier grid and cart sidebar simultaneously watch the same notifier, eliminating complex widget callbacks or cross-widget event bus spaghetti.
- **Dynamic Theming:** `staffThemeProvider` listens to cached merchant branding (primary colors, contrast ratios) and re-themes the application at runtime when merchant settings change.

### 3.2 Dual Form-Factor & Role-Aware Routing (GoRouter)
- **Role-Filtered Bottom Navigation:** Staff shell uses `StatefulShellRoute.indexedStack`. What tabs render (Home, Sell, Inventory, Business) depends strictly on the logged-in user's role (`StaffRole`):
  - **Cashier:** Only sees Home + Sell.
  - **Warehouse:** Only sees Home + Inventory.
  - **Admin / Manager:** Full access.
- **Presentation vs. Security Boundary:** Tab hiding is purely for cashier ergonomics and UI decluttering. The backend independently enforces JWT role and scope permissions on every endpoint.
- **Customer Kiosk Shell:** A completely separated portrait route tree (`/kiosk/...`) running tactile customer-facing themes. Kiosk devices pair without staff PINs and are structurally prevented by backend role constraints from executing payments, discounts, or credit deductions.

### 3.3 Offline Persistence & Local Database (Drift / SQLite)
- **Why Drift:** Strongly typed Dart queries, compile-time SQL verification, reactive streams (`watch()`), and native performance via SQLite.
- **What is cached locally:**
  - `pending_sync_queue`: Queues transactional mutations created while network connectivity is offline.
  - `cached_branding`: Local tenant colors, logo paths, and kiosk hero banner assets.
  - Core catalog snapshots for fast offline barcode lookups.

### 3.4 Ergonomics, Accessibility & Design Tokens
- **Touch Targets:** Minimum 48×48dp up to 72dp touch surfaces built for high-throughput, oily, or gloved environments.
- **Tabular Figures:** Numbers formatted with `FontFeature.tabularFigures()` across monetary tables and receipts to eliminate numeric visual jitter during live tally updates.
- **Semantic Colors:** Strict functional color conventions:
  - **Green:** Successful transaction / Paid.
  - **Red:** Void, stock-out, error.
  - **Amber:** Low stock warning, pending sync.

---

## 4. Backend (.NET 9) & Domain Engine Deep Dive

### 4.1 Clean Architecture Breakdown
- **Purch.Domain:** Core business rules. Encapsulates item types, pricing calculations, BIR tax deductions, and transaction invariants without external dependencies.
- **Purch.Application:** Application logic, CQRS command/query handlers, validation pipelines, and DTO contracts.
- **Purch.Infrastructure:** Data access layer with EF Core 9, PostgreSQL connection handling, password hashing, JWT generation, and tenant filter enforcement.
- **Purch.Api:** Minimal API endpoints organized by feature route groups (e.g., `MapCatalogEndpoints()`, `MapPosEndpoints()`).

### 4.2 The Core Architectural Bet: Dynamic Multi-Vertical Pricing
Instead of branching into separate codebases or custom builds for different industries, every catalog item has a `pricing_type`:
1. **Unit:** Standard retail barcode lookup (groceries, dry goods).
2. **Weight / Volume ("Tingi"):** Fractional quantity pricing with decoupled inventory deduction (e.g., selling cooking oil per 100ml or rice per 250g from a 50kg sack).
3. **Variant Matrix:** Multi-attribute combinations (Size, Color, Material) with independent SKUs and prices (apparel/boutique).
4. **Combo / Meal Builder:** Multi-slot selection with slot-specific add-on pricing and substitutions (QSR, fast food, cafés).
5. **Bundle & Tiered:** Mix-and-match promos, Buy-X-Get-Y, and basket thresholds.
6. **Service:** Duration-based booking and technician/stylist attribution (salons, spas, repair shops).

### 4.3 Multi-Tenancy & Authorization
- **Tenant Isolation:** Tenant ID extracted from JWT claims on each request. EF Core utilizes Global Query Filters (`builder.Entity<T>().HasQueryFilter(e => e.TenantId == _currentTenantId)`), guaranteeing that data leaks between merchants are impossible at the ORM level.
- **Two-Axis Security Model:**
  - **Role (`StaffRole`):** What operations the user can perform (e.g., Cashier, Manager, Admin).
  - **Data Scope (`ScopeType` + `ScopeId`):** Which store/branch data the user can access. A manager at Branch A cannot access or export Branch B's sales reports.

### 4.4 Offline Sync & Conflict Resolution Protocol
- **Idempotency Keys:** Every client mutation carries a unique UUID idempotency key to prevent double charging or duplicate stock deductions on network retries.
- **Conflict Rule:** Server-side `SyncedRecord` ledger. If two devices modify the same record during an offline split:
  - *Later timestamp wins.*
  - *The losing record is auto-cancelled and flagged for administrative review.*
  - **Crucial Rule:** Records are **never silently dropped**.

---

## 5. Philippine SME Compliance & Business Features

- **BIR Compliance:**
  - Sequential, gap-auditable, server-controlled receipt numbering.
  - Automated **X-Reading** (interim shift drawer audit) and **Z-Reading** (end-of-day fiscal reset locking transactions).
  - Mandated statutory discounts: **Senior Citizen (20% + VAT exemption)** and **PWD discounts** with ID validation and proportional deduction.
- **Customer Credit Ledger ("Utang"):**
  - Tracking credit limits per customer, outstanding balances, partial repayments, and due-date alerts directly integrated into checkout.
- **Digital Payments Integration:**
  - Dynamic & static QR Ph countertop generation (GCash, Maya, bank transfer) with cashier verification workflows.

---

## 6. Interview Questions & Model Answers

### Q1: "Walk me through how you structured the Flutter codebase."
**Answer:**  
*"We organized the Flutter codebase by feature rather than layer. Inside `lib/features/`, each domain—such as POS, Catalog, Inventory, Auth, and Kiosk—has its own `domain`, `presentation`, and `data` directories. Cross-cutting concerns like our Drift SQLite database, networking (Dio), theming tokens, and GoRouter live in `lib/core/`.  
For the UI shell, we separated the staff-facing experience from the customer kiosk. Staff tablets operate in a landscape-first layout using GoRouter's `StatefulShellRoute.indexedStack` with role-filtered bottom tabs, while the self-service kiosk runs in an isolated portrait route tree."*

### Q2: "How did you manage state, and why choose Riverpod over Bloc or Provider?"
**Answer:**  
*"We chose Flutter Riverpod 2.x. Compared to original Provider, Riverpod eliminates runtime `ProviderNotFoundException` bugs and doesn't require a `BuildContext` to read providers. Compared to Bloc, Riverpod significantly reduces boilerplate for small to mid-sized teams while offering built-in caching, dependency injection, and automatic lifecycle management via `autoDispose`.  
For example, our POS checkout leverages a `cartNotifierProvider`. Both the landscape item catalog grid and the right-hand cart panel watch this same notifier. When a cashier scans a barcode or taps an item, the cart recalculates VAT, promos, and Senior/PWD exemptions reactively in a single place."*

### Q3: "How does the app work when the internet goes down?"
**Answer:**  
*"Purch.io was designed offline-first using Drift (SQLite) on the client. When an internet dropout occurs:
1. The app detects offline state via `connectivity_plus` and Dio network interceptors.
2. The cashier continues scanning items from locally cached catalog data.
3. Mutations (orders, stock updates) are written to a local `pending_sync_queue` table with a unique idempotency key.
4. When connectivity returns, the `SyncCoordinator` flushes the queue to the backend `/sync` endpoint.
5. In conflict scenarios, our backend applies a 'later timestamp wins' rule, while auto-cancelling the losing transaction and flagging it in an audit table so cashiers and accountants never experience silent data loss."*

### Q4: "How do you handle responsiveness between a Cashier tablet and a Kiosk?"
**Answer:**  
*"Rather than forcing a single UI to stretch across every screen, we implemented dual shell architectures:
1. **Staff Shell (Landscape-first):** Targets 10"–12" countertop tablets. It utilizes a split-pane layout where the left 65% is an item/category grid and the right 35% is a persistent live cart. On smaller widths, it adapts by placing the cart into an accessible end drawer.
2. **Kiosk Shell (Portrait):** Tailored for vertical kiosk stands. It features large 72dp touch targets, a visual hero promotional carousel, and an intuitive step-by-step modifier selection flow. The kiosk route tree is kept completely distinct to enforce security—kiosks cannot process payments or alter prices."*

### Q5: "How do you maintain performance in Flutter with large product catalogs?"
**Answer:**  
*"We implement several key optimizations:
- `ListView.builder` and `GridView.builder` for virtualized rendering of catalog items.
- Memoized pricing calculations and select listeners (`ref.watch(provider.select(...))`) to prevent whole-screen rebuilds when only a single item quantity changes.
- Caching images locally with thumbnail sizing and error placeholders (`PurchImage`).
- Enabling `FontFeature.tabularFigures()` on monetary text to avoid width recalculations and layout jitter as numbers increment."*

### Q6: "How did you design the backend and ensure multi-tenant security?"
**Answer:**  
*"The backend is written in ASP.NET Core (.NET 9) using Clean Architecture and Minimal APIs.  
For multi-tenancy in Cloud mode, we use a shared PostgreSQL database where every tenant-scoped entity has a `TenantId`. Instead of manually adding `WHERE TenantId = @id` across every query, we configure EF Core Global Query Filters in our DbContext. When a request arrives, our JWT middleware extracts the `tenant_id` claim, and EF Core automatically injects the tenant filter into all database queries and writes.  
Additionally, we enforce an orthogonal `ScopeType` (Tenant, Branch, Department) so a store manager's permissions are locked to their specific branch."*

### Q7: "Tell me about a challenging technical problem you solved on this project."
**Answer:**  
*"A major challenge was the 'Tingi' fractional pricing model common in Philippine retail, such as selling sugar by the gram or shampoo in sachets from a bulk sack. If a merchant sells 250 grams from a 50kg sack, deducting 1 unit from inventory would break tracking, while pricing 0.005 units often introduces floating-point rounding errors.  
We solved this by decoupling the pricing unit from the inventory stock-keeping unit in the domain layer. The item definition stores a conversion ratio and integer-based pricing in centavos/centavos equivalents. The cart engine computes exact fractional pricing while emitting whole-integer inventory deductions, avoiding floating-point inaccuracies and preserving compliance with BIR receipt requirements."*

---

## 7. Quick Cheat Sheet for Technical Terms & Acronyms

| Term | What it means in Purch.io |
|---|---|
| **Drift** | Reactive SQLite abstraction library for Dart/Flutter. Used for local caching and write queues. |
| **Riverpod** | Reactive caching & state management framework used across the Flutter client. |
| **GoRouter** | Declarative routing package used for role-based shell navigation and kiosk isolation. |
| **BIR X-Reading** | Mid-shift financial snapshot; drawer audit without resetting sales accumulators. |
| **BIR Z-Reading** | End-of-day fiscal report that resets daily counters and locks transactions for tax audits. |
| **Tingi** | Philippine retail term for fractional / repackaged portions (e.g. bulk sack sold by grams). |
| **Utang** | Customer credit ledger with credit limits, repayment tracking, and due date reminders. |
| **Idempotency Key** | Client-generated UUID ensuring duplicate requests during network retries do not double-bill. |
| **Global Query Filter** | EF Core feature that automatically appends `WHERE tenant_id = @id` to every query. |
| **QR Ph** | National QR code standard in the Philippines supported for GCash, Maya, and bank transfers. |
