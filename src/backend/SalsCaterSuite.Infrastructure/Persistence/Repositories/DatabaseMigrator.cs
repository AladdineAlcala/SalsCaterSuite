namespace SalsCaterSuite.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Applies pending EF Core migrations (DEC-020, DEC-037). Kept in the repositories layer so
/// that schema migration — like every other persistence call — is confined to
/// <c>Infrastructure.Persistence.Repositories</c> and the seeding routine never touches the
/// <c>DbContext</c> itself.
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>Applies any pending EF Core migrations to the configured database.</summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IDatabaseMigrator" />
public sealed class DatabaseMigrator : IDatabaseMigrator
{
    private readonly SalsCaterSuiteDbContext _context;

    public DatabaseMigrator(SalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        _context.Database.MigrateAsync(cancellationToken);
}
