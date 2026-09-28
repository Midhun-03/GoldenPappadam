using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAndExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "staff");

            migrationBuilder.EnsureSchema(
                name: "accounting");

            migrationBuilder.CreateTable(
                name: "AttendanceStatuses",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DayFraction = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceStatuses", x => x.Id);
                    table.CheckConstraint("CK_AttendanceStatuses_DayFraction", "[DayFraction] >= 0 AND [DayFraction] <= 1");
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AttendanceStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AttendanceStatuses_AttendanceStatusId",
                        column: x => x.AttendanceStatusId,
                        principalSchema: "staff",
                        principalTable: "AttendanceStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "staff",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWageRates",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyWage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWageRates", x => x.Id);
                    table.CheckConstraint("CK_EmployeeWageRates_DailyWage", "[DailyWage] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeWageRates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "staff",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WagePayments",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DaysWorked = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    WorkAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CarriedForward = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WagePayments", x => x.Id);
                    table.CheckConstraint("CK_WagePayments_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_WagePayments_CarriedForward", "[CarriedForward] <= 0");
                    table.ForeignKey(
                        name: "FK_WagePayments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "staff",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WagePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.CheckConstraint("CK_Expenses_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "accounting",
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_WagePayments_WagePaymentId",
                        column: x => x.WagePaymentId,
                        principalSchema: "staff",
                        principalTable: "WagePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WagePaymentLines",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WagePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AttendanceStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DayFraction = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    PreviousStatusName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PreviousDayFraction = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    DailyWage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceWagePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WagePaymentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WagePaymentLines_WagePayments_SourceWagePaymentId",
                        column: x => x.SourceWagePaymentId,
                        principalSchema: "staff",
                        principalTable: "WagePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WagePaymentLines_WagePayments_WagePaymentId",
                        column: x => x.WagePaymentId,
                        principalSchema: "staff",
                        principalTable: "WagePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseChanges",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseChanges_ExpenseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "accounting",
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExpenseChanges_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalSchema: "accounting",
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "staff",
                table: "AttendanceStatuses",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DayFraction", "Name", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000001"), "Present", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1m, "Present", 1, null, null },
                    { new Guid("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000002"), "HalfDay", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0.5m, "Half day", 2, null, null },
                    { new Guid("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000003"), "Absent", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0m, "Absent", 3, null, null },
                    { new Guid("5c1d2e3f-2001-4a6b-9c1d-0a0a0a000004"), "Leave", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0m, "Leave", 4, null, null }
                });

            migrationBuilder.InsertData(
                schema: "accounting",
                table: "ExpenseCategories",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "IsActive", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Employee wages", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Raw material", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Packaging", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Fuel", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Electricity", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Gas", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Transportation", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Vehicle maintenance", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Rent", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b00000a"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Repairs", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b00000b"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Marketing", null, null },
                    { new Guid("6d2e3f40-3001-4b7c-8d2e-0b0b0b00000c"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Other", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AttendanceStatusId",
                schema: "staff",
                table: "AttendanceRecords",
                column: "AttendanceStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId_WorkDate",
                schema: "staff",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_WorkDate",
                schema: "staff",
                table: "AttendanceRecords",
                column: "WorkDate");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceStatuses_Code",
                schema: "staff",
                table: "AttendanceStatuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Name",
                schema: "staff",
                table: "Employees",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWageRates_EmployeeId_EffectiveFrom",
                schema: "staff",
                table: "EmployeeWageRates",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Name",
                schema: "accounting",
                table: "ExpenseCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseChanges_CategoryId",
                schema: "accounting",
                table: "ExpenseChanges",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseChanges_ExpenseId_CreatedAt",
                schema: "accounting",
                table: "ExpenseChanges",
                columns: new[] { "ExpenseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CategoryId_ExpenseDate",
                schema: "accounting",
                table: "Expenses",
                columns: new[] { "CategoryId", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseDate",
                schema: "accounting",
                table: "Expenses",
                column: "ExpenseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_WagePaymentId",
                schema: "accounting",
                table: "Expenses",
                column: "WagePaymentId",
                unique: true,
                filter: "[WagePaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WagePaymentLines_SourceWagePaymentId",
                schema: "staff",
                table: "WagePaymentLines",
                column: "SourceWagePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_WagePaymentLines_WagePaymentId",
                schema: "staff",
                table: "WagePaymentLines",
                column: "WagePaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_WagePayments_EmployeeId_PeriodStart",
                schema: "staff",
                table: "WagePayments",
                columns: new[] { "EmployeeId", "PeriodStart" },
                unique: true,
                filter: "[Status] = 'Paid'");

            migrationBuilder.CreateIndex(
                name: "IX_WagePayments_PaymentDate",
                schema: "staff",
                table: "WagePayments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_WagePayments_PeriodStart",
                schema: "staff",
                table: "WagePayments",
                column: "PeriodStart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "EmployeeWageRates",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "ExpenseChanges",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "WagePaymentLines",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "AttendanceStatuses",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "Expenses",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "ExpenseCategories",
                schema: "accounting");

            migrationBuilder.DropTable(
                name: "WagePayments",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "staff");
        }
    }
}
