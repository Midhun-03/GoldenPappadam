using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives every stock movement a place. Until the van started carrying its own stock there was
    /// only one, so the whole of history belongs to the main warehouse.
    ///
    /// Hand-written rather than left as generated: the generated version added LocationId with a
    /// default of Guid.Empty, which would have stamped every existing movement with an id that is
    /// not a location and left a default constraint behind. The column is added nullable, the
    /// history is backfilled, and only then is it made required - so at no point does a row exist
    /// that claims to be somewhere that does not exist.
    /// </summary>
    public partial class AddStockLocations : Migration
    {
        private const string MainWarehouseId = "3b0a4a0f-1002-4b2f-8a6b-1c2b1b000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockLocations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockLocations", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "StockLocations",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "IsActive", "Kind", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid(MainWarehouseId), "MAIN", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Warehouse", "Main warehouse", null, null },
                    { new Guid("3b0a4a0f-1002-4b2f-8a6b-1c2b1b000002"), "VAN-1", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Van", "Sales van 1", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockLocations_Code",
                schema: "inventory",
                table: "StockLocations",
                column: "Code",
                unique: true);

            // Step 1: nullable, so existing rows are untouched and nothing is silently wrong.
            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                schema: "inventory",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            // Step 2: everything that has ever happened, happened at the warehouse.
            migrationBuilder.Sql(
                $"UPDATE [inventory].[StockMovements] SET [LocationId] = '{MainWarehouseId}' WHERE [LocationId] IS NULL;");

            // Step 3: required from here on. No default constraint: every writer must say where.
            migrationBuilder.AlterColumn<Guid>(
                name: "LocationId",
                schema: "inventory",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductId_OccurredAt",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LocationId_OccurredAt",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "LocationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_LocationId_OccurredAt",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "ProductId", "LocationId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_StockLocations_LocationId",
                schema: "inventory",
                table: "StockMovements",
                column: "LocationId",
                principalSchema: "inventory",
                principalTable: "StockLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_StockLocations_LocationId",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_LocationId_OccurredAt",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProductId_LocationId_OccurredAt",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LocationId",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "StockLocations",
                schema: "inventory");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId_OccurredAt",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "ProductId", "OccurredAt" });
        }
    }
}
