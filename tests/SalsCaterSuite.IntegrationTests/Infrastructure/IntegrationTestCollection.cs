namespace SalsCaterSuite.IntegrationTests.Infrastructure;

/// <summary>
/// Serializes every DB-touching integration test against a single disposable SQL Server (LocalDB)
/// database. xUnit runs the test classes of one collection sequentially, so the shared database is
/// never exercised concurrently, and the whole database is dropped when the collection finishes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationDatabaseFixture>
{
    public const string Name = "Integration (SQL Server LocalDB)";
}
