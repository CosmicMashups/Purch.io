# 0002: Render over Railway for Cloud-mode API hosting

**Status:** Accepted

**Decision:** Host the Cloud-mode API on Render, not Railway.

**Rationale:** Render's free tier has a clearer persistent-service model for a long-lived Minimal API. Render supports `render.yaml` blueprint config-as-code, matching NFR17 (config-only migration). Supabase remains the Postgres + Storage provider; Supabase Auth is not used — custom JWT issuance is used instead (see `ARCHITECTURE.md`'s JWT + role-claims spec).

**Known trade-off:** Render's free-tier web services cold-start/spin down after inactivity. Acceptable for pre-revenue development; revisit at first paying Cloud-mode client.
