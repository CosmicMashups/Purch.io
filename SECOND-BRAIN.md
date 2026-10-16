# SECOND-BRAIN.md: deep reference

Companion to [BRAIN.md](BRAIN.md) (task router). This file is the map: layers, routes, entities, auth, rules, decisions, tests, gaps. It links to the detailed docs instead of copying them. Audited against `main` on 2026-10-16.

## 1. Architecture

- **Backend** `backend/src`, projects `Purch.Domain` → `Purch.Application` → `Purch.Infrastructure` → `Purch.Api`.
  - Domain: entities (`Entities/`), enums (`Enums/`, 35 files), base types in `Common/` (`Entity`, `TenantScopedEntity`, `ITenantScoped`, `ISoftDeletable`), `CountryProfiles.cs` (currency and VAT rate per country; only PH).
  - Application: services, DTOs, repository and provider interfaces by area: `Approvals, Audit, Auth, Catalog, Common, CreditLedger, Department, Devices, EquipmentInventory, Inventory, Kiosk, Lifecycle, Onboarding, Orders, Pos, Promotions, Reporting, Reports, Shifts, Supplier, Sync`.
  - Infrastructure: `Persistence/` (DbContext, Configurations, Migrations), `Repositories/`, `Auth/`, `Storage/`, `Retention/`, `Deployment/`, `SyncEngine/`, `Lifecycle/`, `Orders/`.
  - Api: `Program.cs` (DI, middleware, health), `Endpoints/`, `ErrorHandling/`, `RateLimiting/`, `Middleware/`, `Auth/` (policy extensions), `Http/ConditionalGet.cs`.
- **Web** `web/src`: `App.tsx` (all routes), `layouts/`, `features/*`, `components/`, `lib/`, `hardware/`, `offline/`, `permissions/`, `theme/`, `security/`, `hooks/`, `test/`.
- **Flutter** `client/`: Riverpod, Drift, go_router. Offline sale queue exists there. Not updated for U1 to U4.
- **Deploy**: Cloud mode = Supabase (Postgres, Storage, password check) + backend on Vercel (live per `memory`; Render config also exists) + web on Cloudflare Pages/Workers or Vercel. Local mode = `installer/` (Docker or Windows service), identity provider `Local`. Env var names: `.env.example`.

Diagrams: `docs/architecture/diagrams/*.mmd`. Overview: `docs/architecture/overview.md`, `modules.md`, `workflows.md`, `error-flows.md`, `auth.md`.

## 2. Backend request pipeline

Order in `Program.cs`: `CorrelationId` → `SecurityHeaders` → `RequestLogging` → `UseExceptionHandler` → `UseCors` → `UseAuthentication` → `TenantResolutionMiddleware` → `UseAuthorization` → `UseRateLimiter` → endpoints. DI is one flat list of `AddScoped<I, Impl>()` lines (about 62) plus singletons (`IPinHasher`, `IJwtTokenService`, `IDeploymentContext`, `IFileStorage`, `IPosSettings`).

Rate limit policies (per IP, fixed window): `auth-sensitive` 10 per 15 min, `shift-approval` 10 per 15 min, `refresh` 120 per 15 min. Approver PIN lockout: 5 failures per device.

Errors: `AddProblemDetails` with `traceId`; `GlobalExceptionHandler` maps `AppException` subtypes to HTTP status; `AppException.Code` becomes problem-details `code` (for example `total_changed`). Clients match on `code`.

Idempotency: `/sync` by `SyncedRecord` key; checkout by `ClientSaleId`; cart payments by `PaymentKey`; imports by `Idempotency-Key` header.

Unit of work: `IUnitOfWork.ExecuteInTransactionAsync` (retrying strategy; delegate must be re-runnable).

Background: `RetentionHostedService` / `RetentionSweeper` (stale tokens, sync records); Vercel cron hits `GET /internal/retention-sweep` daily 03:00.

## 3. Endpoint files and route prefixes

All in `backend/src/Purch.Api/Endpoints/`; no `/api` prefix on the server (the web proxy adds it).

| File | Owns |
|---|---|
| `AuthEndpoints` | `/auth/sign-in`, `/refresh`, `/register-session`, `/logout` |
| `OnboardingEndpoints` | `/onboarding/bootstrap`, `/branches`, `/staff/*`, `/enrol/*`, `/devices/*` (pair, session, roster, unlock), `/tenant/settings/*`, `/audit-logs` |
| `CatalogEndpoints` | `/categories`, `/items` (+ recipe, equipment, batches, variants, combo-components, tingi-config, bundle-rules, suppliers), `/modifier-groups`, `/modifiers` |
| `PosEndpoints` | `/transactions/*`: cart, lines, open item, notes, discounts, price override, promo-code, senior-pwd, order-type, park, resume, parked list and discard, payments, checkout, refund, exchange, void, receipt-sequence, kiosk-pending and claim |
| `KioskEndpoints` | `/kiosk/*` (session, branding, cart, submit) |
| `DeviceDisplayEndpoints` | `/order-board/*`, `/kitchen-display/*` |
| `CustomerDisplayEndpoints` | `/customer-display/state` |
| `ShiftEndpoints` | `/shifts/current`, `/open`, `/close`, `/manual-drawer-open` |
| `PromoCodeEndpoints` | `/promo-codes`, `/promos/bogo`, `/promos/combos`, `/promos/item-discounts` |
| `ReportingEndpoints` | `/reports/*`: x-reading, z-reading, sales-dashboard, sales-mix, tax, gross-profit, discounts, voids, refunds, staff-performance, department-sales, dashboard-insights, inventory (movement-summary, valuation, cogs), approvals-review, CSV exports |
| `CreditLedgerEndpoints` | `/credit-ledger/*` |
| `InventoryEndpoints` | `/inventory/*` (dashboard, movements, export, imports, restock-suggestions), `/inventory-items`, `/stock-takes`, `/branch-transfers`, `/suppliers`, `/purchase-orders`, `/purchase-requests`, incoming receiving |
| `EquipmentEndpoints` | `/equipment` |
| `LifecycleEndpoints` | `/lifecycle/{kind}/...` (impact, deactivate, reactivate, delete, restore) |
| `OrdersEndpoints` | `/orders`, `/orders/export.csv` |
| `SyncEndpoints` | `/sync`, `/sync/flagged` |
| `UploadEndpoints` | `/uploads/image` |
| `Program.cs` inline | `/health`, `/health/ready`, `/internal/retention-sweep` |

Feature-by-feature route list with roles: [docs/product/feature-inventory.md](docs/product/feature-inventory.md).

## 4. Domain model (entities by area, `Purch.Domain/Entities`)

- **Identity and tenancy:** Tenant, TenantMetering, Branch, Department, Account, User, Membership, MembershipBranch, EnrolmentInvite, LocalCredential, RefreshToken, PasswordResetToken, SignInThrottle, Device, DeviceCredential.
- **Catalog:** Category, Item, ItemVariant, ItemModifier, ModifierGroup, ItemModifierGroup, ModifierGroupCategoryItem, ItemModifierIngredient, ItemComboComponent, ItemRecipeLine, ItemBatch, ItemEquipment, Equipment.
- **Promotions:** PromoCode, BogoPromoRule, BundlePromoRule, ComboPromoRule, ItemDiscountPromoRule.
- **Sales:** Transaction (status includes Parked = 5; VAT stamps; `CustomerId?` unused), TransactionLine (`UnitCost` snapshot, manual discount fields, note), TransactionLineModifierSelection, TransactionLineComboSelection, Payment (many per sale: `Sequence`, `Reference`), PaymentGatewayTransaction, Adjustment, AdjustmentReturnLine, AdjustmentReplacementLine, Shift, ReceiptSequence, CartBatchReceipt, KioskPrepSequence, CustomerDisplayState.
- **Credit (utang):** CustomerCreditLedger, CreditTransaction. The only customer-like entity.
- **Inventory and purchasing:** InventoryItem (ingredients), InventoryCategory, InventoryMovement, StockTake, StockTakeLine (own `xmin` concurrency token), BranchTransfer(+Line), IncomingReceivingReport(+Line), Supplier (+SupplierContact), PurchaseOrder(+Line), PurchaseRequest(+Line), ItemSupplier, DocumentSequence, ImportBatch.
- **Cross-cutting:** AuditLog, SyncedRecord.

Key enums: `Role` (Admin, Manager, Cashier, Warehouse, Kiosk, OrderBoard, KitchenDisplay, CustomerDisplay), `MembershipRole` (Admin, Manager, Staff), `StaffDuty`, `PaymentMethod` (Cash, QrPh, BankTransfer, ManualGcashQr, BillPaymentELoad, UtangCredit, Split), `TransactionStatus`, `ManualDiscountKind` (Percent, Fixed), `MovementType`, `PurchaseOrderStatus`, `StockTakeStatus`, `ImportKind`, `AuditActionType` (about 24+; append only), `LicenseStatus` (Trial, Active, Suspended), `DeploymentMode` (Cloud, Local). ERD: `docs/architecture/diagrams/database-erd.mmd`.

Migrations: `Purch.Infrastructure/Persistence/Migrations`, about 70, `yyyyMMddHHmmss_Name`, latest group are the U1 to U4 ones. Per-entity config: `Persistence/Configurations/`.

## 5. Tenancy, auth and permissions

- **Tenant isolation:** `PurchDbContext` applies a global query filter to every `ITenantScoped` entity; no tenant in context means no rows. Tenant comes from the JWT via `TenantResolutionMiddleware` and `ICurrentTenantProvider` (`HttpContextCurrentTenantProvider`). Cross-tenant reads use `IgnoreQueryFilters()` explicitly.
- **RLS** is enabled on every table with no policies, only to block direct Supabase REST access. The app role bypasses it, so isolation is the EF filter. Startup warns about tables without RLS.
- **Branch scope:** `BranchScopeGuard`, `Membership` + `MembershipBranch`.
- **Sign-in:** email + password (`/auth/sign-in`, Supabase in Cloud, local hash in Local), JWT + rotating hashed refresh tokens. Shared tills: device paired once with a one-time code, then staff pick their name and enter a PIN (`DeviceUnlockService`). Unattended devices get 24h device JWTs. Staff join by invite link (`/enrol/:token`); no email is sent. ADR 0010.
- **Hashing:** BCrypt for passwords and PINs; PIN lookup fingerprint `HmacPinLookupKey`.
- **Roles on the web:** Admin and Manager see Home, Sell, Inventory, Reports, Business; Warehouse sees Inventory; Cashier sees Sell. Devices and Business settings are Admin-only.
- **Permission keys** (`Auth/Permissions.cs`): `cart.park`, `discount.apply`, `price.override`, `openitem.add`, `payment.split`. Admin and Manager have all; Cashier has all five; others none. `For(Role)` is hard-coded (U5 will make it data-driven).
- **Approver PIN:** `ApproverPinMatcher` + `ApproverAuthorizationService` (branch-scoped, lockout per device). Used for discounts, price override, discard parked, refunds, exchange, variance close, import commit, stock-take apply.

## 6. Business rules digest

Full text: [docs/architecture/business-rules.md](docs/architecture/business-rules.md).

- **Pricing:** promos claim quantities in order, no double promo per unit. Senior/PWD: price / 1.12, then 20% off the VATable base, VAT-exempt; excludes promos and manual discounts; Manager/Admin only. Manual discount (percent or fixed, reason, approver) is exclusive with Senior/PWD and promos. Tingi uses `UnitsPerPack`.
- **VAT:** 12% PH. Each completed sale stamps `VatableSales`, `VatAmount`, `VatExemptSales`, `ZeroRatedSales`, `VatRateApplied`, `VatDerived`. Tax report reads stamps, X/Z compute live; they can differ by centavos.
- **POS:** one open cart per device plus parked carts (keep-for hours per tenant). Checkout idempotent on `ClientSaleId`. Split tender: N payments, one receipt, only cash may overpay, at most one utang part. Receipt numbers are strictly sequential per branch and device.
- **Shift:** cashier needs an open shift; blind closing count; variance needs a manager PIN; manual drawer open is audited.
- **Inventory:** stock is tenant-wide; recipes consume ingredients; PO states Draft, Sent, PartiallyReceived, Received, Cancelled; transfers decrement on dispatch, increment on receipt. Stock-take applies `counted - expected_at_count` as a delta, may go negative, idempotent. Import: preview, near-match decisions, all-or-nothing commit, 5,000 rows or 5 MB.
- **Credit:** hard stop at the credit limit unless an approved override; anonymize only at zero balance.
- **Sync:** records deduplicated by idempotency key; later client timestamp wins, the earlier is cancelled and flagged; Manager/Admin acknowledge.
- **Voids:** parked carts expired by the sweeper are excluded from X/Z voided totals; staff voids and approved discards count.

## 7. Web client map

Route table, by guard (from `web/src/App.tsx`):

- Public: `/login`, `/onboarding`, `/legal/:document`, `/enrol/:token`, `/unlock`, `/pair`, `/customer-display`.
- Device (`RequireDevice`): Kiosk `/kiosk`, `/kiosk/menu`, `/kiosk/item/:itemId`, `/kiosk/cart`, `/kiosk/order-type`, `/kiosk/payment`, `/kiosk/processing`, `/kiosk/done`, `/kiosk/printer`; Kitchen `/kitchen`; Order board `/order-board`.
- Staff shell (`RequireAuth` + `AppShell`): `/` (Home); `/sell`, `/sell/shift`, `/sell/payment`, `/sell/receipt`, `/sell/find-sale`, `/sell/exchange`, `/sell/kiosk-orders`, `/sell/hardware`; `/inventory` and `/inventory/{movements, ingredients, equipment, suppliers, purchase-requests, purchase-orders, incoming-receiving, transfers, import, stock-takes}` (+ detail routes); `/reports?tab=`; `/business` and `/business/{promotions, customers, orders, sync-conflicts, staff, branches, audit-log, devices*, settings*}` (* Admin only); `/catalog/{items, categories, modifier-groups}` and item sub-pages.

Feature folders (`web/src/features/`): `auth, branches, business, catalog, credit, dashboard, departments, equipment, inventory, kiosk, lifecycle, onboarding, orders, pos, promotions, reports, shared, shifts, sync, tenant, uploads`. Each has `api.ts` + `queries.ts` (+ `types.ts`, pages, components, tests).

Cross-cutting: API client and refresh (`lib/apiClient.ts`); tokens in localStorage (`purch.accessToken`, `purch.refreshToken`) via `lib/authStore.ts`; error mapping `lib/apiError.ts`; offline cache in Dexie (`offline/db`), caches only `items, categories, modifierGroups, branches, departments, dashboard` for 24h; selling blocked offline; hardware config `purch.hardware` (printer width, scale protocol, drawer, scanner, kiosk slip); theme from tenant branding (`theme/`).

## 8. Tests

- Backend unit: `backend/tests/Purch.UnitTests` (Approvals, Auth, EquipmentInventory, ErrorHandling, Inventory incl. Import, Pos, Promotions, Reporting, `PermissionsTests`).
- Backend integration: `backend/tests/Purch.IntegrationTests` (~70 files, one Postgres per collection via `Fixtures/PostgresContainerFixture.cs`, `PurchApiFactory.cs`, `TestSessions.cs`). Includes tenant isolation, branch scope, RLS coverage, access matrix, payment key, manual discount, parked carts, split tender, VAT stamping, stock-take, import, reporting.
- Web unit: vitest, ~150 `*.test.ts(x)` beside code; `shared/pricing-scenarios.json` drives the pricing mirror test.
- Web e2e: Playwright in `web/e2e` (seed in `global-setup.ts`, helpers in `support/`), workers 1 because each device owns one cart.
- Flutter: `client/test`, ~90 files.
- CI: `.github/workflows/` (web CI: lint, test, e2e types, build, e2e job; backend CI; local-mode CI; Flutter; security; deploys).

## 9. Decisions (ADR digest, `docs/adr/`)

0001 EF Core over Dapper. 0002 Render over Railway (Vercel is the live host; see doc-conflicts C4). 0003 offline sync boundary (ledger built; no per-entity dispatcher). 0004 Dragonpay for bill pay (not connected). 0005 BIR format is best effort, not accredited. 0006 NPC registration tracked outside the repo. 0007 React web client, online-only selling. 0008 tenant-wide stock. 0009 stock-take delta variance. 0010 email sign-in, one-time pairing, PIN unlock.

## 10. Feature status and gaps

- Shipped feature list with IDs: `docs/product/feature-inventory.md`.
- Competitor gap audit: `docs/product/competitor-gap-status.md`. Open P1 items: customer attach, loyalty, CRM (U2); time clock, custom roles (U5).
- Flutter parity (U6): the client lacks parked carts, split tender, manual discounts, open items, cost, stock-take, import, new reports; its local `PricingEngine` must mirror manual discounts, split tender, VAT stamping and the cart cap using the golden cases; it must never receive a Parked transaction.
- Not connected: QR Ph (Xendit), Dragonpay bill pay and e-load. Cash, bank transfer, manual GCash QR and utang work.

## 11. Monetization hooks (nothing enforces them yet)

- `Tenant.LicenseStatus` (Trial, Active, Suspended): defined, unread.
- `Tenant.FeatureFlagsJson` (default `{}`): defined, unread.
- `TenantMetering` (period, transaction count): table only.
- Natural gate points: `Permissions.For` (roles and permissions), branch and device creation (`BranchService`, `DeviceManagementService`), staff creation (`StaffEnrolmentService`), report range parameters in `ReportingEndpoints`, import limits in `ImportService`.
- PRODUCT.md positioning: never gate selling or legal receipts (BIR readings, sequential receipts); gate scale and back-office depth.

## 12. Known stale or open items

- `docs/history/doc-conflicts.md` C1 to C13 (Render vs Vercel, Supabase Auth vs ADR 0002, stale dual-login diagrams, missing `backup_db.sh`, C11 now resolved by `e1a85fc`).
- `docs/history/discrepancies-2026-10-01.md`, `coverage-audit-2026-10-03.md` (numbered file names and test counts are stale).
- At-rest encryption (SQLCipher) is not implemented.
- `docs/engineering/roadmap.md` still has pre-fix parked-item text for U1 and U3; those items were resolved in commits `e1a85fc`, `375a33a`, `de49453`.
- Small residuals recorded in the roadmap: web copy still English only; Filipino l10n in Flutter is partial.

### UI issues seen while taking manual screenshots (2026-10-16, not fixed)

1. Receipt labels a manual 10% cart discount "Less: SC/PWD Discount (20%)".
2. Cart line shows a raw decimal ("Promo: 10.0000% OFF").
3. Stock movement notes for consumption show raw item GUIDs.
4. Audit log shows "Unknown" for action types 11 to 13 and 18 and above (labels in `web/src/features/business/types.ts` stop at 10 and 14 to 17).
5. The item Variants page is unthemed (black "Add Variant" button, different form styling).
6. React console error "two children with the same key `9`" on the unlock or onboarding pages.
7. The shift page shows no running sales totals; they live in the X-reading.
8. Kiosk orders: a manager's GET makes the manager the sale's cashier.
9. X-reading prints a placeholder machine id (`PENDING-MIN-...`).
