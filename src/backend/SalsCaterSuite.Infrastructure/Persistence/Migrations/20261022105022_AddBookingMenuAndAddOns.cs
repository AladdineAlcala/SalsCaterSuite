using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingMenuAndAddOns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "addonsTotal",
                table: "Booking",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "addonCategoryId",
                table: "AddonDetail",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pricingBasis",
                table: "AddonDetail",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AddonCategory",
                columns: table => new
                {
                    addonCategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    addonCategoryName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddonCategory", x => x.addonCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "BookingAddOn",
                columns: table => new
                {
                    bookingAddOnId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trn_Id = table.Column<int>(type: "int", nullable: false),
                    addonId = table.Column<int>(type: "int", nullable: false),
                    description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    pricingBasis = table.Column<int>(type: "int", nullable: false),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingAddOn", x => x.bookingAddOnId);
                    table.ForeignKey(
                        name: "FK_BookingAddOn_AddonDetail_addonId",
                        column: x => x.addonId,
                        principalTable: "AddonDetail",
                        principalColumn: "addonId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingAddOn_Booking_trn_Id",
                        column: x => x.trn_Id,
                        principalTable: "Booking",
                        principalColumn: "trn_Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookingMenu",
                columns: table => new
                {
                    bookingMenuId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    trn_Id = table.Column<int>(type: "int", nullable: false),
                    menuId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    courseId = table.Column<int>(type: "int", nullable: false),
                    prepInstruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    createdDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdByUser = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    updatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingMenu", x => x.bookingMenuId);
                    table.ForeignKey(
                        name: "FK_BookingMenu_Booking_trn_Id",
                        column: x => x.trn_Id,
                        principalTable: "Booking",
                        principalColumn: "trn_Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingMenu_CourseCategory_courseId",
                        column: x => x.courseId,
                        principalTable: "CourseCategory",
                        principalColumn: "courseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingMenu_Menu_menuId",
                        column: x => x.menuId,
                        principalTable: "Menu",
                        principalColumn: "menuid",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddonDetail_addonCategoryId",
                table: "AddonDetail",
                column: "addonCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAddOn_addonId",
                table: "BookingAddOn",
                column: "addonId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingAddOn_Booking",
                table: "BookingAddOn",
                column: "trn_Id");

            migrationBuilder.CreateIndex(
                name: "UX_BookingAddOn_Booking_Addon",
                table: "BookingAddOn",
                columns: new[] { "trn_Id", "addonId" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BookingMenu_Booking",
                table: "BookingMenu",
                column: "trn_Id");

            migrationBuilder.CreateIndex(
                name: "IX_BookingMenu_courseId",
                table: "BookingMenu",
                column: "courseId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingMenu_menuId",
                table: "BookingMenu",
                column: "menuId");

            migrationBuilder.CreateIndex(
                name: "UX_BookingMenu_Booking_Menu",
                table: "BookingMenu",
                columns: new[] { "trn_Id", "menuId" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AddonDetail_AddonCategory_addonCategoryId",
                table: "AddonDetail",
                column: "addonCategoryId",
                principalTable: "AddonCategory",
                principalColumn: "addonCategoryId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AddonDetail_AddonCategory_addonCategoryId",
                table: "AddonDetail");

            migrationBuilder.DropTable(
                name: "AddonCategory");

            migrationBuilder.DropTable(
                name: "BookingAddOn");

            migrationBuilder.DropTable(
                name: "BookingMenu");

            migrationBuilder.DropIndex(
                name: "IX_AddonDetail_addonCategoryId",
                table: "AddonDetail");

            migrationBuilder.DropColumn(
                name: "addonsTotal",
                table: "Booking");

            migrationBuilder.DropColumn(
                name: "addonCategoryId",
                table: "AddonDetail");

            migrationBuilder.DropColumn(
                name: "pricingBasis",
                table: "AddonDetail");
        }
    }
}
