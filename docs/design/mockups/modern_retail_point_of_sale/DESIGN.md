---
name: Modern Retail Point of Sale
colors:
  surface: '#f9f9ff'
  surface-dim: '#cfdaf2'
  surface-bright: '#f9f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f0f3ff'
  surface-container: '#e7eeff'
  surface-container-high: '#dee8ff'
  surface-container-highest: '#d8e3fb'
  on-surface: '#111c2d'
  on-surface-variant: '#3e4947'
  inverse-surface: '#263143'
  inverse-on-surface: '#ecf1ff'
  outline: '#6e7977'
  outline-variant: '#bdc9c6'
  surface-tint: '#006a63'
  primary: '#005c55'
  on-primary: '#ffffff'
  primary-container: '#0f766e'
  on-primary-container: '#a3faef'
  inverse-primary: '#80d5cb'
  secondary: '#216963'
  on-secondary: '#ffffff'
  secondary-container: '#a8ece5'
  on-secondary-container: '#266d68'
  tertiary: '#485452'
  on-tertiary: '#ffffff'
  tertiary-container: '#606c6a'
  on-tertiary-container: '#e0edea'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#9cf2e8'
  primary-fixed-dim: '#80d5cb'
  on-primary-fixed: '#00201d'
  on-primary-fixed-variant: '#00504a'
  secondary-fixed: '#abefe8'
  secondary-fixed-dim: '#8fd3cc'
  on-secondary-fixed: '#00201e'
  on-secondary-fixed-variant: '#00504b'
  tertiary-fixed: '#d8e5e2'
  tertiary-fixed-dim: '#bcc9c6'
  on-tertiary-fixed: '#121e1c'
  on-tertiary-fixed-variant: '#3d4947'
  background: '#f9f9ff'
  on-background: '#111c2d'
  surface-variant: '#d8e3fb'
typography:
  display-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 40px
    fontWeight: '700'
    lineHeight: 48px
    letterSpacing: -0.02em
  display-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.01em
  headline-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Plus Jakarta Sans
    fontSize: 16px
    fontWeight: '500'
    lineHeight: 24px
  body-md:
    fontFamily: Plus Jakarta Sans
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Plus Jakarta Sans
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
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
  label-numeric:
    fontFamily: Plus Jakarta Sans
    fontSize: 18px
    fontWeight: '700'
    lineHeight: 24px
    letterSpacing: -0.01em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1rem
  margin: 1.5rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
---

## Brand & Style

The design system embodies a modern, tactile, and ergonomically focused retail aesthetic engineered for high-throughput environments. Grounded in organic stability, precision, and physical responsiveness, the system replaces generic corporate blues with an authoritative deep viridian teal. This evokes clarity, natural authority, and calm during peak operational rushes.

The personality balances utilitarian efficiency with high-end boutique tactile qualities. Target users are multi-tasking retail floor staff, inventory managers, and cashiers who interact with touch screens across long shifts. The visual language utilizes structured tactile surfaces, intentional contrast tiers, and clear touch affordances to eliminate input hesitation and minimize visual fatigue.

## Colors

The color system centers around a high-contrast pine viridian core, calibrated to surpass WCAG AAA standards on light backdrops while providing a distinctive identity.

- **Primary (`#0F766E`)**: Deep Viridian / Pine Teal. Used for primary touch triggers, dominant action nodes, selected order items, and primary key states.
- **Primary Hover (`#115E59`)**: Teal 800. Applied on pointer hover and pre-press feedback states.
- **Primary Active (`#042F2E`)**: Teal 950. Expresses direct compression and engagement during active touch taps.
- **Primary Container (`#F0FDFA`)**: Eucalyptus Mist / Teal 50. Serves as the high-visibility tinted background for active register rows, subtle selection filters, order badges, and contextual alerts.
- **On-Primary Container (`#115E59`)**: High-contrast legibility text and icon color within container surfaces.
- **Neutral & Structural (`#1E293B` to `#F8FAFC`)**: Slate neutral scale ensuring structural boundaries, scan grids, and non-tinted panels remain crisp, clear, and glare-free.

## Typography

Plus Jakarta Sans is the sole typographic foundation. The geometry provides rapid legibility across point-of-sale display angles, peripheral glances, and handheld scanner tablets.

- **Monospaced Numerics**: All price displays, SKU reads, order totals, and quantity controls apply tabular figures (`tnum`) to maintain vertical column stability across rapid checkout tally adjustments.
- **Labels & Action Anchors**: Capitalization remains standard sentence case with elevated medium to semi-bold weights for rapid readability under harsh store lighting.
- **Scale Restraint**: Display sizes are reserved strictly for checkout totals, cash calculations, and main category headings, avoiding unnecessary visual noise in product grids.

## Layout & Spacing

The layout is split into an asymmetrical fixed-split operational screen designed primarily for landscape tablet and countertop terminal interaction:

- **Left / Center Zone (Product & Catalog Space)**: Dynamic fluid grid showing category tiles and quick-item cards (minimum hit target: 64px).
- **Right Zone (Order Ledger & Cart)**: Fixed 380px–440px wide ticket container with a sticky checkout execution block at the lower base.
- **Touch Spacing Principles**: All tap elements maintain an internal `space-md` pad and external `space-sm` separation buffer to prevent accidental adjacent mis-taps during high-speed scanning.

## Elevation & Depth

Visual hierarchy leverages crisp tonal boundaries combined with tinted tactile shadows:

- **Primary Action Layer**: Buttons marked with the primary viridian palette cast a dedicated directional drop shadow: `0 4px 12px rgba(15, 118, 110, 0.25)`. On active press, the shadow collapses to `0 1px 3px rgba(15, 118, 110, 0.35)` with an instant 1px downward Y-translation.
- **Surface Elevation**: Ground level rests at `#F8FAFC`. Elevated modules (cart container, modal keypads, floating summary panels) use pure `#FFFFFF` bounded by a 1px border (`#E2E8F0`) and an ambient, low-contrast blur `0 8px 24px rgba(15, 23, 42, 0.06)`.
- **Selected Tonal States**: Highlighted order line-items do not depend solely on borders; they apply the `#F0FDFA` container wash with an inset 2px pine teal left-edge indicator.

## Shapes

The design system employs a balanced `rounded-2` scale (`0.5rem` / `8px` baseline). 

- **Item Tiles & Input Shells**: Standard `0.5rem` (`rounded-md`) radius provides smooth edge definition without breaking catalog density.
- **Cart Panel & Modals**: Outer container corners apply `1rem` (`rounded-lg`) to distinctively enclose larger functional workflows.
- **Pill Badges**: Quantity incrementers, operational status badges (e.g., "Paid", "Held"), and scanner search chips use full rounded pills to create a distinct shape contrast against rectangular inventory cards.

## Components

- **Primary Tactical Buttons**:
  - Background: `#0F766E`
  - Text/Icon: `#FFFFFF`
  - Shadow: `0 4px 14px rgba(15, 118, 110, 0.25)`
  - Hover: Background `#115E59`
  - Active: Background `#042F2E`, transform `translateY(1px)`, shadow `0 2px 4px rgba(15, 118, 110, 0.2)`
- **Secondary & Utility Buttons**:
  - Subtle actions use `#F0FDFA` container background with `#115E59` label color, shifting to `#CCFBF1` on tap.
- **Input Fields & Search Bars**:
  - Neutral white surface, 1px `#CBD5E1` border.
  - Active/Focused state: 1.5px solid `#0F766E` outline with a subtle `0 0 0 3px rgba(15, 118, 110, 0.15)` focus ring.
- **Catalog Cards**:
  - 1px border `#E2E8F0`, surface `#FFFFFF`.
  - Active tap feedback features an immediate edge flash of `#0F766E`.
- **Checkboxes & Selection Radios**:
  - Selected state fill: `#0F766E` with crisp white checkmark/dot.
  - Container background on multi-selection lists activates `#F0FDFA`.
- **Numeric Keypad (Numpad) Buttons**:
  - Clean tactile tiles, white surface with slate label `#0F172A`. Function keys (Enter, Charge) use full `#0F766E` fill.