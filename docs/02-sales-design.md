# Sales module — design

_Written 2026-09-14. Covers customers, invoices, payments and the customer ledger._

## 1. What this has to satisfy

From `CLAUDE.md` §1 and §4:

- Shops buy on **credit**. Goods go out today, money comes later, often at the next delivery.
- **Partial payments** are normal: ₹10,000 invoice, ₹5,000 paid, ₹5,000 outstanding.
- One payment may settle **several bills**, and one bill may be settled by **several payments**.
- Selling prices default per product but must be **overridable per bill line**, with customer-specific pricing addable later.
- A **bill-level discount** is enough for now; item-level must remain addable.
- Tax fields must be addable later **without restructuring** — no tax logic until the accountant confirms.
- Selling stock must come off the same movement ledger the inventory module already owns.

## 2. Tables

All in the SQL Server schema `sales`, following the conventions in `CLAUDE.md` §6.

### `sales.Customers` — `AuditableEntity`

| Column | Type | Notes |
|---|---|---|
| Name | nvarchar(150) | shop name |
| ContactPerson | nvarchar(100), null | |
| Phone | nvarchar(20), null | |
| Address | nvarchar(300), null | |
| OpeningBalance | decimal(18,2) | what the shop already owed when the system started; 0 for new customers |
| Notes | nvarchar(300), null | |
| IsActive | bit | deactivated, never deleted |

### `sales.Invoices` — `AuditableEntity`

Editable only in one respect: an invoice can be **cancelled**. Nothing else about it ever changes, and the
service enforces that.

| Column | Type | Notes |
|---|---|---|
| InvoiceNumber | nvarchar(20), unique | `INV-2026-00001`, see §4 |
| CustomerId | FK to Customers | |
| InvoiceDate | date | the business date in IST, not a timestamp |
| Status | string enum: `Issued`, `Cancelled` | |
| SubTotal | decimal(18,2) | sum of line totals |
| DiscountAmount | decimal(18,2) | bill-level, 0 when unused |
| TotalAmount | decimal(18,2) | SubTotal − DiscountAmount |
| Notes | nvarchar(300), null | |
| CancelledAt | datetime2 (UTC), null | |
| CancellationReason | nvarchar(300), null | |

Tax fields would be added here and on the lines later; nothing above has to change for that.

### `sales.InvoiceLines` — `Entity` (immutable)

| Column | Type | Notes |
|---|---|---|
| InvoiceId | FK to Invoices | cascade delete is off; invoices are never deleted |
| ProductId | FK to Products | |
| Description | nvarchar(150) | the product name **as printed on that bill** |
| UnitCode | nvarchar(10) | the unit at the time of sale |
| Quantity | decimal(18,3) | |
| UnitPrice | decimal(18,2) | the price actually charged, defaulted from the product |
| LineTotal | decimal(18,2) | Quantity × UnitPrice, rounded to paise |

**Why the name and unit are copied onto the line:** a bill must keep showing what was printed even after a
product is renamed or its unit changes. The `ProductId` still links to the product for reporting.

### `sales.Payments` — `Entity` (immutable)

| Column | Type | Notes |
|---|---|---|
| CustomerId | FK to Customers | |
| PaymentDate | date | business date, IST |
| Amount | decimal(18,2), > 0 | |
| Method | string enum: `Cash`, `UPI`, `BankTransfer`, `Cheque`, `Other` | |
| Reference | nvarchar(100), null | UPI reference, cheque number |
| Notes | nvarchar(300), null | |

### `sales.PaymentAllocations` — `Entity` (immutable)

Which bills a payment settled. This is what makes bill-to-bill work.

| Column | Type | Notes |
|---|---|---|
| PaymentId | FK to Payments | |
| InvoiceId | FK to Invoices | |
| Amount | decimal(18,2), > 0 | |

A payment does not have to be fully allocated. The unallocated part is money on account, which still reduces
what the customer owes overall and can be applied to a later bill.

## 3. How the money adds up

- **Invoice outstanding** = `TotalAmount` − allocated to it. Cancelled invoices are 0.
- **Customer balance** = `OpeningBalance` + issued invoice totals − payments received.
  Note this uses the payment **amount**, not its allocations, so money on account counts immediately and the
  balance is never overstated.
- **Customer ledger** = opening balance, then invoices and payments in date order with a running balance.

## 4. Invoice numbers

`INV-<financial year>-<5 digits>`, e.g. `INV-2026-00001`. The Indian financial year starts on 1 April, so a bill
dated 2026-04-01 or later belongs to FY 2026, and one dated 2026-03-31 belongs to FY 2025.

The next number comes from the highest number already used in that financial year, inside the same transaction
that writes the invoice. A unique index on `InvoiceNumber` is the real guarantee; if two bills are saved at the
same instant, one fails on that index and the service retries with the next number.

## 5. Rules

**Creating an invoice**
- At least one line; every quantity greater than zero.
- The customer must be active; products must be active.
- `UnitPrice` defaults to the product's selling price and may be overridden per line; a product without a price
  must be given one on the line.
- Discount cannot exceed the subtotal.
- Each line writes a `Sale` stock movement of −quantity referencing the invoice.
- **Stock is never blocked.** Selling more than is on hand saves and returns a warning, the same rule packing uses.

**Cancelling an invoice**
- Allowed only while nothing is allocated to it; remove or reverse the payments first.
- Sets `Status = Cancelled` with a reason, and writes a `SaleReversal` movement per line, returning the stock.
- The invoice and its lines stay exactly as they were. Nothing is deleted.

**Recording a payment**
- Amount greater than zero, customer active.
- **Auto-allocation (default):** oldest unpaid invoice first, until the money runs out. This is how bill-to-bill
  settlement actually happens on the route.
- **Manual allocation:** the caller supplies invoice and amount pairs, for when a shop says which bill it is paying.
- Allocations cannot exceed the payment, cannot exceed an invoice's outstanding, cannot touch a cancelled invoice
  or another customer's invoice.

## 6. Deliberately not included

Sales orders separate from invoices, delivery notes, returns and credit notes, GST or any tax calculation,
customer-specific price lists, item-level discounts, credit limits, invoice PDFs, and payments to suppliers.
Each can be added later without changing the tables above.
