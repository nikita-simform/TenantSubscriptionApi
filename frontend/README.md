# TenantSubscriptionApi demo frontend

A small React (Vite) app that exercises every feature of the backend — built purely to
showcase the demo, not part of the graded spec itself (see `../README.md`).

## Running

```bash
npm install
npm run dev
```

Needs the backend running at `http://localhost:5100` (`VITE_API_BASE_URL` in `.env`) with
CORS enabled for `http://localhost:5173` — already configured in `Program.cs` for
`Development`.

## What to try

- **Sign up** a new organization — auto-logs you in as its OrgAdmin, landing on Resources.
- **Resources** — create a few; log in as a different organization and confirm you never
  see them (the isolation guarantee, visible from the UI side).
- **Org Members** — invite users up to your plan's seat limit (Free = 3, including the
  admin), then try one more — the 409 rejection surfaces as a plain error banner.
- **Platform Admin** — log in as `admin@platform.local` / `change-me-please` (seeded by the
  backend on startup) to see every org's plan/usage, with no path to any org's actual
  content.

## Notes

- JWT decoding (`src/lib/jwt.js`) is for UI routing/display only — which nav links show,
  which pages redirect away. It grants nothing; the backend re-validates the token and
  re-checks the role/org on every single request regardless of what the UI decided.
- `{orgId}` in org-member URLs is cosmetic, matching the backend: every scoped lookup
  there uses the caller's own JWT, never the route.
