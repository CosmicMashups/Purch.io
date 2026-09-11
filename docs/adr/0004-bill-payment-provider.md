# 0004: Bill payment / e-load provider

**Status:** Accepted

**Context:** Bill payment/e-load (PAGES.md D5, convenience-store mode) is a biller-aggregator function — pay a customer's utility/telco/government bill, or top up prepaid mobile load, over the counter. It's a distinct API surface from QR Ph checkout acquiring (Xendit, already chosen for D5's QR Ph tab). No provider was named in any of the six spec docs, so this ADR was left open pending a vendor research spike at the start of Phase 4.

## Candidates evaluated

- **Dragonpay** — Philippine alt-payments pioneer (est. ~2010), ~3,000+ merchants, biller-aggregator plus prepaid mobile load top-up (Globe, TM, Smart, TNT, Sun) and prepaid WiFi (Globe, PLDT). REST/name-value-pair or SOAP/XML API, PHP sample script available. **As of July 2026, Dragonpay was acquired by/integrated into Xendit** — the same vendor this project already uses for QR Ph acquiring.
- **Bayad Center** — the largest multichannel bill-payment platform in the Philippines (500+ billing institutions/government agencies, 130,000+ physical touchpoints, 20+ years in outsourced bill collection). Direct API integration is used in production by Coins.ph, PalawanPay, and Atome for real-time bill posting. Independent company, no existing relationship with Xendit.
- **Xendit's own disbursement/payout API** — ruled out for this use case. It's a mass-payout/send-money product (pay a bank account or e-wallet a specified amount), not a biller-aggregator that knows how to pay a specific electric co-op's account number or top up a specific phone number. It solves a different problem than D5's Bill Payment/E-Load tab.

## Decision

Use **Dragonpay (a Xendit company)** for Bill Payment/E-Load.

## Rationale

- **Single vendor relationship.** Since Dragonpay is now part of the Xendit corporate family, this keeps the whole payment stack (QR Ph acquiring + bill payment/e-load) under one vendor relationship instead of maintaining separate merchant accounts, KYC, and support channels with two unrelated companies.
- **Direct product fit.** Dragonpay's prepaid load top-up already covers all the major PH telcos named in D5's e-load scope (Globe, TM, Smart, TNT, Sun), and its biller network covers the utility/government-bill side of D5 without needing a second aggregator.
- **Established, documented API.** A REST/name-value-pair integration path with existing sample code exists today (Dragonpay Payment Switch API), unlike a from-scratch integration with a brand-new vendor.

## Trade-off / risk accepted

Bayad Center has a larger stated biller network (500+ institutions vs. Dragonpay's coverage) and is used in production by larger PH fintechs (Coins.ph, PalawanPay). If Dragonpay's specific biller list turns out not to cover a merchant's needed billers during real integration, Bayad Center is the documented fallback — swapping the biller-aggregator implementation behind the same `IBillPaymentProvider` seam (to be introduced in Phase 4) should not require touching the QR Ph/Xendit integration at all, since the two are architecturally independent even though they now share a parent company.

## Action required (business, not code)

Dragonpay merchant sign-up (business details, bank account, biller-type selection) is a manual onboarding process handled by their team over 1-2 business days — this is a business/ops action item for whoever operates the tenant, tracked here so it isn't forgotten, same as ADR 0006's NPC registration item. Sandbox/test credentials should be requested before Phase 4's payment-method-tabs implementation reaches the Bill Payment/E-Load tab specifically (Cash, QR Ph, Bank Transfer, and Utang/Credit don't depend on this and can proceed first).
