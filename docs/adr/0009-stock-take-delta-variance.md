# 0009: Stock-take applies variance as a delta

**Status:** Accepted (U3 design, 2026-10-15)

Source: [engineering/specs/2026-10-15-u3-inventory-depth-design.md](../engineering/specs/2026-10-15-u3-inventory-depth-design.md),
"Slice 2: Stock-take"; owner decision recorded in [engineering/roadmap.md](../engineering/roadmap.md).

**Context:** counting takes time while sales continue, and there is no sales lock during a stock-take.

**Decision:** on each count entry the server snapshots `ExpectedQty` (on-hand at that instant). Applying a take posts
a signed `Adjustment` movement of `variance = counted - expected_at_count` per counted line. Stock is never set to the
counted value.

**Consequences:** sales after the count survive the apply; overlapping open takes on one item are allowed (creation
warns). Apply may drive stock negative; negative lines are listed in the audit (a ruling still to confirm, see the
roadmap). Uncounted and skipped lines post nothing. Re-applying an applied take is a no-op.
