using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SalsCaterSuite.IntegrationTests.Infrastructure;
using SalsCaterSuite.Infrastructure.Persistence;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// Test 1 — Migration validation (Scenario 11 / DEC-019, DEC-020).
/// The real EF Core migrations provision the schema on a genuinely empty SQL Server database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class MigrationValidationTests
{
    /// <summary>
    /// The newest migration in the chain. It is resolved from the assembly rather than hard-coded,
    /// so a rollback test can never leave the shared disposable database behind the newest
    /// migration — which would silently strip the newer schema from every test that follows.
    /// </summary>
    private static readonly string LatestMigrationId =
        new SalsCaterSuiteDbContext(
            new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuite_MigrationProbe;Trusted_Connection=True;MultipleActiveResultSets=true")
                .Options)
            .Database
            .GetMigrations()
            .Last();

    private readonly IntegrationDatabaseFixture _fixture;

    public MigrationValidationTests(IntegrationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task InitialCreate_is_applied_and_no_migration_is_pending()
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        applied.Should().Contain(
            m => m.EndsWith("InitialCreate", StringComparison.Ordinal),
            $"the InitialCreate migration must be applied to '{_fixture.DatabaseName}'");

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        pending.Should().BeEmpty(
            "the fixture applies every migration, and the schema must match the model");
    }

    [Fact]
    public async Task The_expected_tables_exist_in_the_test_database()
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var tables = await GetTableNamesAsync(context);

        tables.Should().Contain(new[]
        {
            "Booking", "Payment", "Refund", "Customer", "Package", "Menu",
            "CourseCategory", "Users", "Roles"
        });
    }

    // ─────────────────── the applicable-pax classification (REQ-PKG-04, DEC-173) ───────────────────

    /// <summary>
    /// The lookup table exists with the DEC-173 shape: a fixed (non-identity) primary key, the
    /// verbatim label column and the four DEC-038 audit columns.
    /// </summary>
    [Fact]
    public async Task The_applicable_pax_lookup_table_should_exist_with_the_documented_shape()
    {
        var columns = await GetColumnNamesAsync("PackagePaxApplicable");

        columns.Should().Contain(new[]
        {
            "numPaxId", "packageApplicablePax", "createdDate", "createdByUser", "updatedDate", "is_deleted"
        });

        // The primary key is fixed reference data, so the column is NOT nullable and NOT an
        // identity.
        (await GetColumnNullableAsync("PackagePaxApplicable", "numPaxId")).Should().BeFalse();
        (await GetIdentityColumnCountAsync("PackagePaxApplicable")).Should().Be(0);

        // The label is a required, bounded column.
        (await GetColumnNullableAsync("PackagePaxApplicable", "packageApplicablePax")).Should().BeFalse();
    }

    /// <summary>
    /// The tier's classification column is required and is backed by a restrictive foreign key
    /// to the lookup (DEC-173).
    /// </summary>
    [Fact]
    public async Task The_package_classification_column_should_be_required_and_constrained()
    {
        var columns = await GetColumnNamesAsync("Package");
        columns.Should().Contain("numPaxId");

        (await GetColumnNullableAsync("Package", "numPaxId")).Should().BeFalse();

        var indexes = await GetIndexNamesAsync("Package");
        indexes.Should().Contain("IX_Package_numPaxId");

        var foreignKeys = await GetForeignKeyNamesAsync("Package");
        foreignKeys.Should().Contain("FK_Package_PackagePaxApplicable_numPaxId");

        (await GetForeignKeyColumnsAsync("Package", "FK_Package_PackagePaxApplicable_numPaxId"))
            .Should().Be("numPaxId");
        (await GetForeignKeyPrincipalTableAsync("Package", "FK_Package_PackagePaxApplicable_numPaxId"))
            .Should().Be("PackagePaxApplicable");
    }

    /// <summary>
    /// The five fixed classification rows are seeded by the migration, live, and carrying
    /// truthful audit values (DEC-173).
    /// </summary>
    [Fact]
    public async Task The_five_fixed_classification_rows_should_be_seeded_and_live()
    {
        var rows = await _fixture.QueryAsync(context => context.PackagePaxApplicables
            .OrderBy(option => option.NumPaxId)
            .ToListAsync());

        rows.Should().HaveCount(5);

        rows.Select(row => row.NumPaxId).Should().ContainInOrder(1, 2, 3, 4, 5);
        rows.Select(row => row.PackageApplicablePax).Should().ContainInOrder(
            "15 to 19 Pax",
            "20 to 29 Pax",
            "30 to 49 Pax",
            "80 to 100 Above",
            "Not Applicable to PackMeals");

        foreach (var row in rows)
        {
            row.IsDeleted.Should().BeFalse("the reference rows are live");
            row.CreatedDate.Should().NotBeNull("the seeded rows carry a real creation timestamp");
            row.CreatedByUser.Should().BeNull("a schema migration has no human author");
            row.UpdatedDate.Should().BeNull("the reference rows have never been updated");
        }
    }

    /// <summary>
    /// The migration applies only the owner-approved existing-row mapping (DEC-173).
    /// <para>
    /// The mapping is the migration's own backfill, so it is proved on a <b>disposable</b>
    /// database that carries the two approved rows and is then migrated forward: p_id 1 must
    /// receive numPaxId 4 and p_id 2 must receive numPaxId 2 — and nothing else.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_migration_should_apply_only_the_owner_approved_existing_row_mapping()
    {
        await WithProbeDatabaseAsync("SalsCaterSuiteDb_MappingProbe", async context =>
        {
            var migrator = context.GetService<IMigrator>();

            // The schema up to the migration immediately before DEC-173.
            await migrator.MigrateAsync("20261026232102_RetireCourseRuleInclusionTagAndUpgradeAddon");

            // Exactly the two owner-approved rows, with the identifiers the owner supplied.
            // p_id is an identity column, so IDENTITY_INSERT is used to place the rows on the
            // two identifiers the owner named.
            await context.Database.ExecuteSqlRawAsync(
                """
                SET IDENTITY_INSERT [Package] ON;
                INSERT INTO [Package]
                    ([p_id], [p_code], [p_type], [p_descripton], [p_amountPax], [p_min], [isActive],
                     [date_Created], [date_Updated], [createdByUser], [is_deleted])
                VALUES
                    (1, N'PKG-REG-01', N'Regular', N'Approved row one', 950.00, 80, 1,
                     SYSUTCDATETIME(), NULL, NULL, 0),
                    (2, N'PKG-VIP-01', N'Vip', N'Approved row two', 1200.00, 100, 1,
                     SYSUTCDATETIME(), NULL, NULL, 0);
                SET IDENTITY_INSERT [Package] OFF;
                """);

            // The migration now applies the approved mapping.
            await migrator.MigrateAsync(LatestMigrationId);

            var rows = await context.Packages
                .OrderBy(package => package.PackageId)
                .Select(package => new { package.PackageId, package.NumPaxId })
                .ToListAsync();

            rows.Should().HaveCount(2);
            rows[0].PackageId.Should().Be(1);
            rows[0].NumPaxId.Should().Be(4, "p_id 1 must receive numPaxId 4");
            rows[1].PackageId.Should().Be(2);
            rows[1].NumPaxId.Should().Be(2, "p_id 2 must receive numPaxId 2");
        });
    }

    /// <summary>
    /// Runs an action against a private disposable probe database and drops the database
    /// afterwards.
    /// <para>
    /// The context is disposed <b>before</b> the database is dropped: SQL Server refuses to
    /// drop a database that still has an open connection, and the connection pool holds one
    /// until the context is disposed.
    /// </para>
    /// </summary>
    private static async Task WithProbeDatabaseAsync(
        string prefix,
        Func<SalsCaterSuiteDbContext, Task> action)
    {
        var (options, databaseName) = BuildProbeDatabaseOptions(prefix);

        try
        {
            await using (var context = new SalsCaterSuiteDbContext(options))
            {
                await action(context);
            }
        }
        finally
        {
            await DropProbeDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// Drops a disposable probe database and never fails a test run.
    /// <para>
    /// The drop is issued through a connection to <c>master</c>, because a database that still
    /// has a pooled connection open against it cannot be dropped from inside another database.
    /// </para>
    /// </summary>
    private static async Task DropProbeDatabaseAsync(string databaseName)
    {
        try
        {
            var masterConnectionString =
                $"Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;MultipleActiveResultSets=true";

            await using var masterConnection = new Microsoft.Data.SqlClient.SqlConnection(masterConnectionString);
            await masterConnection.OpenAsync();

            await using var command = masterConnection.CreateCommand();
            command.CommandText =
                $"IF DB_ID('{databaseName}') IS NOT NULL " +
                $"BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{databaseName}]; END";

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // Evidence cleanup must never fail a test run.
        }
    }

    /// <summary>
    /// Builds the options of a private disposable probe database. The name is unique, so the
    /// probe can never collide with the shared fixture database or with the development
    /// database (DEC-020).
    /// </summary>
    private static (DbContextOptions<SalsCaterSuiteDbContext> Options, string DatabaseName)
        BuildProbeDatabaseOptions(string prefix)
    {
        var databaseName = $"{prefix}_{Guid.NewGuid():N}";
        var connectionString = IntegrationTestWebApplicationFactory.BuildConnectionString(databaseName);

        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer(connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(typeof(SalsCaterSuiteDbContext).Assembly.FullName))
            .Options;

        return (options, databaseName);
    }

    /// <summary>
    /// The migration's guard: it refuses a package row outside the owner-approved mapping
    /// instead of inferring a classification for it (DEC-173).
    /// <para>
    /// The guard is exercised against a <b>disposable</b> database of its own: it is created and
    /// dropped inside this test, and the development or any persistent database is never
    /// touched (DEC-020).
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_migration_should_refuse_a_package_row_outside_the_owner_approved_mapping()
    {
        await WithProbeDatabaseAsync("SalsCaterSuiteDb_GuardProbe", async context =>
        {
            // Build the schema up to the migration immediately before DEC-173.
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20261026232102_RetireCourseRuleInclusionTagAndUpgradeAddon");

            // Three package rows. The identity assigns 1, 2 and 3 in order, so the THIRD row
            // carries p_id 3 — outside the owner-approved mapping { 1, 2 } — and is the row that
            // must make the migration abort.
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO [Package]
                    ([p_code], [p_type], [p_descripton], [p_amountPax], [p_min], [isActive],
                     [date_Created], [date_Updated], [createdByUser], [is_deleted])
                VALUES
                    (N'PKG-REG-01', N'Regular', N'Approved row', 950.00, 80, 1,
                     SYSUTCDATETIME(), NULL, NULL, 0),
                    (N'PKG-REG-02', N'Regular', N'Second approved row', 950.00, 80, 1,
                     SYSUTCDATETIME(), NULL, NULL, 0),
                    (N'PKG-REG-03', N'Regular', N'Unapproved row', 950.00, 80, 1,
                     SYSUTCDATETIME(), NULL, NULL, 0);
                """);

            // The unapproved row's identity is whatever the database assigned — it is outside
            // the approved set { 1, 2 } because three rows were inserted.
            var unapprovedId = await context.Packages
                .Where(package => package.Description == "Unapproved row")
                .Select(package => package.PackageId)
                .FirstAsync();

            unapprovedId.Should().NotBe(1, "the probe needs a row outside the approved mapping");
            unapprovedId.Should().NotBe(2, "the probe needs a row outside the approved mapping");

            // The migration must fail EXPLICITLY — never infer a classification for the row.
            var act = async () => await migrator.MigrateAsync(LatestMigrationId);

            // The failure is not just "some" failure: it must be the DEC-173 guard's own
            // explicit message, so an unrelated error (a missing table, a broken connection,
            // a generic constraint violation) cannot satisfy this test.
            var thrown = await act.Should().ThrowAsync<Exception>(
                "the migration must abort when a package row lies outside the owner-approved mapping");

            var failure = thrown.Which;
            var failureText = failure.ToString();

            failureText.Should().Contain(
                "DEC-173:",
                "the migration must surface the explicit DEC-173 guard message, not a generic error");

            failureText.Should().Contain(
                "outside the owner-approved applicable-pax mapping",
                "the DEC-173 guard message must name the reason the migration refused the row");

            failureText.Should().Contain(
                "p_id 1 -> numPaxId 4, p_id 2 -> numPaxId 2",
                "the DEC-173 guard message must state the owner-approved mapping it enforces");

            // The failed migration must not have applied its schema changes: EF Core 10 wraps
            // each migration in its own transaction, so the database is left as it was.
            var applied = await context.Database.GetAppliedMigrationsAsync();
            applied.Should().NotContain(
                LatestMigrationId,
                "the failed migration must not be recorded as applied");

            // The classification column must not exist — the migration's own DDL rolled back.
            var hasColumn = await context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Package') AND name = 'numPaxId'")
                .FirstAsync();
            hasColumn.Should().Be(0, "the failed migration's column must have been rolled back");
        });
    }

    // ─────────────────────── the retired course-rule columns (DEC-172) ───────────────────────

    /// <summary>
    /// The retired inclusion tag and upgrade add-on are gone from the table (DEC-172): the
    /// migration dropped the two columns.
    /// </summary>
    [Fact]
    public async Task The_retired_course_rule_columns_should_not_exist()
    {
        var columns = await GetColumnNamesAsync("PackageCourseRule");

        columns.Should().NotContain("inclusionTag");
        columns.Should().NotContain("addonId");

        // The surviving rule columns are untouched.
        columns.Should().Contain(new[] { "pcr_id", "p_id", "courseId", "requiredQuota", "portionGrams", "ruleDescription" });
    }

    /// <summary>
    /// The add-on index and the add-on foreign key are gone with the column (DEC-172).
    /// </summary>
    [Fact]
    public async Task The_retired_course_rule_index_and_foreign_key_should_not_exist()
    {
        var indexes = await GetIndexNamesAsync("PackageCourseRule");
        var foreignKeys = await GetForeignKeyNamesAsync("PackageCourseRule");

        indexes.Should().NotContain("IX_PackageCourseRule_addonId");
        foreignKeys.Should().NotContain("FK_PackageCourseRule_AddonDetail_addonId");

        // The surviving rule indexes and foreign keys are untouched.
        indexes.Should().Contain("IX_PackageCourseRule_courseId");
        indexes.Should().Contain("UX_PackageCourseRule_Package_Course");
        foreignKeys.Should().Contain("FK_PackageCourseRule_CourseCategory_courseId");
        foreignKeys.Should().Contain("FK_PackageCourseRule_Package_p_id");
    }

    /// <summary>
    /// The unrelated add-on tables keep their own <c>addonId</c> column and their own foreign
    /// key: only the course rule's reference was retired (DEC-172).
    /// </summary>
    [Fact]
    public async Task The_unrelated_addon_columns_and_foreign_keys_should_be_untouched()
    {
        var addonColumns = await GetColumnNamesAsync("AddonDetail");
        var bookingAddonColumns = await GetColumnNamesAsync("BookingAddOn");

        addonColumns.Should().Contain("addonId");
        bookingAddonColumns.Should().Contain("addonId");

        var bookingForeignKeys = await GetForeignKeyNamesAsync("BookingAddOn");

        bookingForeignKeys.Should().Contain("FK_BookingAddOn_AddonDetail_addonId");
    }

    /// <summary>
    /// The <c>Down</c> restores the <b>schema</b> only — the columns, the index and the
    /// nullable foreign key come back — and the <c>Up</c> then removes them again. The
    /// discarded values are <b>not</b> recoverable, and this proves the recreated schema is
    /// valid SQL Server (DEC-172).
    /// <para>
    /// The round trip runs against a database <b>of its own</b>, not the shared fixture
    /// database. Rolling the shared database back would strip the schema of every migration
    /// after the retirement one — including the DEC-173 classification — from the database the
    /// rest of the suite runs against, and the DEC-173 guard would then correctly refuse to
    /// migrate it forward again. A private disposable database keeps the test hermetic.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_retirement_migration_should_round_trip_down_and_up_on_a_disposable_database()
    {
        const string retirement = "20261026232102_RetireCourseRuleInclusionTagAndUpgradeAddon";
        const string predecessor = "20261024021725_AddDepartmentsAndPackageCodes";

        await WithProbeDatabaseAsync("SalsCaterSuiteDb_RoundTripProbe", async context =>
        {
            var migrator = context.GetService<IMigrator>();

            // The probe database starts at the latest migration.
            await migrator.MigrateAsync(LatestMigrationId);

            // ── Down: the schema comes back, nullable, with the index and the foreign key ──
            await migrator.MigrateAsync(predecessor);

            var downColumns = await GetColumnNamesAsync(context, "PackageCourseRule");
            downColumns.Should().Contain("inclusionTag");
            downColumns.Should().Contain("addonId");

            var downIndexes = await GetIndexNamesAsync(context, "PackageCourseRule");
            downIndexes.Should().Contain("IX_PackageCourseRule_addonId");

            var downForeignKeys = await GetForeignKeyNamesAsync(context, "PackageCourseRule");
            downForeignKeys.Should().Contain("FK_PackageCourseRule_AddonDetail_addonId");

            // The recreated column is NULLABLE and the foreign key is recreated against the
            // add-on master — the schema is restored, not re-pointed.
            (await GetColumnNullableAsync(context, "PackageCourseRule", "addonId")).Should().BeTrue();
            (await GetForeignKeyColumnsAsync(context, "PackageCourseRule", "FK_PackageCourseRule_AddonDetail_addonId"))
                .Should().Be("addonId");
            (await GetForeignKeyPrincipalTableAsync(context, "PackageCourseRule", "FK_PackageCourseRule_AddonDetail_addonId"))
                .Should().Be("AddonDetail");

            // ── Up: the retired columns are gone again ──
            await migrator.MigrateAsync(retirement);
            await migrator.MigrateAsync(LatestMigrationId);

            var upColumns = await GetColumnNamesAsync(context, "PackageCourseRule");
            upColumns.Should().NotContain("inclusionTag");
            upColumns.Should().NotContain("addonId");

            (await GetIndexNamesAsync(context, "PackageCourseRule")).Should().NotContain("IX_PackageCourseRule_addonId");
            (await GetForeignKeyNamesAsync(context, "PackageCourseRule")).Should().NotContain("FK_PackageCourseRule_AddonDetail_addonId");

            // The probe database is fully up to date again.
            var pending = await context.Database.GetPendingMigrationsAsync();
            pending.Should().BeEmpty("the disposable database must be left at the latest migration");
        });
    }

    private static async Task<List<string>> GetTableNamesAsync(SalsCaterSuiteDbContext context)
    {
        var tables = new List<string>();
        var connection = context.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;

        if (mustClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sys.tables ORDER BY name";
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }

        return tables;
    }

    /// <summary>
    /// How many identity columns the table has. The applicable-pax lookup must have none: its
    /// primary key is fixed reference data, not a generated sequence (DEC-173).
    /// </summary>
    private async Task<int> GetIdentityColumnCountAsync(string tableName)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var values = await QueryCatalogAsync(context,
            "SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID(@table)",
            ("@table", tableName));

        return values.Count == 0 ? 0 : Convert.ToInt32(values[0]);
    }

    // ─────────────────────── the catalog helpers (DEC-172) ───────────────────────

    /// <summary>The column names of a table, read from the live catalog.</summary>
    private Task<List<string>> GetColumnNamesAsync(string tableName) =>
        QueryCatalogAsync(
            "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@table) ORDER BY column_id",
            ("@table", tableName));

    /// <summary>The column names of a table, read from the live catalog of a given context.</summary>
    private static Task<List<string>> GetColumnNamesAsync(SalsCaterSuiteDbContext context, string tableName) =>
        QueryCatalogAsync(context,
            "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@table) ORDER BY column_id",
            ("@table", tableName));

    /// <summary>Whether a column accepts NULL, read from the live catalog.</summary>
    private async Task<bool> GetColumnNullableAsync(string tableName, string columnName)
    {
        var values = await QueryCatalogAsync(
            "SELECT CAST(c.is_nullable AS int) FROM sys.columns c " +
            "WHERE c.object_id = OBJECT_ID(@table) AND c.name = @column",
            ("@table", tableName),
            ("@column", columnName));

        return values.Count == 1 && values[0] == "1";
    }

    /// <summary>Whether a column accepts NULL, read from the live catalog of a given context.</summary>
    private static async Task<bool> GetColumnNullableAsync(
        SalsCaterSuiteDbContext context,
        string tableName,
        string columnName)
    {
        var values = await QueryCatalogAsync(context,
            "SELECT CAST(c.is_nullable AS int) FROM sys.columns c " +
            "WHERE c.object_id = OBJECT_ID(@table) AND c.name = @column",
            ("@table", tableName),
            ("@column", columnName));

        return values.Count == 1 && values[0] == "1";
    }

    /// <summary>The index names of a table, read from the live catalog.</summary>
    private Task<List<string>> GetIndexNamesAsync(string tableName) =>
        QueryCatalogAsync(
            "SELECT i.name FROM sys.indexes i " +
            "WHERE i.object_id = OBJECT_ID(@table) AND i.name IS NOT NULL ORDER BY i.name",
            ("@table", tableName));

    /// <summary>The index names of a table, read from the live catalog of a given context.</summary>
    private static Task<List<string>> GetIndexNamesAsync(SalsCaterSuiteDbContext context, string tableName) =>
        QueryCatalogAsync(context,
            "SELECT i.name FROM sys.indexes i " +
            "WHERE i.object_id = OBJECT_ID(@table) AND i.name IS NOT NULL ORDER BY i.name",
            ("@table", tableName));

    /// <summary>The foreign-key names of a table, read from the live catalog.</summary>
    private Task<List<string>> GetForeignKeyNamesAsync(string tableName) =>
        QueryCatalogAsync(
            "SELECT fk.name FROM sys.foreign_keys fk " +
            "WHERE fk.parent_object_id = OBJECT_ID(@table) ORDER BY fk.name",
            ("@table", tableName));

    /// <summary>The foreign-key names of a table, read from the live catalog of a given context.</summary>
    private static Task<List<string>> GetForeignKeyNamesAsync(SalsCaterSuiteDbContext context, string tableName) =>
        QueryCatalogAsync(context,
            "SELECT fk.name FROM sys.foreign_keys fk " +
            "WHERE fk.parent_object_id = OBJECT_ID(@table) ORDER BY fk.name",
            ("@table", tableName));

    /// <summary>The column a foreign key is defined on.</summary>
    private Task<string> GetForeignKeyColumnsAsync(string tableName, string foreignKeyName) =>
        QueryForeignKeyColumnAsync(
            "SELECT c.name FROM sys.foreign_key_columns fkc " +
            "INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id " +
            "WHERE fkc.constraint_object_id = OBJECT_ID(@fk) AND fkc.constraint_column_id = 1",
            foreignKeyName);

    /// <summary>The column a foreign key is defined on, read from a given context.</summary>
    private static Task<string> GetForeignKeyColumnsAsync(
        SalsCaterSuiteDbContext context,
        string tableName,
        string foreignKeyName) =>
        QueryForeignKeyColumnAsync(context,
            "SELECT c.name FROM sys.foreign_key_columns fkc " +
            "INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id " +
            "WHERE fkc.constraint_object_id = OBJECT_ID(@fk) AND fkc.constraint_column_id = 1",
            foreignKeyName);

    /// <summary>The principal table a foreign key points at.</summary>
    private Task<string> GetForeignKeyPrincipalTableAsync(string tableName, string foreignKeyName) =>
        QueryForeignKeyColumnAsync(
            "SELECT OBJECT_NAME(fkc.referenced_object_id) FROM sys.foreign_key_columns fkc " +
            "WHERE fkc.constraint_object_id = OBJECT_ID(@fk) AND fkc.constraint_column_id = 1",
            foreignKeyName);

    /// <summary>The principal table a foreign key points at, read from a given context.</summary>
    private static Task<string> GetForeignKeyPrincipalTableAsync(
        SalsCaterSuiteDbContext context,
        string tableName,
        string foreignKeyName) =>
        QueryForeignKeyColumnAsync(context,
            "SELECT OBJECT_NAME(fkc.referenced_object_id) FROM sys.foreign_key_columns fkc " +
            "WHERE fkc.constraint_object_id = OBJECT_ID(@fk) AND fkc.constraint_column_id = 1",
            foreignKeyName);

    private async Task<string> QueryForeignKeyColumnAsync(string sql, string foreignKeyName)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        var values = await QueryCatalogAsync(context, sql, ("@fk", foreignKeyName));

        return values.Count == 0 ? string.Empty : values[0];
    }

    private static async Task<string> QueryForeignKeyColumnAsync(
        SalsCaterSuiteDbContext context,
        string sql,
        string foreignKeyName)
    {
        var values = await QueryCatalogAsync(context, sql, ("@fk", foreignKeyName));

        return values.Count == 0 ? string.Empty : values[0];
    }

    /// <summary>
    /// Runs a catalog query against the fixture's disposable database and reads the first
    /// column of every row.
    /// </summary>
    private async Task<List<string>> QueryCatalogAsync(string sql, params (string Name, string Value)[] parameters)
    {
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SalsCaterSuiteDbContext>();

        return await QueryCatalogAsync(context, sql, parameters);
    }

    /// <summary>
    /// Runs a catalog query against a given context's database and reads the first column of
    /// every row.
    /// </summary>
    private static async Task<List<string>> QueryCatalogAsync(
        SalsCaterSuiteDbContext context,
        string sql,
        params (string Name, string Value)[] parameters)
    {
        var results = new List<string>();

        var connection = context.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;

        if (mustClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty);
            }
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync();
            }
        }

        return results;
    }
}
