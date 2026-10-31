---
name: local-docker-deploy
description: Build and deploy Sal's CaterSuite to local Docker (SQL Server + .NET API + nginx-served SPA) and smoke-test it. Use after any backend or frontend implementation task, before reporting the task complete.
---

# Local Docker deployment

## When to use

After `npm run build` and `npm test` are green, and **before** you report an
implementation task complete. A task is not done until the stack is deployed and
smoke-tested.

## Steps

1. From the repository root run:

   ```sh
   node scripts/deploy.mjs
   ```

2. The script builds three images (`db`, `api`, `web`), waits for SQL Server's
   health check, starts the API and nginx, then runs four smoke tests:
   the SPA shell, a login through nginx, an authenticated
   `GET /api/packages`, and Swagger.

3. Exit code `0` means deployed. Report the three URLs and the smoke result.

4. On a non-zero exit, diagnose before retrying:

   ```sh
   docker compose ps
   docker compose logs --tail 200 api
   docker compose logs --tail 100 web
   ```

   Fix the cause, then run the script again. Do **not** report success on a
   non-zero exit, and do not describe the failure as a "known limitation".

## Hard rules

- **Never** run `node scripts/deploy.mjs --reset` or `docker compose down -v`
  unless the user explicitly asks. Both delete the local database volume.
- **Never** commit `.env`. It is git-ignored; `.env.example` is the template.
- **Never** set `ASPNETCORE_HTTPS_PORT` in the API container. `UseHttpsRedirection`
  would answer every API call with a `307` to a port that does not exist.
- `Database__ApplyMigrationsOnStartup` and `Database__SeedOnStartup` are for this
  local container only. Never enable them in a shared or production configuration
  (DEC-018 / DEC-020).
- The API listens on plain HTTP inside the container. TLS belongs at the edge.
- If the frontend image build fails, the cause is almost always `tsc`, not Docker.
  Read the build log before touching the Dockerfile.

## What "deployed" means

| URL | Expected |
| :--- | :--- |
| `http://localhost:8080` | the SPA shell |
| `http://localhost:8080/api/...` | the API, proxied by nginx (same origin, no CORS) |
| `http://localhost:5150/swagger` | Swagger UI |
| `localhost:14330` | SQL Server, database `SalsCaterSuiteDb` |

Host ports come from `.env` (`WEB_HOST_PORT`, `API_HOST_PORT`, `DB_HOST_PORT`).
The defaults avoid the local `dotnet watch` API (5149) and any native/container
SQL Server already on 1433.

The login smoke test is the important one: it only passes when nginx, the API,
SQL Server, the migrations and the seed all worked.
