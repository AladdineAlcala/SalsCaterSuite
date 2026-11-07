using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingLifecyclePaymentStateAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DEC-178: add the end date nullable, refuse to infer an overnight end, then backfill
            // and require the column. A sentinel date is never written.
            migrationBuilder.AddColumn<DateOnly>(
                name: "eventEndDate",
                table: "Booking",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Booking]
                    WHERE [endTime] < [startTime])
                BEGIN
                    THROW 51000, 'DEC-178: a Booking row has endTime before startTime. EventEndDate was NOT inferred and NOT defaulted; the migration was aborted.', 1;
                END
                """);

            migrationBuilder.Sql(
                """
                UPDATE [Booking]
                SET [eventEndDate] = [eventDate]
                WHERE [eventEndDate] IS NULL;
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "eventEndDate",
                table: "Booking",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "payment_state",
                table: "Booking",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BookingStatusHistory",
                columns: table => new
                {
                    bookingStatusHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trn_Id = table.Column<int>(type: "int", nullable: false),
                    statusOrdinal = table.Column<int>(type: "int", nullable: false),
                    statusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    previousStatusOrdinal = table.Column<int>(type: "int", nullable: true),
                    previousStatusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    actorSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    effectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    recordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingStatusHistory", x => x.bookingStatusHistoryId);
                    table.ForeignKey(
                        name: "FK_BookingStatusHistory_Booking_trn_Id",
                        column: x => x.trn_Id,
                        principalTable: "Booking",
                        principalColumn: "trn_Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_trn_Id",
                table: "BookingStatusHistory",
                column: "trn_Id");

            // DEC-177: preserve the original ordinal and name before the lifecycle remap.
            // An ordinal outside the legacy 0-9 vocabulary is refused rather than renamed.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Booking]
                    WHERE [booking_status] NOT BETWEEN 0 AND 9)
                BEGIN
                    THROW 51000, 'DEC-177: a Booking row has a booking_status outside the legacy 0-9 map. The status was NOT remapped; the migration was aborted.', 1;
                END

                INSERT INTO [BookingStatusHistory]
                    ([trn_Id], [statusOrdinal], [statusName], [previousStatusOrdinal], [previousStatusName],
                     [actorSource], [effectiveAtUtc], [recordedAtUtc], [createdDate], [createdByUser], [is_deleted])
                SELECT
                    b.[trn_Id],
                    b.[booking_status],
                    CASE b.[booking_status]
                        WHEN 0 THEN N'Pending'
                        WHEN 1 THEN N'Confirmed'
                        WHEN 2 THEN N'DepositPaid'
                        WHEN 3 THEN N'FullyPaid'
                        WHEN 4 THEN N'InProgress'
                        WHEN 5 THEN N'Completed'
                        WHEN 6 THEN N'Cancelled'
                        WHEN 7 THEN N'NoShow'
                        WHEN 8 THEN N'Rescheduled'
                        WHEN 9 THEN N'Expired'
                    END,
                    NULL,
                    NULL,
                    N'system/migration-baseline',
                    COALESCE(b.[b_createdDate], SYSUTCDATETIME()),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    N'system/migration-baseline',
                    0
                FROM [Booking] AS b;

                UPDATE [Booking]
                SET [booking_status] = CASE [booking_status]
                    WHEN 0 THEN 0
                    WHEN 1 THEN 1
                    WHEN 2 THEN 1
                    WHEN 3 THEN 1
                    WHEN 4 THEN 2
                    WHEN 5 THEN 3
                    WHEN 6 THEN 4
                    WHEN 7 THEN 5
                    WHEN 8 THEN 6
                    WHEN 9 THEN 7
                END;

                IF EXISTS (
                    SELECT 1
                    FROM [Payment]
                    WHERE [is_deleted] = 0
                      AND [amtPay] < 0)
                BEGIN
                    THROW 51000, 'DEC-177: a live Payment row has a negative amtPay. Payment state was NOT inferred; the migration was aborted.', 1;
                END

                UPDATE b
                SET b.[payment_state] = CASE
                    WHEN pay.[collected] = 0 AND b.[amountDue] > 0 THEN 0
                    WHEN pay.[collected] > 0 AND pay.[collected] < b.[amountDue] THEN 1
                    ELSE 2
                END
                FROM [Booking] AS b
                CROSS APPLY (
                    SELECT ISNULL(SUM(p.[amtPay]), 0) AS [collected]
                    FROM [Payment] AS p
                    WHERE p.[trn_Id] = b.[trn_Id]
                      AND p.[is_deleted] = 0
                ) AS pay;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DEC-177/DEC-178: Down is fail-closed unless the database is in the exact
            // pre-remap state that the migration created. Never reconstruct a legacy
            // status from payment sums. Only a pure baseline (no post-cutover transitions)
            // may be rolled back safely.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [BookingStatusHistory]
                    WHERE [actorSource] <> N'system/migration-baseline')
                BEGIN
                    THROW 51000, 'DEC-177/DEC-178: post-cutover booking lifecycle transitions exist. Down is fail-closed; restore from a verified backup or use a separately approved compatibility mapping.', 1;
                END

                IF EXISTS (
                    SELECT 1
                    FROM [Booking] b
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [BookingStatusHistory] h
                        WHERE h.[trn_Id] = b.[trn_Id]
                          AND h.[actorSource] = N'system/migration-baseline'))
                BEGIN
                    THROW 51000, 'DEC-177/DEC-178: a Booking row has no migration baseline in BookingStatusHistory. Down is fail-closed.', 1;
                END

                IF EXISTS (
                    SELECT 1
                    FROM [Payment]
                    WHERE [is_deleted] = 0
                      AND [createdDate] IS NOT NULL
                      AND [createdDate] > (
                          SELECT COALESCE(MAX([recordedAtUtc]), '1900-01-01')
                          FROM [BookingStatusHistory]
                          WHERE [actorSource] = N'system/migration-baseline'))
                BEGIN
                    THROW 51000, 'DEC-177/DEC-178: payments created after the lifecycle migration exist. Down is fail-closed and must not infer legacy statuses from payment sums.', 1;
                END

                UPDATE b
                SET b.[booking_status] = h.[statusOrdinal]
                FROM [Booking] AS b
                INNER JOIN [BookingStatusHistory] AS h ON h.[trn_Id] = b.[trn_Id]
                WHERE h.[actorSource] = N'system/migration-baseline'
                  AND h.[previousStatusOrdinal] IS NULL;
                """);

            migrationBuilder.DropTable(
                name: "BookingStatusHistory");

            migrationBuilder.DropColumn(
                name: "eventEndDate",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "payment_state",
                table: "Booking");
        }
    }
}
