# Purch.io — System Workflow (Non-Technical)

*A walkthrough of how a real business actually uses the system, day to day, across the verticals it's built to support: convenience stores, cafés, groceries, department stores, specialty retail (e.g. clothing/apparel), and service salons. Every vertical shares the same underlying engine — what changes per business is configuration, not code.*

---

## 1. Getting Set Up (Owner, one-time)

The owner signs up and picks a **business type** — Convenience Store, Restaurant/Café, Grocery, Clothing/Specialty Retail, Department Store, Service Establishment, Sari-Sari Store, or Other. That single choice pre-arranges which catalog tools and POS behaviors are relevant later; nothing about the underlying system changes, only which options are surfaced.

At the same time, the owner:
- Uploads a **logo**, sets a **theme color**, and (optionally) a **font** — applied globally across POS screens, the self-order kiosk, and printed/digital receipts.
- Sets up the first **branch** and its first **device** (the tablet or terminal that will actually run the app), generating a device pairing code.
- Creates the **admin account** with a PIN — PINs, not passwords, are how every staff member signs into a shared terminal throughout the day.
- Decides on **deployment mode**: a shared cloud installation (the default, works anywhere with internet), or a dedicated on-premises installation running on a server inside the store's own network (for a business that wants its data to never leave the building). This choice doesn't change how staff use the app day to day — it only changes where the data physically lives.
- Configures **BIR compliance basics** — TIN, registered business name and address — and business-specific settings like whether every item requires a barcode, what kind of receipt printer is connected, and how the cash drawer behaves.

Setup itself runs as a short guided wizard (business identity → first branch → admin account) rather than one long form, and the owner must accept the **Terms of Service and Privacy Policy** on the final step before the business is created — both documents stay reachable afterward from Business Settings for the owner or any manager to re-check.

## 2. Building the Catalog (Owner or Manager)

Every catalog item is created with a name, price, optional barcode/SKU, a category, and a **pricing type** — this is the one setting that makes the same system behave differently per vertical:

- **Unit pricing** — the default for most items in a **convenience store** or **sari-sari store**: scan or tap, price is fixed per piece.
- **Weight/volume (tingi) pricing** — mainly for a **sari-sari store** selling rice, cooking oil, or other bulk goods by the kilo or liter, with batch/lot tracking and expiry dates for perishables, and a "tingi" mode letting the same item be sold as a whole pack *or* broken into smaller fixed sizes or custom increments.
- **Variant matrices** — for a **clothing/specialty retail** shop or a **department store**, where one product (e.g. a T-shirt) exists in a grid of sizes and colors, each with its own stock count and optional price override.
- **Combo/meal builder** — for a **café or restaurant**, where a value meal is built from slots (e.g. "choose a drink," "choose a side"), each slot pointing at a category the customer picks from, with optional upcharges for substitutions.
- **Bundle/multi-buy pricing** — "buy 2 get 1," "3 for ₱99" — usable in any vertical carrying multi-buy promos, most common in convenience/grocery.
- **Service/appointment pricing** — for a **service salon** (haircuts, manicures, spa treatments) or any service-based business: a fixed price plus a configured service duration, since a service doesn't have physical stock to deduct.

On top of pricing type, items can carry **modifier groups** (add-ons like "Extra Cheese" or "No Ice," optionally required before checkout — relevant to cafés and any made-to-order vertical) and can be assigned to a **department**, letting a **department store** or **grocery** run one or more sections as concessionaires with their own inventory attribution and a split sales report at the end of the day.

## 3. A Typical Sale (Cashier)

The cashier logs into a paired device with their PIN and opens a fresh cart. From there:

1. **Add items** — tap a unit-priced item to add it directly; a variant-matrix item (clothing, department store) opens a size/color picker first; a combo item (café) opens a slot-by-slot customization sheet; other pricing types route to their own entry flow.
2. **Apply discounts** — toggle the Senior Citizen/PWD 20% discount only after physically verifying the customer's ID (this is a manual, staff-verified action, never automatic), or enter a promo code if the business runs one.
3. **Choose fulfillment**, where relevant (e.g. dine-in vs. take-out for a café).
4. **Charge** — pick a payment method: **Cash** (system computes change), **Bank Transfer** (cashier confirms manually after checking the transfer landed), **Manual GCash QR** (a merchant-printed static QR code, same manual-confirmation trust model), or **Utang/Credit** (charges a returning customer's account instead of collecting payment now — see §8). Digital-gateway QR Ph, e-load/bill payment, and split-tender payments are recognized by the system but intentionally not available yet — the app tells the cashier why rather than pretending to accept them.
5. **Receipt** — on success, the sale gets a sequential, gap-auditable receipt number and a receipt is shown; the cashier's next action is always "New Sale," so there's no way to accidentally back into a completed sale's cart.

Stock decrements automatically in the background the moment a sale completes — nobody manually reconciles the register against the shelf count after the fact.

## 4. The Self-Service Kiosk (Customer, unattended)

For verticals that support it — most naturally a café, but usable anywhere the owner wants a self-order station — a second, portrait-orientation terminal lets customers build their own order without a cashier: a welcome screen, a category carousel, the same combo/variant customization sheets a cashier would use, a fulfillment choice, and a submitted order with a short **prep number** the customer takes to the counter.

The kiosk deliberately **never handles payment, discounts, or utang** — a customer can build and submit an order, but only a cashier at the main POS can finalize payment against it (the system enforces this at the server level, not just by hiding buttons). A cashier picks up a submitted kiosk order by its prep number, and it drops into the exact same payment flow as any other sale.

## 5. When the Internet Drops

The terminal keeps working — sales queue locally on the device and sync automatically once connectivity returns; the cashier doesn't do anything differently. If two devices happen to submit conflicting changes to the same record while both were offline, the system never silently picks a winner and discards the other: the later-timestamped change is flagged for a manager to review, and nothing is ever quietly dropped.

A dedicated on-premises installation (see §1) is even more resilient to this, since its server lives on the same in-store network as every terminal — only truly external services (like a live payment gateway, once one is connected) are affected by an internet outage at all.

## 6. Restocking, Transfers, and Shrinkage (Owner or Storekeeper)

- **Purchase orders** — create a PO against a supplier with expected quantities and costs, mark it sent, and receive stock against it (in full or in partial deliveries) — receiving generates the matching stock-in movements and keeps the PO's own status (Draft → Sent → Partially Received → Received) automatically in sync with what's actually been received, rather than something a person tracks by hand.
- **Branch transfers** — for a business with more than one location, move stock from one branch to another with a status tracker (Pending → In Transit → Received); the transfer nets to zero on total stock, but each leg is logged so the movement history at each branch tells the full story.
- **Every other kind of stock change** — spoilage, damage, customer returns, and manual count corrections — is logged as its own typed event with a reason, not lumped into a generic "adjustment." This gives the owner a real picture of *why* stock disappears, not just that it did — especially relevant for a grocery or convenience store carrying perishables.
- **Low-stock alerts** — set a reorder threshold per item and the inventory dashboard flags anything running low before it actually runs out.

## 7. Utang / Credit Ledger (Owner or Manager, where enabled)

A business that extends informal credit to regular customers — common in a sari-sari or convenience store — can turn on a **customer credit ledger**. Each customer account carries a name, phone number, an optional address, and a credit limit. A cashier can charge a sale to a customer's account instead of collecting payment at checkout, but the system won't let a charge push the account past its limit. Repayments are recorded separately and can never exceed what's actually owed. The system also surfaces which accounts have a payment coming due soon or are already overdue, so nothing falls through the cracks — though it's the owner's job to actually follow up; the system doesn't send reminders on its own yet.

## 8. End of Day (Owner/Manager)

- **Shift / cash-drawer reconciliation** — a cashier opens a shift with a starting cash float and closes it with an actual count; the system computes what the drawer *should* hold based on cash sales during that shift, flags any mismatch, and requires a manager's PIN to approve closing out a shift that doesn't balance.
- **BIR X-reading and Z-reading** — a mid-shift summary (X) can be run any time without affecting anything; the end-of-day reset (Z) rolls the day's sales into the permanent running total and can't be undone, matching how a real fiscal register behaves.
- **Sales dashboard** — today/this-week/this-month revenue, a short trend view, top-selling items, and — for a business with more than one branch — a side-by-side branch comparison.
- **Inventory reports** — a stock-movement summary broken down by type (stock-in, spoilage, damage, returns, etc.) over any date range, plus a downloadable low-stock/reorder list.
- **Staff performance** — sales totals per cashier and a shift-attendance summary flagging anyone with a history of cash-count discrepancies.
- **Department sales split** — for a department store or grocery running concessionaire sections, a report splitting revenue between each department and general (no-department) sales.

## 9. Multiple Staff, Controlled Access

Every staff member has a **role** — Admin, Manager, Cashier, or Warehouse — governing what they're allowed to *do*: cashiers ring up sales, warehouse staff log inventory movements, but only an Admin or Manager can void a transaction, apply an after-the-fact discount, approve a shift-count mismatch, or view full reports. The tablet app itself reflects this at a glance — a cashier's home screen only shows Home and Sell, a warehouse staffer's only shows Home and Inventory, and only Admin/Manager see the full five-section layout (Home, Sell, Reports, Inventory, Business) — but this is a convenience, not the actual security boundary: every one of those actions is independently re-checked and enforced on the server regardless of what the device's screen happens to show. Separately, every staff member has a **scope** (the whole business, one branch, or one department) governing what they're allowed to *see* — a manager scoped to one branch of a multi-branch business can't view or act on another branch's data, even though they hold the same role as a manager elsewhere. Every sensitive action — voids, discount overrides, inventory adjustments, credit-limit decisions — leaves a timestamped audit trail of exactly who did it.

## 10. Growing Beyond One Type of Store

The same core engine reconfigures per business type by design, not by exception: a café gets the combo-builder and modifier groups it needs; a clothing shop or department store gets the variant matrix and department/concessionaire tools it needs; a grocery gets weight/volume pricing and batch/expiry tracking; a service salon gets duration-based service pricing. The customer-facing experience changes shape per vertical — the underlying pricing, inventory, payment, sync, and compliance engine underneath it does not. Adding support for a new vertical is meant to be a configuration change, not a rewrite.

---

**Status note:** This describes the system as actually built and running (backend + client, both deployment modes), not a speculative plan — the reference vertical used to validate the engine during development was convenience store, but every vertical named above is supported by the same shipped codebase. Some payment methods (QR Ph gateway, e-load/bill payment) are recognized by the system but intentionally not yet connected to a live provider — the app is explicit about this rather than faking it.
