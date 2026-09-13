# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Stack
Flutter client (Riverpod, Drift SQLite, Dart) with an ASP.NET Core (.NET 9) Minimal API backend. Two primary client shells:
1. Landscape tablet shell (min 48x48dp tap targets, high contrast, dense functional layout) for Cashier / POS / Admin / Reports / Inventory.
2. Portrait kiosk shell (client/lib/features/kiosk/) for customer self-service ordering (customer-facing, tactile, inviting, distinct from staff app).

## Users
- **Cashiers / Floor Staff**: Computer-illiterate to semi-skilled staff operating tablet-in-a-stand POS in Philippine SME environments (sari-sari, grocery, convenience, café, retail). Need speed, high contrast, error prevention, instant tactile feedback, and large tap targets.
- **Store Managers / Admins**: Configure pricing, manage inventory ledgers, review Z/X-readings, oversee BIR compliance, and monitor sales.
- **Customers**: Walk-up self-service shoppers at portrait kiosks in convenience/QSR settings. Need an inviting, frictionless, transparent order-building experience.

## Product Purpose
Config-driven, offline-first POS and store management system for Philippine SMEs. Solves vertical rigidity, fragile connectivity, compliance headaches (BIR Z/X-readings, Senior/PWD discounts), and lack of merchant branding by providing a single adaptable core across multiple business types.

## Positioning
\ One POS core, BIR-ready and offline-capable, that reconfigures itself — not its price tag — for whatever you sell.\

## Operating Context
- Physical environment: Countertop tablet stands (landscape 10\-12\ Android/iOS/Windows tablets), convenience store checkout counters, and vertical customer kiosks.
- Connectivity: Unstable internet requiring local SQLite storage and bidirectional offline sync queue.
- Currency & Locale: Philippine Peso (PHP / ₱), 12% VAT, BIR sequential receipt formatting, Senior Citizen/PWD mandated discounts.

## Capabilities and Constraints
- Dual-shell architecture: Landscape staff shell (~90 screens) vs Portrait customer kiosk flow.
- 145 passing Flutter client unit/widget tests acting as strict regression gate.
- Deliberate non-visual widget structures: PopScope(canPop: false) on receipt and kiosk confirmation screens; disabled payment tiles on unbuilt methods.
- Offline resilience via Drift DB and synchronization queue.
- Multi-vertical pricing engine: Unit, Weight/Volume, Bundle/Promo, Service, Variant Matrix, Combo.

## Brand Commitments
- Name: Purch.io
- Assets: Logo square icon (client/assets/logo.jpg), horizontal wordmark (client/assets/wordmark.png).
- Token system target: docs/design/ (scaffolded for design tokens).
- Dynamic branding capability: Merchants can configure logo, primary color, and fonts per tenant.

## Evidence on Hand
- Full documentation suite (docs/PROPOSAL.md, ARCHITECTURE.md, ARCHITECTURE-ESSENTIALS.md, PAGES.md, PROJECT-CASE-STUDY.md).
- Brand assets: client/assets/logo.jpg, client/assets/wordmark.png.
- 145 passing unit & widget tests in client/test/.

## Product Principles
1. **Speed & Ergonomics over Decoration**: Staff operate under pressure and long shifts; large touch targets (min 48x48dp), clear visual hierarchy, and one primary action per screen.
2. **Color carries operational meaning**: Green for paid/success, red for stock-out/error/void, amber for warning/low-stock. Never use color purely decoratively where it could mislead staff.
3. **Customer Delight vs Staff Utility**: Customer-facing kiosk should feel tactile, modern, and delightful, while staff landscape screens prioritize scanability, density, and throughput.
4. **Resilient & Transparent**: Clear offline/online sync state and explicit confirmation on destructive actions.

## Accessibility & Inclusion
- Large tap targets (min 48x48dp).
- High-contrast text and controls for varying retail ambient lighting.
- Screen reader accessibility / semantic labels preserved on icons and buttons.
