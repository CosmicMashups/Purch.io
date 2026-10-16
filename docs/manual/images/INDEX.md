# Manual screenshots

Produced by `npm run manual:shots` in `web/` against a seeded demo business ("Kapihan ni Aling Nena"). Do not edit by hand.

| File | Role | Route | Caption |
| --- | --- | --- | --- |
| 01-login.png | Anyone (signed out) | `/login` | The sign-in page: email and password for owners and managers. |
| 01-login-mobile.png | Anyone (signed out) | `/login` | The sign-in page on a phone. |
| 02-onboarding-business.png | Anyone (signed out) | `/onboarding` | Setup, step 1: the business name and what kind of business it is. |
| 03-onboarding-admin.png | Anyone (signed out) | `/onboarding` | Setup, last step: the owner picks a name, a PIN for registers, and an email and password. |
| 04-unlock-pin.png | Cashier (on a paired register) | `/unlock` | Unlocking a register: choose your name, then type your PIN on the keypad. |
| 05-enrol-invite.png | New staff member (invite link) | `/enrol/<invite token>` | Opening an invite link: the new person checks their details and sets a password and PIN. |
| 06-pair-device.png | Device being set up | `/pair` | Pairing a new device with the one-time code from Business, Devices. |
| 10-home-dashboard.png | Admin | `/` | The home dashboard: today and the last 30 days of revenue, busiest days and top sellers. |
| 10-home-dashboard-mobile.png | Admin | `/` | The home dashboard on a phone. |
| 11-sell-grid.png | Cashier | `/sell` | The register: categories on the left, items to tap in the middle, the cart on the right. |
| 11-sell-grid-mobile.png | Cashier | `/sell` | The register on a phone: the same sale, stacked to fit a narrow screen. |
| 12-sell-cart-with-items.png | Cashier | `/sell` | A sale in progress: lines, quantities, order type and the total to charge. |
| 13-options-dialog.png | Cashier | `/sell` | Choosing options (sugar level and add-ons) for a drink before it goes in the cart. |
| 14-open-item-dialog.png | Cashier | `/sell` | Open item: ring up something that is not in the catalogue by typing a name and price. |
| 15-discount-dialog-pin.png | Cashier (approved by a manager PIN) | `/sell` | A manual discount asks for a manager or admin PIN before it is applied. |
| 16-park-drawer.png | Cashier | `/sell` | Parked carts: sales set aside to finish later, with resume and discard. |
| 17-payment-cash.png | Cashier | `/sell/payment` | Paying in cash: the keypad and quick amounts, with the change worked out for the cashier. |
| 18-payment-split.png | Cashier | `/sell/payment` | Split payment: part in cash, the rest by GCash, until the parts add up to the total. |
| 19-receipt.png | Cashier | `/sell/receipt` | The receipt after a sale, with Print, Exchange and Refund. |
| 20-refund-dialog.png | Cashier (needs a manager PIN) | `/sell/receipt` | Refunding a sale: a reason and a manager or admin PIN are required. |
| 21-find-sale.png | Cashier | `/sell/find-sale` | Find a sale: type the receipt number to refund or exchange it. |
| 22-exchange.png | Cashier (needs a manager PIN) | `/sell/exchange` | Exchange: choose what comes back and what the customer takes instead; the difference is settled. |
| 23-shift-open.png | Cashier | `/sell/shift` | Opening a shift: count the cash in the drawer before the first sale. |
| 24-shift-close-x-reading.png | Cashier | `/sell/shift` | An open shift: the running summary, and the closing cash count with a manager PIN. |
| 25-kiosk-orders.png | Cashier | `/sell/kiosk-orders` | Orders placed at the kiosk, waiting for a cashier to take them and collect payment. |
| 26-hardware.png | Cashier | `/sell/hardware` | Hardware: the receipt printer, cash drawer and barcode scanner for this register. |
| 30-items-list.png | Admin | `/catalog/items` | The item list, grouped by category, with price, pricing type and stock. |
| 30-items-list-mobile.png | Admin | `/catalog/items` | The item list on a phone. |
| 31-item-dialog.png | Admin | `/catalog/items` | Editing an item: name, SKU, barcode, category, price, cost and picture. |
| 32-categories.png | Admin | `/catalog/categories` | Categories: the groups items are sorted into at the register and on the kiosk. |
| 33-modifier-groups.png | Admin | `/catalog/modifier-groups` | Modifier groups: choices like sugar level and add-ons, with their price changes. |
| 34-item-variants.png | Admin | `/catalog/items/892becef-afb0-4b32-8892-f9557cc97dd2/variants` | Variants of one item, such as the latte in three sizes with their own prices. |
| 35-item-recipe.png | Admin | `/catalog/items` | The ingredients a drink uses, so each sale takes them out of stock. |
| 36-promotions-codes.png | Admin | `/business/promotions` | Promo codes a cashier can type at the register. |
| 37-promotions-bogo.png | Admin | `/business/promotions` | Promotions: a Buy 1 Take 1 deal. Combo deals, item discounts and promo codes have their own tabs. |
| 40-inventory-home.png | Admin | `/inventory` | Inventory home: stock health, what to restock first and the shortcuts for purchasing and counts. |
| 41-movements.png | Admin | `/inventory/movements` | The stock movement log: every sale, delivery, spoilage and count, with who and when. |
| 42-record-movement.png | Admin | `/inventory/movements` | Recording a stock movement by hand: stock in, spoilage, damage or a correction. |
| 43-ingredients.png | Admin | `/inventory/ingredients` | Ingredients: what is bought in bulk and used in recipes, with packaging and low-stock alerts. |
| 44-suppliers.png | Admin | `/inventory/suppliers` | Suppliers, with contacts and what each one supplies. |
| 45-purchase-requests.png | Admin | `/inventory/purchase-requests` | Purchase requests: what to buy, waiting for approval or approved and ready to turn into orders. |
| 46-purchase-order-detail.png | Admin | `/inventory/purchase-orders/fb94b2eb-425c-4edb-95e9-843abbcfd2bc` | One purchase order: supplier, items ordered and delivered, totals, and Record delivery. |
| 46b-purchase-orders.png | Admin | `/inventory/purchase-orders` | Purchase orders in Draft, Submitted and Received states. |
| 47-incoming-receiving.png | Admin | `/inventory/incoming-receiving` | Incoming receiving: deliveries checked in, linked to their purchase orders. |
| 48-transfers.png | Admin | `/inventory/transfers` | Stock transfers between branches, from pending to in transit to received. |
| 49-stock-take-counting.png | Admin | `/inventory/stock-takes/17357652-2994-4f08-b11c-39ec95f1057a` | A blind stock take in progress: the counter types what they see, without the expected quantity. |
| 50-stock-take-review.png | Admin | `/inventory/stock-takes/0a1c0470-8c4c-4066-ae90-97d5ecba3c1e` | Reviewing a stock take: expected, counted, variance and its value before the counts are applied. |
| 51-import-wizard.png | Admin | `/inventory/import` | Importing a CSV: the file is checked first, and you see what will be created or changed before anything is saved. |
| 60-reports-sales-mix.png | Admin | `/reports?tab=sales` | Sales mix: gross sales by payment type, category and hour of day over the chosen period. |
| 61-reports-tax.png | Admin | `/reports?tab=tax` | The tax report: VATable, VAT-exempt and zero-rated sales and the VAT collected. |
| 62-reports-profit.png | Admin | `/reports?tab=profit` | The profit report: sales less the cost of what was sold, by item and by category. |
| 63-reports-discounts.png | Admin | `/reports?tab=discounts` | Discounts: promotions and manual discounts given, and who approved them. |
| 64-reports-voids.png | Admin | `/reports?tab=voids` | Voids and refunds: what was cancelled or returned, by whom and why. |
| 65-reports-x-z.png | Admin | `/reports?tab=bir` | X and Z readings: the end-of-shift and end-of-day summaries. |
| 66-reports-valuation-cogs.png | Admin | `/reports?tab=valuation` | Stock valuation: what the stock on hand is worth at cost. |
| 67-reports-exports.png | Admin | `/reports?tab=exports` | Exports: CSV files of sales, discounts, voids and refunds to download. |
| 70-business-hub.png | Admin | `/business` | The Business hub: what needs attention today, money owed, the team, and links to every setup page. |
| 71-staff.png | Admin | `/business/staff` | Staff: who can work, their role and branches, and invitations still waiting to be opened. |
| 72-invite-link.png | Admin | `/business/staff` | After inviting someone: the one-time link and QR code to hand to them. No email is sent. |
| 73-branches.png | Admin | `/business/branches` | Branches: each place the business sells from, with its code and address. |
| 74-branch-hardware-gcash.png | Admin | `/business/branches` | Branch settings: receipt printer, cash drawer and the GCash QR customers scan to pay. |
| 75-devices.png | Admin | `/business/devices` | Devices: every register, kiosk and display paired to a branch, and when each was last seen. |
| 76-settings-branding.png | Admin | `/business/settings` | Business settings: your colours, logo and font across the app. |
| 77-settings-options.png | Admin | `/business/settings` | Business settings: how the business sells, tracks ingredients and handles utang. |
| 78-customers-utang.png | Admin | `/business/customers` | Customers with utang: balances against credit limits, and payments coming due. |
| 79-orders.png | Admin | `/business/orders` | Orders: every sale and its receipt, with filters for branch, cashier, status and payment. |
| 80-audit-log.png | Admin | `/business/audit-log` | The audit log: sensitive actions such as voids, refunds and discount overrides, and who did them. |
| 81-sync-conflicts.png | Admin | `/business/sync-conflicts` | Sync conflicts: changes from two devices on the same record, kept for review. |
| 90-kiosk-welcome.png | Kiosk (paired device) | `/kiosk` | The kiosk welcome screen customers see while it waits. |
| 91-kiosk-menu.png | Kiosk (paired device) | `/kiosk/menu` | The kiosk menu: pictures and prices by category for the customer to tap. |
| 92-kiosk-cart.png | Kiosk (paired device) | `/kiosk/cart` | The customer reviews their order before choosing how to pay. |
| 93-kiosk-done.png | Kiosk (paired device) | `/kiosk/done` | The order is placed: the customer gets an order number and pays at the counter. |
| 94-kitchen-display.png | Kitchen display (paired device) | `/kitchen` | The kitchen display: orders waiting to be made, oldest first, to tap through to ready. |
| 95-order-board.png | Order board (paired device) | `/order-board` | The order board customers watch to see when their number is ready. |
| 96-customer-display.png | Customer display (paired device) | `/customer-display` | The customer display: it follows the register and shows each item and the total as it is rung up. |
| 97-offline-banner.png | Cashier | `/sell` | When the connection drops, a banner says so; selling continues from what the register already has. |
| 98-lock-button.png | Cashier | `/sell` | The Lock button: a cashier locks the register when stepping away; the next person unlocks with their PIN. |
