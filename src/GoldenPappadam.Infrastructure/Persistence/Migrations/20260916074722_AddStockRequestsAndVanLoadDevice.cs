using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockRequestsAndVanLoadDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                schema: "fieldsales",
                table: "VanLoads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockRequests",
                schema: "fieldsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequiredDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockRequests_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "fieldsales",
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockRequestLines",
                schema: "fieldsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockRequestLines", x => x.Id);
                    table.CheckConstraint("CK_StockRequestLines_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_StockRequestLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockRequestLines_StockRequests_StockRequestId",
                        column: x => x.StockRequestId,
                        principalSchema: "fieldsales",
                        principalTable: "StockRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VanLoads_DeviceId",
                schema: "fieldsales",
                table: "VanLoads",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_StockRequestLines_ProductId",
                schema: "fieldsales",
                table: "StockRequestLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StockRequestLines_StockRequestId",
                schema: "fieldsales",
                table: "StockRequestLines",
                column: "StockRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StockRequests_DeviceId",
                schema: "fieldsales",
                table: "StockRequests",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_StockRequests_RequiredDate_Status",
                schema: "fieldsales",
                table: "StockRequests",
                columns: new[] { "RequiredDate", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_VanLoads_Devices_DeviceId",
                schema: "fieldsales",
                table: "VanLoads",
                column: "DeviceId",
                principalSchema: "fieldsales",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VanLoads_Devices_DeviceId",
                schema: "fieldsales",
                table: "VanLoads");

            migrationBuilder.DropTable(
                name: "StockRequestLines",
                schema: "fieldsales");

            migrationBuilder.DropTable(
                name: "StockRequests",
                schema: "fieldsales");

            migrationBuilder.DropIndex(
                name: "IX_VanLoads_DeviceId",
                schema: "fieldsales",
                table: "VanLoads");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                schema: "fieldsales",
                table: "VanLoads");
        }
    }
}
