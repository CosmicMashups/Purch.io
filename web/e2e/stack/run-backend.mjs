// Starts what the end-to-end tests need behind the web app: a throwaway Postgres and the real ASP.NET
// backend pointed at it. Playwright starts this as a web server and waits for /health/ready.
//
// The database comes from Docker when it works. If Docker is unavailable, a private Postgres is started
// from locally installed binaries (PG_BIN, or a standard install folder) in e2e/.pgdata on its own port.
// Set E2E_DB=docker or E2E_DB=local to force one.
import { spawn, spawnSync } from 'node:child_process';
import { createWriteStream, existsSync, mkdirSync, readdirSync, writeFileSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const here = path.dirname(fileURLToPath(import.meta.url));
const webDir = path.resolve(here, '..', '..');
const backendProject = path.resolve(webDir, '..', 'backend', 'src', 'Purch.Api');
const logDir = path.join(webDir, 'e2e', '.logs');
mkdirSync(logDir, { recursive: true });

const DB_CONTAINER = 'purch-e2e-db';
const DB_PORT = process.env.E2E_DB_PORT ?? '55432';
const API_PORT = process.env.E2E_API_PORT ?? '5099';
const DB_PASSWORD = 'purch_e2e_password';
const CONNECTION = `Host=127.0.0.1;Port=${DB_PORT};Database=purch;Username=purch;Password=${DB_PASSWORD}`;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const docker = (...args) => spawnSync('docker', args, { encoding: 'utf8' });

// ---- Docker ---------------------------------------------------------------------------------------
function startDockerDatabase() {
  const state = docker('inspect', '-f', '{{.State.Running}}', DB_CONTAINER);
  if (state.status === 0) {
    if (state.stdout.trim() !== 'true' && docker('start', DB_CONTAINER).status !== 0) return false;
  } else {
    const run = docker('run', '-d', '--name', DB_CONTAINER, '-e', 'POSTGRES_DB=purch', '-e', 'POSTGRES_USER=purch', '-e', `POSTGRES_PASSWORD=${DB_PASSWORD}`, '-p', `${DB_PORT}:5432`, 'postgres:16');
    if (run.status !== 0) {
      console.error(`Docker could not start Postgres:\n${run.stderr}`);
      return false;
    }
  }
  return true;
}

async function waitForDockerDatabase() {
  for (let i = 0; i < 60; i++) {
    if (docker('exec', DB_CONTAINER, 'pg_isready', '-U', 'purch', '-d', 'purch').status === 0) return true;
    await sleep(1000);
  }
  return false;
}

// ---- Local binaries -------------------------------------------------------------------------------
function findPgBin() {
  if (process.env.PG_BIN && existsSync(process.env.PG_BIN)) return process.env.PG_BIN;
  const root = process.env.ProgramFiles ? path.join(process.env.ProgramFiles, 'PostgreSQL') : null;
  if (root && existsSync(root)) {
    const versions = readdirSync(root).filter((v) => existsSync(path.join(root, v, 'bin'))).sort((a, b) => Number(b) - Number(a));
    if (versions.length > 0) return path.join(root, versions[0], 'bin');
  }
  return null;
}

async function startLocalDatabase() {
  const bin = findPgBin();
  if (!bin) return false;
  const exe = (name) => path.join(bin, process.platform === 'win32' ? `${name}.exe` : name);
  const dataDir = path.join(webDir, 'e2e', '.pgdata');
  const env = { ...process.env, PGPASSWORD: DB_PASSWORD };
  const run = (name, ...args) => spawnSync(exe(name), args, { encoding: 'utf8', env });

  const alreadyUp = run('pg_isready', '-h', '127.0.0.1', '-p', DB_PORT).status === 0;

  if (!alreadyUp && !existsSync(path.join(dataDir, 'PG_VERSION'))) {
    const pwFile = path.join(logDir, 'pg-password.txt');
    writeFileSync(pwFile, DB_PASSWORD);
    const init = run('initdb', '-D', dataDir, '-U', 'purch', `--pwfile=${pwFile}`, '--auth=scram-sha-256', '-E', 'UTF8');
    rmSync(pwFile, { force: true });
    if (init.status !== 0) {
      console.error(`initdb failed:\n${init.stderr}`);
      return false;
    }
  }
  if (!alreadyUp) {
    // The server inherits this call's pipes and would keep it waiting forever, so its output goes to a file.
    const start = spawnSync(exe('pg_ctl'), ['-D', dataDir, '-o', `-p ${DB_PORT} -c listen_addresses=127.0.0.1`, '-l', path.join(logDir, 'postgres.log'), '-w', 'start'], { stdio: 'ignore', env });
    if (start.status !== 0) {
      console.error('pg_ctl could not start Postgres; see e2e/.logs/postgres.log');
      return false;
    }
  }
  const create = run('createdb', '-h', '127.0.0.1', '-p', DB_PORT, '-U', 'purch', 'purch');
  // "already exists" is fine on a reused data folder.
  return create.status === 0 || /already exists/.test(create.stderr);
}

// ---- Choose ---------------------------------------------------------------------------------------
async function startDatabase() {
  const want = process.env.E2E_DB ?? 'auto';
  if (want !== 'local' && startDockerDatabase() && (await waitForDockerDatabase())) {
    console.log('Using Postgres from Docker');
    return;
  }
  if (want !== 'docker' && (await startLocalDatabase())) {
    console.log('Using a private Postgres from local binaries');
    return;
  }
  throw new Error('No database available. Start Docker Desktop, or install PostgreSQL and set PG_BIN to its bin folder.');
}

await startDatabase();

const log = createWriteStream(path.join(logDir, 'backend.log'));

// Cloud mode is used because it honours X-Forwarded-For, which lets each test present its own client
// address and never share the 10-per-15-minutes sign-in limit. The storage settings are unused here.
const api = spawn(process.env.DOTNET ?? 'dotnet', ['run', '--project', backendProject, '--no-launch-profile'], {
  env: {
    ...process.env,
    PURCH_DEPLOYMENT_MODE: 'Cloud',
    SUPABASE_DB_CONNECTION_STRING: CONNECTION,
    SUPABASE_STORAGE_URL: 'http://127.0.0.1:1/unused',
    SUPABASE_STORAGE_KEY: 'unused',
    JWT_SIGNING_KEY: 'e2e-only-signing-key-not-a-secret-0123456789',
    JWT_ISSUER: 'purch.io',
    PORT: API_PORT,
    ASPNETCORE_ENVIRONMENT: 'Development',
  },
  stdio: ['ignore', 'pipe', 'pipe'],
});
api.stdout.pipe(log);
api.stderr.pipe(log);
api.on('exit', (code) => process.exit(code ?? 1));

for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => api.kill());
