# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.

## Deployment

The web client is configured for deployment to **Cloudflare** (Workers Static Assets).
- Direct navigation and SPA routing fallback are handled natively via `"not_found_handling": "single-page-application"` in `wrangler.jsonc`.
- Caching policies and security headers are specified via `public/_headers`.
- Cloudflare configuration is defined in `wrangler.jsonc` and `package.json`.

Commands:
- `npm run preview:cf`: Test the Cloudflare edge build locally with Wrangler.
- `npm run deploy:cf`: Build and deploy to Cloudflare.

For detailed setup instructions, CI/CD, and backend CORS configuration, see [docs/CLOUDFLARE-DEPLOYMENT.md](../docs/CLOUDFLARE-DEPLOYMENT.md).
