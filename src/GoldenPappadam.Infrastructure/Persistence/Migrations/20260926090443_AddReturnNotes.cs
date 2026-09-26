using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReturnNotes",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SeriesCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Settlement = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SettledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnNotes", x => x.Id);
                    table.CheckConstraint("CK_ReturnNotes_Credit", "([Settlement] = 'Credit' AND [CreditAmount] > 0 AND [CreditPaymentId] IS NOT NULL) OR ([Settlement] <> 'Credit' AND [CreditAmount] = 0 AND [CreditPaymentId] IS NULL)");
                    table.CheckConstraint("CK_ReturnNotes_Sequence", "[SequenceNumber] > 0");
                    table.CheckConstraint("CK_ReturnNotes_Value", "[Value] >= 0");
                    table.ForeignKey(
                        name: "FK_ReturnNotes_CustomerBranches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "sales",
                        principalTable: "CustomerBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnNotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnNotes_Payments_CreditPaymentId",
                        column: x => x.CreditPaymentId,
                        principalSchema: "sales",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReturnNoteLines",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnitCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnNoteLines", x => x.Id);
                    table.CheckConstraint("CK_ReturnNoteLines_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_ReturnNoteLines_UnitRate", "[UnitRate] >= 0");
                    table.ForeignKey(
                        name: "FK_ReturnNoteLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnNoteLines_ReturnNotes_ReturnNoteId",
                        column: x => x.ReturnNoteId,
                        principalSchema: "sales",
                        principalTable: "ReturnNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNoteLines_ProductId",
                schema: "sales",
                table: "ReturnNoteLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNoteLines_ReturnNoteId_LineNumber",
                schema: "sales",
                table: "ReturnNoteLines",
                columns: new[] { "ReturnNoteId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNotes_BranchId",
                schema: "sales",
                table: "ReturnNotes",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNotes_CreditPaymentId",
                schema: "sales",
                table: "ReturnNotes",
                column: "CreditPaymentId",
                unique: true,
                filter: "[CreditPaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNotes_CustomerId_ReturnDate",
                schema: "sales",
                table: "ReturnNotes",
                columns: new[] { "CustomerId", "ReturnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNotes_ReturnDate",
                schema: "sales",
                table: "ReturnNotes",
                column: "ReturnDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNotes_ReturnNumber",
                schema: "sales",
                table: "ReturnNotes",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnNotes_SeriesCode_FinancialYear_SequenceNumber",
                schema: "sales",
                table: "ReturnNotes",
                columns: new[] { "SeriesCode", "FinancialYear", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReturnNoteLines",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "ReturnNotes",
                schema: "sales");
        }
    }
}
