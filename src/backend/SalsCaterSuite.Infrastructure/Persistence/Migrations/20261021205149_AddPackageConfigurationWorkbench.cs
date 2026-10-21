using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageConfigurationWorkbench : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "distanceKm",
                table: "Area",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "logisticsCost",
                table: "Area",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logisticsNote",
                table: "Area",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackageAreaCoverage",
                columns: table => new
                {
                    pac_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    p_id = table.Column<int>(type: "int", nullable: false),
                    areaId = table.Column<int>(type: "int", nullable: false),
                    isActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageAreaCoverage", x => x.pac_id);
                    table.ForeignKey(
                        name: "FK_PackageAreaCoverage_Area_areaId",
                        column: x => x.areaId,
                        principalTable: "Area",
                        principalColumn: "areaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageAreaCoverage_Package_p_id",
                        column: x => x.p_id,
                        principalTable: "Package",
                        principalColumn: "p_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageCourseRule",
                columns: table => new
                {
                    pcr_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    p_id = table.Column<int>(type: "int", nullable: false),
                    courseId = table.Column<int>(type: "int", nullable: false),
                    requiredQuota = table.Column<int>(type: "int", nullable: false),
                    portionGrams = table.Column<int>(type: "int", nullable: true),
                    ruleDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    inclusionTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    addonId = table.Column<int>(type: "int", nullable: true),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageCourseRule", x => x.pcr_id);
                    table.ForeignKey(
                        name: "FK_PackageCourseRule_AddonDetail_addonId",
                        column: x => x.addonId,
                        principalTable: "AddonDetail",
                        principalColumn: "addonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageCourseRule_CourseCategory_courseId",
                        column: x => x.courseId,
                        principalTable: "CourseCategory",
                        principalColumn: "courseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageCourseRule_Package_p_id",
                        column: x => x.p_id,
                        principalTable: "Package",
                        principalColumn: "p_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageAreaCoverage_areaId",
                table: "PackageAreaCoverage",
                column: "areaId");

            migrationBuilder.CreateIndex(
                name: "UX_PackageAreaCoverage_Package_Area",
                table: "PackageAreaCoverage",
                columns: new[] { "p_id", "areaId" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PackageCourseRule_addonId",
                table: "PackageCourseRule",
                column: "addonId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageCourseRule_courseId",
                table: "PackageCourseRule",
                column: "courseId");

            migrationBuilder.CreateIndex(
                name: "UX_PackageCourseRule_Package_Course",
                table: "PackageCourseRule",
                columns: new[] { "p_id", "courseId" },
                unique: true,
                filter: "[is_deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageAreaCoverage");

            migrationBuilder.DropTable(
                name: "PackageCourseRule");

            migrationBuilder.DropColumn(
                name: "distanceKm",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "logisticsCost",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "logisticsNote",
                table: "Area");
        }
    }
}
