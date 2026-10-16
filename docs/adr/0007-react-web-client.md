# 0007: React web client extends the existing web app, online-only selling

**Status:** Accepted (recorded 2026-09-24 in the React migration log)

Source: [history/react-migration.md](../history/react-migration.md), "Decisions" table. This ADR only restates it.

**Context:** `web/` existed as a catalog-admin slice. The migration brought the Flutter client's features to the web
(React 19, TypeScript, Vite, Tailwind 4, react-router 7, TanStack Query 5, zustand). The ASP.NET Core backend is the
source of truth and was not changed for the migration.

**Decision:**
- Extend `web/`, do not rewrite it.
- Offline sales are blocked on web. No TypeScript pricing engine is added; Flutter's `pricing_engine.dart` is not ported.
- Tokens stay in `localStorage` (accessed in try/catch); the XSS trade-off is accepted because the backend uses bearer
  headers, not cookies.
- The tenant is never client-selectable; it derives from the JWT or device pairing.
- Reports followed Flutter at the time (charts on Home, tiles on Business, no Reports tab).

**Consequences:** web selling needs connectivity; pricing logic exists only on the server (and in Flutter for its
offline path). Later changes: a Reports area was added in the U4 unit
([engineering/specs/2026-10-15-u4-reports-design.md](../engineering/specs/2026-10-15-u4-reports-design.md)), and the
`localStorage` token risk is tracked as open item 17 in the
[security assessment](../operations/security/security-assessment.md).
