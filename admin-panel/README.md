# BlastScale admin / LiveOps panel

Internal operations console for the BlastScale backend:

- **Player support**: search players, profile, wallet, ledger, sessions, level progress, telemetry
  timeline, and manual resource grants (fully audited in the ledger).
- **LiveOps**: live events (Rocket Race / Double Reward), A/B experiments, remote config,
  leaderboard seasons, hand-tuned level definitions.
- **System**: actuator health per component, liveness/readiness, outbox backlog, anti-cheat chain,
  Prometheus traffic cards (requests/sec, p95/p99, error rate, cache hit rate).

Stack: Vite + React 18 + TypeScript + react-router v6, plain CSS, no UI library. The panel talks to
the backend's `/api/v1/admin/**` endpoints (JWT bearer token, role `ADMIN`), the public actuator
health endpoints and the Prometheus HTTP API.

## Development

```bash
cd admin-panel
npm install
npm run dev        # http://localhost:5173
```

Configuration is read at build time (see `.env.example`, copy it to `.env`):

| Variable              | Default                 | Purpose                        |
|-----------------------|-------------------------|--------------------------------|
| `VITE_API_BASE_URL`   | `http://localhost:8080` | BlastScale API base URL        |
| `VITE_PROMETHEUS_URL` | `http://localhost:9090` | Prometheus HTTP API base URL   |

Sign in with the bootstrap admin account the backend creates on first start: `admin` /
`admin12345`. The credentials come from the backend's `BLASTSCALE_ADMIN_USERNAME` /
`BLASTSCALE_ADMIN_PASSWORD` environment variables. Only accounts with role `ADMIN` can enter.

`npm run build` type-checks the project (`tsc --noEmit`) and writes the production bundle to `dist/`.

## Docker / docker-compose

`docker compose up --build` from the repository root builds the `admin-panel` service and serves it
on <http://localhost:3001>. The image is multi-stage: `node:22-alpine` runs `npm run build` with the
`VITE_API_BASE_URL` / `VITE_PROMETHEUS_URL` build args, and `nginx:1.27-alpine` serves the static
files with an SPA fallback (`try_files $uri /index.html`) on port 80.
