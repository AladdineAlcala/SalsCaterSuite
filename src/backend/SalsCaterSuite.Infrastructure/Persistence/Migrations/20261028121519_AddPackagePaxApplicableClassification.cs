using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// REQ-PKG-04 / DEC-173 — the applicable-pax classification of a package tier.
    /// <para>
    /// The migration follows the canonical EF Core pattern for making a column non-nullable:
    /// the column is added <b>nullable</b>, the existing rows are backfilled with the
    /// owner-approved mapping, and only then is the column altered to <c>NOT NULL</c>. There is
    /// no automatic NULL-fill step, so the developer must write it — and it is written here.
    /// </para>
    /// <para>
    /// <b>The guard.</b> Before the backfill, the migration aborts with an explicit SQL error if
    /// any package row lies outside the owner-approved mapping (<c>p_id</c> 1 and 2). The
    /// classification is never inferred and never defaulted: an unmapped row makes the migration
    /// fail loudly rather than receive a guessed value. EF Core 10 wraps each migration in its
    /// own transaction, so the failed migration leaves the database untouched.
    /// </para>
    /// <para>
    /// <b>The seed.</b> The five fixed classification rows are inserted with their stated
    /// identifiers and labels, verbatim. Their creation timestamp is the fixed UTC moment this
    /// migration was authored — a deterministic literal, because EF Core's model managed data
    /// requires deterministic values and a non-deterministic one would make the model snapshot
    /// unstable and report perpetual pending model changes. <c>createdByUser</c> is NULL: the
    /// rows were inserted by a schema migration and have no human author, so no creator identity
    /// is fabricated (BR-21). <c>updatedDate</c> is NULL because the rows have never been
    /// updated, and <c>is_deleted</c> is false — the reference rows are live.
    /// </para>
    /// <para>
    /// <b>Safety.</b> Generated and script-reviewed only. It must never be applied to an
    /// existing or production database without explicit human authorization (DEC-020).
    /// </para>
    /// </remarks>
    public partial class AddPackagePaxApplicableClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The lookup table. Its primary key is fixed reference data, so it carries no
            //    identity: the five identifiers are part of the reference data itself.
            migrationBuilder.CreateTable(
                name: "PackagePaxApplicable",
                columns: table => new
                {
                    numPaxId = table.Column<int>(type: "int", nullable: false),
                    packageApplicablePax = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePaxApplicable", x => x.numPaxId);
                });

            // 2. The five fixed classification rows, with their labels stored verbatim. The
            //    skipped ranges and the "80 to 100 Above" wording are intentional (DEC-173).
            //    This is EF Core's model managed data: the identifiers and the labels are fixed
            //    reference data, so the scaffold turns them into InsertData/DeleteData.
            migrationBuilder.InsertData(
                table: "PackagePaxApplicable",
                columns: new[] { "numPaxId", "createdByUser", "createdDate", "packageApplicablePax", "updatedDate" },
                values: new object[,]
                {
                    { 1, null, new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc), "15 to 19 Pax", null },
                    { 2, null, new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc), "20 to 29 Pax", null },
                    { 3, null, new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc), "30 to 49 Pax", null },
                    { 4, null, new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc), "80 to 100 Above", null },
                    { 5, null, new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc), "Not Applicable to PackMeals", null }
                });

            // 3. The tier's column is added NULLABLE first, so the existing rows can be
            //    backfilled before the NOT NULL constraint is applied.
            migrationBuilder.AddColumn<int>(
                name: "numPaxId",
                table: "Package",
                type: "int",
                nullable: true);

            // 4. THE GUARD. The migration aborts explicitly when a package row lies outside the
            //    owner-approved mapping. The classification is never inferred for such a row and
            //    never defaulted to a placeholder: the migration fails instead. The check runs
            //    BEFORE the backfill and BEFORE the NOT NULL alteration, so the operator sees the
            //    explicit message rather than a generic constraint violation.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Package]
                    WHERE [p_id] NOT IN (1, 2))
                BEGIN
                    THROW 51000, 'DEC-173: a Package row lies outside the owner-approved applicable-pax mapping (p_id 1 -> numPaxId 4, p_id 2 -> numPaxId 2). The classification was NOT inferred and NOT defaulted; the migration was aborted.', 1;
                END
                """);

            // 5. The owner-approved backfill, applied to exactly the two confirmed rows.
            migrationBuilder.Sql(
                """
                UPDATE [Package]
                SET [numPaxId] = CASE [p_id]
                    WHEN 1 THEN 4
                    WHEN 2 THEN 2
                END
                WHERE [p_id] IN (1, 2);
                """);

            // 6. The column becomes required.
            migrationBuilder.AlterColumn<int>(
                name: "numPaxId",
                table: "Package",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 7. The foreign-key backing index and the constraint itself. The delete behaviour is
            //    Restrict: a tier can never delete the classification it points at.
            migrationBuilder.CreateIndex(
                name: "IX_Package_numPaxId",
                table: "Package",
                column: "numPaxId");

            migrationBuilder.AddForeignKey(
                name: "FK_Package_PackagePaxApplicable_numPaxId",
                table: "Package",
                column: "numPaxId",
                principalTable: "PackagePaxApplicable",
                principalColumn: "numPaxId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Package_PackagePaxApplicable_numPaxId",
                table: "Package");

            migrationBuilder.DropIndex(
                name: "IX_Package_numPaxId",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "numPaxId",
                table: "Package");

            migrationBuilder.DropTable(
                name: "PackagePaxApplicable");
        }
    }
}
