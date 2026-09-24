# Purch.io React Migration

Status legend: DONE / IN PROGRESS / TODO / BLOCKED / BACKEND GAP / BROWSER LIMITATION / NEEDS DECISION.

The web app lives in `web/` (React 19, TypeScript, Vite, Tailwind 4, react-router 7, TanStack Query 5, zustand, axios, React Hook Form + zod, Vitest, oxlint). It existed before this migration as a catalog-admin slice and is **extended, not rewritten**. The ASP.NET Core backend is the source of truth and is not modified. Deployed backend: `https://purch-io-backend.vercel.app/`.

## Decisions

| # | Type | Decision |
|---|------|----------|
| 1 | DESIGN DECISION | Offline **sales are blocked** on web. No TypeScript pricing engine exists or will be added. Flutter's `pricing_engine.dart` (offline mirror of server pricing) is intentionally not ported. |
| 2 | DESIGN DECISION | Reports follow Flutter: charts on Home and tiles on Business; no dedicated Reports tab. |
| 3 | DESIGN DECISION | Tokens stay in `localStorage` (existing convention, accessed in try/catch). XSS trade-off accepted; backend uses bearer headers, not cookies. |
| 4 | DESIGN DECISION | Tenant is never client-selectable. It derives from the JWT / device pairing code. |
| 5 | PROCESS | Front-end work uses the `impeccable` and `design-taste-frontend` skills. |

## A. Architecture mapping

| Concern | Flutter | React |
|---|---|---|
| Entry | `main.dart` | `web/src/main.tsx`, `app/App.tsx` |
| Routing | go_router, `authGateProvider` redirect, `StatefulShellRoute` | react-router 7, `RequireAuth` + `RequireRole` guards, `AppShell` layout |
| Server state | Riverpod + repositories | TanStack Query (`features/*/queries.ts`) |
| Client state | Riverpod notifiers | zustand (cart UI, selected branch, modals) |
| API | Dio + interceptor, `failure_mapper` | axios `lib/apiClient.ts`, `lib/apiError.ts` |
| Auth storage | flutter_secure_storage | localStorage via `lib/authStore.ts` |
| Token refresh | interceptor, single retry | existing single-flight refresh in `apiClient.ts` |
| Persistence | Drift/SQLite | Dexie/IndexedDB (Phase 9, read cache only) |
| Theming | `app_theme`, tenant branding | ThemeProvider driven by `/tenant/settings`, CSS variables from `docs/design/tokens.json` |
| Forms | Flutter forms | React Hook Form + zod |
| Session reset | `resetSessionScope` | `queryClient.clear()` + store reset on login/logout |
| Role nav | `core/auth/role_nav_policy.dart` | central `permissions/` module |

## B. Feature mapping

| Feature | Flutter | Web destination | Status |
|---|---|---|---|
| PIN + admin login | `features/auth` | `features/auth` (admin login exists; PIN login TODO) | IN PROGRESS |
| Onboarding bootstrap | `features/onboarding` | `/onboarding` (public wizard) | DONE. Three steps (business, branch, admin), Terms and Privacy acceptance required, optional email + password so the owner can also sign in on the web. Ends on the first register's pairing code. Linked from the sign-in page. |
| Home / dashboard | `features/home` | `features/dashboard` | DONE for revenue cards, sales trend, top sellers, branch comparison, low stock, sync-conflict notice. Deferred to Phase 7: sales range picker, stock-movement, staff, shift-attendance, department and category charts, low-stock CSV, payment-reminder badge, shift quick action. Not ported on purpose: the period-over-period % (Flutter derives it client-side from the trend; web shows only server values). |
| Catalog (items, categories, modifiers, variants, batches, bundle, tingi, service, combo, dept, low-stock) | `features/catalog` | `features/catalog` | DONE (untested) |
| Recipe / BOM | catalog + inventory | `features/catalog/pages/RecipePage.tsx` | DONE. Link shows only when tenant `useSeparateInventoryTracking` is on, which only an Admin can read (G11), same as Flutter. |
| Promotions (BOGO, combo, item discount, promo codes) | `features/pos/promos` | `features/promotions` (`/business/promotions`) | DONE. Create and edit for the three rule types; promo codes list and create only (the API has no update). Admin/Manager only. The web never computes promo prices; the server does. |
| Image upload | `core/uploads` | `components/forms/ImageUploadField` + `features/uploads` | DONE for items and categories (replaces the raw URL box). Client checks mirror the API limits. |
| Catalog page tests | `client/test/widget/features/catalog` | Categories, Recipe, Promotions, upload field covered. Still untested: item list/add/edit forms and the per-item sub-pages. | IN PROGRESS |
| Inventory dashboard, low-stock alerts | `home/inventory_tab_screen` | `/inventory` (`InventoryHomePage`) | DONE. Not ported: the "avg. cost" items table (Flutter derives it on the device from purchase orders; web will not duplicate that maths). |
| Movement log, record movement | `movement_log_screen`, `record_movement_screen` | `/inventory/movements`, `/inventory/movements/new` | DONE. Cursor paging, URL-held filters, Sale rows shown as automatic and never recordable by hand. Photo attachment is not offered (Flutter's form has none either). |
| Ingredients (inventory items), physical count, receive delivery | `inventory_item_list_screen` | `/inventory/ingredients` | DONE |
| Suppliers | `supplier_list_screen` | `/inventory/suppliers` | DONE (list and add; the API has no update or delete) |
| Purchase orders | `purchase_order_list_screen`, `create_`, `receive_` | `/inventory/purchase-orders` | DONE. Draft, send, partial receive, confirmed cancel. |
| Branch transfers | `branch_transfer_list_screen`, `create_` | `/inventory/transfers` | DONE. Send, receive and cancel each confirm first. |
| POS cashier (grid, cart, options, promo code, order type, Senior/PWD, payment, receipt, new sale) | `features/pos` | `features/pos` (`/sell`, `/sell/payment`, `/sell/receipt`) | DONE for the core sale. Uses the **server cart** (`/transactions/cart/*`); the web has no pricing engine. Item flows: plain, modifiers, variants, combos, by-weight (manual entry). Payments: cash, bank transfer, manual GCash QR (branch QR shown), Utang. Barcode: exact code + Enter adds the item. Credit-ledger management is done (Phase 8, `/business/customers`). Refunds are blocked: the API has no way to look up a past sale (G19). Manager-PIN Senior/PWD approval for cashiers is not planned (no such flow exists in Flutter either). |
| Shifts (open, close, reconciliation) | `pos/shift_screen` | `features/shifts` (`/sell/shift`) | DONE. The server computes expected cash and variance; the web shows them. Closing asks for confirmation (Flutter does not). Manager PIN field is always shown and the server says when it is needed. Not built: **manual drawer open**, which only writes an audit row and cannot physically open a drawer from a browser (BROWSER LIMITATION, hardware phase). |
| Kiosk order claiming | `pos` (server-backed carts) | `features/pos/KioskOrdersPage` (`/sell/kiosk-orders`) | DONE. Auto-refreshing list for this device's branch; "Take this order" makes it the device cart, then the normal payment flow finishes it. See G15. |
| X/Z reading | `pos` BIR reading | `features/reports` (Reports, "X and Z readings") | DONE. The server's figures are shown as-is. A Z-reading asks for confirmation because it closes the day. Needs a device sign-in (G17). Print via the browser. Not an accredited BIR output (the DTO itself says best effort). |
| Credit ledger (customers) | `features/credit_ledger` | `/business/customers` | DONE for Admin/Manager: accounts, repayments, credit limit changes, erase details (only at zero balance, confirmed), payments due soon. Utang at the till is done (Phase 6). Cashiers cannot reach this page (G18). |
| Reports (staff sales, shift attendance, department sales, stock movement, CSV exports) | `features/reports` | `/business/reports` (Business tile) plus a "Last 30 days" section on Home | DONE. Presets Today / 7 / 30 days and a custom range, in Manila-time days, plus a branch filter. Exports: low-stock reorder CSV (Admin/Manager) and raw sales CSV (Admin only). Not ported: the category-sales chart and the sales-trend range aggregation, which Flutter derives on the device from the dashboard series (D15). |
| Business settings (branding, BIR details, barcode / credit / ingredient-tracking switches) | `features/onboarding` | `/business/settings` (Admin only) | DONE. Saves re-theme the app live. Warns when chosen colours fail contrast (the app would ignore them). |
| Staff | `features/onboarding` | `/business/staff` | DONE. Admin creates and edits; Manager views. Scope, scope id and branch are derived together (D17). No rename or PIN reset exists in the API (G19). |
| Branches, hardware settings, manual GCash QR, departments | `features/onboarding` | `/business/branches` | DONE. Hardware here only records what a branch is set up for; connecting it is the hardware phase. |
| Devices | `features/onboarding` | `/business/devices` (Admin only) | DONE. Pairing codes shown large; new code and PIN reset each confirm because they sign the device out. |
| Audit log | `features/onboarding` | `/business/audit-log` | DONE. Filters by action, staff and Manila-day dates; older entries load by cursor. |
| Sync conflicts | `core/sync/flagged_sync_screen` | `offline/conflict` | TODO |
| Kiosk | `features/kiosk` | `features/kiosk` (`/kiosk/pair`, `/kiosk`, `/menu`, `/cart`, `/order-type`, `/done`) | DONE. Portrait, customer-facing. Pairs with device code and PIN, builds the kiosk cart (server priced), choosing Dine In or Take Out sends the order, the confirmation shows the prep number and resets itself after 30 seconds. Reuses the POS options dialog. See G22 to G24. |
| Kitchen display / order board | `features/kitchen_display`, `order_board` | `/kitchen`, `/order-board` (each with `/pair`) | DONE. Both poll every 5 seconds for the branch in the token. Kitchen advances a ticket Queued, Preparing, Ready, Picked up. The board shows numbers only. |
| Legal (terms, privacy) | `features/legal` | `/legal/terms`, `/legal/privacy` (public) | DONE. Text carried over word for word by script from `legal_content.dart` (all 22 sections). See D18. |

## C. API mapping

Verified by reading `backend/src/Purch.Api/Endpoints/*.cs`. Role sets: **anyStaff** = Admin, Manager, Cashier, Warehouse. **posOperator** = Admin, Manager, Cashier. **supervisor** = Admin, Manager. **catalogManager** = Admin, Manager. **inventoryManager** = Admin, Manager, Warehouse. **reportGenerator** = Admin, Manager. **reviewer** (sync flagged) = Admin, Manager. The backend has no OpenAPI, so TypeScript types are hand-written from `Purch.Application/**` DTOs. Scope (Tenant/Branch/Department) is enforced in the application layer (`ReportScopeResolver`, `BranchScopeGuard`), not by endpoint policy.

| Area | Endpoints | Auth / role | React consumer |
|---|---|---|---|
| Health | `GET /health`, `/health/ready` | anonymous | connectivity check |
| Auth | `POST /auth/login` (DevicePairingCode, Pin), `/auth/admin-login`, `/auth/refresh`, `/auth/logout`, `/auth/password-reset/request\|confirm` | anonymous, rate limited | `features/auth`, `apiClient` |
| Onboarding | `POST /onboarding/bootstrap` | anonymous | `features/onboarding` |
| Staff | `GET /staff` (Admin, Manager); `POST`, `PUT /staff/{id}` (Admin) | role | `features/staff` |
| Branches | `GET /branches` (any auth); `POST`, hardware-settings, manual-gcash-qr (Admin); `/branches/{id}/departments` GET any, POST Admin | role | `features/branches` |
| Devices | `/devices`, reset-pairing-code, reset-pairing-pin | Admin | `features/business` |
| Tenant settings | `GET /tenant/settings`; `PUT` branding, bir, barcode, credit-ledger, inventory-tracking | Admin | theme + `features/business` |
| Audit | `GET /audit-logs` | Admin, Manager | `features/business` |
| Catalog reads | `/categories`, `/items`, `/modifier-groups`, `/items/{id}/{modifier-groups,recipe,batches,bundle-rules,variants,combo-components}` (ETag/304) | any auth | `features/catalog` |
| Catalog writes | POST/PUT on the above, plus tingi-config, service-duration, department, low-stock-threshold | catalogManager | `features/catalog` |
| POS cart | `GET cart`, `POST cart/lines`, `PUT\|DELETE cart/lines/{id}`, `PUT cart/promo-code`, `PUT cart/order-type`, `POST cart/payments`, `GET receipt-sequence`, `POST checkout`, `GET kiosk-pending`, `POST kiosk-pending/{id}/claim` (all under `/transactions`) | posOperator | `features/pos` |
| POS supervisor | `POST cart/void`, `POST {id}/refund`, `PUT cart/senior-pwd-discount` | supervisor | `features/pos` |
| Shifts | `GET /shifts/current`, `POST /shifts/open`, `/shifts/close`, `/shifts/manual-drawer-open` | posOperator (close rate limited) | `features/pos` |
| Promotions | `GET /promo-codes`, `/promos/{bogo,combos,item-discounts}` (Admin, Manager, Cashier); POST/PUT (Admin, Manager) | role | `features/promotions` |
| Credit ledger | `GET`, `POST {id}/payments` (posOperator); create, `GET reminders`, `PUT {id}/credit-limit`, `POST {id}/anonymize` (supervisor) | role | `features/pos` |
| Inventory | `/inventory/dashboard`, `/inventory/movements`, `/inventory-items` (+`physical-count`, `receive`), `/suppliers`, `/purchase-orders` (+`mark-sent`, `cancel`, `receive`), `/branch-transfers` (+`mark-in-transit`, `mark-received`, `cancel`) | inventoryManager | `features/inventory` |
| Uploads | `POST /uploads/image` | Admin, Manager, Warehouse | shared uploader |
| Reports | `POST /reports/x-reading`, `/z-reading`; `GET /reports/sales-dashboard`, `/inventory/movement-summary`, `/inventory/low-stock-export.csv`, `/staff-performance`, `/department-sales` | supervisor | `features/dashboard`, `features/reports` |
| Reports (admin) | `GET /reports/sales/transactions-export.csv` | Admin | `features/reports` |
| Sync | `POST /sync`; `GET /sync/flagged`; `POST /sync/flagged/{id}/acknowledge` | anyStaff; flagged = supervisor | `offline/conflict` |
| Kiosk | `POST /kiosk/session` (anonymous); `/kiosk/cart`, `/cart/lines`, `/cart/order-type`, `/cart/submit`, `/kiosk/branding` | Kiosk role only | `features/kiosk` |
| Displays | `POST /order-board/session`, `/kitchen-display/session` (anonymous); `GET /order-board/pending`; `GET /kitchen-display/pending`; `PUT /kitchen-display/orders/{id}/status` | OrderBoard / KitchenDisplay | `features/kiosk` |

JWT claims: `sub`, `tenant_id`, `role`, `scope_type`, `scope_id`, `branch_id`, `device_id`, `device_session_ver`. Staff and admin tokens last 30 minutes; kiosk tokens 24 hours. Claims are decoded client-side for navigation only; the server enforces everything.

## D. Data mapping

| Dart (`features/*/domain`) | TypeScript | Source of truth |
|---|---|---|
| Catalog models, `pricing_type`, `tingi_mode` | `features/catalog/types.ts` | `Purch.Application/Catalog` DTOs (`ItemDto`, `CategoryDto`, `ModifierGroupDto`, `ItemVariantDto`) |
| POS cart, payment enums | `features/pos/types.ts` | `TransactionDto`, `PaymentDto`, `CheckoutRequest` |
| Sync DTOs (`sync_dto.dart`) | `offline/sync/types.ts` | `SyncBatchRequest`, `SyncBatchResultDto` |
| Inventory, supplier, PO, transfer | `features/inventory/types.ts` | inventory DTOs |
| Hardware config enums | `services/hardware/types.ts` | branch hardware-settings DTO |

Persistence models (Dexie, Phase 9): one row, the saved query cache of catalog and branch structure, stamped with the tenant id. There is no cart draft, queued-sale or receipt-sequence store, because offline sales are blocked.

## E. Offline mapping

| Flutter | Browser equivalent |
|---|---|
| `PendingSyncQueue`, `QueuedSales`, `SaleSyncCoordinator` | **Not ported.** Offline sales blocked (Decision 1). |
| `LocalCartDrafts` | Not ported; the cart is server-side (`/transactions/cart`). |
| `CachedCatalogLists`, stale-catalog banner | **DONE.** TanStack Query cache saved to IndexedDB (Dexie, `offline/db`), restored on load. Only the roots `items`, `categories`, `modifierGroups`, `branches`, `departments` are saved (`cachePolicy.ts`); a new feature stays out of the cache until it is added there. Data older than 24 hours is dropped. `StaleDataNotice` says when the shown data was saved (item list). |
| `CachedBranding` | Already covered by the localStorage branding cache (G10); not duplicated in Dexie. |
| `DeviceIdentity` (receipt sequence) | Not ported. |
| Flagged conflicts (`/sync/flagged`) | **DONE.** `/business/sync-conflicts` (Admin/Manager): review list, Acknowledge, Home notice links to it. |
| Offline banner | **DONE.** Says the loaded catalog can be browsed and that sales and changes are unavailable. |

Safety rules of the cache:
- **Tenant isolation:** the saved snapshot is stamped with the tenant id from the token. A different tenant, or nobody signed in, gets nothing and the row is deleted. Sign-out also wipes it (`authStore.clearTokens`).
- **No personal data:** staff, customers, credit, audit, reports, settings, carts and sales are never saved.
- **No write queue:** mutations use `networkMode: 'always'`, so a save fails immediately and toasts, instead of being held and replayed on reconnect (possibly hours later, out of context).
- **Storage failures never throw** (private windows, quota): the app just runs without the cache.

This is a **parity gap versus Flutter**, chosen deliberately to avoid duplicating the pricing engine in the browser.

## F. Hardware (from `client/lib/core/hardware`)

| Device | Class | Web approach | Status |
|---|---|---|---|
| Barcode scanner | A / B | USB/Bluetooth keyboard-mode scanners: a fast run of keys ending in Enter is read as a scan anywhere on Sell (`hardware/scanner/wedge.ts`), and the search box still works. Camera scan uses the browser's `BarcodeDetector` (Chrome and Edge, secure page). | DONE. Camera scanning needs a real device to try; only the unsupported and mocked paths are tested. |
| Receipt printer | B / C | Browser print. The receipt is a named 80 mm or 58 mm page (`@page`), chosen in Hardware settings. Raw ESC/POS was not built (see G25). | DONE for browser print. Direct ESC/POS: NOT BUILT. |
| Cash drawer | C | It opens from a pulse sent through the receipt printer, so it needs the same raw printer link. Not faked. | NOT BUILT (G25) |
| Scale | B | Web Serial (`hardware/scale`): CAS and Mettler-Toledo lines parsed by a port of the Flutter parsers, zero and tare, and a live panel. A weight is only accepted when settled, recent, above zero, not overloaded and in a known unit (kg, g, lb; converted to kg). | DONE. Not tried on a physical scale (G26). |
| Customer-facing display | B | A second window at `/customer-display` fed by `BroadcastChannel`; shows welcome, the order, amount to pay, and thank you with change. Shows only server figures. | DONE. Same browser only (G27). |
| Kiosk OS lock | E | Full screen button on the kiosk, kitchen display, order board and customer display. Hides the browser bars only. | BROWSER LIMITATION |

## G. Discrepancy and gap log

| ID | Type | Item |
|---|---|---|
| D1 | DOCUMENTATION CONFLICT | WORKFLOW.md/PAGES.md describe five tabs including Reports; Flutter removed the Reports tab. Web follows Flutter. |
| D2 | DOCUMENTATION CONFLICT | Docs say Home/Sell; Flutter labels the tabs Dashboard/Cashier. Web uses Home/Sell. |
| D3 | DOCUMENTATION CONFLICT | ARCHITECTURE.md section 10 ("deferred") is stale; the case study says those items shipped. |
| D4 | DOCUMENTATION | `docs/WEB-ARCHITECTURE.md` was empty; now points here. |
| D5 | IMPLEMENTATION | `web-ci.yml` runs lint and build but not `npm test`. |
| G1 | BACKEND GAP | No OpenAPI/Swagger; types are hand-written. |
| G2 | BACKEND GAP | CORS is blocked unless `CORS_ALLOWED_ORIGINS` includes the web origin (deployment config). **Confirmed 2026-09-24:** an OPTIONS preflight from `http://localhost:5173` to `purch-io-backend.vercel.app/auth/login` returns 405 with no CORS headers. The Vercel env var must be set to the web origin(s); in dev, use the Vite proxy. |
| G10 | BACKEND GAP | `GET /tenant/settings` (the only branding read besides `/kiosk/branding`) is Admin-only, so Manager/Cashier/Warehouse cannot load tenant branding. Web caches the last branding an Admin loaded on the device and otherwise uses Purch.io defaults. Needs a decision: a staff-readable branding endpoint. Not changed. |
| D6 | IMPLEMENTATION CONFLICT | Flutter shows low stock only to Admin/Manager, but `/inventory/dashboard` also allows Warehouse. Web shows Running low to Warehouse too (the backend permits it and it is that role's main job). |
| G11 | BACKEND GAP | The `useSeparateInventoryTracking` flag lives in `/tenant/settings` (Admin-only), but recipes are editable by Manager (`catalogManager`). A Manager therefore never sees the Recipe link. Flutter has the same behaviour. A staff-readable flag would fix both. Not changed. |
| D7 | IMPLEMENTATION | Every mutation failure now toasts a friendly message (central `MutationCache`), where the pre-existing catalog pages showed nothing or a raw message. A mutation can opt out with `meta: { silent: true }`. |
| G12 | BACKEND GAP | A Department-scoped account's `scope_id` is a department id, and the token carries no branch for staff. The web cannot narrow branch pickers for it (Branch scope is narrowed); the API's `BranchScopeGuard` still rejects other branches. |
| D8 | IMPLEMENTATION | Inventory movements, purchase orders and transfers reference catalog **Items** (not ingredients). The pickers therefore list catalog items. |
| D9 | IMPLEMENTATION | `ConfirmModal` was upgraded in place: dialog semantics, focus on Cancel, Escape closes, 48px targets. All destructive actions use it. |
| G13 | IMPLEMENTATION / RISK | **The POS needs a paired device.** Every cart, receipt sequence and sale is keyed to `device_id` + `branch_id`, and only a staff PIN sign-in carries them. An email admin sign-in cannot sell (the Sell tab explains this). A web terminal should be its **own** paired device: two clients on one device code would share one open cart and one receipt sequence. |
| D10 | IMPLEMENTATION CONFLICT | Flutter prices the cart on the device and sends one `POST /transactions/checkout`. Web uses the server cart routes (which Flutter only uses for kiosk-claimed carts) because the web must not duplicate the pricing engine and offline sales are blocked. Consequence: one round trip per cart change. |
| D11 | IMPLEMENTATION CONFLICT | Voiding the cart is Admin/Manager-only on the server cart route (Flutter's local cart lets a cashier void). Web hides "Clear cart" from cashiers; they remove lines instead. Senior/PWD stays Admin/Manager-only, shown disabled to cashiers as in Flutter. |
| D12 | IMPLEMENTATION CONFLICT | Flutter blocks payment on dine-in/take-out tenants until an order type is chosen, but that vertical flag needs `/tenant/settings` (Admin-only, G10). Web always offers Dine In / Take Out and treats it as optional. |
| G14 | BACKEND GAP | The server returns no VAT breakdown, TIN, business name or cashier name with a sale. Flutter derives VAT (total/1.12) on the device and reads the TIN from the Admin-only settings. The web receipt is therefore a **sale summary** (lines, promos, discounts, total, tender, change) and says it is not the BIR official receipt. An official receipt needs a server-side receipt endpoint. |
| D13 | IMPLEMENTATION | Extra over Flutter: the GCash screen shows the branch's uploaded QR and account (from `GET /branches`); Flutter only prints an instruction. |
| D14 | TECH DEBT | Some pre-existing forms (`promotions`, `inventory`, catalog) `await mutateAsync` inside handlers; a refused save shows the shared error toast but also logs an unhandled rejection. The POS uses callback mutations. The others should be moved over. |
| G15 | BACKEND GAP / RISK | `POST /transactions/kiosk-pending/{id}/claim` refuses when the device already has an **open** cart, even an empty one, and voiding is Admin/Manager-only. The web creates an empty open cart whenever Sell opens (the server cart is get-or-create), so a **cashier who has visited Sell cannot claim a kiosk order** until a manager clears the cart. Flutter avoids this because its cart is local until payment. Mitigations in place: the claim button explains the rule, and Home links to Kiosk orders directly (a cashier who goes there before opening Sell has no cart yet). A proper fix is a backend change: let the owner of an empty cart discard it, or have claim replace an empty cart. Not changed. |
| G16 | RISK | `GET /shifts/current` answers the JSON literal `null` for "no shift". The web treats null as no shift; an HTTP error is shown as an error, never as "no shift". |
| G17 | IMPLEMENTATION | X and Z readings are per device and need `device_id` + `branch_id`, so an email admin sign-in cannot take them. A Manager or Admin must sign in with a device code and PIN. |
| D15 | IMPLEMENTATION CONFLICT | Flutter's Home builds a category-sales chart and re-buckets the sales trend by day/week/month on the device. Web does not compute those; it shows only what the report endpoints return (revenue cards, the daily trend and top sellers from `/reports/sales-dashboard`). A category or granularity report needs a server endpoint. |
| D16 | IMPLEMENTATION | Report ranges are sent as UTC instants that start at Manila midnight (UTC+8), matching how the API counts a day. Custom ranges are validated before any request is made. |
| G18 | BACKEND / DESIGN | Customer accounts and repayments live under Business, which Cashier does not have, so a cashier cannot record a customer's repayment on the web (the API allows it, `posOperator`). Flutter reaches it from its own Home. Needs a decision on a cashier-facing entry point. |
| G19 | BACKEND GAP | No endpoint to rename a staff member or reset their PIN (only devices have PIN reset), and no endpoint to look up past transactions, so **refunds** (`POST /transactions/{id}/refund`, supervisor) have no way to find the sale on the web. Refunds are therefore not built. |
| D17 | IMPLEMENTATION | The API stores staff `scopeType`, `scopeId` and `branchId` without checking they agree, and reports and stock limits read them. The web derives all three from one choice (whole business, one branch, or one department with that department's branch) and locks admins to whole-business. |
| G20 | BROWSER LIMIT | The offline cache is per browser profile and is only restored after a token exists, so an expired session that cannot refresh offline shows the sign-in page, not the cached catalog. Browsing offline works only while the session is still valid. Deciding whether to allow read-only access with an expired token is a product and security call. |
| G21 | DESIGN | Only the item list shows the "saved on" notice so far. Other cached lists (categories, branches, modifier groups) restore silently. Extend `StaleDataNotice` to them if wanted. |
| D18 | NEEDS DECISION | The Terms and Privacy text is placeholder copy ("a reasonable starting draft" in the Flutter source) and describes on-device storage that the web does not share (the web keeps a token and a small cache in the browser). It was carried over unchanged, not adapted. It needs legal review before real use. |
| G3 | BACKEND GAP | `Content-Disposition` is not exposed, so CSV filenames cannot be read by the browser. **Handled:** the web names downloads itself (`low-stock-reorder-<date>.csv`, `sales-transactions-<date>.csv`). Note the CSV endpoints also send no real download header, so the web fetches the text and saves it as a file with a byte-order mark for Excel. |
| G4 | BACKEND GAP | `/sync` records metadata only; payloads are applied via `/transactions/checkout`. Unverified. |
| G22 | BACKEND GAP | The kiosk has no way to empty its cart (there is no void route for the Kiosk role), and a customer who walks away leaves their lines in it. The next customer sees them, exactly as in Flutter. The web does not hide or delete them silently. Needs a kiosk void or an idle-expiry on the server. |
| G23 | DESIGN | A browser stores one session per site, so a browser paired as a kiosk or display cannot also be used for staff. It shows a plain screen saying so, with Sign out. Use a separate browser profile per device. A staff token is likewise kept out of device screens and a device token out of the staff shell (`RequireAuth`, `RequireDevice`). |
| G24 | DESIGN | Resetting a public device (ending its session) is behind a 3 second hold on the heading for the kiosk and order board, so a customer cannot strand it, and a visible Unpair button on the kitchen display. Re-pairing needs the device PIN. A full screen button is provided on each device screen (F). |
| G25 | BROWSER LIMIT | Direct receipt printing (ESC/POS) and the cash drawer were not built. Browsers cannot send raw commands to a printer driver. It would need Web Serial or WebUSB against a printer that shows up as a port, which cannot be verified without that hardware, or a small local bridge program. Browser print covers receipts. The drawer has to be opened by hand, and the Flutter option to open it on cash sales has no web equivalent. |
| G26 | RISK | The scale code follows the Flutter parsers and was tested against a mocked serial port. It has not been tried on a physical CAS or Mettler-Toledo scale. Flutter only read from network (TCP) scales; the web reads USB or serial ones, so the first real test may need a different baud rate or line format. Weighed quantities are kilograms only, as in Flutter (a litre-priced item would be wrong). |
| G27 | BROWSER LIMIT | The customer display works only in the same browser as the till (a second window on a second monitor). Flutter served it over a local HTTP server so a separate tablet could show it. That is not possible from a browser without a relay. It shows only the order and total. |
| G5 | RISK | `/order-board/session` and `/kitchen-display/session` are anonymous and not rate limited. |
| G6 | RISK | Scope enforcement for catalog/promo writes is unverified. |
| G7 | RISK | PIN login checks every active user's BCrypt hash in the tenant (latency). |
| G8 | RISK | Vercel serverless deployment: rate limits are per instance; cold starts. |
| G9 | RISK | Tokens in localStorage are exposed to XSS. |

## H. Delivery phases

1. Foundation: config, PIN login, JWT claims, theme provider, toasts, error mapping, app shell
2. Role-aware navigation
3. Home / dashboard
4. Catalog gaps (promotions, recipe, uploads) and tests
5. Inventory
6. POS and checkout (server cart)
7. Reports
8. Business / settings
9. Offline read cache and conflict screen (DONE)
10. Kiosk and displays (DONE)
11. Hardware (DONE, except direct printing and the cash drawer)
12. Regression (Playwright end-to-end, DONE; see section J)

## J. End-to-end tests

Playwright drives a real browser against the **real backend and a real Postgres**. Nothing is mocked. `npm run e2e` (from `web/`) does everything: `e2e/stack/run-backend.mjs` starts the database and the API, the web dev server proxies `/api` to it, and `e2e/global-setup.ts` creates a brand new business through the API (categories, three items with stock, a manager, a cashier, a warehouse officer, and a kiosk, order board and kitchen display device). Each test then signs in through the API and only uses the screens for what it is testing.

Running it locally needs .NET 9 and either Docker or a local PostgreSQL install:
- Set `DOTNET` to the dotnet executable if it is not on the PATH.
- The database comes from Docker (`postgres:16`). If Docker is not usable, a private Postgres is started from installed binaries (`PG_BIN`, or the standard install folder) in `web/e2e/.pgdata` on port 55432. `E2E_DB=docker` or `E2E_DB=local` forces one.
- `PW_CHANNEL=chrome` (or `msedge`) uses an installed browser instead of Playwright's own download.
- Ports: API 5099, web 5180, database 55432 (`E2E_API_PORT`, `E2E_WEB_PORT`, `E2E_DB_PORT`).

What is covered (36 tests, 35 running and 1 known-problem test marked `fixme`):
- **Sign-in:** register code and PIN, wrong PIN message, owner email and password, redirect when signed out, sign out clearing the session.
- **Roles:** the tabs each role sees, pages each role is turned away from, Manager without Devices and Settings, admin-only pages by address.
- **Device sessions:** a kiosk token cannot enter the staff shell, a staff token cannot use a device screen, unpaired kiosk goes to pairing.
- **Register:** two items and a cash sale with change computed by the server, barcode scanner typing, admin email sign-in told to use a device.
- **Kiosk to kitchen to board:** the customer orders, the kitchen ticket shows the lines, the status moves through preparing, ready and picked up, and the order board follows across three separate browsers.
- **Counter:** a cashier takes a kiosk order and is paid, a shift is opened and closed with a matching count, Home loads for an admin.
- **Customer display:** a second window follows the sale, including one opened after the order started.
- **Offline:** the saved catalog is shown with no connection and says how old it is, selling is paused, sign-out wipes the saved data, and another business never sees it.
- **Quick adds:** two items tapped back to back both reach the cart; five taps against a server made 1.5 seconds slow are all accepted at once, shown as "adding" rows, merged into fewer requests, and end at the right total; a burst of seven taps on one item comes out as seven.
- **Admin:** add an item, add a staff member who can then sign in, a reused PIN is refused, a manager only sees the staff list.

Each test uses its own client address (`X-Forwarded-For`) so the 10-per-15-minutes sign-in limit is never shared. Tests run one at a time because every device owns one open cart on the server.

| # | Type | Note |
|---|------|------|
| G28 | TEST LIMIT | The backend runs in **Cloud** mode against the local database, because only that mode honours `X-Forwarded-For`. Local (on-premises) mode is not covered. File storage and uploads point at a dummy address and are not tested. |
| G29 | TEST LIMIT | The hardware (scale over serial, camera scan, printing, full screen, second monitor) cannot be driven by the test browser and is covered by unit tests only. |
| G30 | NOT COVERED | Direct comparison with the Flutter client's behaviour is not automated. Flutter has no test hook for it, so parity was checked by reading its code (sections B and C). |
| G31 | UNVERIFIED | The CI job (`e2e` in `web-ci.yml`) has not run yet. It follows the same steps used locally, with Docker for Postgres and Playwright's Chromium. Its first run may need small fixes. |
| D21 | IMPLEMENTATION | **Register adds are queued, not awaited** (`features/pos/addQueue.ts`, `useCartAdds.ts`). Every tap is recorded at once and sent in order, one request at a time, because the server keeps one cart per device and merges lines, so two at once could race. Taps on the same plain item that are waiting are merged into one request with the summed quantity. The screen shows waiting adds as "Adding..." rows with no price (the web still has no pricing of its own), and editing, promo codes and Charge stay disabled until the queue is empty, so nothing can be charged before the server has priced it. A failed add is named in a message and the rest carry on. The queue is emptied when the session ends. Adding no longer blocks the item grid, and dialogs close the moment they are confirmed. When a business has no modifier groups at all, the per-item modifier lookup is skipped. The kiosk menu uses the same queue. |
| D22 | IMPLEMENTATION | **Flutter register**: pricing was already local, but every tap first awaited a fresh network lookup of that item's modifier groups (the provider was dropped as soon as the tap finished) and the first add of each item then fetched them a second time for pricing. The lookup is now one shared provider kept for five minutes (a failure is never kept), the pricing code reads through it, and the "Added" confirmation replaces the previous one instead of queueing a four-second snackbar per tap. Not changed: the kiosk cart notifier still shows a loading state on every add. |
| D20 | KNOWN PROBLEM | A test marked `fixme` reproduces G15: a cashier who has merely opened Sell has an empty open cart, and the server then refuses to hand them a kiosk order (400). Only a manager can clear that cart. |

## I. Follow-ups queued by the product owner

To be done after the remaining delivery steps.

1. **Name the app "Purch.io".** `npm run dev` still prints `web@0.0.0 dev` and the browser tab title is "web". Set the package name and the page title.
2. **Vary the Home charts.** Use different chart types on Home instead of only bars and raw numbers. The figures must still come from the server.
3. **Faster add to cart (web and Flutter). DONE, see D21 and D22.** Adding an item made the register wait for the server, and the next item could not be added until it finished. Cashier speed is the top priority, so adds no longer block each other. Prices stay the server's; there is no client pricing.

Per phase: `npm --prefix web run lint`, `tsc -b`, `npm --prefix web test`, `npm --prefix web run build`.
