# Purch.io Client

The **Purch.io Client** is a high-performance, cross-platform **Flutter** application engineered for retail POS terminals and customer-facing self-service kiosks. Designed specifically for the operational demands of Philippine SMEs, the client features a dual-shell architecture that adapts dynamically to hardware form factor, screen orientation, and user role.

---

## Dual-Shell Experience

```
                             ┌───────────────────────────────────┐
                             │       Purch.io Flutter Core       │
                             └─────────────────┬─────────────────┘
                                               │
                       ┌───────────────────────┴───────────────────────┐
                       │                                               │
                       ▼                                               ▼
         ┌───────────────────────────┐                   ┌───────────────────────────┐
         │   Landscape Staff Shell   │                   │   Portrait Kiosk Shell    │
         │   (10"-15.6" Tablets/PCs) │                   │ (15.6"-21.5" Touchscreens)│
         └─────────────┬─────────────┘                   └─────────────┬─────────────┘
                       │                                               │
       ┌───────────────┴───────────────┐               ┌───────────────┴───────────────┐
       │ • High-Speed Cashier POS      │               │ • Full-Bleed 16:9 Hero Banner │
       │ • Barcode & QR Scanner        │               │ • Tactile Category Carousel   │
       │ • Split-Pane Inventory Admin  │               │ • Meal / Variant Customizer   │
       │ • Shift & BIR X/Z Readings    │               │ • Self-Service Order Dispatch │
       │ • Credit Ledger Management    │               │ • Kitchen Prep Ticket Screen  │
       └───────────────────────────────┘               └───────────────────────────────┘
```

### 1. Landscape Staff Shell (Cashier & Store Management)
- **Role-Filtered Bottom Navigation**: A `go_router` `StatefulShellRoute` drives a five-tab dashboard — Home, Sell, Reports, Inventory, Business — each tab keeping its own navigation stack. Tab visibility is role-driven (Admin/Manager see all five; Cashier sees Home + Sell; Warehouse sees Home + Inventory), and the Home tab surfaces a "New Sale" launch action plus anything needing attention (unsynced conflicts, overdue customer payments).
- **High-Throughput Point of Sale**: Split-pane layout with rapid item scanning, unit/variant/combo selections, line-item adjustments, and instant discount vouchers.
- **Cash Drawer & Shift Auditing**: Shift opening cash count, mid-day cash-drops, cash drawers, and closing balance variance reporting.
- **Inventory & Stock Management**: In-app purchase orders, receiving logs, inter-branch stock transfers, supplier management, and low-stock threshold alerts.
- **BIR Fiscal Compliance**: Direct generation and review of official X-Readings (mid-shift summary) and Z-Readings (daily fiscal closure).
- **Credit Ledger (*Utang*)**: Track customer credit accounts, repayment histories, and outstanding balance reminders.

### 2. Portrait Kiosk Shell (Customer Self-Service)
- **Promotional Hero Poster**: Full-bleed 16:9 promotional banner loaded dynamically from tenant branding settings, falling back seamlessly to an omnichannel retail showcase.
- **Visual Category Browsing**: Large, touch-friendly 2-column tiles with category icons and dynamic badges.
- **Interactive Customization**: Seamless multi-step variant pickers and combo meal slot selectors.
- **Touch-Optimized UX**: Large 64–72dp interactive buttons, clear order confirmation badges, and back-navigation safety locks (`PopScope`).

---

## Offline Durability & Sync Engine

Purch.io is architected for zero-downtime store operations, even during complete broadband or LAN disruptions:

- **Local Storage Engine (Drift SQLite)**: All active catalog records, tenant branding, branch configs, and pending checkout transactions are cached locally on device.
- **Mutation Queue**: When offline, checkout transactions are queued locally with guaranteed persistence.
- **Background Sync Coordinator**: Upon network restoration, queued mutations are automatically drained and synchronized with the backend. Conflicting records are safely flagged without halting the cashier checkout flow.
- **Runtime Server Reconfiguration**: Supports dynamic API base URL overrides at runtime (`ServerConnectionScreen`) for local LAN deployments where devices must point to an on-premise server IP.

---

## Design System & Unified Media (`PurchImage`)

The client UI is driven by a centralized design token system ([`lib/core/theming/app_tokens.dart`](file:///d:/Projects/Research%20Projects/Purch.io/client/lib/core/theming/app_tokens.dart)):
- **Color Palette**: Brand Ultramarine (`#1E40AF`), Warm Amber alerts (`#D97706`), and Emerald positive indicators (`#059669`) set against high-contrast Slate neutrals.
- **Tabular Figures**: All monetary amounts (`₱`) utilize `FontFeature.tabularFigures()` so financial columns align without visual jitter during high-speed scanning.
- **Standardized State Handling**: Every screen incorporates dedicated [`EmptyStateView`](file:///d:/Projects/Research%20Projects/Purch.io/client/lib/core/widgets/empty_state_view.dart) and [`ErrorStateView`](file:///d:/Projects/Research%20Projects/Purch.io/client/lib/core/widgets/error_state_view.dart) components featuring clear action triggers rather than blank screens or raw exception traces.
- **Unified Media Resolver ([`PurchImage`](file:///d:/Projects/Research%20Projects/Purch.io/client/lib/core/widgets/purch_image.dart))**:
  - Automatically detects and resolves local bundled assets (`assets/...`), dynamic backend uploads (`/uploads/...` with base URL prepending), and remote CDN URLs.
  - Features shimmer placeholders during load states and automatic fallback to default brand assets on connection failures.

---

## Getting Started

### Prerequisites
- [Flutter SDK](https://docs.flutter.dev/) (3.24 or newer)
- Android SDK (for mobile/tablet targets) or Visual Studio 2022 with the "Desktop development with C++" workload **plus** the "C++ ATL for latest v14x build tools" individual component (for the Windows desktop target — `flutter_secure_storage_windows` won't compile without it)
- Web (`chrome`/`web-server`) is **not** a supported target: `sqlite3_flutter_libs`, backing the offline Drift database, has no web implementation and the build will fail at compile time.

> **Windows toolset gotcha**: even with ATL installed, `flutter run -d windows` can still fail with `error C1083: Cannot open include file: 'atlstr.h'`. This happens when VS's default `v143` toolset resolves to an *older* MSVC version than the one ATL got installed against (common after a VS update). Use [`tool/run_windows.ps1`](tool/run_windows.ps1) instead of calling `flutter run -d windows` directly — it auto-detects the newest MSVC toolset that actually has ATL and builds against that.

---

### Installation & Code Generation

1. Install project dependencies:
   ```bash
   flutter pub get
   ```

2. Run the code generator to produce Drift database code and Riverpod providers:
   ```bash
   dart run build_runner build --delete-conflicting-outputs
   ```

---

### Running the Application

Launch the application targeting your preferred device or emulator:

**Windows Desktop (Recommended for Cashier POS testing)**:
```powershell
.\tool\run_windows.ps1 --dart-define=PURCH_API_BASE_URL=https://localhost:5001
```
*(Or `flutter run -d windows` directly, once you know your VS install doesn't have the toolset/ATL version mismatch described above.)*

**Android Tablet / Mobile**:
```bash
flutter run -d <device-id> --dart-define=PURCH_API_BASE_URL=http://10.0.2.2:5000
```

> **Tip**: If `--dart-define=PURCH_API_BASE_URL` is omitted, the app defaults to `https://localhost:5001`. You can also switch the target server address at any time via the "Connect to a local server" button on the login screen.
>
> **Web is not supported** (`flutter run -d chrome`/`web-server` will fail to compile) — see Prerequisites above.

---

### Running the Test Suite

The client repository includes a comprehensive unit and widget testing suite:
```bash
flutter test
```
All **148 automated tests** run in under 65 seconds with 100% green pass rates, verifying transaction math, state immutability, offline queuing, and UI contracts across all screens.
