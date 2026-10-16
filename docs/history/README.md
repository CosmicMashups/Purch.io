# History

Status: Historical · Last substantive change: 2026-10-16 · Audience: anyone researching how the system got here

Point-in-time documents. They are **not maintained** and may be stale; prefer the living docs linked from
[../README.md](../README.md). Dates are each file's last content change in git before the 2026-10-16 restructure.

| Document | Date | What it is |
|---|---|---|
| [project-case-study.md](project-case-study.md) | 2026-10-03 | Portfolio write-up of the design and the phase-by-phase build |
| [interview-guide.md](interview-guide.md) | 2026-09-17 | Interview preparation guide about the project |
| [react-migration.md](react-migration.md) | 2026-09-24 | Full narrative and gap log of the Flutter-to-React web migration (its decisions are now [ADR 0007](../adr/0007-react-web-client.md)) |
| [discrepancies-2026-10-01.md](discrepancies-2026-10-01.md) | 2026-10-01 | Code-vs-docs discrepancy report from the system analysis |
| [coverage-audit-2026-10-03.md](coverage-audit-2026-10-03.md) | 2026-10-03 | Coverage matrix of the reverse-engineering analysis |
| [doc-conflicts.md](doc-conflicts.md) | 2026-10-16 | Conflicts found during the documentation restructure and how each was handled |

Path references inside these documents were updated to the new layout; their content was otherwise not changed.

## Old paths (2026-10-16 restructure)

| Old path | New path |
|---|---|
| `docs/PROPOSAL.md`, `REQUIREMENTS.md`, `PAGES.md`, `WORKFLOW.md` | `docs/product/proposal.md`, `requirements.md`, `pages.md`, `workflow.md` |
| `docs/system-analysis/10-feature-inventory.md` | `docs/product/feature-inventory.md` |
| `docs/ARCHITECTURE.md`, `docs/ARCHITECTURE-ESSENTIALS.md`, `docs/WEB-ARCHITECTURE.md`, `docs/system-analysis/00-executive-overview.md` | merged into `docs/architecture/overview.md` |
| `docs/system-analysis/11-module-documentation.md`, `12-workflows.md`, `13-business-rules.md`, `14-error-flows.md` | `docs/architecture/modules.md`, `workflows.md`, `business-rules.md`, `error-flows.md` |
| `docs/system-analysis/01..09-*.mmd` | `docs/architecture/diagrams/*.mmd` (number prefix dropped) |
| `docs/AUTH-REDESIGN.md` | `docs/architecture/auth.md` |
| `docs/BACKUPS.md` + `backend/docs/BACKUPS.md` | merged into `docs/operations/backups.md` |
| `docs/CLOUDFLARE-DEPLOYMENT.md`, `backend/docs/VERCEL-DEPLOYMENT.md` | `docs/operations/cloudflare-deployment.md`, `docs/operations/vercel-deployment.md` |
| `docs/security/SECURITY-ASSESSMENT.md` | `docs/operations/security/security-assessment.md` |
| `docs/DEPLOY-NOTE-*.md`, `backend/docs/DEPLOY-NOTE-checkout-and-offline-sales.md` | `docs/operations/deploy-notes/*.md` |
| `docs/superpowers/specs`, `docs/superpowers/plans` | `docs/engineering/specs`, `docs/engineering/plans` |
| `docs/superpowers/plans/P1-roadmap-status.md` | `docs/engineering/roadmap.md` |
| `docs/PROJECT-CASE-STUDY.md`, `INTERVIEW_GUIDE.md`, `REACT-MIGRATION.md` | `docs/history/project-case-study.md`, `interview-guide.md`, `react-migration.md` |
| `docs/system-analysis/15-discrepancies.md`, `16-coverage-audit.md` | `docs/history/discrepancies-2026-10-01.md`, `coverage-audit-2026-10-03.md` |
