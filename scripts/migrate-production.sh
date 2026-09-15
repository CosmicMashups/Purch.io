#!/bin/bash
# Applies any pending EF Core migrations to the production Supabase database.
#
# Render's purch-api service is on the Free plan, which doesn't support
# preDeployCommand (see render.yaml) — so this has to be run by hand after
# any deploy that includes a new migration under
# backend/src/Purch.Infrastructure/Persistence/Migrations/.
#
# Run from the repository root:
#   ./scripts/migrate-production.sh
#
# The production connection string is never hardcoded here or echoed to the
# terminal — it's read silently, or picked up from an already-exported
# SUPABASE_DB_CONNECTION_STRING if you prefer to set it yourself first.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend"

if [ ! -d "$BACKEND_DIR/src/Purch.Infrastructure" ]; then
    echo "Error: expected to find backend/src/Purch.Infrastructure under $REPO_ROOT — run this from the repo root." >&2
    exit 1
fi

# Prefer a known-good local SDK if one exists at D:\dotnet-sdk (set up
# earlier when this machine's default dotnet install had no SDK) — falls
# back to whatever "dotnet" resolves to on PATH otherwise. Exported (not
# just invoked by full path) because dotnet-ef internally shells out to
# "dotnet msbuild" on its own, which needs to resolve the same SDK via
# PATH/DOTNET_ROOT regardless of how this script itself invokes dotnet-ef.
DOTNET_BIN="dotnet"
if [ -x "/d/dotnet-sdk/dotnet.exe" ]; then
    DOTNET_BIN="/d/dotnet-sdk/dotnet.exe"
    export DOTNET_ROOT="D:\\dotnet-sdk"
    export PATH="/d/dotnet-sdk:$PATH"
fi

echo "Using dotnet: $DOTNET_BIN"
"$DOTNET_BIN" --version

if ! "$DOTNET_BIN" tool list --global 2>/dev/null | grep -q "dotnet-ef"; then
    echo "dotnet-ef not found — installing..."
    "$DOTNET_BIN" tool install --global dotnet-ef
fi

if [ -z "${SUPABASE_DB_CONNECTION_STRING:-}" ]; then
    echo "Production Supabase connection string (input hidden, paste and press Enter):"
    read -r -s SUPABASE_DB_CONNECTION_STRING
    echo
    if [ -z "$SUPABASE_DB_CONNECTION_STRING" ]; then
        echo "Error: no connection string entered." >&2
        exit 1
    fi
fi
export SUPABASE_DB_CONNECTION_STRING

# Only the connection string matters for a migration — these satisfy the
# app's own startup config validation without needing real values.
export PURCH_DEPLOYMENT_MODE="${PURCH_DEPLOYMENT_MODE:-Cloud}"
export SUPABASE_STORAGE_URL="${SUPABASE_STORAGE_URL:-https://placeholder}"
export JWT_SIGNING_KEY="${JWT_SIGNING_KEY:-placeholder}"
export JWT_ISSUER="${JWT_ISSUER:-purch.io}"

echo
echo "Applying pending migrations to production..."
"$DOTNET_BIN" ef database update \
    --project "$BACKEND_DIR/src/Purch.Infrastructure" \
    --startup-project "$BACKEND_DIR/src/Purch.Api"

echo
echo "Done."
