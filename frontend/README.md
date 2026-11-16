# DotNextAI web app

Next.js 16 + Fluent UI v9 front end of DotNextAI (port 3030). Run it from the repository root with `./scripts/dev.sh` (API + web) or `./scripts/dev.sh web`; see the [root README](../README.md#web-app) and the [web app spec](../docs/specs/feature-web-app.md).

```bash
npm ci --ignore-scripts
npm run dev          # http://localhost:3030 (needs .env.local, see .env.example)
npm run lint && npm run typecheck && npm test
npm run build
```

Code layout: `src/app` (routes), `src/components` (shell, home, chat, preferences, ui), `src/lib` (i18n, theme, chat rules, security), `src/services` (API client and types).
