# Deploying the backend to Vercel

Vercel doesn't have a native .NET runtime, but it does run arbitrary Docker
images as Vercel Functions ("container" services). `Dockerfile.vercel` and
`vercel.json` in this directory set that up. This is an alternative to the
existing Render deployment (`render.yaml`), not a replacement for it — both
can point at the same image with minor differences.

## Setup

1. In the Vercel dashboard, create a project from this repo with **Root
   Directory** set to `backend`.
2. Vercel picks up `vercel.json` automatically and builds `Dockerfile.vercel`
   as a container service.
3. Set these environment variables on the Vercel project (same values as the
   Render deployment, with one exception — see `SUPABASE_DB_CONNECTION_STRING`
   below):
   - `PURCH_DEPLOYMENT_MODE=Cloud`
   - `SUPABASE_DB_CONNECTION_STRING` — **must be Supabase's connection
     pooler (Supavisor), not the direct `db.<ref>.supabase.co` host.** The
     direct host resolves to an IPv6-only address in most regions, and
     Vercel's container network has no IPv6 egress — the app fails to start
     with `Npgsql.NpgsqlException: Failed to connect to [2406:...]:5432 —
     Network is unreachable`. Get the pooler string from Supabase dashboard
     → **Project Settings → Database → Connection pooling**, mode
     **Session** (not Transaction — Transaction mode needs extra Npgsql
     tuning to disable prepared-statement caching, Session mode doesn't).
     Convert it to Npgsql's `Host=...;` format, and note the username
     changes to `postgres.<project-ref>` through the pooler:
     ```
     Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-db-password>
     ```
     Render's connection (a long-lived host, not a serverless function) can
     keep using the direct host — this only matters for Vercel.
   - `SUPABASE_STORAGE_URL`
   - `SUPABASE_STORAGE_KEY`
   - `SUPABASE_STORAGE_BUCKET` (optional, defaults to `uploads` — create this
     bucket in the Supabase dashboard and make it public before going live)
   - `JWT_SIGNING_KEY` (generate a real secret — Render's `generateValue`
     has no Vercel equivalent)
   - `JWT_ISSUER=purch.io`
   - `XENDIT_API_KEY`, `XENDIT_WEBHOOK_SECRET`, `BILL_PAYMENT_PROVIDER_API_KEY`
   - `PORT=8080` — Vercel's routing layer reads this from the **project's**
     env vars to know which port to forward traffic to. Setting `PORT` only
     inside `Dockerfile.vercel` (which we also do, as a container-local
     default) isn't enough — Vercel needs its own copy to route correctly.
     Without it, every request 500s with no app-level log at all, since the
     request never reaches the container.

## Things that work differently than on Render

- **No `preDeployCommand`, but the API applies pending migrations at
  startup.** Render runs `dotnet Purch.Api.dll migrate` before each deploy
  (see `render.yaml`); Vercel has no equivalent hook. In Cloud mode the API
  therefore migrates itself when it starts. This is **deliberately
  non-fatal**: Supabase's pooler can reject migration DDL, and a failed
  auto-migrate must not take an otherwise-working API down, so it logs
  `Automatic migration failed; run scripts/migrate-production against the
  database.` and keeps serving — against a stale schema. Treat that log line
  as a failed deploy. For any migration that also changes data, run it by
  hand first against `SUPABASE_DB_CONNECTION_STRING` (direct connection, not
  the pooler):
  ```bash
  dotnet Purch.Api.dll migrate
  ```
  See `DEPLOY-NOTE-checkout-and-offline-sales.md` for the current batch.
- **Scale-to-zero.** The container stops after ~5 minutes idle and cold-starts
  on the next request. Fine for a low-traffic API, but expect occasional
  cold-start latency that Render's always-on free instance doesn't have.
- **4.5 MB request/response body limit.** Enforced by the Vercel Functions
  layer in front of the container, not configurable from the Dockerfile.
- **No persistent disk.** `POST /uploads/image` now writes to Supabase
  Storage in Cloud mode ([SupabaseFileStorage.cs](../src/Purch.Infrastructure/Storage/SupabaseFileStorage.cs)),
  selected via `IFileStorage` based on `PURCH_DEPLOYMENT_MODE`
  ([Program.cs](../src/Purch.Api/Program.cs)) — local disk
  ([LocalFileStorage.cs](../src/Purch.Infrastructure/Storage/LocalFileStorage.cs))
  is only used in Local mode, where a single long-lived instance owns its
  own disk. Make sure the `SUPABASE_STORAGE_BUCKET` bucket exists and is
  public in the Supabase dashboard before deploying.
- **No shell in the runtime image.** `mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled`
  (the final stage in `Dockerfile.vercel`) ships with no `/bin/sh` at all —
  it's a minimal/distroless-style image. An `ENTRYPOINT` that needs a shell
  (e.g. `["/bin/sh", "-c", "..."]`, which Render's own `Dockerfile` uses to
  translate `$PORT` into `ASPNETCORE_HTTP_PORTS`) fails to start the
  container before .NET ever runs — no exception, no log line, nothing.
  `Dockerfile.vercel` uses the exec-form `ENTRYPOINT ["dotnet", "Purch.Api.dll"]`
  instead, and `Program.cs` binds Kestrel to `$PORT` directly in code
  (`builder.WebHost.UseUrls(...)`) so no shell is ever needed.
- **Non-root runtime user.** The chiseled image runs as a non-root user by
  default, so `/app` must be `--chown`'d to `$APP_UID` on `COPY` in the
  final stage, or the app can't write to its own working directory (this
  only actually mattered before local-disk uploads were removed from Cloud
  mode, but keep the `--chown` — it's the documented pattern for any future
  code that writes under `/app`).

## Troubleshooting a 500 with no app-level log message

If `vercel logs` shows requests failing with an empty `message` and
`"logs":[]`, the container is crashing (or never starting) before it can log
anything — not a normal app-level exception. In descending order of how this
project has actually hit it:

| Symptom | Cause | Fix |
| --- | --- | --- |
| Every request 500s, zero log output, even after redeploying with fixes below | `ENTRYPOINT` needs a shell the image doesn't have | Use exec-form `ENTRYPOINT`, bind `$PORT` in code, not via shell |
| Same as above, but `docker build` locally succeeds fine | Confirms it's a runtime issue, not a build issue — check the entrypoint next | — |
| Requests 500 with `"source":"static"` in the log, deployment shows "Ready" | Vercel's routing layer doesn't know which port to forward to | Add `PORT` as a **project** env var (Settings → Environment Variables), not just inside the Dockerfile |
| Error code changes from `INTERNAL_FUNCTION_INVOCATION_FAILED` to `FUNCTION_INVOCATION_FAILED`, with an actual Npgsql stack trace mentioning an IPv6 address | Direct Supabase DB host resolves to IPv6; Vercel has no IPv6 egress | Switch `SUPABASE_DB_CONNECTION_STRING` to Supabase's Session-mode connection pooler |

To get past an empty-message 500, don't guess from the dashboard's log
panel alone — pull structured JSON logs, which sometimes carry a real
`message`/`logs` array the table view truncates:
```bash
vercel logs <deployment-url> --json -n 5
```

## Local build check

This catches build-time issues (missing files, compile errors) but **not**
the shell/entrypoint or IPv6 issues above — those only show up once the
container actually runs somewhere without a shell and without IPv6 egress,
which your local Docker daemon has both of. Use this to sanity-check the
build and confirm basic startup with a real Postgres, not as proof a Vercel
deploy will work.

```bash
cd backend
docker build -f Dockerfile.vercel -t purch-api-vercel .
docker run -e PORT=8080 -p 8080:8080 \
  -e PURCH_DEPLOYMENT_MODE=Cloud \
  -e SUPABASE_DB_CONNECTION_STRING="Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=..." \
  -e SUPABASE_STORAGE_URL="https://<project-ref>.supabase.co/storage/v1" \
  -e SUPABASE_STORAGE_KEY="..." \
  -e JWT_SIGNING_KEY="..." \
  purch-api-vercel
curl http://localhost:8080/health
```
