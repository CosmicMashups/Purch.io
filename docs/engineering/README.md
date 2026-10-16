# Engineering: specs, plans and roadmap

Status: Living · Last substantive change: 2026-10-16 · Audience: contributors

Formerly `superpowers/`. File names and dates are unchanged.

**Flow:** a unit starts as a design **spec** (`specs/`, owner decisions recorded inline), becomes an implementation
**plan** (`plans/`, checkbox tasks) when it is large enough to need one, ships with a **deploy note**
([../operations/deploy-notes/](../operations/deploy-notes/README.md)) when it needs deploy steps, and adds rows to the
[feature inventory](../product/feature-inventory.md). Current state and handoff: [roadmap.md](roadmap.md) (read first).

| Unit | Spec | Plan | Deploy note | Status (from the files) |
|---|---|---|---|---|
| U1 POS foundations | [spec](specs/2026-10-14-u1-pos-foundations-design.md), [golden cases](specs/u1-golden-cases.json) | [plan](plans/2026-10-14-u1-pos-foundations.md) | [u1-pos-foundations.md](../operations/deploy-notes/u1-pos-foundations.md) | Done |
| Restock / purchase requests | [spec](specs/2026-10-15-restock-purchase-requests-design.md) | none | none; the spec says to run its migration out of band as in the U4 note | Built; known gaps listed in the spec |
| U3 Inventory depth | [spec](specs/2026-10-15-u3-inventory-depth-design.md) | [plan](plans/2026-10-15-u3-inventory-depth.md) | [u3-inventory-depth.md](../operations/deploy-notes/u3-inventory-depth.md) | Shipped; rulings to confirm are in the roadmap |
| U4 Reports | [spec](specs/2026-10-15-u4-reports-design.md) | none | [u4-reports.md](../operations/deploy-notes/u4-reports.md) | Shipped |

The migrations of all four units are on `origin/main` as of 2026-10-16. Remaining units (U2 customers and loyalty, U5
staff, U6 Flutter parity) have no spec yet; their order is in the roadmap.
