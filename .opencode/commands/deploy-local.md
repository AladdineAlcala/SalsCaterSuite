---
description: Build the current working tree into local Docker and smoke-test it
---

Run `node scripts/deploy.mjs` from the repository root.

If it exits non-zero: read `docker compose logs --tail 200 api`, diagnose the
cause, fix it, and re-run once. Report the UI URL (`http://localhost:8080`), the
API URL (`http://localhost:5150`), and the smoke-test outcome.

Do not claim success unless the script exits `0`. Do not run
`node scripts/deploy.mjs --reset` unless the user explicitly asks — it deletes
the local database volume.
