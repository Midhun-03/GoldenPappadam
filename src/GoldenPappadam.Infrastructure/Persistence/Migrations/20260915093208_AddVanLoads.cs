using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVanLoads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fieldsales");

            migrationBuilder.CreateTable(
                name: "VanLoads",
                schema: "fieldsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VanLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VanLoads", x => x.Id);
                    table.CheckConstraint("CK_VanLoads_Ends", "[VanLocationId] <> [WarehouseLocationId]");
                    table.ForeignKey(
                        name: "FK_VanLoads_StockLocations_VanLocationId",
                        column: x => x.VanLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VanLoads_StockLocations_WarehouseLocationId",
                        column: x => x.WarehouseLocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VanLoadLines",
                schema: "fieldsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VanLoadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VanLoadLines", x => x.Id);
                    table.CheckConstraint("CK_VanLoadLines_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_VanLoadLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VanLoadLines_VanLoads_VanLoadId",
                        column: x => x.VanLoadId,
                        principalSchema: "fieldsales",
                        principalTable: "VanLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VanLoadLines_ProductId",
                schema: "fieldsales",
                table: "VanLoadLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_VanLoadLines_VanLoadId",
                schema: "fieldsales",
                table: "VanLoadLines",
                column: "VanLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_VanLoads_VanLocationId_BusinessDate",
                schema: "fieldsales",
                table: "VanLoads",
                columns: new[] { "VanLocationId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VanLoads_WarehouseLocationId",
                schema: "fieldsales",
                table: "VanLoads",
                column: "WarehouseLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VanLoadLines",
                schema: "fieldsales");

            migrationBuilder.DropTable(
                name: "VanLoads",
                schema: "fieldsales");
        }
    }
}
