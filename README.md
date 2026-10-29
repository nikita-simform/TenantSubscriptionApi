# TenantSubscriptionApi

ASP.NET Core 8 + EF Core + PostgreSQL implementation of the multi-tenant subscription
management POC spec (`~/Downloads/15-multi-tenant-subscription-management.md`), ported
from an existing Next.js/Prisma reference implementation (`~/Documents/saas-tenant-poc`).

## Architecture — schema-per-tenant isolation

Every organization gets its own real Postgres schema (`org_<slug>`) holding its content
(`Resource`). There is no `OrganizationId` column on `Resource` to forget to filter by —
isolation is structural: it comes from which schema a request's connection is bound to,
resolved from the caller's JWT, never from a route parameter. Control-plane data
(`Organization`, `Plan`, `User`, `Invite`, `AuditLog`) lives in the shared `public` schema.

See the full design rationale in the implementation plan:
`/home/nikita/.claude/plans/home-nikita-downloads-15-multi-tenant-s-prancy-fiddle.md`

## Running locally (without Docker)

Prerequisites: .NET 8 SDK, PostgreSQL 16 reachable at `localhost:5432` with a `postgres`/`postgres`
superuser, databases `tenant_subscription_poc` and `tenant_subscription_poc_test` created.

```bash
cd src/TenantSubscriptionApi
dotnet run
```

The app applies `public`-schema migrations, seeds a "Free" plan and a platform admin
(`admin@platform.local` / `change-me-please`, from `appsettings.json`), and catches up
every existing org's tenant schema — all automatically, on every startup.

## Running via Docker

```bash
cp .env.example .env   # fill in JWT_SECRET and seed admin credentials
docker compose up --build
```

Postgres is exposed on host port **5433** (not 5432, to avoid colliding with a native
Postgres already running on this dev machine) — the app container talks to it internally
as `db:5432` over the Docker network. The API listens on `http://localhost:8080`.

## Running the tests

```bash
dotnet test
```

Requires the same local PostgreSQL instance as above (tests run against
`tenant_subscription_poc_test`, not a mocked/in-memory database — the whole point of the
isolation and concurrency tests is proving behavior against real Postgres semantics).

## Key endpoints

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/signup` | none | Self-service onboarding |
| POST | `/api/auth/login` | none | Returns a JWT |
| GET/POST | `/api/resources`, `/api/resources/{id}` | OrgAdmin/OrgMember | Tenant-scoped content |
| POST | `/api/orgs/{orgId}/users` | OrgAdmin | Invite (seat-limit enforced atomically) |
| DELETE | `/api/orgs/{orgId}/users/{userId}` | OrgAdmin | `{orgId}` is cosmetic — scoping comes from the caller's own JWT |
| PUT | `/api/orgs/{orgId}/users/{userId}/role` | OrgAdmin | |
| GET | `/api/platform/orgs` | PlatformAdmin | Paginated org list — metadata/usage only, never content |
| GET | `/health` | none | Postgres connectivity check |

## Audit trail

`AuditLog` rows (shared `public` schema) are written for `org.onboarded`,
`invite.limit_rejected`, and `user.cross_tenant_access_denied` — query them directly via
`psql` for now; there's no dedicated endpoint.

## Demo frontend

A small React app (Vite) in `frontend/` exercises every feature above — signup, login,
tenant-scoped resources, org member management (invite/remove/change role, with the seat
limit visibly enforced), and the platform admin org list. See `frontend/README.md`.

```bash
cd src/TenantSubscriptionApi && dotnet run &     # API on :5100
cd frontend && npm install && npm run dev        # UI on :5173
```

The seeded platform admin (`admin@platform.local` / `change-me-please`) logs straight into
the Platform Admin view; sign up a new organization to see the OrgAdmin/OrgMember side.

## Deployment

Two deployment paths are documented, both deliberately kept to the "basics" scope of this
project (no Key Vault/Managed Identity/ECS-Fargate-grade hardening):

- **Azure** (Container Apps + Azure Database for PostgreSQL) — see the conversation history
  / implementation plan for the step-by-step `az` CLI walkthrough.
- **AWS via LocalStack** (`deploy/localstack/`) — runs the API/Postgres exactly as above and
  deploys the React build to a LocalStack-emulated S3 static website, entirely free on
  LocalStack's Community edition (ECS Fargate and RDS backing engines are Pro-only features,
  deliberately not used here). See `deploy/localstack/README.md`.
