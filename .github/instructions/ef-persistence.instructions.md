---
applyTo: "**/SalsCaterSuite.Application/**/*.cs, **/SalsCaterSuite.Infrastructure/Persistence/**/*.cs"
---
# EF Core Persistence Rule (DEC-037 — not negotiable)

- Application project MUST NOT reference Microsoft.EntityFrameworkCore or any Identity package.
- `ISalsCaterSuiteDbContext` does not exist. Repository interfaces live in `Application/Common/Interfaces`, expressed in Domain entities and Application DTOs/projections — no `DbSet`, no `IQueryable`, no `DbContext` types.
- Implementations live only in `Infrastructure/Persistence/Repositories`.
- Only Infrastructure (plus `Infrastructure/DependencyInjection.cs` and `WebApi/Program.cs`) may touch EF Core or Identity.
- Every repository shares the one request-scoped `SalsCaterSuiteDbContext`; a single `SaveChangesAsync` commits the whole change set. No `IUnitOfWork`.
- Application-layer unit tests use hand-written fakes, never EF.