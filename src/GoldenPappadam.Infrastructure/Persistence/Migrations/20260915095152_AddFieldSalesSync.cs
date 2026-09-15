using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldSalesSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                schema: "fieldsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_StockLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "inventory",
                        principalTable: "StockLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShopVisits",
                schema: "fieldsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopVisits_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "sales",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopVisits_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "fieldsales",
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopVisits_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sales",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopVisits_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "sales",
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncSubmissions",
                schema: "fieldsales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceMismatch = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncSubmissions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "fieldsales",
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LocationId",
                schema: "fieldsales",
                table: "Devices",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                schema: "fieldsales",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopVisits_CustomerId_VisitedAt",
                schema: "fieldsales",
                table: "ShopVisits",
                columns: new[] { "CustomerId", "VisitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopVisits_DeviceId",
                schema: "fieldsales",
                table: "ShopVisits",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopVisits_InvoiceId",
                schema: "fieldsales",
                table: "ShopVisits",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopVisits_PaymentId",
                schema: "fieldsales",
                table: "ShopVisits",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopVisits_VisitedAt",
                schema: "fieldsales",
                table: "ShopVisits",
                column: "VisitedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncSubmissions_ClientRequestId",
                schema: "fieldsales",
                table: "SyncSubmissions",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncSubmissions_CreatedRecordId",
                schema: "fieldsales",
                table: "SyncSubmissions",
                column: "CreatedRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncSubmissions_DeviceId_ReceivedAt",
                schema: "fieldsales",
                table: "SyncSubmissions",
                columns: new[] { "DeviceId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopVisits",
                schema: "fieldsales");

            migrationBuilder.DropTable(
                name: "SyncSubmissions",
                schema: "fieldsales");

            migrationBuilder.DropTable(
                name: "Devices",
                schema: "fieldsales");
        }
    }
}
