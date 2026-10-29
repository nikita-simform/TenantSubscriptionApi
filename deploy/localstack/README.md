# Deploying the frontend to LocalStack (S3 static website hosting)

LocalStack emulates AWS services locally — it's unrelated to Azure. This folder is a
**separate, additional** deployment path to practice a real, common AWS pattern (S3
static website hosting for an SPA), fully free on LocalStack's Community edition.

## Why not ECS + RDS here

A "full AWS" deployment would run the API on ECS Fargate and Postgres on RDS. Both of
those need **LocalStack Pro** (paid) to actually execute containers / run a real
database engine — Community edition only stubs the control-plane API calls for them.
So this setup keeps the API and Postgres exactly as they already run (the project's own
`docker-compose.yml`, or plain `dotnet run` + local Postgres) and uses LocalStack
**only** for the piece that's genuinely free and fully functional: S3.

## Architecture

```
Browser ──► S3 static website (LocalStack)   [the React build]
              │
              └──► fetch() calls ──► http://localhost:8080   [the API — NOT in LocalStack,
                                                                runs exactly as before]
```

## Prerequisites

- Docker daemon access working (`docker ps` succeeds — this needs the `docker` group
  permission fix; LocalStack itself is a Docker container)
- The API already running and reachable at `http://localhost:8080` (via the project's
  main `docker-compose.yml`, or `dotnet run` in `src/TenantSubscriptionApi`)
- AWS CLI installed (`aws --version`) — no real AWS account/credentials needed

## Running it

```bash
# 1. Start LocalStack
cd deploy/localstack
docker compose up -d

# 2. Build the frontend and deploy it to an S3-hosted static website
./deploy.sh
```

The script builds the React app (pointed at `http://localhost:8080` for the API),
creates a bucket, turns on static website hosting, applies a public-read policy
(required for the website endpoint to be viewable without AWS credentials), and
uploads the build — printing the resulting URL when done.

## Tearing down

```bash
docker compose down -v   # -v also removes LocalStack's own data volume
```
