using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.SqlClient;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.IntegrationTests.Infrastructure;

namespace SalsCaterSuite.IntegrationTests;

/// <summary>
/// DEC-177 / DEC-178 data transform. Runs only against a unique LocalDB database and drops it.
/// It never targets SalsCaterSuiteDb.
/// </summary>
public class BookingLifecycleMigrationTests
{
    private const string PreviousMigration = "20261028121519_AddPackagePaxApplicableClassification";

    [Fact]
    public async Task The_migration_should_remap_lifecycle_status_and_backfill_payment_state_and_history()
    {
        await WithProbeDatabaseAsync("SalsCaterSuiteDb_LifecycleMap", async context =>
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await InsertLegacyBookingsAsync(context);

            await migrator.MigrateAsync(
                "20261031144115_AddBookingLifecyclePaymentStateAndHistory");

            var rows = await context.Database.SqlQuery<LegacyResult>(
                $"""
                SELECT b.[trn_Id] AS [TransactionId],
                       b.[booking_status] AS [Status],
                       b.[payment_state] AS [PaymentState],
                       b.[eventEndDate] AS [EventEndDate],
                       b.[eventDate] AS [EventDate],
                       h.[statusOrdinal] AS [HistoryOrdinal],
                       h.[statusName] AS [HistoryName],
                       h.[actorSource] AS [ActorSource]
                FROM [Booking] AS b
                INNER JOIN [BookingStatusHistory] AS h ON h.[trn_Id] = b.[trn_Id]
                ORDER BY b.[trn_Id]
                """)
                .ToListAsync();

            rows.Should().HaveCount(10);
            rows.Select(row => row.ActorSource).Should().OnlyContain(source => source == "system/migration-baseline");
            rows.Should().OnlyContain(row => row.EventEndDate == row.EventDate);

            rows[0].Should().Match<LegacyResult>(row => row.Status == 0 && row.PaymentState == 0 && row.HistoryOrdinal == 0 && row.HistoryName == "Pending");
            rows[1].Should().Match<LegacyResult>(row => row.Status == 1 && row.PaymentState == 0 && row.HistoryOrdinal == 1 && row.HistoryName == "Confirmed");
            rows[2].Should().Match<LegacyResult>(row => row.Status == 1 && row.PaymentState == 1 && row.HistoryOrdinal == 2 && row.HistoryName == "DepositPaid");
            rows[3].Should().Match<LegacyResult>(row => row.Status == 1 && row.PaymentState == 2 && row.HistoryOrdinal == 3 && row.HistoryName == "FullyPaid");
            rows[4].Should().Match<LegacyResult>(row => row.Status == 2 && row.PaymentState == 2 && row.HistoryOrdinal == 4 && row.HistoryName == "InProgress");
            rows[5].Should().Match<LegacyResult>(row => row.Status == 3 && row.PaymentState == 0 && row.HistoryOrdinal == 5 && row.HistoryName == "Completed");
            rows[6].Should().Match<LegacyResult>(row => row.Status == 4 && row.PaymentState == 0 && row.HistoryOrdinal == 6 && row.HistoryName == "Cancelled");
            rows[7].Should().Match<LegacyResult>(row => row.Status == 5 && row.PaymentState == 0 && row.HistoryOrdinal == 7 && row.HistoryName == "NoShow");
            rows[8].Should().Match<LegacyResult>(row => row.Status == 6 && row.PaymentState == 0 && row.HistoryOrdinal == 8 && row.HistoryName == "Rescheduled");
            rows[9].Should().Match<LegacyResult>(row => row.Status == 7 && row.PaymentState == 0 && row.HistoryOrdinal == 9 && row.HistoryName == "Expired");
        });
    }

    [Fact]
    public async Task The_migration_should_fail_closed_when_end_time_is_before_start_time()
    {
        await WithProbeDatabaseAsync("SalsCaterSuiteDb_LifecycleGuard", async context =>
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO [Customer] ([firstname], [lastname]) VALUES (N'Guard', N'Probe');
                DECLARE @customerId int = CAST(SCOPE_IDENTITY() AS int);
                INSERT INTO [Booking]
                    ([bookingNumber], [c_Id], [numberOfHeads], [booktype], [typeofservice],
                     [eventDate], [startTime], [endTime], [transdate],
                     [p_pricePerHead], [p_amount], [subtotal], [discountType], [discountPercentage],
                     [discountAmount], [taxAmount], [amountDue], [depositAmount], [amountPaid],
                     [apply_extendedAmount], [booking_status])
                VALUES
                    (N'BK-GUARD-01', @customerId, 10, 0, 0,
                     '2026-01-01', '18:00', '09:00', SYSUTCDATETIME(),
                     100, 1000, 1000, 0, 0, 0, 0, 1000, 0, 0, 0, 1);
                """);

            var act = async () => await migrator.MigrateAsync(
                "20261031144115_AddBookingLifecyclePaymentStateAndHistory");

            var failure = await act.Should().ThrowAsync<Exception>();
            failure.Which.ToString().Should().Contain("DEC-178");
        });
    }

    private static async Task InsertLegacyBookingsAsync(SalsCaterSuiteDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [Customer] ([firstname], [lastname]) VALUES (N'Legacy', N'Probe');
            DECLARE @customerId int = CAST(SCOPE_IDENTITY() AS int);

            SET IDENTITY_INSERT [Booking] ON;
            INSERT INTO [Booking]
                ([trn_Id], [bookingNumber], [c_Id], [numberOfHeads], [booktype], [typeofservice],
                 [eventDate], [startTime], [endTime], [transdate], [b_createdDate],
                 [p_pricePerHead], [p_amount], [subtotal], [discountType], [discountPercentage],
                 [discountAmount], [taxAmount], [amountDue], [depositAmount], [amountPaid],
                 [apply_extendedAmount], [booking_status])
            VALUES
                (1, N'BK-LEGACY-00', @customerId, 10, 0, 0, '2026-01-01', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-01T08:00:00', 50, 500, 500, 0, 0, 0, 0, 500, 0, 0, 0, 0),
                (2, N'BK-LEGACY-01', @customerId, 10, 0, 0, '2026-01-02', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-02T08:00:00', 100, 1000, 1000, 0, 0, 0, 0, 1000, 0, 0, 0, 1),
                (3, N'BK-LEGACY-02', @customerId, 10, 0, 0, '2026-01-03', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-03T08:00:00', 100, 1000, 1000, 0, 0, 0, 0, 1000, 0, 400, 0, 2),
                (4, N'BK-LEGACY-03', @customerId, 10, 0, 0, '2026-01-04', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-04T08:00:00', 100, 1000, 1000, 0, 0, 0, 0, 1000, 0, 1000, 0, 3),
                (5, N'BK-LEGACY-04', @customerId, 10, 0, 0, '2026-01-05', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-05T08:00:00', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4),
                (6, N'BK-LEGACY-05', @customerId, 10, 0, 0, '2026-01-06', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-06T08:00:00', 80, 800, 800, 0, 0, 0, 0, 800, 0, 0, 0, 5),
                (7, N'BK-LEGACY-06', @customerId, 10, 0, 0, '2026-01-07', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-07T08:00:00', 80, 800, 800, 0, 0, 0, 0, 800, 0, 0, 0, 6),
                (8, N'BK-LEGACY-07', @customerId, 10, 0, 0, '2026-01-08', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-08T08:00:00', 80, 800, 800, 0, 0, 0, 0, 800, 0, 0, 0, 7),
                (9, N'BK-LEGACY-08', @customerId, 10, 0, 0, '2026-01-09', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-09T08:00:00', 80, 800, 800, 0, 0, 0, 0, 800, 0, 0, 0, 8),
                (10, N'BK-LEGACY-09', @customerId, 10, 0, 0, '2026-01-10', '10:00', '14:00', SYSUTCDATETIME(), '2026-01-10T08:00:00', 20, 200, 200, 0, 0, 0, 0, 200, 0, 0, 0, 9);
            SET IDENTITY_INSERT [Booking] OFF;

            INSERT INTO [Payment]
                ([payNo], [trn_Id], [dateofPayment], [amtPay], [pay_means], [is_deleted])
            VALUES
                (N'OR-LEGACY-02', 3, SYSUTCDATETIME(), 400, N'Cash', 0),
                (N'OR-LEGACY-03', 4, SYSUTCDATETIME(), 1000, N'Cash', 0),
                (N'OR-LEGACY-09', 10, SYSUTCDATETIME(), 200, N'Cash', 1);
            """);
    }

    private static async Task WithProbeDatabaseAsync(
        string prefix,
        Func<SalsCaterSuiteDbContext, Task> action)
    {
        var databaseName = $"{prefix}_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer(
                IntegrationTestWebApplicationFactory.BuildConnectionString(databaseName),
                sql => sql.MigrationsAssembly(typeof(SalsCaterSuiteDbContext).Assembly.FullName))
            .Options;

        try
        {
            await using var context = new SalsCaterSuiteDbContext(options);
            await action(context);
        }
        finally
        {
            await DropAsync(databaseName);
        }
    }

    private static async Task DropAsync(string databaseName)
    {
        try
        {
            await using var connection = new SqlConnection(
                "Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"IF DB_ID('{databaseName}') IS NOT NULL " +
                $"BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{databaseName}]; END";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // Cleanup must not hide the assertion.
        }
    }

    private sealed class LegacyResult
    {
        public int TransactionId { get; set; }
        public int Status { get; set; }
        public int PaymentState { get; set; }
        public DateOnly EventEndDate { get; set; }
        public DateOnly EventDate { get; set; }
        public int HistoryOrdinal { get; set; }
        public string HistoryName { get; set; } = string.Empty;
        public string ActorSource { get; set; } = string.Empty;
    }
}
