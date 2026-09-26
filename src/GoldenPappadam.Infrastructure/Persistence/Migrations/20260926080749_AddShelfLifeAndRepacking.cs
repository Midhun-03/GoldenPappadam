using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShelfLifeAndRepacking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeDays",
                schema: "inventory",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RepackEntries",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ToProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LeftoverProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeftoverQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepackEntries", x => x.Id);
                    table.CheckConstraint("CK_RepackEntries_FromQuantity", "[FromQuantity] > 0");
                    table.CheckConstraint("CK_RepackEntries_Leftover", "([LeftoverProductId] IS NULL AND [LeftoverQuantity] = 0) OR ([LeftoverProductId] IS NOT NULL AND [LeftoverQuantity] > 0)");
                    table.CheckConstraint("CK_RepackEntries_ToQuantity", "[ToQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_RepackEntries_Products_FromProductId",
                        column: x => x.FromProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepackEntries_Products_LeftoverProductId",
                        column: x => x.LeftoverProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RepackEntries_Products_ToProductId",
                        column: x => x.ToProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ShelfLifeDays",
                schema: "inventory",
                table: "Products",
                sql: "[ShelfLifeDays] IS NULL OR [ShelfLifeDays] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_RepackEntries_FromProductId",
                schema: "inventory",
                table: "RepackEntries",
                column: "FromProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RepackEntries_LeftoverProductId",
                schema: "inventory",
                table: "RepackEntries",
                column: "LeftoverProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RepackEntries_OccurredAt",
                schema: "inventory",
                table: "RepackEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_RepackEntries_ToProductId",
                schema: "inventory",
                table: "RepackEntries",
                column: "ToProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepackEntries",
                schema: "inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ShelfLifeDays",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShelfLifeDays",
                schema: "inventory",
                table: "Products");
        }
    }
}
