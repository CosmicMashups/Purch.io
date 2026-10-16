# Purch.io documentation

Status: Living · Last substantive change: 2026-10-16 · Audience: everyone

Every document under `docs/` is listed here. Start with the path for your role.

## Start here

**New contributor**
1. [Root README](../README.md): what the repo contains, quick start, tests.
2. [architecture/overview.md](architecture/overview.md): the one architecture overview.
3. [engineering/roadmap.md](engineering/roadmap.md): current state, standing decisions, open follow-ups (read before starting work).
4. [adr/README.md](adr/README.md): decisions and their status.
5. [design/README.md](design/README.md) and [design/tokens.json](design/tokens.json) before UI work.

**Operator (deploy, run, recover)**
1. [operations/deploy-notes/README.md](operations/deploy-notes/README.md): per-release deploy steps and the common checklist.
2. [operations/vercel-deployment.md](operations/vercel-deployment.md) (backend) and
   [operations/cloudflare-deployment.md](operations/cloudflare-deployment.md) (web client).
3. [operations/backups.md](operations/backups.md): backups, restore drills, archival.
4. [operations/security/security-assessment.md](operations/security/security-assessment.md): findings and open items.
5. [installer/README.md](../installer/README.md): Local (on-prem) mode packaging.

**Product**
1. [product/proposal.md](product/proposal.md), then [product/requirements.md](product/requirements.md).
2. [product/workflow.md](product/workflow.md): non-technical day-to-day walkthrough.
3. [product/pages.md](product/pages.md): page and component specification.
4. [product/feature-inventory.md](product/feature-inventory.md): living feature catalogue (IDs such as `POS-`, `INV-`, `REP-` are stable).
5. [PRODUCT.md](../PRODUCT.md) (repo root): product and design context read by the design tooling.

## Taxonomy

| Folder | Holds | Kind |
|---|---|---|
| [product/](product/) | Why and what: proposal, requirements, workflow, page spec, feature inventory | Living |
| [architecture/](architecture/) | How it works: overview plus reference docs and diagrams | Living |
| [adr/](adr/README.md) | One decision per file, never renumbered | Accepted records |
| [operations/](operations/) | Deploying, backing up, securing a running system | Living runbooks |
| [design/](design/README.md) | Design tokens, reference comps and mockups | Living assets |
| [engineering/](engineering/README.md) | Unit specs, implementation plans and the roadmap | Living (specs/plans are dated) |
| [history/](history/README.md) | Point-in-time material, not maintained | Historical |

### Full list

- Product: [proposal](product/proposal.md) · [requirements](product/requirements.md) · [workflow](product/workflow.md) ·
  [pages](product/pages.md) · [feature-inventory](product/feature-inventory.md)
- Architecture: [overview](architecture/overview.md) · [modules](architecture/modules.md) ·
  [workflows](architecture/workflows.md) · [business-rules](architecture/business-rules.md) ·
  [error-flows](architecture/error-flows.md) · [auth](architecture/auth.md)
- Diagrams ([index](architecture/diagrams/README.md)): 1 [system-context](architecture/diagrams/system-context.mmd) ·
  2 [system-architecture](architecture/diagrams/system-architecture.mmd) · 3 [feature-map](architecture/diagrams/feature-map.mmd) ·
  4 [navigation-map](architecture/diagrams/navigation-map.mmd) ·
  5 [authentication-authorization](architecture/diagrams/authentication-authorization.mmd) ·
  6 [database-erd](architecture/diagrams/database-erd.mmd) · 7 [api-architecture](architecture/diagrams/api-architecture.mmd) ·
  8 [integration-map](architecture/diagrams/integration-map.mmd) · 9 [state-machines](architecture/diagrams/state-machines.mmd)
- ADRs ([index](adr/README.md)): [0001](adr/0001-ef-core-over-dapper.md) · [0002](adr/0002-render-over-railway.md) ·
  [0003](adr/0003-offline-sync-stub-boundary.md) · [0004](adr/0004-bill-payment-provider.md) ·
  [0005](adr/0005-bir-format-pending-accreditation-review.md) · [0006](adr/0006-npc-registration-tracked-externally.md) ·
  [0007](adr/0007-react-web-client.md) · [0008](adr/0008-tenant-wide-stock.md) ·
  [0009](adr/0009-stock-take-delta-variance.md) · [0010](adr/0010-login-and-access-redesign.md)
- Operations: [backups](operations/backups.md) · [vercel-deployment](operations/vercel-deployment.md) ·
  [cloudflare-deployment](operations/cloudflare-deployment.md) ·
  [security-assessment](operations/security/security-assessment.md) ·
  deploy notes ([index](operations/deploy-notes/README.md)):
  [checkout-and-offline-sales](operations/deploy-notes/checkout-and-offline-sales.md) ·
  [u1-pos-foundations](operations/deploy-notes/u1-pos-foundations.md) · [u4-reports](operations/deploy-notes/u4-reports.md) ·
  [u3-inventory-depth](operations/deploy-notes/u3-inventory-depth.md)
- Design: [README](design/README.md) · [tokens.json](design/tokens.json) ·
  [modern_retail_point_of_sale/DESIGN.md](design/mockups/modern_retail_point_of_sale/DESIGN.md) ·
  [ultramarine_slate_pos/DESIGN.md](design/mockups/ultramarine_slate_pos/DESIGN.md) · images and other mockups under
  [design/](design/)
- Engineering ([index](engineering/README.md)): [roadmap](engineering/roadmap.md) ·
  specs: [U1](engineering/specs/2026-10-14-u1-pos-foundations-design.md) ([golden cases](engineering/specs/u1-golden-cases.json)) ·
  [restock](engineering/specs/2026-10-15-restock-purchase-requests-design.md) ·
  [U3](engineering/specs/2026-10-15-u3-inventory-depth-design.md) · [U4](engineering/specs/2026-10-15-u4-reports-design.md) ·
  plans: [U1](engineering/plans/2026-10-14-u1-pos-foundations.md) · [U3](engineering/plans/2026-10-15-u3-inventory-depth.md)
- History ([index](history/README.md)): [project-case-study](history/project-case-study.md) ·
  [interview-guide](history/interview-guide.md) · [react-migration](history/react-migration.md) ·
  [discrepancies-2026-10-01](history/discrepancies-2026-10-01.md) · [coverage-audit-2026-10-03](history/coverage-audit-2026-10-03.md) ·
  [doc-conflicts](history/doc-conflicts.md)

## Conventions

- **Header line.** A doc that is new or substantively edited starts with its title and one line:
  `Status: <Living|Accepted|Draft|Historical> · Last substantive change: <YYYY-MM-DD from git> · Audience: <who>`.
  Documents only moved in the restructure keep their original header.
- **One fact, one home.** If a fact is already stated in a canonical doc, link to it instead of restating it.
  Canonical homes: architecture → `architecture/overview.md`; sign-in and devices → `architecture/auth.md`;
  deploy steps → `operations/deploy-notes/`; current work state → `engineering/roadmap.md`; features →
  `product/feature-inventory.md`; decisions → `adr/`.
- **Links** are relative and GitHub-renderable. No `file:///` links.
- **Moving or renaming** a doc: use `git mv`, update every reference (`git grep` the old path, including code
  comments), and record the old path in [history/README.md](history/README.md).
- **Stale material** is moved to `history/` with its date, not deleted. Conflicts between docs, or between a doc and
  the code, are resolved in favour of the code and logged in [history/doc-conflicts.md](history/doc-conflicts.md).
- **New unit of work:** spec in `engineering/specs/`, plan in `engineering/plans/` if needed, deploy note in
  `operations/deploy-notes/`, feature rows in `product/feature-inventory.md`, and an ADR for any lasting decision.

## Deviations from the planned layout

- `PRODUCT.md` stays at the repo root: design tooling and skills read it from there. It is linked above.
- `architecture/business-rules.md` (former system-analysis 13) is under architecture, not product: its rules cite
  code (`PinPolicy.Validate`, EF query filters) rather than product policy.
- `backend/docs/` held a second backup runbook, the Vercel guide and an older deploy note; they were moved into
  `operations/` (the runbooks merged) so all docs live under `docs/`.
- The security assessment lives at `operations/security/`.
- Diagrams keep the `.mmd` extension.
