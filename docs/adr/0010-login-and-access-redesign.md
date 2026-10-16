# 0010: Accounts with email sign-in, one-time device pairing and PIN unlock

**Status:** Accepted and implemented (design 2026-10-03, steps 1 to 8 built)

Source: [architecture/auth.md](../architecture/auth.md). That document holds the full design; this ADR only records
the decision.

**Context:** registers signed staff in with a device pairing code plus a PIN; the PIN was both username and password,
had to be unique within a business (leaking validity), unattended devices paired with a reusable PIN, and pairing
codes never expired.

**Decision:**
- People sign in with email and password (Supabase Auth in Cloud mode, `LocalCredentials` in Local mode); the API then
  issues its own short-lived access token and refresh token. Permissions stay in Purch.io's database, never in
  Supabase metadata.
- One account can hold memberships in several businesses.
- Devices pair once with a short-lived one-time code; a paired till is unlocked with the person's PIN.
- The old PIN-at-a-pairing-code sign-in, admin login and related endpoints are removed; existing staff and devices
  re-enrol (`migrate-legacy`).

**Consequences:** see auth.md "Open points and risks" (no email verification, reset email deliverability, owner
recovery). Upgrading installs must run `migrate` then `migrate-legacy` once.
