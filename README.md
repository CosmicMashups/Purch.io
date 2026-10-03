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
- **Automatic, No-Code Promotions**: Time-boxed rules that apply themselves at checkout — Buy-1-Take-1 (same or cross-item), fixed-total combo bundles (e.g. two named items for one flat price), and per-item percentage/fixed/override discounts — stacked on top of existing Senior/PWD and promo-code discounts via a claimed-quantity pricing pass that prevents double-discounting a unit matched by more than one rule.
- **Ingredient-Level Inventory (Recipe/BOM)**: An opt-in per-tenant mode that decouples what's sold at the register from what's tracked in the stockroom — a cafe can track "coffee beans" and "milk" as Inventory Items with their own unit of measure and delivery packaging (e.g. 10 pcs of 450 mL bottles), link them to menu items via a recipe with optional per-order consumption quantities, and let Out-of-Stock status derive automatically from ingredient availability. Items without a configured recipe keep working exactly as before via manual Physical Count.
- **Dual Form-Factor Client & Unattended Displays**:
  - **Landscape Staff Shell**: A role-filtered, four-tab bottom-navigation dashboard (Home, Sell, Inventory, Business) built on `go_router`, wrapping rapid-scan cashier POS, split-pane manager console, live shift drawer audits, stock movement logging, and integrated report analytics. Which tabs render is driven by the signed-in staff member's role — a Cashier only sees Sell (or Home + Sell), a Warehouse account sees Inventory, and Admin/Manager access the full operational dashboard.
  - **Portrait Self-Service Kiosk & Secondary Displays**: Customer-facing ordering terminal with customizable 16:9 promotional hero posters, visual category carousels, and order ticket dispatch. Also includes dedicated Kitchen Display System (KDS), Order Status Board, and a low-latency Customer-Facing Display (CFD) paired to the Register with conditional GET caching.
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
- **`web/`**: React + TypeScript + Vite web client (the supported client): cashier, inventory, business admin, kiosk, kitchen and order-board screens, device pairing and lock screen. Vitest unit tests and Playwright end-to-end tests.
- **`client/`**: Cross-platform Flutter client (not yet ported to the new sign-in; see [Flutter client status](#flutter-client-status)):
  - `lib/core/`: Theming tokens, network clients, Drift database, and shared UI components (`PurchImage`, `EmptyStateView`, `ErrorStateView`).
  - `lib/features/`: Feature modules for Auth, Catalog, POS, Kiosk, Inventory, Credit Ledger, and Reports.
  - `test/`: 230+ automated unit and widget regression tests.
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

The fastest way to a working system is the **web app + backend in Local mode** against a Postgres you run yourself. No Supabase account or cloud service is needed.

### Prerequisites

| Tool | Version | Needed for |
|------|---------|-----------|
| .NET SDK | 9.0 (64-bit) | Backend and its tests |
| Node.js | 22 (npm 10+) | Web app |
| PostgreSQL | 16+ (a local install or Docker) | The database |
| Docker | any recent | Backend integration tests (Testcontainers) and the optional Postgres container below |
| Flutter SDK | 3.24+ | Only the `client/` app (see [Flutter client status](#flutter-client-status)) |

---

### Quick start (web + backend, Local mode)

**1. Start a Postgres 16 database.** With Docker:

```bash
docker run -d --name purch-db -e POSTGRES_DB=purch -e POSTGRES_USER=purch -e POSTGRES_PASSWORD=purch_dev_password -p 5432:5432 postgres:16
```

Or create an empty database and a superuser in a Postgres you already have. The user must be a superuser (or have `BYPASSRLS`): the API checks this at startup because tenant isolation uses row-level security.

**2. Run the API.** From the repository root, set the variables for your shell and start it. In Local mode it creates and migrates the database itself on first start.

Bash / Git Bash:

```bash
export PURCH_DEPLOYMENT_MODE=Local
export LOCAL_DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=purch;Username=purch;Password=purch_dev_password"
export LOCAL_STORAGE_PATH="$PWD/.local-storage"
export JWT_SIGNING_KEY="any-long-random-string-of-at-least-32-characters"
export JWT_ISSUER=purch.io
export ASPNETCORE_ENVIRONMENT=Development
mkdir -p "$LOCAL_STORAGE_PATH"
dotnet run --project backend/src/Purch.Api --no-launch-profile -- --urls http://localhost:5062
```

PowerShell:

```powershell
$env:PURCH_DEPLOYMENT_MODE = "Local"
$env:LOCAL_DB_CONNECTION_STRING = "Host=localhost;Port=5432;Database=purch;Username=purch;Password=purch_dev_password"
$env:LOCAL_STORAGE_PATH = "$PWD\.local-storage"
$env:JWT_SIGNING_KEY = "any-long-random-string-of-at-least-32-characters"
$env:JWT_ISSUER = "purch.io"
$env:ASPNETCORE_ENVIRONMENT = "Development"
New-Item -ItemType Directory -Force $env:LOCAL_STORAGE_PATH | Out-Null
dotnet run --project backend/src/Purch.Api --no-launch-profile -- --urls http://localhost:5062
```

`JWT_SIGNING_KEY` is required and has no default. Use any long random string, and keep it the same between restarts if you want existing sessions to stay valid. `scripts/run-backend-local.sh` does the same interactively (it prompts for the connection string and hides it).

The API is ready when `http://localhost:5062/health/ready` answers.

**3. Run the web app** in a second terminal:

```bash
cd web
npm install
echo "VITE_DEV_PROXY_TARGET=http://localhost:5062" > .env.local
npm run dev
```

Open <http://localhost:5173>. In development the web app calls `/api`, which Vite forwards to `VITE_DEV_PROXY_TARGET`. Without that variable it forwards to the hosted demo backend instead of your local one, so do not skip `.env.local`.

**4. Create your first business.** Open <http://localhost:5173/onboarding> and enter the business name, type, branch, and the owner's name, **email, password, and PIN**. You are then signed in as the Admin. (Over HTTP this is `POST /onboarding/bootstrap`.)

**5. Set up people and devices** from the web app as the Admin:
- **Business → Staff**: invite a person. You get a single-use link and QR code (no email is sent). They open it, choose a password and a PIN, and are enrolled.
- **Business → Devices**: add a device (Register, Kiosk, Order Board, Kitchen Display, Warehouse, or Customer Display). You get a one-time pairing code. Open `/pair` in that device's browser and enter it. The device then keeps its own revocable credential.
- Registers and Warehouse devices lock themselves; staff unlock them at `/unlock` with their own PIN.

How sign-in works, in short: **people** sign in with email and password, or unlock a paired till with their PIN; **devices** are paired once with a short-lived code and never hold a person's password. The full design is in [docs/AUTH-REDESIGN.md](docs/AUTH-REDESIGN.md).

---

### Configuration reference

`.env.example` lists the same names as a checklist. The backend reads plain environment variables.

| Variable | Mode | Purpose |
|----------|------|---------|
| `PURCH_DEPLOYMENT_MODE` | both | `Cloud` or `Local` |
| `JWT_SIGNING_KEY` | both | **Required.** Signs access tokens |
| `JWT_ISSUER` | both | Token issuer; defaults to `purch.io` |
| `LOCAL_DB_CONNECTION_STRING` | Local | Npgsql keyword string (`Host=...;Username=...`) |
| `LOCAL_STORAGE_PATH` | Local | Folder for uploaded images |
| `SUPABASE_DB_CONNECTION_STRING` | Cloud | Supabase Postgres in Npgsql format, session pooler or direct (not the transaction pooler) |
| `SUPABASE_AUTH_URL`, `SUPABASE_AUTH_SERVICE_KEY`, `SUPABASE_AUTH_ANON_KEY` | Cloud | Supabase Auth, which checks passwords in Cloud mode. Local mode keeps passwords in its own table |
| `SUPABASE_STORAGE_URL`, `SUPABASE_STORAGE_KEY` | Cloud | Image storage |
| `CORS_ALLOWED_ORIGINS` | both | Comma-separated web origins allowed to call the API |
| `PORT` | both | Listening port when `--urls` is not given; defaults to 8080 |
| `VITE_API_BASE_URL` | web build | API address baked into a production web build |
| `VITE_DEV_PROXY_TARGET` | web dev | Where the dev server forwards `/api` |

Never commit real keys. `.env`, `.env.local`, and `.local-storage/` are git-ignored.

---

### Database migrations

- **Local mode** migrates automatically at startup.
- **Cloud mode** does not. Run `scripts/migrate-production.sh` (or the `.ps1` / `.bat` version) against the target database before deploying, or call the CLI directly: `dotnet run --project backend/src/Purch.Api -- migrate`.
- **Upgrading an installation that used the old PIN-at-a-pairing-code sign-in:** after `migrate`, run `dotnet run --project backend/src/Purch.Api -- migrate-legacy` once. It carries owners over, ends old sessions, sets old devices back to waiting for a one-time code, and prints a **single-use claim link** for any owner who never had an email and password. Those links appear only in that command's output. See [docs/AUTH-REDESIGN.md](docs/AUTH-REDESIGN.md). A brand-new database does not need this.

---

### Running the tests

**Backend** (integration tests start their own throwaway Postgres through Docker, so Docker must be running):

```bash
cd backend
dotnet build
dotnet test tests/Purch.UnitTests
dotnet test tests/Purch.IntegrationTests   # slow; needs Docker
```

**Web:**

```bash
cd web
npm test              # unit and component tests (Vitest)
npx tsc --noEmit      # type-check
npm run lint
npm run build
```

**Web end-to-end (Playwright)** drives a real browser against the real backend and a throwaway Postgres. It needs the .NET SDK and either Docker or a local PostgreSQL install (set `PG_BIN` to its `bin` folder):

```bash
cd web
npx playwright install chromium   # once; or set PW_CHANNEL=chrome to use installed Chrome
npm run e2e
```

---

### Flutter client status

The Flutter app in `client/` has **not yet been ported** to the new email sign-in, one-time device pairing, lock screen, and staff-enrolment flows above. Until it is, it cannot sign in to a current backend. The web app is the supported client for now. For reference, the Flutter setup is:

```bash
cd client
flutter pub get
dart run build_runner build --delete-conflicting-outputs
flutter test
flutter run -d windows --dart-define=PURCH_API_BASE_URL=http://localhost:5062
```

Chrome/web is not a supported Flutter target (the offline Drift database is not web-compatible). The Windows target also needs the "C++ ATL for latest v14x build tools" component alongside Visual Studio's Desktop development with C++ workload.

---

### Troubleshooting

- **API exits at startup with a database error:** the connection string is wrong or Postgres is not running. Use the `Host=...;Username=...;Password=...` form, not a `postgres://` URI.
- **API exits mentioning BYPASSRLS:** connect as the Postgres superuser, or grant the role `BYPASSRLS`.
- **`JWT_SIGNING_KEY is not configured`:** set it to any long random string.
- **Web app shows network errors or talks to the wrong server:** check `web/.env.local` contains `VITE_DEV_PROXY_TARGET=http://localhost:5062`, then restart `npm run dev`.
- **Browser blocks calls to the API (CORS):** add the web origin to `CORS_ALLOWED_ORIGINS`. This only matters when the web app calls the API directly, not through the dev proxy.
- **Sign-in says too many attempts:** sign-in is rate limited; wait for the window to pass.
- **Integration tests cannot start a database:** start Docker and check that `docker ps` works.

---

### Deployment

- **Cloud:** backend on Vercel (`backend/vercel.json`, `backend/Dockerfile.vercel`) or Render (`render.yaml`), with Supabase for Postgres, Auth, and storage; web app on Cloudflare ([docs/CLOUDFLARE-DEPLOYMENT.md](docs/CLOUDFLARE-DEPLOYMENT.md)) or Vercel (`web/vercel.json`). CI workflows are in `.github/workflows/`.
- **On-premises:** see [installer/README.md](installer/README.md) (Docker Compose or a Windows service).

Further reading: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/WEB-ARCHITECTURE.md](docs/WEB-ARCHITECTURE.md), [docs/AUTH-REDESIGN.md](docs/AUTH-REDESIGN.md), [docs/BACKUPS.md](docs/BACKUPS.md).

---

## Quality Assurance & Verification

- **Backend Solution**: Clean compilation with 0 warnings/errors across all projects.
- **Integration Tests**: Tested with Dockerized PostgreSQL testcontainers for authentication, tenant onboarding, catalog operations, inventory reconciliation, and multipart image uploads.
- **Web Test Suite**: Vitest unit and component tests plus Playwright end-to-end tests against the real backend.
- **Client Test Suite**: Flutter unit and widget tests (`flutter test` in `client/`).
- **Static analysis**: `tsc --noEmit` and `oxlint` for the web app; `flutter analyze` for the client.
