# 0006: NPC / Data Privacy Act registration — tracked externally

**Status:** Accepted

**Context:** The `customer_credit_ledger` (utang) feature stores customer PII, which per NFR10 triggers real Data Privacy Act (NPC) obligations the moment it's live.

**Decision:** Code-level mitigations reduce exposure but do not eliminate the legal registration requirement:
- Minimum required fields only: full name + phone number. Address is optional, merchant-added, not required.
- No government ID or other NPC "sensitive personal information" category is collected.
- Configurable retention/auto-purge policy on the ledger (per NFR10).
- Data export/erasure endpoints for data-subject rights.

**Action required (business, not code):** NPC registration is a tracked external action item for whoever operates the tenant storing this data. This ADR exists so it isn't forgotten because the code shipped.
