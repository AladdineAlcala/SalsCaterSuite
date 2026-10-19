namespace SalsCaterSuite.Infrastructure.Persistence;

/// <summary>
/// Applies database migrations and seeds the system-required reference data.
///
/// Only system-required data is seeded (DEC-018): Identity roles (DEC-017), a single
/// bootstrap administrator and the standard <c>CourseCategory</c> reference values.
/// Every operation is idempotent and matches on a natural key; an existing row is never
/// mutated or overwritten.
///
/// The implementation is environment-agnostic. It is invoked from application startup
/// only when <c>Database:ApplyMigrationsOnStartup</c> / <c>Database:SeedOnStartup</c>
/// are enabled, which they are not by default outside Development (DEC-020).
///
/// The contract is deliberately neutral (no EF Core type appears in it); its
/// implementation lives in <c>Infrastructure.Persistence.Repositories</c> (DEC-037)
/// alongside the repository abstractions it uses.
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Applies any pending EF Core migrations to the configured database.
    /// Intended for the Development/test database only (DEC-020).
    /// </summary>
    Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds the system-required roles, bootstrap administrator and course categories.
    /// Safe to run repeatedly against the same database.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}
