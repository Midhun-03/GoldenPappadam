using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products");

            migrationBuilder.EnsureSchema(
                name: "ownshop");

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumSellingPrice",
                schema: "inventory",
                table: "Products",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShopSales",
                schema: "ownshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SeriesCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    SaleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopSales", x => x.Id);
                    table.CheckConstraint("CK_ShopSales_PaymentMethod", "[PaymentMethod] <> 'ReturnCredit'");
                    table.CheckConstraint("CK_ShopSales_Sequence", "[SequenceNumber] > 0");
                    table.CheckConstraint("CK_ShopSales_TotalAmount", "[TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_ShopSales_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopSales_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShopTransfers",
                schema: "ownshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PiecesProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityKg = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PiecesPerKg = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PiecesReceived = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SourceOnHandBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ShopOnHandBefore = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopTransfers", x => x.Id);
                    table.CheckConstraint("CK_ShopTransfers_DifferentLocations", "[FromLocationId] <> [ToLocationId]");
                    table.CheckConstraint("CK_ShopTransfers_DifferentProducts", "[SourceProductId] <> [PiecesProductId]");
                    table.CheckConstraint("CK_ShopTransfers_PiecesPerKg", "[PiecesPerKg] > 0");
                    table.CheckConstraint("CK_ShopTransfers_PiecesReceived", "[PiecesReceived] > 0");
                    table.CheckConstraint("CK_ShopTransfers_QuantityKg", "[QuantityKg] > 0");
                    table.ForeignKey(
                        name: "FK_ShopTransfers_Products_PiecesProductId",
                        column: x => x.PiecesProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopTransfers_Products_SourceProductId",
                        column: x => x.SourceProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopTransfers_StockLocations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopTransfers_StockLocations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShopSaleLines",
                schema: "ownshop",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopSaleLines", x => x.Id);
                    table.CheckConstraint("CK_ShopSaleLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_ShopSaleLines_UnitPrice", "[MinimumPrice] > 0 AND [UnitPrice] >= [MinimumPrice] AND [UnitPrice] <= [DefaultPrice]");
                    table.ForeignKey(
                        name: "FK_ShopSaleLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopSaleLines_ShopSales_ShopSaleId",
                        column: x => x.ShopSaleId,
                        principalSchema: "ownshop",
                        principalTable: "ShopSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "StockLocations",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "IsActive", "Kind", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("3b0a4a0f-1002-4b2f-8a6b-1c2b1b000003"), "SHOP", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Shop", "Own shop", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Products_SourceProductId_ActivePieces",
                schema: "inventory",
                table: "Products",
                column: "SourceProductId",
                unique: true,
                filter: "[Kind] = 'Pieces' AND [IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_MinimumSellingPrice",
                schema: "inventory",
                table: "Products",
                sql: "[MinimumSellingPrice] IS NULL OR ([Kind] = 'Pieces' AND [MinimumSellingPrice] > 0 AND [SellingPrice] IS NOT NULL AND [MinimumSellingPrice] <= [SellingPrice])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products",
                sql: "([Kind] = 'Packed' AND [SourceProductId] IS NOT NULL AND (([SourceQuantityPerPack] > 0 AND [PiecesPerPack] IS NULL) OR ([SourceQuantityPerPack] IS NULL AND [PiecesPerPack] > 0))) OR ([Kind] = 'Loose' AND [SourceProductId] IS NULL AND [SourceQuantityPerPack] IS NULL AND [PiecesPerPack] IS NULL) OR ([Kind] = 'Pieces' AND [SourceProductId] IS NOT NULL AND [SourceQuantityPerPack] IS NULL AND [PiecesPerPack] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSaleLines_ProductId",
                schema: "ownshop",
                table: "ShopSaleLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSaleLines_ShopSaleId_LineNumber",
                schema: "ownshop",
                table: "ShopSaleLines",
                columns: new[] { "ShopSaleId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopSales_ClientRequestId",
                schema: "ownshop",
                table: "ShopSales",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSales_CustomerId_SaleDate",
                schema: "ownshop",
                table: "ShopSales",
                columns: new[] { "CustomerId", "SaleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopSales_LocationId",
                schema: "ownshop",
                table: "ShopSales",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSales_SaleDate",
                schema: "ownshop",
                table: "ShopSales",
                column: "SaleDate");

            migrationBuilder.CreateIndex(
                name: "IX_ShopSales_SaleNumber",
                schema: "ownshop",
                table: "ShopSales",
                column: "SaleNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopSales_SeriesCode_FinancialYear_SequenceNumber",
                schema: "ownshop",
                table: "ShopSales",
                columns: new[] { "SeriesCode", "FinancialYear", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopTransfers_ClientRequestId",
                schema: "ownshop",
                table: "ShopTransfers",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShopTransfers_FromLocationId",
                schema: "ownshop",
                table: "ShopTransfers",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopTransfers_OccurredAt",
                schema: "ownshop",
                table: "ShopTransfers",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShopTransfers_PiecesProductId",
                schema: "ownshop",
                table: "ShopTransfers",
                column: "PiecesProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopTransfers_SourceProductId",
                schema: "ownshop",
                table: "ShopTransfers",
                column: "SourceProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopTransfers_ToLocationId",
                schema: "ownshop",
                table: "ShopTransfers",
                column: "ToLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopSaleLines",
                schema: "ownshop");

            migrationBuilder.DropTable(
                name: "ShopTransfers",
                schema: "ownshop");

            migrationBuilder.DropTable(
                name: "ShopSales",
                schema: "ownshop");

            migrationBuilder.DropIndex(
                name: "IX_Products_SourceProductId_ActivePieces",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_MinimumSellingPrice",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DeleteData(
                schema: "inventory",
                table: "StockLocations",
                keyColumn: "Id",
                keyValue: new Guid("3b0a4a0f-1002-4b2f-8a6b-1c2b1b000003"));

            migrationBuilder.DropColumn(
                name: "MinimumSellingPrice",
                schema: "inventory",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products",
                sql: "([Kind] = 'Packed' AND [SourceProductId] IS NOT NULL AND (([SourceQuantityPerPack] > 0 AND [PiecesPerPack] IS NULL) OR ([SourceQuantityPerPack] IS NULL AND [PiecesPerPack] > 0))) OR ([Kind] = 'Loose' AND [SourceProductId] IS NULL AND [SourceQuantityPerPack] IS NULL AND [PiecesPerPack] IS NULL)");
        }
    }
}
