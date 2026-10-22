# Sal's CaterSuite — backend

The .NET 10 Web API (Clean Architecture).

| Project | Responsibility |
| :--- | :--- |
| `SalsCaterSuite.Domain` | Entities, value objects, domain rules, enumerations |
| `SalsCaterSuite.Application` | Use cases, commands, queries, DTOs, validators, repository interfaces |
| `SalsCaterSuite.Infrastructure` | EF Core / SQL Server persistence, exports, document generation, external adapters |
| `SalsCaterSuite.WebApi` | HTTP endpoints, authentication, DI composition, middleware |

Persistence is repository-only (DEC-037): there is no `ISalsCaterSuiteDbContext`.
Every repository interface lives in `Application/Common/Interfaces` and is expressed
in Domain entities and Application DTOs.

## Build and test

From the **repository root** (not from this folder):

```bash
npm run build:api
npm run test:api
```

See the repository root `README.md` for the full monorepo layout, the frontend
workspace, the test projects, and the development workflow.
