# Purch.io Design System & Asset Integration

This directory contains the design system specifications, design tokens, and visual reference comps for Purch.io.

## Visual Design Reference Comps (`docs/design/images/`)

1. **`hero_section.jpg`**
   - **Vertical Focus:** Omnichannel retail and hospitality POS & Kiosk hero.
   - **Tone & Mood:** Warm editorial minimalism, tactile physical counter materials, warm timber surfaces, matte cream hardware, soft directional lighting.
   - **Role:** Reference visual comp for the high-taste web showcase and marketing presentation.

2. **`bento_grid.jpg`**
   - **Vertical Focus:** Multi-vertical capabilities across retail, grocery, cafe, convenience, department store, and service salons.
   - **Tone & Mood:** Structured bento grid layout with high-contrast typography, functional density, soft rounded corners, and micro-metrics.
   - **Role:** Reference architectural design layout for dashboard and features.

---

## Client Asset Integration (`client/assets/images/`)

Bundled in the Flutter application under `flutter.assets` in `pubspec.yaml`:

| Asset File | Purpose & Screen Usage | Fallback / Behavior |
| :--- | :--- | :--- |
| `kiosk_poster_default.jpg` | **Customer Kiosk Landing Screen (`kiosk_landing_screen.dart`)**<br>Universal multi-vertical showcase hero (grocery, cafe, retail, services). | Displayed automatically when tenant has not uploaded a custom poster or when loading fails. |
| `sample_qr_ph.jpg` | **GCash / QR Ph Settings (`manual_gcash_qr_settings_screen.dart`)**<br>Official merchant countertop QR stand template. | Quick "Use Sample" action and visual guide for merchants setting up static QR codes. |
| `combo_rice_bowl.jpg` | **Catalog & Kiosk Items (`add_item_screen.dart`, `kiosk_item_list_screen.dart`)**<br>Filipino rice bowl combo meal preview. | Quick chip in Add Item screen; rendered in kiosk item grid. |
| `beverage_iced_latte.jpg` | **Catalog & Kiosk Items (`add_item_screen.dart`, `kiosk_item_list_screen.dart`)**<br>Artisan iced latte beverage preview. | Quick chip in Add Item screen; rendered in kiosk item grid. |

---

## Dynamic Tenant Uploads (`POST /uploads/image`)

To allow merchants to dynamically upload custom branding posters, item images, and QR Ph stand photos, the backend provides an image upload endpoint:

- **Endpoint:** `POST /uploads/image`
- **Authentication:** Bearer token (Admin / Manager)
- **Allowed Formats:** `.jpg`, `.jpeg`, `.png`, `.webp`, `.svg`, `.gif`
- **Max File Size:** 10 MB
- **Storage Location:** `backend/src/Purch.Api/wwwroot/uploads/{tenantId}/{guid}{ext}`
- **Static File Serving:** Enabled via `app.UseStaticFiles()` in `Purch.Api/Program.cs`
- **Response Format:**
  ```json
  {
    "url": "https://localhost:5001/uploads/{tenantId}/{guid}.jpg",
    "relativePath": "/uploads/{tenantId}/{guid}.jpg",
    "fileName": "poster.jpg",
    "size": 1048576,
    "contentType": "image/jpeg"
  }
  ```

---

## Unified Image Resolver (`PurchImage`)

The Flutter client uses `PurchImage` (`client/lib/core/widgets/purch_image.dart`) to transparently resolve:
1. **Local Assets:** Strings starting with `assets/` (e.g. `assets/images/kiosk_poster_default.jpg`) -> `Image.asset`
2. **Dynamic Backend Uploads:** Strings starting with `/uploads/` -> automatically prefixed with `AppConfig.apiBaseUrl` and loaded via `Image.network`
3. **External Remote URLs:** Strings starting with `http://` or `https://` -> loaded via `Image.network`
4. **Graceful Fallbacks:** Shimmer loading state, rounded border clipping, and automatic fallback to default asset or placeholder on network error.
