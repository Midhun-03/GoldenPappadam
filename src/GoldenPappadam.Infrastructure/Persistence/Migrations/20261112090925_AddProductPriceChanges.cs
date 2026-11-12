using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPriceChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductPriceChanges",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NewPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPriceChanges_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPriceChanges_ProductId_CreatedAt",
                schema: "inventory",
                table: "ProductPriceChanges",
                columns: new[] { "ProductId", "CreatedAt" });

            // Every product that already has a standard price gets it as the first entry of its history,
            // dated when the product was last saved - the same start the agreed rates were given. A
            // product left blank or at zero gets none: it has no price to record until someone sets one.
            migrationBuilder.Sql(
                """
                INSERT INTO inventory.ProductPriceChanges (Id, ProductId, PreviousPrice, NewPrice, CreatedAt, CreatedBy)
                SELECT NEWID(), Id, NULL, SellingPrice, COALESCE(UpdatedAt, CreatedAt), COALESCE(UpdatedBy, CreatedBy)
                FROM inventory.Products
                WHERE SellingPrice > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPriceChanges",
                schema: "inventory");
        }
    }
}
