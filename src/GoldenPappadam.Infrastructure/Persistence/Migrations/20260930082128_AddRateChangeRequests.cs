using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRateChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RateRequestId",
                schema: "sales",
                table: "CustomerPriceChanges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerRateRequests",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceWhenRequested = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRateRequests", x => x.Id);
                    table.CheckConstraint("CK_CustomerRateRequests_Decision", "([Status] = 'Pending' AND [DecidedAt] IS NULL) OR ([Status] <> 'Pending' AND [DecidedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_CustomerRateRequests_RequestedPrice", "[RequestedPrice] > 0");
                    table.ForeignKey(
                        name: "FK_CustomerRateRequests_CustomerRateRequests_ReplacedById",
                        column: x => x.ReplacedById,
                        principalSchema: "sales",
                        principalTable: "CustomerRateRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerRateRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerRateRequests_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPriceChanges_RateRequestId",
                schema: "sales",
                table: "CustomerPriceChanges",
                column: "RateRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRateRequests_CreatedBy",
                schema: "sales",
                table: "CustomerRateRequests",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRateRequests_CustomerId_ProductId",
                schema: "sales",
                table: "CustomerRateRequests",
                columns: new[] { "CustomerId", "ProductId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRateRequests_ProductId",
                schema: "sales",
                table: "CustomerRateRequests",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRateRequests_ReplacedById",
                schema: "sales",
                table: "CustomerRateRequests",
                column: "ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRateRequests_Status_CreatedAt",
                schema: "sales",
                table: "CustomerRateRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerPriceChanges_CustomerRateRequests_RateRequestId",
                schema: "sales",
                table: "CustomerPriceChanges",
                column: "RateRequestId",
                principalSchema: "sales",
                principalTable: "CustomerRateRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerPriceChanges_CustomerRateRequests_RateRequestId",
                schema: "sales",
                table: "CustomerPriceChanges");

            migrationBuilder.DropTable(
                name: "CustomerRateRequests",
                schema: "sales");

            migrationBuilder.DropIndex(
                name: "IX_CustomerPriceChanges_RateRequestId",
                schema: "sales",
                table: "CustomerPriceChanges");

            migrationBuilder.DropColumn(
                name: "RateRequestId",
                schema: "sales",
                table: "CustomerPriceChanges");
        }
    }
}
