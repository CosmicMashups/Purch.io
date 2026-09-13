# Purch.io

**Purch.io** is an enterprise-grade, config-driven Point-of-Sale (POS) and self-service Kiosk platform tailored for the Philippine retail and service ecosystem. Engineered with a unified data model, a single codebase dynamically adapts its pricing engine and user interfaces across diverse retail verticals—including **convenience stores, cafés, groceries, department stores, specialty retail, and service salons**—without requiring separate vertical forks or fragmented codebases.

Built for mission-critical operations, Purch.io features offline-resilient local persistence, strict Bureau of Internal Revenue (BIR) compliance readiness, multi-branch inventory tracking, customer credit ledgers (*utang* management), tenant-isolated branding, and dual deployment architectures supporting both shared cloud infrastructure and on-premises air-gapped LAN installations.

---

## Key Capabilities

- **Unified Multi-Vertical Pricing Engine**:
  - **Unit Pricing**: Standard retail barcodes and SKU lookups.
  - **Tingi (Fractional / Weight / Volume)**: Decoupled inventory deduction with fixed-portion or continuous unit pricing.
  - **Variant Matrices**: Multi-attribute matrix support (Size, Color, Material) with independent pricing and stock tracking.
  - **Combo Meals & Customization**: Multi-tier slot selection with optional add-ons and substitution pricing.
  - **Bundles & Tiered Volume Pricing**: Mix-and-match bundle rules, buy-X-get-Y, and automatic basket discounts.
  - **Timed Services**: Time-based service tracking with practitioner scheduling and duration-based rates.
- **Dual Form-Factor Client**:
  - **Landscape Staff Shell**: A role-filtered, five-tab bottom-navigation dashboard (Home, Sell, Reports, Inventory, Business) built on `go_router`, wrapping rapid-scan cashier POS, split-pane manager console, live shift drawer audits, and stock movement logging. Which tabs render is driven by the signed-in staff member's role — a Cashier only ever sees Home + Sell, a Warehouse account only Home + Inventory.
  - **Portrait Self-Service Kiosk**: Customer-facing ordering terminal with customizable 16:9 promotional hero posters, visual category carousels, and order ticket dispatch.
- **Offline-First Resilience & Sync**:
  - SQLite local database powered by Drift for zero-latency cashier interactions during connectivity dropouts.
  - Robust background sync coordinator that queues mutations, handles network retry backoff, and safely detects transaction conflicts.
- **Philippine Compliance & Business Workflows**:
  - Automated BIR X-Reading (mid-shift summary) and Z-Reading (daily fiscal reset) generation with sequential counter logging.
  - Customer Credit Ledger (*Utang*) with credit limits, payment schedules, and partial collection tracking.
  - Static QR Ph and GCash countertop display management with live visual confirmation.
- **Tenant Media & Customization**:
  - Built-in secure image upload service (`POST /uploads/image`) with tenant directory isolation.
  - Dynamic branding system controlling theme colors, receipt wordmarks, and kiosk landing banners.
- **Enterprise Design System**:
  - Cohesive design tokens ensuring high visual contrast, 48–72dp touch targets, and tabular numeric figures (`FontFeature.tabularFigures()`) across all monetary tables to prevent visual jitter.
  - Contextual empty states and error recovery workflows across every functional module.

---

## System Architecture

```
                                  ┌───────────────────────────────┐
                                  │   Purch.io Client (Flutter)   │
                                  │  - Landscape Cashier / Admin  │
                                  │  - Portrait Self-Order Kiosk  │
                                  └──────────────┬────────────────┘
                                                 │
                               ┌─────────────────┴─────────────────┐
                               │ HTTP / JSON API (Dio & Riverpod)  │
                               │ Local Offline Store (Drift SQLite)│
                               └─────────────────┬─────────────────┘
                                                 │
                                                 ▼
                                  ┌───────────────────────────────┐
                                  │   Purch.Api (.NET 9 Web API)  │
                                  │   - Minimal API Route Groups  │
                                  │   - JWT Auth & Role Security  │
                                  │   - Dynamic Upload Pipeline   │
                                  └──────────────┬────────────────┘
                                                 │
                                                 ▼
                                  ┌───────────────────────────────┐
                                  │       Purch.Application       │
                                  │  - CQRS Commands & Queries    │
                                  │  - Domain Service Validation  │
                                  └──────────────┬────────────────┘
                                                 │
                                                 ▼
                                  ┌───────────────────────────────┐
                                  │      Purch.Infrastructure     │
                                  │  - EF Core 9 / PostgreSQL     │
                                  │  - Multi-Tenant Data Filters  │
                                  │  - File System Storage        │
                                  └───────────────────────────────┘
```

### Directory Structure

- **`backend/`**: ASP.NET Core 9 Clean Architecture solution:
  - `src/Purch.Domain`: Core entities, enums, value objects, and business rules.
  - `src/Purch.Application`: Use case handlers, service contracts, and DTOs.
  - `src/Purch.Infrastructure`: EF Core PostgreSQL persistence, authentication, and file storage.
  - `src/Purch.Api`: Minimal API endpoints, middleware, upload endpoints, and static file hosting.
  - `tests/`: Comprehensive unit test and Testcontainers integration test suites.
- **`client/`**: Cross-platform Flutter client:
  - `lib/core/`: Theming tokens, network clients, Drift database, and shared UI components (`PurchImage`, `EmptyStateView`, `ErrorStateView`).
  - `lib/features/`: Feature modules for Auth, Catalog, POS, Kiosk, Inventory, Credit Ledger, and Reports.
  - `test/`: 148 automated unit and widget regression tests.
- **`docs/`**: Architecture decision records (`docs/adr/`), specifications, database schemas, and design token documentation (`docs/design/`).
- **`installer/`**: Deployment configurations for dedicated on-premise deployments (Docker Compose / Windows Service).

---

## Deployment Modes

Every installation operates in one of two deployment modes, controlled by the `PURCH_DEPLOYMENT_MODE` environment variable:

1. **Cloud Mode (`Cloud`)**:
   - Multi-tenant shared backend hosted on cloud container infrastructure (e.g. Render).
   - Backed by managed PostgreSQL (e.g. Supabase) with tenant isolation enforced at the data layer via JWT claim inspection and EF Core global query filters.
2. **Local Mode (`Local`)**:
   - Single-tenant, dedicated installation running on a merchant's on-premises LAN server (Docker or Windows Service).
   - Automatically executes database migrations against an isolated PostgreSQL instance upon first boot, ensuring full offline functionality on local network segments.

---

## Getting Started

### Prerequisites
- **.NET 9 SDK** (64-bit)
- **Flutter SDK** (3.24+ recommended)
- **PostgreSQL 16+** (or Docker for running integration tests)

---

### Backend Setup

1. Navigate to the backend directory:
   ```bash
   cd backend
   ```
2. Restore dependencies and compile the solution:
   ```bash
   dotnet build
   ```
3. Run the unit test suite:
   ```bash
   dotnet test tests/Purch.UnitTests
   ```
4. Run the API locally:
   ```bash
   dotnet run --project src/Purch.Api
   ```
   *Note: Ensure environment variables for database connection and deployment mode are configured (refer to `.env.example`).*

---

### Client Setup

1. Navigate to the client directory:
   ```bash
   cd client
   ```
2. Install package dependencies:
   ```bash
   flutter pub get
   ```
3. Run code generation for Drift and Riverpod:
   ```bash
   dart run build_runner build --delete-conflicting-outputs
   ```
4. Execute the test suite:
   ```bash
   flutter test
   ```
5. Launch the application:
   ```bash
   flutter run -d windows --dart-define=PURCH_API_BASE_URL=https://localhost:5001
   ```
   *(Replace target with `android` or macOS as required. The API endpoint can also be reconfigured on the fly within the application's connection settings. Note: Chrome/web is **not** a supported target — `sqlite3_flutter_libs`, used for the offline Drift database, isn't web-compatible. The Windows desktop target additionally requires the "C++ ATL for latest v14x build tools" component installed alongside Visual Studio's Desktop development with C++ workload, for `flutter_secure_storage`.)*

---

## Quality Assurance & Verification

- **Backend Solution**: Clean compilation with 0 warnings/errors across all projects.
- **Integration Tests**: Tested with Dockerized PostgreSQL testcontainers for authentication, tenant onboarding, catalog operations, inventory reconciliation, and multipart image uploads.
- **Client Test Suite**: 100% green test suite (148/148 passing tests) validating state management, user flows, tabular financial calculations, and edge-case error recovery.
