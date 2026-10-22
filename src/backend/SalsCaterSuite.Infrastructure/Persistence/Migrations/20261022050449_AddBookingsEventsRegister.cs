using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Bookings &amp; Events Register and the Reservation Workbench (REQ-BKG-05, DEC-106,
    /// DEC-116).
    /// <para>
    /// The migration is <b>purely additive</b>: four columns on <c>Booking</c> — three nullable
    /// (<c>callTime</c>, <c>packoutTime</c>, <c>discountCode</c>) and one
    /// <c>NOT NULL DEFAULT 0</c> (<c>extendedAreaAmount</c>). No column is dropped, renamed or
    /// retyped and no existing row is modified; a pre-existing row's surcharge is <c>0</c>,
    /// which is the truth, because no booking has ever been charged one.
    /// </para>
    /// <para>
    /// Generated and script-reviewed only — it is <b>never</b> applied to an existing or a
    /// production database without explicit human authorization (DEC-020).
    /// </para>
    /// </summary>
    public partial class AddBookingsEventsRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "callTime",
                table: "Booking",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discountCode",
                table: "Booking",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "extendedAreaAmount",
                table: "Booking",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "packoutTime",
                table: "Booking",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "callTime",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "discountCode",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "extendedAreaAmount",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "packoutTime",
                table: "Booking");
        }
    }
}
