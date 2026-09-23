using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                schema: "sales",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMultipleBranches",
                schema: "sales",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CustomerBranches",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerBranches_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BranchId",
                schema: "sales",
                table: "Invoices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBranches_CustomerId_Name",
                schema: "sales",
                table: "CustomerBranches",
                columns: new[] { "CustomerId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_CustomerBranches_BranchId",
                schema: "sales",
                table: "Invoices",
                column: "BranchId",
                principalSchema: "sales",
                principalTable: "CustomerBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_CustomerBranches_BranchId",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "CustomerBranches",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_BranchId",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "HasMultipleBranches",
                schema: "sales",
                table: "Customers");
        }
    }
}
