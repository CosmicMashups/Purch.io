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
   Render deployment):
   - `PURCH_DEPLOYMENT_MODE=Cloud`
   - `SUPABASE_DB_CONNECTION_STRING`
   - `SUPABASE_STORAGE_URL`
   - `SUPABASE_STORAGE_KEY`
   - `SUPABASE_STORAGE_BUCKET` (optional, defaults to `uploads` — create this
     bucket in the Supabase dashboard and make it public before going live)
   - `JWT_SIGNING_KEY` (generate a real secret — Render's `generateValue`
     has no Vercel equivalent)
   - `JWT_ISSUER=purch.io`
   - `XENDIT_API_KEY`, `XENDIT_WEBHOOK_SECRET`, `BILL_PAYMENT_PROVIDER_API_KEY`

## Things that work differently than on Render

- **No `preDeployCommand`.** Render runs `dotnet Purch.Api.dll migrate`
  before each deploy (see `render.yaml`). Vercel has no equivalent hook, so
  after any deploy that adds a migration, run it by hand against
  `SUPABASE_DB_CONNECTION_STRING`:
  ```bash
  dotnet Purch.Api.dll migrate
  ```
  from a machine that can reach the DB, or trigger it as a one-off step in
  your CI pipeline before the Vercel deploy completes.
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

## Local build check

```bash
cd backend
docker build -f Dockerfile.vercel -t purch-api-vercel .
docker run -e PORT=8080 -p 8080:8080 \
  -e PURCH_DEPLOYMENT_MODE=Cloud \
  -e SUPABASE_DB_CONNECTION_STRING="..." \
  -e SUPABASE_STORAGE_URL="..." \
  -e JWT_SIGNING_KEY="..." \
  purch-api-vercel
curl http://localhost:8080/health
```
