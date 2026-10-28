using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Retires the course rule's inclusion tag and tier-1 upgrade add-on (DEC-172).
    /// <para>
    /// This is the project's first <b>destructive</b> migration: it drops
    /// <c>PackageCourseRule.inclusionTag</c> and <c>PackageCourseRule.addonId</c>, together with
    /// the add-on foreign key and index that hung off <c>addonId</c>. The values in those two
    /// optional columns are <b>discarded</b>; no backfill is approved.
    /// </para>
    /// <para>
    /// The <c>Up</c> drops the foreign key and the index <b>before</b> the columns, because
    /// SQL Server refuses to drop a column that a dependent foreign key or index still
    /// references. The <c>Down</c> recreates the two <b>nullable</b> columns, then the index,
    /// then the foreign key (last, because it depends on the column) — it restores the
    /// <b>schema only</b>; the discarded values are not recoverable.
    /// </para>
    /// <para>
    /// Only <c>PackageCourseRule.addonId</c> is touched. <c>AddonDetail.addonId</c> and
    /// <c>BookingAddOn.addonId</c> are unrelated and remain untouched.
    /// </para>
    /// <para>
    /// Generated and script-reviewed only. This migration must never be applied to an existing
    /// or production database without explicit human authorization (DEC-020); the disposable
    /// per-run integration-test database is the only execution target.
    /// </para>
    /// </summary>
    public partial class RetireCourseRuleInclusionTagAndUpgradeAddon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PackageCourseRule_AddonDetail_addonId",
                table: "PackageCourseRule");

            migrationBuilder.DropIndex(
                name: "IX_PackageCourseRule_addonId",
                table: "PackageCourseRule");

            migrationBuilder.DropColumn(
                name: "addonId",
                table: "PackageCourseRule");

            migrationBuilder.DropColumn(
                name: "inclusionTag",
                table: "PackageCourseRule");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "addonId",
                table: "PackageCourseRule",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inclusionTag",
                table: "PackageCourseRule",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageCourseRule_addonId",
                table: "PackageCourseRule",
                column: "addonId");

            migrationBuilder.AddForeignKey(
                name: "FK_PackageCourseRule_AddonDetail_addonId",
                table: "PackageCourseRule",
                column: "addonId",
                principalTable: "AddonDetail",
                principalColumn: "addonId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
