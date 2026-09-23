using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPriceChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerPriceChanges",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPriceChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPriceChanges_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerPriceChanges_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceChanges_CreatedAt",
                schema: "sales",
                table: "CustomerPriceChanges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceChanges_CustomerId_CreatedAt",
                schema: "sales",
                table: "CustomerPriceChanges",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceChanges_ProductId",
                schema: "sales",
                table: "CustomerPriceChanges",
                column: "ProductId");

            // Every rate agreed before the history existed becomes its first entry, stamped with
            // when and by whom it was last set - so the history starts from what is true today rather
            // than from nothing. Removed rates are left out: when they were first set is not known.
            // NEWID() rather than a sequential id is fine for a one-off backfill.
            migrationBuilder.Sql("""
                INSERT INTO [sales].[CustomerPriceChanges]
                    ([Id], [CustomerId], [ProductId], [PreviousPrice], [NewPrice], [CreatedAt], [CreatedBy])
                SELECT NEWID(), [CustomerId], [ProductId], NULL, [UnitPrice],
                       COALESCE([UpdatedAt], [CreatedAt]), COALESCE([UpdatedBy], [CreatedBy])
                FROM [sales].[CustomerPrices]
                WHERE [IsActive] = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerPriceChanges",
                schema: "sales");
        }
    }
}
