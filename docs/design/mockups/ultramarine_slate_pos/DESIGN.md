---
name: Ultramarine Slate POS
colors:
  surface: '#faf8ff'
  surface-dim: '#d2d9f4'
  surface-bright: '#faf8ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f3ff'
  surface-container: '#eaedff'
  surface-container-high: '#e2e7ff'
  surface-container-highest: '#dae2fd'
  on-surface: '#131b2e'
  on-surface-variant: '#444653'
  inverse-surface: '#283044'
  inverse-on-surface: '#eef0ff'
  outline: '#757684'
  outline-variant: '#c4c5d5'
  surface-tint: '#3755c3'
  primary: '#00288e'
  on-primary: '#ffffff'
  primary-container: '#1e40af'
  on-primary-container: '#a8b8ff'
  inverse-primary: '#b8c4ff'
  secondary: '#006c4a'
  on-secondary: '#ffffff'
  secondary-container: '#82f5c1'
  on-secondary-container: '#00714e'
  tertiary: '#532a00'
  on-tertiary: '#ffffff'
  tertiary-container: '#743d00'
  on-tertiary-container: '#ffa85d'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dde1ff'
  primary-fixed-dim: '#b8c4ff'
  on-primary-fixed: '#001453'
  on-primary-fixed-variant: '#173bab'
  secondary-fixed: '#85f8c4'
  secondary-fixed-dim: '#68dba9'
  on-secondary-fixed: '#002114'
  on-secondary-fixed-variant: '#005137'
  tertiary-fixed: '#ffdcc3'
  tertiary-fixed-dim: '#ffb77d'
  on-tertiary-fixed: '#2f1500'
  on-tertiary-fixed-variant: '#6e3900'
  background: '#faf8ff'
  on-background: '#131b2e'
  surface-variant: '#dae2fd'
typography:
  display-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 24px
    fontWeight: '700'
    lineHeight: 32px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 24px
    letterSpacing: -0.005em
  body-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
    letterSpacing: 0em
  body-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: 0em
  body-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
    letterSpacing: 0.01em
  label-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: 0.01em
  label-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.02em
  label-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 10px
    fontWeight: '700'
    lineHeight: 14px
    letterSpacing: 0.04em
  currency-display:
    fontFamily: Plus Jakarta Sans
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 36px
    letterSpacing: -0.02em
  currency-body:
    fontFamily: Plus Jakarta Sans
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 20px
    letterSpacing: 0em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 0.75rem
  margin: 1rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 0.75rem
  space-lg: 1rem
  space-xl: 1.5rem
---

## Brand & Style

The design system powers an omnichannel point-of-sale platform tailored for high-volume retail environments and fast-paced Philippine specialty cafés. The brand personality balances uncompromising enterprise rigor with modern ergonomic precision: calm, authoritative, frictionless, and laser-focused on transactional clarity.

The visual style synthesizes the clean line-work of modern developer utilities with the tangible tactility of dedicated checkout hardware (reminiscent of Linear and Square Register). Surfaces are anchored by crisp structural 1px borders, subtle low-elevation drop shadows, and high information density. Every touch target is optimized for rapid multi-finger tablet interaction in landscape orientation. Visual fluff is systematically stripped away to eliminate cashier cognitive load during peak rushes.

## Colors

The palette establishes a high-contrast, functional hierarchy engineered for varying café and retail lighting conditions:

- **Primary (`#1E40AF` Ultramarine):** The dominant action anchor. Reserved for primary operational triggers, selected tab indicators, active customer orders, and checkout execution paths.
- **Secondary (`#059669` Emerald):** Strictly communicates settled states, online sync health, paid invoices, and successful fiscal transactions.
- **Tertiary (`#D97706` Warm Amber):** Flags combos, pending kitchen modifiers, open split-bills, tax overrides, and non-blocking warnings.
- **Neutral Surface / Canvas (`#F8FAFC` Slate Canvas):** High-clarity neutral backdrop that mitigates eye fatigue while maintaining contrast against white product cards.
- **Text Primary (`#0F172A` Deep Slate):** Uncompromising contrast against canvas and container tiers, ensuring instant readability of unit prices, item codes, and modifiers.
- **Container Tint (`#EFF6FF` Ultramarine Wash):** Applied to active cart lines, highlighted modifiers, and selected operational filters.
- **Structural Border (`#E2E8F0` Slate Border):** Monolithic 1px boundary dividing catalog grids, payment keypad matrices, and cart line items.

## Typography

Plus Jakarta Sans governs the entire interface, balancing geometrical legibility with modern humanist details.

### Numerical & Currency Rules
All currency displays, stock counts, weight values, and numeric calculations strictly leverage **OpenType tabular lining figures (`font-variant-numeric: tabular-nums lining-nums`)**. This guarantees precise vertical decimal alignment across receipt summaries, order lines, and balance ledgers. 

The Philippine Peso sign (`₱`) is treated as an integral typographic element: always rendered in matching font weight and scale, directly prepended to amounts without whitespace (e.g., `₱1,250.00`).

### Hierarchy & Restraint
Uppercase styles are restricted exclusively to `label-sm` metadata flags (e.g., `TAX INCL`, `GCASH`, `DINE-IN`, `VOID`). Body copy remains sentence case with tight letter spacing for fast horizontal scanning across dense multi-column receipt tickets.

## Layout & Spacing

The interface is constructed explicitly for fixed-orientation tablet landscape viewports (10.2" to 13" screens, baseline 1280×800 to 2048×1536).

### Two-Panel Structural Layout
- **Left Panel (60–65% width):** The catalog exploration surface. Houses top-level category pills, nested subcategory strips, search/barcode input, and a responsive CSS grid of item cards (4 to 5 columns depending on display density).
- **Right Panel (35–40% width):** The order ledger and register execution dock. Persistent, non-collapsible vertical structure housing ticket header info, scrollable itemized lines, discounts/promos, payment summary, and bottom checkout keypad actions.

### Spacing Principles
- Minimum interactive touch bounds remain 44×44px, even within compact receipt tables.
- Tight 0.75rem (12px) gutters maintain maximum visual density without accidental tap collisions.
- No fluid margins: canvas borders remain hard-locked to 1rem (16px) margins to preserve ergonomic palm rest boundaries around tablet hardware bezels.

## Elevation & Depth

Visual separation avoids heavy, blurry drop shadows in favor of industrial, tactile precision built on 1px crisp borders and subtle micro-shadows:

- **Surface Baseline (Canvas):** `#F8FAFC` flat surface.
- **Card / Surface Default:** Crisp `#FFFFFF` fill framed by a 1px solid border in `#E2E8F0`.
- **Level 1 (Interactive Grid Cards / Resting Buttons):** `box-shadow: 0 1px 2px 0 rgba(15, 23, 42, 0.05)`.
- **Level 2 (Modals, Overlays, Active Keypad Triggers):** `box-shadow: 0 4px 6px -1px rgba(15, 23, 42, 0.08), 0 2px 4px -2px rgba(15, 23, 42, 0.04)`.
- **Tactile Active Press:** On tap (`:active`), transform downward by 1px with inner border highlight (`#1E40AF`) and suppressed elevation to deliver instantaneous haptic feedback under the finger.
- **Divider Treatment:** 1px hairline borders (`#E2E8F0`) with zero shadow bleed.

## Shapes

The interface adopts a refined, modern industrial feel with `roundedness: 1`:
- Base component elements (buttons, text inputs, list line rows) utilize `0.25rem` (4px) corner radii.
- Cards, checkout ledger containers, and modal dialogs step up to `0.5rem` (8px).
- Status badges and category filters utilize `0.25rem` or full pills strictly for category pill navigators.
- Sharp internal data cells within tables feature 0px inner boundaries to emphasize grid stability and alignment.

## Components

### Buttons & Keypad Actuators
- **Primary Checkout Trigger:** Full-width `#1E40AF` solid button with white text, 52px height, bold tabular totals, and instantaneous visual response on touch.
- **Secondary Actions:** White background, 1px `#E2E8F0` border, `#0F172A` text, hovering to `#F8FAFC`.
- **Destructive (Void / Cancel):** Red-tinted alert state with 1px border (`#FEE2E2` fill, `#DC2626` text and border).
- **Payment Keypad:** Monolithic 3×4 numerical grid with 1px hairline dividers; cells sized at 56px minimum height for uninhibited thumb input.

### Cart Line Items (Order Ledger)
- Individual ticket items feature a 2-line layout: item title and attributes on line 1; modifier chips, quantity stepper `[- 1 +]`, and right-aligned tabular currency on line 2.
- Active or editing line item shifts background to container tint (`#EFF6FF`) with a left 3px accent stroke in `#1E40AF`.

### Category & Filter Chips
- Resting state: Outline chip, 36px height, 1px `#E2E8F0` border, `#0F172A` text.
- Selected state: Inverted `#0F172A` slate fill with `#FFFFFF` text or `#1E40AF` fill indicating active workflow mode.

### Product Catalog Cards
- Minimalist inventory tile consisting of a 1:1 or 4:3 product thumbnail (or fallback typographic monograms for cafés), item label (2 lines max truncation), and bold price (`₱XX.XX`).
- Out-of-stock items feature a 40% opacity layer with an overlay pill badge (`OUT OF STOCK`) and disabled pointer events.

### Input Fields & Quick-Search
- Barcode/SKU search input sits permanently fixed at the catalog header.
- Height: 44px, inset padding 12px, accompanied by a 1px border `#E2E8F0`. Focus state triggers an immediate 2px outer ring in `#1E40AF` without layout shift.

### Omnichannel & Status Pills
- Micro status badges (e.g., `GCASH`, `MAYA`, `GRABFOOD`, `FOODPANDA`, `SYNCED`): compact 20px height, uppercase `label-sm`, subtle background washes with deep text equivalents (e.g., Emerald `#059669` wash for paid sync).