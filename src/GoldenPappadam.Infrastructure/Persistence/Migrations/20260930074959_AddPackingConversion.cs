using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPackingConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products");

            migrationBuilder.AddColumn<decimal>(
                name: "PiecesPerKg",
                schema: "inventory",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PiecesPerPack",
                schema: "inventory",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                schema: "inventory",
                table: "PackingEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PiecesPerKg",
                schema: "inventory",
                table: "PackingEntries",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PiecesPerPack",
                schema: "inventory",
                table: "PackingEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourceOnHandBefore",
                schema: "inventory",
                table: "PackingEntries",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SourcePerPack",
                schema: "inventory",
                table: "PackingEntries",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            // The one figure the owner confirmed (2026-09-30): the standard pappadam is 200 pieces per kg,
            // and it is the only loose product counted in kg. The packets are deliberately NOT rewritten:
            // their pieces cannot be derived from the kg figure they carry (0.250 kg x 200 is 50, not 20),
            // so the office sets "20 pieces" / "6 pieces" on the product screen, where the edit is audited.
            migrationBuilder.Sql(
                """
                UPDATE inventory.Products
                SET PiecesPerKg = 200
                WHERE Kind = 'Loose' AND UnitOfMeasureId = '2a9f3f9e-0f01-4a1e-9f7a-0b1a0a000001' AND PiecesPerKg IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_PiecesPerKg",
                schema: "inventory",
                table: "Products",
                sql: "[PiecesPerKg] IS NULL OR ([Kind] = 'Loose' AND [PiecesPerKg] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products",
                sql: "([Kind] = 'Packed' AND [SourceProductId] IS NOT NULL AND (([SourceQuantityPerPack] > 0 AND [PiecesPerPack] IS NULL) OR ([SourceQuantityPerPack] IS NULL AND [PiecesPerPack] > 0))) OR ([Kind] = 'Loose' AND [SourceProductId] IS NULL AND [SourceQuantityPerPack] IS NULL AND [PiecesPerPack] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_PackingEntries_ClientRequestId",
                schema: "inventory",
                table: "PackingEntries",
                column: "ClientRequestId",
                unique: true,
                filter: "[ClientRequestId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_PiecesPerKg",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_PackingEntries_ClientRequestId",
                schema: "inventory",
                table: "PackingEntries");

            migrationBuilder.DropColumn(
                name: "PiecesPerKg",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PiecesPerPack",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                schema: "inventory",
                table: "PackingEntries");

            migrationBuilder.DropColumn(
                name: "PiecesPerKg",
                schema: "inventory",
                table: "PackingEntries");

            migrationBuilder.DropColumn(
                name: "PiecesPerPack",
                schema: "inventory",
                table: "PackingEntries");

            migrationBuilder.DropColumn(
                name: "SourceOnHandBefore",
                schema: "inventory",
                table: "PackingEntries");

            migrationBuilder.DropColumn(
                name: "SourcePerPack",
                schema: "inventory",
                table: "PackingEntries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Source",
                schema: "inventory",
                table: "Products",
                sql: "([Kind] = 'Packed' AND [SourceProductId] IS NOT NULL AND [SourceQuantityPerPack] > 0) OR ([Kind] = 'Loose' AND [SourceProductId] IS NULL AND [SourceQuantityPerPack] IS NULL)");
        }
    }
}
