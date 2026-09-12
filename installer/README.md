# Local/On-Prem Installer

Packaging for the **Local** deployment mode (see `docs/ARCHITECTURE.md` and
the implementation plan): a dedicated, single-tenant Purch.io backend +
Postgres running on a machine inside the store's own network, reachable by
every POS/kiosk device on that LAN. This is about **data isolation**, not
offline capability — see `IDeploymentContext` in the backend for the seam
that makes Cloud vs. Local a config value, never a code branch.

There is **no licensing or activation step** anywhere in this installer —
`DeploymentMode` is a config value you (the installer, not the customer) set
per installation. See the implementation plan's "Key Architecture Decisions"
for why that's a deliberate choice, not an oversight.

## Two ways to run it

Pick one. Both end with the same thing: a backend listening on this
machine's LAN, and a database that migrates itself on first boot (see
`Program.cs` — Local-mode instances call `Database.MigrateAsync()` at
startup, so there's no separate `dotnet ef` step to run by hand).

### Option A — Docker (recommended)

Requires [Docker Desktop](https://www.docker.com/products/docker-desktop/).

```powershell
cd installer
copy .env.local.template .env.local
# edit .env.local: set POSTGRES_PASSWORD and JWT_SIGNING_KEY
.\setup-local.ps1
```

`docker-compose.local.yml` brings up Postgres and the backend as two
containers, both restarting automatically if the machine reboots. Data lives
in named Docker volumes (`purch_postgres_data`, `purch_storage_data`), not
inside the containers, so `docker compose down` never loses data.

Useful commands (run from `installer/`):

```powershell
docker compose -f docker-compose.local.yml logs -f backend   # tail logs
docker compose -f docker-compose.local.yml down              # stop
docker compose -f docker-compose.local.yml up -d              # start again
```

### Option B — Windows Service (no Docker)

For a machine where installing Docker Desktop isn't practical. You provide
your own Postgres (the official installer from
[postgresql.org](https://www.postgresql.org/download/windows/)) — this path
doesn't bundle one.

```powershell
cd installer
.\publish-self-contained.ps1
.\install-windows-service.ps1 `
    -DbConnectionString "Host=localhost;Database=purch;Username=purch;Password=<your-postgres-password>" `
    -JwtSigningKey "<a long random string>"
```

This publishes a self-contained, single-file `Purch.Api.exe` (no .NET
runtime install needed on the target machine — the runtime is bundled into
the exe), registers it as a Windows Service (`PurchIoBackend`, starts
automatically on boot), and opens the firewall port for LAN devices.

Manage it like any Windows service: `Get-Service PurchIoBackend`,
`Restart-Service PurchIoBackend`, or via `services.msc`.

## Connecting POS/kiosk devices to the server

Every physical device on the LAN — cashier POS, kiosk terminal — needs
pointing at this specific installation's address once. There's no
automatic LAN discovery (that would need an mDNS-style package; flagged as
a deferred enhancement, not built here) — instead, on each device:

1. Find this machine's LAN IP: `ipconfig` → look for the IPv4 address on
   the store's Wi-Fi/Ethernet network (typically `192.168.x.x`).
2. On the device, open the app's login screen → **"Connect to a local
   server"** (or, once logged in, **Business Settings → "Local Server
   Connection"** to change it later) and enter `<that-ip>:<port>` (port
   `8080` unless you changed `PURCH_PORT`/`-Port`).
3. The app tests the address against `/health` before saving it, and the
   change takes effect immediately — no app restart, no rebuild.

## Known v1 boundary

Single-branch, single-LAN per installation (see the implementation plan) —
a tenant with multiple physical branches under Local mode needs either a
separate installation per branch (no cross-branch reporting) or a VPN
tunnel back to a primary server. Not solved here.

## The Dockerfile is shared with Cloud mode

`backend/Dockerfile` (not `installer/`) builds the same image `render.yaml`
uses for the Cloud deployment mode — one image, entirely env-var driven
(`IDeploymentContext` picks Cloud vs. Local at startup from
`PURCH_DEPLOYMENT_MODE`), so there's nothing Local-specific to duplicate.
`.github/workflows/backend-local-mode-ci.yml` runs this whole flow in CI on
every backend change: publish self-contained, boot against a schema-less
Postgres, and prove `Database.MigrateAsync()` actually ran by completing a
real onboarding bootstrap write — not just that `/health` responds, which
touches no database at all.
