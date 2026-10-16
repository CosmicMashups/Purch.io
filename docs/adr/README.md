# Architecture decision records

Status: Living · Last substantive change: 2026-10-16 · Audience: contributors, reviewers

One decision per file, numbered in the order recorded. Numbers are never reused. Status is taken from each ADR's own
text. ADRs 0007 to 0010 were written during the 2026-10-16 documentation restructure to give decisions that were
already recorded in other documents a stable home; each links to its source.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-ef-core-over-dapper.md) | EF Core over Dapper | Accepted |
| [0002](0002-render-over-railway.md) | Render over Railway for Cloud-mode API hosting | Accepted (see note) |
| [0003](0003-offline-sync-stub-boundary.md) | Offline sync engine scope boundary | Accepted, implemented in Phase 6 (see note) |
| [0004](0004-bill-payment-provider.md) | Bill payment / e-load provider | Accepted |
| [0005](0005-bir-format-pending-accreditation-review.md) | BIR Z/X-reading format | Accepted as best-effort, not final |
| [0006](0006-npc-registration-tracked-externally.md) | NPC / Data Privacy Act registration tracked externally | Accepted |
| [0007](0007-react-web-client.md) | React web client extends `web/`, online-only selling | Accepted |
| [0008](0008-tenant-wide-stock.md) | Stock is tenant-wide, not per branch | Accepted |
| [0009](0009-stock-take-delta-variance.md) | Stock-take applies variance as a delta | Accepted |
| [0010](0010-login-and-access-redesign.md) | Email sign-in, one-time pairing, PIN unlock | Accepted, implemented |

Notes (details in [history/doc-conflicts.md](../history/doc-conflicts.md)):
- **0002:** the repo now also deploys the backend to Vercel (`backend/vercel.json`,
  [operations/vercel-deployment.md](../operations/vercel-deployment.md)), and 0002's line that Supabase Auth is not
  used predates [0010](0010-login-and-access-redesign.md). Whether 0002 is superseded is an owner decision.
- **0003:** Flutter offline sales were later added through a separate queued-sale path to checkout, not through the
  generic sync queue described as unretrofitted.

New ADR: copy the shape of an existing one (Status, Context/Decision, Rationale or Consequences), take the next
number, and add a row here.
