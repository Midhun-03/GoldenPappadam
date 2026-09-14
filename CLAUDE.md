# Golden Pappadam — Business Management System

Core context for all work on "Golden Pappadam", "the Pappadam software", or "the business management system".
These requirements stand unless the owner explicitly changes them. Keep the **Project status** section current.

## 1. The business

Golden Pappadam is a family pappadam manufacturing and distribution business in Kundara, Kollam, Kerala, India.

Operational flow:

1. **Raw materials** (urad dal and other ingredients) are purchased and managed.
2. **Production** consumes raw materials; a given quantity of raw material yields a certain quantity of pappadams.
3. Production output becomes **loose/bulk stock**.
4. Loose stock is either sold loose or **packed** into packaged products — e.g. 6-piece pack, 10-piece pack, 250 g pack. Many other sizes, weights and variations exist and more will be added. Packing is driven by what the sales team says is needed.
5. Products are **delivered** to shops and customers.
6. **Sales, bills, credit and customer payments** are managed.

### Sales process
Salespeople visit shops on known routes. They know which shops to visit, what each shop usually takes and roughly how much. The salesperson tells the company the expected requirements *before* delivery; the company packs accordingly and the goods are delivered.

### Payments and credit (critical)
Most business runs on **credit and bill-to-bill payments**:

- ₹10,000 of goods delivered today may be paid at the next delivery.
- **Partial payments** are common: invoice ₹10,000, paid ₹5,000, balance ₹5,000.
- A balance may be settled later, possibly together with other outstanding bills.

The system must therefore support invoices, credit sales, outstanding balances, partial payments, multiple payments against one invoice (and one payment across several invoices), payment history, a customer ledger and receivables tracking. Never assume cash-only sales or fixed product sizes.

## 2. Long-term vision (NOT phase 1)

A custom ERP-style web app that gradually digitizes the whole business. Eventual modules:

- **Inventory:** products, loose stock, packaged products, stock movements, adjustments, low-stock alerts.
- **Production:** raw material purchases/inventory/consumption, production batches and quantities, production costs, finished goods, wastage.
- **Sales:** customers, orders, sales, invoices, sales history, credit sales.
- **Payments:** customer payments, partial payments, outstanding balances, customer ledger.
- **Accounting:** income, expenses, receivables, payables, basic financial reports.
- **Sales team:** salespersons, routes, route/visit planning, shop visits, order collection, deliveries.
- **Dashboard & reports:** daily/monthly sales, inventory statistics, low stock, outstanding balances, sales reports.

Use this list only to keep the foundation extensible. Do **not** build these modules or add speculative tables/columns for them during phase 1.

## 3. Phase 1 — current scope (MVP)

A small, usable, **admin-side only** MVP. Scope set 2026-09-11; target completion ~3 months (around mid-December 2026).

**A. Admin dashboard** (expandable later): today's sales, monthly sales, recent sales, total customers, inventory overview, low-stock products, outstanding customer payments.

**B. Inventory management — built first:**
- Products, product categories, product units, current stock.
- Stock additions, reductions and adjustments; low-stock alerts.
- A proper **stock-movement history** (see §4).

**C. Sales management (basic):**
- Customers: shop/customer name, contact info, address, customer history.
- Sales: create sale, select customer, multiple product lines, quantities, automatic totals, generate invoice/bill, sales history.
- Payments: full payment, credit sale, partial payment, outstanding balance.

**D. Admin login:** simple login for a few admin accounts using ASP.NET Core Identity. No roles or permissions yet — every logged-in user is an admin.

**Build order within phase 1** (owner-set, 2026-09-14): 1. Inventory → 2. Packing / stock conversion → 3. Sales → 4. Customers → 5. Credit and partial payments → 6. Dashboard → 7. Basic reports.

**Out of phase 1** unless explicitly added: production/raw-material management, salesperson/route/visit/delivery management, full accounting, a salesperson-facing app.
Keep payments simple and practical — not a full enterprise accounting system. Push back on scope creep.

## 4. Inventory design requirements

**Flexible product model**
- Loose/bulk products and packaged products.
- Different packet sizes; weight-based (e.g. 250 g) and count-based (e.g. 6 pcs, 10 pcs) products.
- New product types/variations must be addable without schema redesign.
- Packaging converts loose stock into packaged stock (loose goes down, packaged goes up) — the design must accommodate this even if the workflow starts simple.

**Stock-movement ledger**
- Never just overwrite a stock number. Every important change is recorded as a movement with a reason.
- Reasons include Production, Packaging, Sale, Damage, Adjustment — keep the set extensible for future operations.
- Current stock must be derivable from / reconcilable with the movement history.
- Sales create Sale movements; future production will create Production movements.

### Confirmed requirements (2026-09-14)

- **Units:** products may use different units (kg, pieces, packets). Never assume one unit for all products. Each variety/size/type of pappadam has its own stock.
- **Packing is in phase 1.** Loose stock down, packed stock up, recorded as a packing transaction with history. Actual quantities may differ from the theoretical ones because of packing loss, so the recorded quantity is what was actually used.
- **Pricing:** every product has a default selling price, overridable on a sale line. Customer-specific pricing must be addable later without redesign. Never hard-code one unchangeable price.
- **Returns:** the design must stay return-ready (extensible movement types + the reference pattern). Do not build a returns workflow in phase 1.
- **Discounts:** a simple bill-level discount field is enough for now; item-level discounts must remain addable later. No promotion/discount engine.
- **Tax/GST:** the invoice structure must allow tax fields (GSTIN, HSN/SAC, tax %, tax amount, CGST/SGST/IGST) to be added later without restructuring sales. Do **not** assume sales are GST-exempt and do not implement tax logic until the accountant confirms it.

**Rule for anything unconfirmed:** mark it TBD / business decision required (§10) instead of assuming.

The full inventory design is in `docs/01-inventory-design.md`.

## 5. Technology stack

- **Backend:** C#, ASP.NET Core Web API, Entity Framework Core.
- **Database:** **Microsoft SQL Server** (decided 2026-09-11, "for now"). Keep data access through EF Core and avoid SQL Server-only features unless they clearly earn their place, so a later switch stays possible.
- **Frontend:** React + TypeScript. Candidates: Vite, Tailwind CSS, shadcn/ui (or another modern component library).

Stay within this stack.

## 6. Database design principles

The schema may grow to dozens of tables across many modules, so consistent standards matter.

- Consistent naming conventions across the whole schema.
- Proper primary keys and foreign keys; practical normalization; no duplicated data.
- New modules must be addable without redesigning existing tables.
- A **common identifier pattern** across all tables, possibly with a globally unique identifier (GUID/UUID) for future sync and integrations.
- Reusable common/audit fields **where appropriate**: Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsActive/status.
- Do not assume every table links to Products or Customers — consistency means shared *standards*, not forced relationships.
- Do not add a column or relationship just because it is "common" (e.g. immutable ledger rows such as stock movements may not need UpdatedAt/UpdatedBy).

### Agreed conventions (2026-09-11)

**Primary keys:** every table has `Guid Id`. Generated by EF Core (sequential GUIDs on SQL Server — do not use `Guid.CreateVersion7()` for keys). No separate "PublicId" column. Reason: one globally unique identifier supports future offline sync (e.g. a salesperson app) without an ID-mapping layer, and a PK type is very expensive to change later.
Human-facing identifiers are separate business fields where needed (`InvoiceNumber` like `INV-2026-00123`, `ProductCode`, etc.).

**Base classes:**

```csharp
// Every table
public abstract class Entity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }   // UTC
    public Guid? CreatedBy { get; set; }      // Identity user id
}

// Tables whose rows are edited after creation
public abstract class AuditableEntity : Entity
{
    public DateTime? UpdatedAt { get; set; }  // UTC
    public Guid? UpdatedBy { get; set; }
}
```

- Master data (Product, Customer, Category, …) → `AuditableEntity`.
- Immutable records (StockMovement, Payment, …) → `Entity`.
- Audit fields are set in one place (DbContext `SaveChanges`/`SaveChangesAsync` override), never manually in endpoints.

**Active / deletion rules:**

- `IsActive` only on master data. Master data is deactivated, never deleted, because history references it.
- Transactions (invoices, payments, stock movements) are never edited or deleted. Corrections are new records: cancelled invoice → status Cancelled + reversing stock movement; wrong stock → Adjustment movement.
- No global soft-delete (`IsDeleted`) framework.

**Data types:**

| What | Type |
|---|---|
| Money | `decimal(18,2)` — never float/double |
| Quantities | `decimal(18,3)` — covers pieces and weights |
| Timestamps | `datetime2`, stored in UTC; display and day-based reports use IST (Asia/Kolkata) through one shared helper |
| Text | `nvarchar` with explicit max lengths (Malayalam text must work; avoid EF's default `nvarchar(max)`) |
| Enums | stored as strings |

**Naming:**

- Tables plural PascalCase (`Products`, `StockMovements`); columns match C# property names.
- PK `Id`; FK `<Entity>Id` (`ProductId`, `CustomerId`).
- One SQL Server schema per module: `inventory`, `sales`, later `production`, `accounting`, etc.

**Deliberately not used:** a concurrency token on every table (add only where concurrent updates are a real risk), soft-delete framework, generic repositories, separate public-ID columns.

## 7. Architecture principles

- **Modular monolith**, not microservices.
- Possible backend layout: Domain / Application / Infrastructure / API — only as much structure as the real project size justifies.
- No patterns, abstractions, repositories, services or layers added just for "enterprise architecture" (e.g. generic repositories over EF Core, interfaces with a single implementation, extra projects with no clear purpose).
- Code must be clean, maintainable, scalable, expandable, easy to understand and production-oriented.
- For every decision ask: *"Will this design make it difficult to add future modules?"* — but do not overengineer the MVP for hypothetical requirements.
- **Build a simple working system today with a clean foundation that can grow tomorrow.**

## 8. How AI assistance should work

The owner is building this both as a real business application and to grow as a **Full Stack .NET Developer**. AI should act as a senior software engineer, software architect, technical mentor, code generator and code reviewer. AI may generate large parts of the app; the owner reviews everything.

1. Understand the existing project before changing code.
2. Do not create unnecessary files or duplicate code.
3. Follow the existing architecture and conventions.
4. Make changes carefully and incrementally.
5. Explain important decisions when necessary (the owner is learning — explain trade-offs briefly).
6. Avoid overengineering.
7. Consider future expansion.
8. Keep code clean and maintainable.
9. Proactively identify bugs and logical problems.
10. Never generate large amounts of code without considering how it integrates with the existing project.

Priorities when generating code: **correctness, simplicity, maintainability, consistency, real-world usability.**
Design before large code drops; deliver in reviewable increments.

## 9. Project status

_Last updated: 2026-09-14_

- Solution scaffolded on .NET 10: `GoldenPappadam.sln` with `src/GoldenPappadam.Domain`, `src/GoldenPappadam.Infrastructure`, `src/GoldenPappadam.Api` and `tests/GoldenPappadam.Tests`. No Application project: use-case code lives in the API project in feature folders until it earns its own project. Controllers, not minimal APIs. React client (`client/`) comes once the inventory endpoints exist.
- Inventory entities, EF Core configurations, the `AppDbContext` (audit handling, ledger immutability, UTC DateTime conversion) and Identity with `Guid` keys are in place. Migration `InitialCreate` applied to LocalDB; units KG/PCS/PKT/BOX are seeded.
- Inventory API done and covered by 20 tests against LocalDB: categories, units, products (with source/cycle validation), stock on hand with low-stock flag, movement history with running balance, manual entries (opening/production/damage), count-based adjustments, and packing. Business-rule failures return problem details via `DomainException`.
- Admin login done: cookie authentication, `POST /api/auth/login`, `logout`, `GET /api/auth/me`, `change-password`, and `/api/admin/users` for adding or deactivating admins. Every endpoint requires a signed-in user through a fallback authorization policy; only login and the OpenAPI document are anonymous. Audit fields now record the signed-in user.
- Immediate priority: the React client (inventory screens), then sales.

Agreed order of work:

1. [x] Finalize phase-1 requirements (done apart from the TBDs in §10).
2. [x] Design the database architecture (conventions in §6).
3. [x] Design the inventory module — `docs/01-inventory-design.md` (approved 2026-09-14).
4. [x] Define products and product types — same document.
5. [x] Define stock-movement logic — same document.
6. [ ] Plan how inventory connects to future sales and production modules.
7. [ ] Build the backend incrementally — inventory module done (categories, units, products, stock, packing); login and sales next.
8. [ ] Build the React frontend incrementally.

Conventions that emerged while building, worth following in new features:

- Feature folders under `src/GoldenPappadam.Api/Features/<Module>/<Feature>` hold the controller, its DTOs and its service.
- Controllers stay thin; rules the database cannot express live in a small service (`ProductService`, `StockService`, `PackingService`).
- Validate everything **before** mutating a tracked entity, so a rejected request leaves nothing half-changed.
- Business-rule failures throw `DomainException` (400) or `NotFoundException` (404); `DomainExceptionHandler` turns them into problem details.
- Records used as request DTOs put validation attributes on the **constructor parameter**, not `[property: ...]`, which throws on .NET 10.
- Filter and order the entity query **before** projecting to a DTO: SQL Server cannot order by an already-projected record. Keep list projections in a `*Queries` class so a test can run them against real SQL.
- Tests run against a throwaway LocalDB database per test class (`TestDatabase`), not an in-memory provider, so constraints and transactions behave as in production.

Decisions made:

- 2026-09-11 — Database: Microsoft SQL Server.
- 2026-09-11 — ID, audit-field, data-type and naming conventions (see §6 "Agreed conventions").
- 2026-09-11 — Phase 1 includes simple admin login (ASP.NET Core Identity, no roles).
- Target framework: .NET 10 (SDK 10.0.301 installed). Local SQL Server available: LocalDB (`MSSQLLocalDB`) and SQL Express. Do not touch the `BARTENDER` SQL instance on the owner's laptop.

- 2026-09-14 — Phase-1 requirement answers recorded in §4 "Confirmed requirements" and the build order in §3.
- 2026-09-14 — Inventory design approved: `docs/01-inventory-design.md`. Current stock is computed from the movement ledger (no cache column in phase 1). Stock shortfalls **warn, never block**, so stock may go negative. A packed product's source may be a loose product or another packed product (`SourceProductId`), with no cycles allowed.

- 2026-09-14 — Solution structure (3 projects + tests, feature folders, controllers), cookie-based login with ASP.NET Core Identity, and **LocalDB (`(localdb)\MSSQLLocalDB`, database `GoldenPappadam`) for development**.

Pending decisions:

- None blocking. Open business questions remain in §10.

## 11. Running the project locally

```bash
dotnet build
dotnet run --project src/GoldenPappadam.Api
dotnet ef migrations add <Name> -p src/GoldenPappadam.Infrastructure -s src/GoldenPappadam.Api -o Persistence/Migrations
dotnet ef database update -p src/GoldenPappadam.Infrastructure -s src/GoldenPappadam.Api
```

The first admin account is created at start-up from user secrets, only when the user table is empty. Never put these in a committed file:

```bash
dotnet user-secrets set "Bootstrap:AdminEmail" "you@example.com" --project src/GoldenPappadam.Api
dotnet user-secrets set "Bootstrap:AdminPassword" "<a strong password>" --project src/GoldenPappadam.Api
```

The LocalDB connection string is the default in `src/GoldenPappadam.Api/appsettings.json`; a server overrides it with the `ConnectionStrings__GoldenPappadam` environment variable. Inspect the data with SSMS or `sqlcmd -S "(localdb)\MSSQLLocalDB" -d GoldenPappadam`.

## 10. Open business decisions (TBD — do not assume)

Never design around an assumption for these; ask, or keep the design open.

| # | Question | Status | Affects |
|---|---|---|---|
| 1 | Do different shops pay different prices? | TBD — owner to confirm with the business | sales pricing |
| 2 | Returns: do shops return damaged/unsold stock, and is it replaced, credited, restocked or discarded? | TBD — owner to confirm the actual process | inventory + sales |
| 3 | Are discounts given, and at bill level or item level? | TBD — owner to confirm | invoice totals |
| 4 | Must bills carry GST (GSTIN, HSN/SAC, tax amounts)? | TBD — confirm with the accountant | invoice structure |

Answered on 2026-09-14 and now part of the design: stock shortfall warns instead of blocking; one loose variety can be packed into many packet sizes; a pack can occasionally be made from another pack.
