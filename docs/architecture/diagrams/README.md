# Architecture diagrams

Status: Living · Last substantive change: 2026-10-03 (diagram content) · Audience: contributors

Mermaid sources, reconstructed from the code during the 2026-10-01 to 2026-10-03 system analysis. Paste a file into a
Mermaid viewer, or wrap it in a `mermaid` code block, to render it. Listed in the original reading order.

| # | Diagram | Shows |
|---|---|---|
| 1 | [system-context.mmd](system-context.mmd) | Human actors, terminals and external systems around Purch.io |
| 2 | [system-architecture.mmd](system-architecture.mmd) | Tiers: Flutter and web clients, API, application/infrastructure layers, data stores |
| 3 | [feature-map.mmd](feature-map.mmd) | Feature tree by module |
| 4 | [navigation-map.mmd](navigation-map.mmd) | App launch, session check and route trees per role |
| 5 | [authentication-authorization.mmd](authentication-authorization.mmd) | Sign-in sequence. **Stale:** shows the removed `POST /auth/login` pairing-code + PIN flow; the current flow is in [../auth.md](../auth.md) |
| 6 | [database-erd.mmd](database-erd.mmd) | Entity-relationship diagram of the core tables |
| 7 | [api-architecture.mmd](api-architecture.mmd) | Request sequence for a cashier sale through the API |
| 8 | [integration-map.mmd](integration-map.mmd) | Backend integrations (storage, payments, hardware, displays) |
| 9 | [state-machines.mmd](state-machines.mmd) | Lifecycle state machines (purchase orders and other entities) |
