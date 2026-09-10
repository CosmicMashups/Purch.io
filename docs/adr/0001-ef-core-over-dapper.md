# 0001: EF Core over Dapper

**Status:** Accepted

**Decision:** Use EF Core (not Dapper) as the backend ORM.

**Rationale:** Team is learning C#. EF Core migrations keep schema in sync with the Flutter/drift side more easily than hand-rolled SQL + Dapper mapping. Npgsql's EF Core provider works cleanly with Supabase Postgres and with a locally-hosted Postgres instance (Local deployment mode) via the same models/migrations. Performance gap vs Dapper is not a concern at SME POS scale.

**Trade-off:** EF Core migrations must be reviewed carefully since Supabase manages some Postgres extensions that a naive `dotnet ef database update` won't know about.
