using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentsAndPackageCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The code is added nullable first so the existing rows can be backfilled before the
            // NOT NULL constraint and the unique index are applied.
            migrationBuilder.AddColumn<string>(
                name: "p_code",
                table: "Package",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "p_extendedLocationAmount",
                table: "Package",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "deptId",
                table: "AddonDetail",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Department",
                columns: table => new
                {
                    deptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    deptName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Department", x => x.deptId);
                });

            // Backfill the existing tiers deterministically: the abbreviation of the stored type,
            // then the per-type sequence in p_id order. The six known types have the same fixed
            // abbreviations the application generator uses; any other free-text type uses its
            // first three alphanumeric characters (upper-cased, padded with X when shorter).
            migrationBuilder.Sql(
                """
                ;WITH Abbr AS (
                    SELECT p_id,
                           CASE UPPER(LTRIM(RTRIM(ISNULL(p_type, ''))))
                               WHEN 'REGULAR' THEN 'REG'
                               WHEN 'WEDDING' THEN 'WED'
                               WHEN 'VIP' THEN 'VIP'
                               WHEN 'PREMIER' THEN 'PRE'
                               WHEN 'PM' THEN 'PM'
                               WHEN 'SD' THEN 'SD'
                               WHEN '' THEN 'GEN'
                               ELSE LEFT(UPPER(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(p_type, ''), ' ', ''), '-', ''), '/', ''), '.', '')) + 'XXX', 3)
                           END AS abbr
                    FROM Package
                ),
                Ranked AS (
                    SELECT p_id, abbr,
                           ROW_NUMBER() OVER (PARTITION BY abbr ORDER BY p_id) AS rn
                    FROM Abbr
                )
                UPDATE p
                SET p_code = 'PKG-' + r.abbr + '-' +
                    CASE WHEN r.rn < 100
                         THEN RIGHT('00' + CAST(r.rn AS varchar(10)), 2)
                         ELSE CAST(r.rn AS varchar(10))
                    END
                FROM Package p
                JOIN Ranked r ON r.p_id = p.p_id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "p_code",
                table: "Package",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            // The legacy Menu.deptId column was never a foreign key and may hold values that
            // have no department row. The Department master ships empty, so an orphaned value
            // cannot be mapped: it is cleared rather than allowed to break the new constraint.
            // A later import can re-link the row to a real department.
            migrationBuilder.Sql(
                """
                UPDATE [Menu]
                SET [deptId] = NULL
                WHERE [deptId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM [Department] AS d WHERE d.[deptId] = [Menu].[deptId]);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Package_p_code",
                table: "Package",
                column: "p_code",
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Menu_deptId",
                table: "Menu",
                column: "deptId");

            migrationBuilder.CreateIndex(
                name: "IX_AddonDetail_deptId",
                table: "AddonDetail",
                column: "deptId");

            migrationBuilder.AddForeignKey(
                name: "FK_AddonDetail_Department_deptId",
                table: "AddonDetail",
                column: "deptId",
                principalTable: "Department",
                principalColumn: "deptId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Menu_Department_deptId",
                table: "Menu",
                column: "deptId",
                principalTable: "Department",
                principalColumn: "deptId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AddonDetail_Department_deptId",
                table: "AddonDetail");

            migrationBuilder.DropForeignKey(
                name: "FK_Menu_Department_deptId",
                table: "Menu");

            migrationBuilder.DropTable(
                name: "Department");

            migrationBuilder.DropIndex(
                name: "IX_Package_p_code",
                table: "Package");

            migrationBuilder.DropIndex(
                name: "IX_Menu_deptId",
                table: "Menu");

            migrationBuilder.DropIndex(
                name: "IX_AddonDetail_deptId",
                table: "AddonDetail");

            migrationBuilder.DropColumn(
                name: "p_code",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "p_extendedLocationAmount",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "deptId",
                table: "AddonDetail");
        }
    }
}
