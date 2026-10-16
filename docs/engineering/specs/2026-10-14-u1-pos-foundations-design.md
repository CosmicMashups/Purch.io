# U1 design: Foundations + POS selling (park carts, split tender, manual discounts, open item, cost, VAT stamping)

Status: shipped. Date: 2026-10-14. Part of the P1 competitor-gap roadmap (U1 of U6). Pricing/tier gating is out of scope.

## Decisions (agreed in brainstorm)
- Parked carts: `Status=Parked` on `Transaction`; one Open + N Parked per device; filtered unique index unchanged.
- Manual discounts / price override: Manager/Admin only, reason code required, audited.
- Split payments: split tender only (N payments, one receipt); split bill deferred to P2 table management.
- Customers: new `Customer` entity (U2); `CustomerCreditLedger.CustomerId?`; `Transaction.CustomerId?`.
- Cost: manual `Item.CostPrice`, optional update on receiving, snapshot on each sale line; null = unknown.
- Tax: stamp VAT breakdown on `Transaction` at completion; backfill history by derivation.
- Scope: backend + web first; Flutter parity is U6.

## U1 detail: Foundations + POS selling

### U1.1 Data model (one migration per concern, RLS on every new table)
- `Item.CostPrice decimal?`; `InventoryItem.UnitCost decimal?`. Optional tenant toggle `UpdateCostOnReceiving` (default off) hooked in `IncomingReceivingService` (~L117) as last-cost.
- `TransactionLine`: `UnitCost decimal?` snapshot (set in `StageLineAsync` L220-229, L289-298; recipe items sum ingredient cost), `ManualDiscountAmount`, `PriceOverrideFrom decimal?`, `IsOpenItem bool`, `OpenItemName`, `Note`.
- `Transaction`: status `Parked` added; `ParkedLabel`, `ParkedAt`, `ParkedByUserId`; `CustomerId Guid?` (column only; Customer entity in U2, so U1 uses a nullable Guid with no FK until U2); `ManualDiscountAmount`, `ManualDiscountReason`; stamped `VatableSales`, `VatAmount`, `VatExemptSales`, `ZeroRatedSales`, `VatRateApplied`.
- `Payment`: allow N rows per transaction; add `Reference`, `Sequence`. `Transaction.TotalPaid` derived.
- `TenantSettings`: `ParkedCartTtlHours` (default 24), `OpenItemEnabled` (default true).
- Backfill migration: derive VAT breakdown for completed sales with the legacy 12% rule and flag `VatDerived=true`; cost left null ("unknown", excluded from margin, never treated as 0).

### U1.2 Park / resume carts (decision: Status=Parked, one Open + N Parked)
- `POST /transactions/cart/park {label?}`: Open -> Parked (empty cart rejected). `GET /transactions/parked` (branch-wide, any cashier can resume). `POST /transactions/{id}/resume`: if the device has a non-empty Open cart it is auto-parked first (swap, in one DB transaction); empty Open cart is discarded.
- Keep the filtered unique index unchanged (Parked != 0). Review every site assuming Open==in progress: `RequireOwnLineAsync` L1856 (resume transfers device ownership: update `DeviceId`), `ClaimKioskOrderAsync` L1736-1765, kiosk pending query, `CustomerDisplayService`, orders/reporting filters, `RetentionSweeper` (void parked carts past TTL with system audit, `CartParked/CartResumed/CartExpired` added to `AuditActionType`).
- Edge cases: stock/price drift on resume (reprice via `RecalculateTotalAsync`, surface changed lines); promo code expired; kiosk-claimed orders cannot be parked; resumed on a different branch is rejected; concurrent resume uses the existing concurrency token (409 loser).

### U1.3 Split tender (decision: N payments, one sale, one receipt; split bill deferred to F&B tables in P2)
- Extend `RecordPaymentRequest` with `Parts[]` (method, amount, tendered, reference, ledgerId). Sum must equal total; only a Cash part may overpay (change); at most one Utang part, validated against credit limit as today (`ChargeToCreditLedgerAsync`). `Payment` rows written atomically with receipt issue; idempotency stays at `ClientSaleId`.
- `PaymentMethod.Split` stays unused as a sale label, and the transaction summary reports the parts. `EnforceExpectedTotal` unchanged.
- Refund: `RestockRefundedSaleAsync`/`ReverseCreditChargeAsync` must reverse per part; refund settlement lists which methods to return. Z-reading and X-reading cash totals must sum only cash parts (add regression tests).

### U1.4 Manual discounts, price override, open item (decision: Manager/Admin only)
- Line and cart discount (percent or fixed) and per-line price override: `posSupervisor` roles only, reason code required, every action audited (`DiscountOverride` / `PriceOverride`), approver PIN reused for non-manager devices' sessions where applicable.
- Pricing: manual discount is mutually exclusive with Senior/PWD (BIR wins; applying Senior/PWD removes it), and stacks with nothing else to match current exclusivity rule; if a manual cart discount exists, item/promo-code promos are suppressed for the lines it covers. Discount can't exceed line total; VAT computed on the net.
- Open item: `POST /transactions/cart/lines/open {name, price, quantity, categoryId?, vatClass}`; no stock decrement, no item record, flagged in reports; gated by `OpenItemEnabled` and cashier-allowed (price entry is the whole point), audited when above a tenant threshold.

### U1.5 Attach customer (non-credit)
- `PUT /transactions/cart/customer {customerId|null}` writes `Transaction.CustomerId`; in U1 it accepts an existing `CustomerCreditLedger`-linked or (after U2) Customer id; U1 ships the field and endpoint behind the U2 entity, so land U1.5 with U2 if preferred (flagged as a coupling in the spec).

### U1.6 Web (impeccable + design-taste-frontend; tokens in docs/design/tokens.json; Tailwind v4, hand-rolled components, Phosphor icons, react-hook-form+zod)
- `CartPanel`: Park button, "Parked (n)" drawer (list, resume, discard), line action menu (discount, override, note), cart discount, open-item dialog, split-payment rows in `PaymentPage` with remaining/ change display and `CashKeypad` reuse. Approver gating through `useApproverGatedAction`. Item form: cost price + margin hint. Settings: parked TTL, open item, cost-on-receiving.
- Follow web test conventions (`vi.mock('./api')`, `signInAs`, `renderPage`).

### U1.7 Permission shim
- Add `Permissions` constants and a `RequirePermission(key)` helper that today resolves from built-in role -> permission set, so U1 endpoints are declared with permissions and U5 only has to make the mapping data-driven.

### U1 Tests
- Backend integration (Postgres container, `PosEndpointsTests` pattern): park/resume/swap, cross-device resume, TTL sweep, kiosk-claim interaction, split tender sums/overpay/utang limit/idempotent retry/refund reversal, discount authorization matrix (extend `AccessMatrixTests`), discount-vs-Senior/PWD exclusivity, VAT stamping + backfill, cost snapshot, open item, RLS coverage.
- Unit: pricing with manual discount + VAT rounding; web rules tests and page tests.
- Regression gates: all existing backend/web tests, X/Z totals unchanged for single-tender sales.


## Front-end process (applies to U1.6 and every later web unit)
Before building any screen, invoke in this order: `/impeccable` (shape/critique), `/design-taste-frontend` (anti-slop pre-flight), `/ui-ux-pro-max:ui-ux-pro-max` (design-system query, stack `react` + `html-tailwind`, then its pre-delivery checklist). Honour `docs/design/tokens.json` (variance 6, motion 4, density 3) and the 48dp-equivalent touch targets from PRODUCT.md; pass those dials to the ui-ux-pro-max search rather than accepting its defaults. Persisted output, if any, goes under `design-system/` with `--output-dir` at the repo root and must not override existing tokens.
