using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesmanDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SalesmanDiscountCapPercent",
                schema: "sales",
                table: "InvoiceSettings",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "sales",
                table: "InvoiceSettings",
                keyColumn: "Id",
                keyValue: new Guid("6a1c0de2-3f5b-4c1e-9e57-1f0b7a2d4c01"),
                column: "SalesmanDiscountCapPercent",
                value: 20m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceSettings_SalesmanDiscountCapPercent",
                schema: "sales",
                table: "InvoiceSettings",
                sql: "[SalesmanDiscountCapPercent] >= 0 AND [SalesmanDiscountCapPercent] <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceSettings_SalesmanDiscountCapPercent",
                schema: "sales",
                table: "InvoiceSettings");

            migrationBuilder.DropColumn(
                name: "SalesmanDiscountCapPercent",
                schema: "sales",
                table: "InvoiceSettings");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                schema: "sales",
                table: "Invoices");
        }
    }
}
