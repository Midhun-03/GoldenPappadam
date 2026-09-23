using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoldenPappadam.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Total",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_InvoiceId",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.AddColumn<decimal>(
                name: "GstRate",
                schema: "inventory",
                table: "Products",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HsnCode",
                schema: "inventory",
                table: "Products",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxTreatment",
                schema: "inventory",
                table: "Products",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchAddress",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchGstin",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchPhone",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchStateCode",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CessAmount",
                schema: "sales",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CgstAmount",
                schema: "sales",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomerAddress",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerGstin",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerStateCode",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FinancialYear",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "IgstAmount",
                schema: "sales",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsInterState",
                schema: "sales",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlaceOfSupplyStateCode",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PricesIncludeTax",
                schema: "sales",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReverseCharge",
                schema: "sales",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundOff",
                schema: "sales",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                schema: "sales",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SeriesCode",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SgstAmount",
                schema: "sales",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SupplierAddress",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierGstin",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupplierStateCode",
                schema: "sales",
                table: "Invoices",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                schema: "sales",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CessAmount",
                schema: "sales",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CgstAmount",
                schema: "sales",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "sales",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GstRate",
                schema: "sales",
                table: "InvoiceLines",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "HsnCode",
                schema: "sales",
                table: "InvoiceLines",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IgstAmount",
                schema: "sales",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                schema: "sales",
                table: "InvoiceLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SgstAmount",
                schema: "sales",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TaxTreatment",
                schema: "sales",
                table: "InvoiceLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableValue",
                schema: "sales",
                table: "InvoiceLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gstin",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateCode",
                schema: "sales",
                table: "Customers",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gstin",
                schema: "sales",
                table: "CustomerBranches",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateCode",
                schema: "sales",
                table: "CustomerBranches",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceDocuments",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceDocuments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sales",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceEmailLogs",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceEmailLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceEmailLogs_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "sales",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceNumberSequences",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeriesCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FinancialYear = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceNumberSequences", x => x.Id);
                    table.CheckConstraint("CK_InvoiceNumberSequences_LastNumber", "[LastNumber] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceSettings",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Gstin = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    StateCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SeriesCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PricesIncludeTax = table.Column<bool>(type: "bit", nullable: false),
                    RoundToNearestRupee = table.Column<bool>(type: "bit", nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BankDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TermsAndConditions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "sales",
                table: "InvoiceSettings",
                columns: new[] { "Id", "Address", "BankDetails", "CreatedAt", "CreatedBy", "Email", "Gstin", "LegalName", "PaymentTerms", "Phone", "PricesIncludeTax", "RoundToNearestRupee", "SeriesCode", "StateCode", "TermsAndConditions", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new Guid("6a1c0de2-3f5b-4c1e-9e57-1f0b7a2d4c01"), "Kundara, Kollam, Kerala", null, new DateTime(2026, 9, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "Golden Pappadam", null, null, true, false, "GP", "32", null, null, null });

            // ---- Backfill the bills made before invoices carried their own snapshot. ----
            // Runs before the new constraints and unique indexes below, which the empty defaults
            // would otherwise break. Existing bills were never taxed, so each is a plain Invoice
            // whose taxable value is its total, and its printed details are the best record there
            // is: the customer and branch as they stand today.

            // INV-2026-00014 -> series INV, financial year 2026-27, number 14.
            migrationBuilder.Sql("""
                UPDATE sales.Invoices
                SET SeriesCode = 'INV',
                    FinancialYear = SUBSTRING(InvoiceNumber, 5, 4) + '-' +
                                    RIGHT(CAST(CAST(SUBSTRING(InvoiceNumber, 5, 4) AS int) + 1 AS varchar(4)), 2),
                    SequenceNumber = CAST(SUBSTRING(InvoiceNumber, 10, 10) AS int)
                WHERE InvoiceNumber LIKE 'INV-[0-9][0-9][0-9][0-9]-[0-9]%';

                -- Anything not in that shape (there should be none) still gets a unique position.
                WITH odd AS (
                    SELECT Id, InvoiceDate,
                           ROW_NUMBER() OVER (ORDER BY InvoiceDate, CreatedAt, Id) AS Position
                    FROM sales.Invoices
                    WHERE SeriesCode = '')
                UPDATE i
                SET SeriesCode = 'OLD',
                    FinancialYear = '0000-00',
                    SequenceNumber = odd.Position
                FROM sales.Invoices i
                JOIN odd ON odd.Id = i.Id;

                UPDATE i
                SET DocumentType = 'Invoice',
                    PricesIncludeTax = 1,
                    TaxableAmount = i.TotalAmount,
                    SupplierName = s.LegalName,
                    SupplierAddress = s.Address,
                    SupplierStateCode = s.StateCode,
                    CustomerName = c.Name,
                    CustomerAddress = c.Address,
                    CustomerPhone = c.Phone,
                    BranchName = b.Name,
                    BranchAddress = b.Address,
                    BranchPhone = b.Phone
                FROM sales.Invoices i
                JOIN sales.Customers c ON c.Id = i.CustomerId
                LEFT JOIN sales.CustomerBranches b ON b.Id = i.BranchId
                CROSS JOIN sales.InvoiceSettings s;

                WITH numbered AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY InvoiceId ORDER BY CreatedAt, Id) AS Position
                    FROM sales.InvoiceLines)
                UPDATE l
                SET LineNumber = numbered.Position,
                    TaxableValue = l.LineTotal
                FROM sales.InvoiceLines l
                JOIN numbered ON numbered.Id = l.Id;

                -- The counters start where the old numbering left off, so they account for every
                -- number already issued.
                INSERT INTO sales.InvoiceNumberSequences (Id, SeriesCode, FinancialYear, LastNumber, CreatedAt)
                SELECT NEWID(), SeriesCode, FinancialYear, MAX(SequenceNumber), SYSUTCDATETIME()
                FROM sales.Invoices
                GROUP BY SeriesCode, FinancialYear;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_GstRate",
                schema: "inventory",
                table: "Products",
                sql: "([TaxTreatment] = 'Taxable' AND [GstRate] > 0 AND [GstRate] <= 100) OR (([TaxTreatment] IS NULL OR [TaxTreatment] <> 'Taxable') AND [GstRate] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SeriesCode_FinancialYear_SequenceNumber",
                schema: "sales",
                table: "Invoices",
                columns: new[] { "SeriesCode", "FinancialYear", "SequenceNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Sequence",
                schema: "sales",
                table: "Invoices",
                sql: "[SequenceNumber] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_TaxSplit",
                schema: "sales",
                table: "Invoices",
                sql: "([IsInterState] = 1 AND [CgstAmount] = 0 AND [SgstAmount] = 0) OR ([IsInterState] = 0 AND [IgstAmount] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Total",
                schema: "sales",
                table: "Invoices",
                sql: "[TotalAmount] = [TaxableAmount] + [CgstAmount] + [SgstAmount] + [IgstAmount] + [CessAmount] + [RoundOff]");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId_LineNumber",
                schema: "sales",
                table: "InvoiceLines",
                columns: new[] { "InvoiceId", "LineNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_Discount",
                schema: "sales",
                table: "InvoiceLines",
                sql: "[DiscountAmount] >= 0 AND [DiscountAmount] <= [LineTotal]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceLines_GstRate",
                schema: "sales",
                table: "InvoiceLines",
                sql: "[GstRate] >= 0 AND [GstRate] <= 100");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_InvoiceId",
                schema: "sales",
                table: "InvoiceDocuments",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEmailLogs_InvoiceId_CreatedAt",
                schema: "sales",
                table: "InvoiceEmailLogs",
                columns: new[] { "InvoiceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceNumberSequences_SeriesCode_FinancialYear",
                schema: "sales",
                table: "InvoiceNumberSequences",
                columns: new[] { "SeriesCode", "FinancialYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceDocuments",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "InvoiceEmailLogs",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "InvoiceNumberSequences",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "InvoiceSettings",
                schema: "sales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_GstRate",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SeriesCode_FinancialYear_SequenceNumber",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Sequence",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_TaxSplit",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Total",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLines_InvoiceId_LineNumber",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceLines_Discount",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceLines_GstRate",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "GstRate",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HsnCode",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TaxTreatment",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BranchAddress",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchGstin",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchName",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchPhone",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BranchStateCode",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CessAmount",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CgstAmount",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerAddress",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerGstin",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerStateCode",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FinancialYear",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IgstAmount",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsInterState",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PlaceOfSupplyStateCode",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PricesIncludeTax",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReverseCharge",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RoundOff",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SeriesCode",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SgstAmount",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SupplierAddress",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SupplierGstin",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SupplierStateCode",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                schema: "sales",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CessAmount",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "CgstAmount",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "GstRate",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "HsnCode",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "IgstAmount",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "SgstAmount",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "TaxTreatment",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "TaxableValue",
                schema: "sales",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Gstin",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "StateCode",
                schema: "sales",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Gstin",
                schema: "sales",
                table: "CustomerBranches");

            migrationBuilder.DropColumn(
                name: "StateCode",
                schema: "sales",
                table: "CustomerBranches");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Total",
                schema: "sales",
                table: "Invoices",
                sql: "[TotalAmount] = [SubTotal] - [DiscountAmount]");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                schema: "sales",
                table: "InvoiceLines",
                column: "InvoiceId");
        }
    }
}
