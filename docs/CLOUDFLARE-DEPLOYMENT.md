# Deploying the Web Client to Cloudflare Pages

This guide covers deploying the Purch.io web client (`web/` — React 19, TypeScript, Vite, Tailwind CSS 4, React Router 7) to [Cloudflare Pages](https://pages.cloudflare.com/).

---

## Key Configurations in the Codebase

The following files prepare the web client for Cloudflare Pages:

| File | Purpose |
|---|---|
| [`web/public/_redirects`](file:///d:/Projects/Research%20Projects/Purch.io/web/public/_redirects) | SPA fallback rewrite rule (`/*  /index.html  200`) so direct navigation and refreshes on client routes (e.g. `/sell`, `/kiosk`, `/inventory`, `/business`) do not return 404s. |
| [`web/public/_headers`](file:///d:/Projects/Research%20Projects/Purch.io/web/public/_headers) | Sets security headers (`nosniff`, `SAMEORIGIN`, permissions policy), enforces revalidation for `index.html` on new releases, and sets 1-year immutable caching for hashed files in `/assets/*`. |
| [`web/wrangler.jsonc`](file:///d:/Projects/Research%20Projects/Purch.io/web/wrangler.jsonc) | Cloudflare Pages configuration declaring project name (`purch-io-web`), build output directory (`./dist`), and compatibility date. |
| [`web/.nvmrc`](file:///d:/Projects/Research%20Projects/Purch.io/web/.nvmrc) | Enforces Node.js version `22` in Cloudflare's build runners. |
| [`.github/workflows/web-deploy.yml`](file:///d:/Projects/Research%20Projects/Purch.io/.github/workflows/web-deploy.yml) | Automated deployment workflow via GitHub Actions and Wrangler. |
| [`web/package.json`](file:///d:/Projects/Research%20Projects/Purch.io/web/package.json) | Includes `wrangler` dev dependency and scripts `preview:cf` and `deploy:cf`. |

---

## Deployment Methods

You can deploy either using Cloudflare's native Git integration (recommended for automatic branch previews and zero-maintenance CI), via GitHub Actions, or manually via the Wrangler CLI.

### Option 1: Cloudflare Dashboard Git Integration (Recommended)

1. Log into the [Cloudflare Dashboard](https://dash.cloudflare.com/).
2. Navigate to **Workers & Pages** → **Create application** → **Pages** → **Connect to Git**.
3. Select your GitHub repository.
4. Set up the build configuration:
   - **Project name**: `purch-io-web` (or your preferred name)
   - **Production branch**: `main`
   - **Framework preset**: `Vite` (or `None`)
   - **Root directory**: `web`
   - **Build command**: `npm run build`
   - **Build output directory**: `dist`
5. Configure **Environment Variables** (Settings → Environment variables):
   - `NODE_VERSION`: `22` (Cloudflare also reads `web/.nvmrc` automatically)
   - `VITE_API_BASE_URL`: The production URL of the deployed backend (e.g., `https://purch-io-backend.vercel.app` or custom domain). *If omitted, defaults to `https://purch-io-backend.vercel.app`.*
6. Click **Save and Deploy**.

Cloudflare will automatically build preview deployments for pull requests and deploy `main` to production.

---

### Option 2: GitHub Actions (`.github/workflows/web-deploy.yml`)

If you prefer building and deploying via GitHub Actions:

1. In your GitHub repository, go to **Settings** → **Secrets and variables** → **Actions**.
2. Add the following repository secrets:
   - `CLOUDFLARE_API_TOKEN`: Create a token in Cloudflare Dashboard → **My Profile** → **API Tokens** → **Create Token** using the **Cloudflare Pages** template (or custom permissions: `Account:Cloudflare Pages:Edit`).
   - `CLOUDFLARE_ACCOUNT_ID`: Located on the right sidebar of the Cloudflare Dashboard overview page.
3. (Optional) Under **Variables**, add `VITE_API_BASE_URL` if pointing to a non-default backend endpoint.
4. When code is pushed to `main` under `web/**`, the workflow will run tests/build and deploy to Cloudflare Pages.

---

### Option 3: Manual CLI Deployment with Wrangler

You can deploy directly from your local terminal:

```bash
cd web

# 1. Authenticate with Cloudflare (only needed once)
npx wrangler login

# 2. Build and deploy to production
npm run deploy:cf

# Or deploy to a preview branch
npx wrangler pages deploy dist --branch=preview
```

---

## Backend CORS Configuration (Crucial)

The web client runs as a Single Page Application in the browser. In production, calls to the backend API (`https://purch-io-backend.vercel.app` or Render) are cross-origin requests.

The backend requires the web origin to be listed in `CORS_ALLOWED_ORIGINS` (see [Program.cs](file:///d:/Projects/Research%20Projects/Purch.io/backend/src/Purch.Api/Program.cs#L240-L251)).

### Steps to update backend CORS:

1. Copy your Cloudflare Pages URL (e.g., `https://purch-io-web.pages.dev`) and any custom domains (e.g., `https://app.purch.io`).
2. Add them to the backend environment variables:
   - On **Vercel** (or Render): Go to **Project Settings** → **Environment Variables**.
   - Set or update:
     ```
     CORS_ALLOWED_ORIGINS=https://purch-io-web.pages.dev,https://purch-io-backend.vercel.app
     ```
   - Redeploy the backend for the updated CORS origins to take effect.

---

## Local Testing with Cloudflare Rules

To test how Cloudflare Pages serves the built application (including `_redirects` and `_headers`):

```bash
cd web

# Builds the app and runs Cloudflare Pages local dev server
npm run preview:cf
```

This starts a local server (typically at `http://localhost:8788`) mimicking the Cloudflare Pages edge environment. You can test deep-linking into routes (e.g. `/sell`, `/onboarding`) without 404 errors.
