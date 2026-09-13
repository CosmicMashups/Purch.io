# Purch.io Backend Services

The backend of **Purch.io** is built with **ASP.NET Core 9 Minimal APIs** adhering to the principles of **Clean Architecture** and **Domain-Driven Design (DDD)**. It serves as the authoritative transaction and data synchronization engine for both the cashier POS and customer-facing self-service kiosk terminals.

---

## Architectural Layers

```
                           ┌─────────────────────────────┐
                           │          Purch.Api          │
                           │  - Minimal API Endpoints    │
                           │  - Auth & Role Middleware   │
                           │  - Multipart Uploads        │
                           └──────────────┬──────────────┘
                                          │
                                          ▼
                           ┌─────────────────────────────┐
                           │      Purch.Application      │
                           │  - Use Cases & CQRS Handlers│
                           │  - DTOs & Validation Rules  │
                           │  - Abstraction Interfaces   │
                           └──────────────┬──────────────┘
                                          │
                                          ▼
                           ┌─────────────────────────────┐
                           │        Purch.Domain         │
                           │  - Core Domain Entities     │
                           │  - Enums (PricingType, etc.)│
                           │  - Invariants & Business Logic
                           └──────────────▲──────────────┘
                                          │
                                          │ implements
                           ┌──────────────┴──────────────┐
                           │    Purch.Infrastructure     │
                           │  - EF Core 9 / Npgsql       │
                           │  - PostgreSQL Migrations    │
                           │  - JWT Auth & Hashing       │
                           │  - Disk Storage Providers   │
                           └─────────────────────────────┘
```

### 1. `Purch.Domain`
Contains all core domain models, aggregates, domain enums, and business invariants with **zero external framework dependencies**:
- **Entities**: `Tenant`, `Branch`, `Device`, `Staff`, `Item`, `ItemBatch`, `ItemVariant`, `ModifierGroup`, `ComboSlot`, `BundleRule`, `Transaction`, `TransactionLine`, `Shift`, `CreditAccount`, `PurchaseOrder`, `AuditLog`.
- **Enums**: `PricingType` (Unit, Tingi/Weight, VariantMatrix, Combo, Bundle, Service), `BusinessType` (ConvenienceStore, Cafe, Grocery, Retail, ServiceSalon), `Role`, `TingiMode`, `PaymentMethod`, `MovementType`.

### 2. `Purch.Application`
Encapsulates business use cases, orchestrates transaction workflows, and defines persistence and infrastructure contracts:
- **Use Case Handlers**: Tenant provisioning, device pairing, transaction checkout, stock reconciliation, credit ledger updates, and BIR counter increments.
- **DTOs & Contracts**: Strongly-typed request/response models mapping input contracts to domain actions.

### 3. `Purch.Infrastructure`
Provides concrete implementations of application interfaces:
- **Persistence**: Entity Framework Core 9 using the official Npgsql provider (`PurchDbContext`). Includes tenant-scoped query filters for multi-tenant cloud deployments.
- **Authentication**: JWT token generation, claims validation, and Argon2/PBKDF2 secure PIN/password hashing.
- **File System Storage**: Handles tenant-isolated storage for dynamic image uploads.

### 4. `Purch.Api`
The HTTP entry point structured into modular, discoverable Minimal API route groups:
- **Global Error Handling**: Standardized RFC 7807 Problem Details responses.
- **Static File Serving**: Serves dynamic uploads from `wwwroot/uploads/` via `app.UseStaticFiles()`.

---

## API Route Groups

| Route Group | Base Path | Key Capabilities |
| :--- | :--- | :--- |
| **Authentication** | `/auth` | Device pairing, PIN-based staff login, JWT token issuance, and token refresh. |
| **Onboarding** | `/onboarding` | Tenant bootstrapping, branch management, device provisioning, staff invites, and tenant branding settings. |
| **Catalog** | `/catalog` | Products, categories, variant matrices, combo meal configuration, modifier groups, bundle rules, and tingi settings. |
| **Point of Sale** | `/pos` | Real-time transactions, payment processing, cash drawer kick triggers, receipt printing data, and shift management. |
| **Inventory** | `/inventory` | Purchase orders, supplier directory, inter-branch stock transfers, real-time stock-in/out, and low-stock threshold alerting. |
| **Compliance & Reports**| `/reporting` | Automated BIR-compliant X-Readings (mid-shift summary) and Z-Readings (daily fiscal closure), sales analytics, and audit logs. |
| **Credit Ledger** | `/credit-ledger`| Customer accounts, credit balance tracking (*utang* management), repayments, and credit limits. |
| **Kiosk** | `/kiosk` | Public tenant branding endpoints and self-service order dispatching. |
| **Uploads** | `/uploads` | Authenticated multipart image uploading with validation and tenant directory isolation. |
| **Sync Engine** | `/sync` | Batch ingestion of offline queued mutations and conflict resolution. |

---

## Media Upload Pipeline (`POST /uploads/image`)

The backend provides a high-performance endpoint for dynamic tenant assets:
- **Endpoint**: `POST /uploads/image`
- **Security**: Requires an active Bearer token with `Admin` or `Manager` privileges.
- **Validation**:
  - File extension verification: `.jpg`, `.jpeg`, `.png`, `.webp`, `.svg`, `.gif`.
  - Content size enforcement: 10 MB maximum per asset.
- **Storage Strategy**: Files are saved into tenant-isolated paths under `wwwroot/uploads/{tenantId}/{guid}{ext}` to guarantee strict multi-tenant boundary isolation.
- **Output**: Returns both the absolute URL and the relative path (e.g., `/uploads/{tenantId}/{guid}.jpg`) for database persistence.

---

## Deployment Configuration

The application runtime adapts based on the `PURCH_DEPLOYMENT_MODE` environment variable:

1. **`Cloud`**:
   - Designed for containerized clusters (e.g. Render, AWS ECS, Azure Container Apps).
   - Connects to a managed multi-tenant PostgreSQL database (e.g. Supabase).
   - Global query filters partition data automatically using the `tenant_id` claim in caller JWTs.
2. **`Local`**:
   - Designed for isolated local LAN installations running directly inside a store.
   - Executes automatic database migrations (`context.Database.Migrate()`) on startup against an embedded or local PostgreSQL service.

---

## Development & Build Commands

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (x64)
- PostgreSQL 16+ instance (or Docker Desktop)

### Build Solution
```bash
dotnet build
```

### Run Unit Tests
Unit tests run completely in-memory without external infrastructure dependencies:
```bash
dotnet test tests/Purch.UnitTests
```

### Run Integration Tests
Integration tests leverage **Testcontainers** to automatically spin up a dedicated PostgreSQL container to validate database transactions and HTTP endpoints:
```bash
dotnet test tests/Purch.IntegrationTests
```

### Apply Database Migrations
```bash
dotnet ef database update --project src/Purch.Infrastructure --startup-project src/Purch.Api
```

### Add a New Migration
```bash
dotnet ef migrations add <MigrationName> --project src/Purch.Infrastructure --startup-project src/Purch.Api
```

### Run Locally
```bash
dotnet run --project src/Purch.Api
```
The API documentation and Swagger endpoints become available at `https://localhost:5001/swagger`.
