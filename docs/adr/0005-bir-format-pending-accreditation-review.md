# 0005: BIR Z/X-reading format — pending accreditation review

**Status:** Accepted as best-effort, NOT final

**Decision:** Implement Z-reading (end of day) / X-reading (mid-shift) reports to a best-effort format based on publicly known BIR POS/CRM accreditation guidance: sequential invoice numbers, VAT breakdown, sales/refund/void summary, machine identification number.

**Explicit risk:** Neither `ARCHITECTURE.md` nor `PAGES.md` specifies the actual required BIR accreditation format, and this is a legal compliance surface, not a UI spec. Every field mapping in the report-generation code carries a `// TODO(BIR-ACCREDITATION):` marker.

**Action required before any real/live deployment:** Verify against actual BIR accreditation paperwork or an accountant/BIR consultant's input. Do not treat this implementation as accreditation-ready.
