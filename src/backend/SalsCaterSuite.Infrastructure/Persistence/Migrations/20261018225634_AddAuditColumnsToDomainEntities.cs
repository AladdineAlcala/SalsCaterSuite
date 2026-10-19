using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalsCaterSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumnsToDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "Reservation",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdDate",
                table: "Reservation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Reservation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedDate",
                table: "Reservation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "Refund",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdDate",
                table: "Refund",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Refund",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedDate",
                table: "Refund",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Payment",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "Package",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Package",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "Menu",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdDate",
                table: "Menu",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Menu",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedDate",
                table: "Menu",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "Discount",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Discount",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "Customer",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdDate",
                table: "Customer",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Customer",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedDate",
                table: "Customer",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "CourseCategory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdDate",
                table: "CourseCategory",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "CourseCategory",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedDate",
                table: "CourseCategory",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "Area",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdDate",
                table: "Area",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "Area",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedDate",
                table: "Area",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "createdByUser",
                table: "AddonDetail",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "createdDate",
                table: "AddonDetail",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "AddonDetail",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updatedDate",
                table: "AddonDetail",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "createdDate",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "updatedDate",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "Refund");

            migrationBuilder.DropColumn(
                name: "createdDate",
                table: "Refund");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Refund");

            migrationBuilder.DropColumn(
                name: "updatedDate",
                table: "Refund");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "Menu");

            migrationBuilder.DropColumn(
                name: "createdDate",
                table: "Menu");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Menu");

            migrationBuilder.DropColumn(
                name: "updatedDate",
                table: "Menu");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "Discount");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Discount");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "createdDate",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "updatedDate",
                table: "Customer");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "CourseCategory");

            migrationBuilder.DropColumn(
                name: "createdDate",
                table: "CourseCategory");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "CourseCategory");

            migrationBuilder.DropColumn(
                name: "updatedDate",
                table: "CourseCategory");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "createdDate",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "updatedDate",
                table: "Area");

            migrationBuilder.DropColumn(
                name: "createdByUser",
                table: "AddonDetail");

            migrationBuilder.DropColumn(
                name: "createdDate",
                table: "AddonDetail");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "AddonDetail");

            migrationBuilder.DropColumn(
                name: "updatedDate",
                table: "AddonDetail");
        }
    }
}
