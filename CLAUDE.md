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
- **Returns:** the design must stay return-ready (extensible movement types + the reference pattern). Do not build a returns workflow in phase 1. _Superseded 2026-09-25: returns were answered (§10 Q2) and built in the office on 2026-09-26 - see `docs/05-reports-returns-design.md` §3._
- **Discounts:** a simple bill-level discount field is enough for now; item-level discounts must remain addable later. No promotion/discount engine.
- **Tax/GST:** the invoice structure must allow tax fields (GSTIN, HSN/SAC, tax %, tax amount, CGST/SGST/IGST) to be added later without restructuring sales. Do **not** assume sales are GST-exempt. _Superseded 2026-09-23: the owner asked for configurable GST to be built - see "Invoices and GST" below. The rates and treatments themselves are still the accountant's to confirm._

### Confirmed requirements (2026-09-15, phase 3)

- **Customer-specific pricing is real** (closes §10 question 1). The same product has a different price for
  different shops. Prices live in `sales.CustomerPrices`; a shop with no price row pays the product's
  `SellingPrice`. *Who may set a price was changed on 2026-09-23 — see below; the admin-only rule no longer
  holds.* A bill line still cannot be priced by hand on the phone: the salesperson changes the customer's
  rate, which is audited, rather than typing a one-off price onto a bill.
- **A recorded price is never rewritten.** An offline sale is a transaction that already happened, so it is
  saved at the price the phone used; if the price changed while the phone was offline, the bill is *flagged*
  for the admin, not silently re-priced.
- **Stock leaves the warehouse when the van is loaded**, not when a shop is billed. That makes stock
  location-aware: `inventory.StockLocations` plus `StockMovement.LocationId`. Unsold stock returns every
  evening, and a shortfall is **shown to the admin, never auto-adjusted** — that reconciliation is what
  catches a sale nobody recorded.
- **Both the admin and the salesperson record the van load** (corrected 2026-09-16; the original answer was
  admin-only). This is how the business already works: the packing book records what was packed, say 500, and
  the salesman takes 350 and writes that in his own book. His entry is the honest record of what left. The
  salesperson's power over stock is deliberately narrow — the mobile request carries no location and no
  direction, so the server can only move stock from the main warehouse onto *that phone's own van*. No
  adjustments, no damage, no other location. A load entered from a phone carries its `DeviceId`, and the
  admin's van screen names the phone, which is how the office is told the salesman entered it.
- **One user account per person**, with two roles: `Admin` and `Salesperson`.
- **No van, no bill** (2026-09-16). A phone the office has not assigned to a van cannot record a
  delivery: the bill follows the goods and the goods come off the van, so with no van there is nothing
  to have delivered. It is refused rather than quietly drawn from the warehouse, which would balance the
  books and leave the van's wrong. Payments, visits and stock requests still work without a van, because
  none of them moves product.

### Confirmed requirements (2026-09-22/23, customers and branches)

- **Parent customer, physical branches.** A customer may be one shop or a parent company with several
  physical shops ("Danya Supermarket" with Kundara, Coimbatore, Kundrathur, Elambalur). A branch is a
  `sales.CustomerBranches` row under the parent, **never a separate customer**. Branches are deactivated,
  never deleted, so a closed branch's old bills still name it.
- **A bill for a multi-branch customer must name its branch**, stored on the bill (`Invoices.BranchId`);
  a single-location customer never shows or stores one. Bills made before branches existed stay valid.
- **Pricing is per customer, inherited by every branch.** Danya's ₹37 rate for Pappadam 200 g (MRP ₹45.50)
  applies at all four branches. No branch-specific prices for now; the model must allow adding them later
  (a nullable `BranchId` on `CustomerPrices`, resolved branch → customer → product) without a redesign.
- **Salesmen acquire new shops** (2026-09-23), so they must not depend on the office to create customers.
  A salesperson may: create a customer; say whether it has multiple branches; add branches to a new or
  existing customer; edit a customer's and a branch's details; set and change customer-product rates,
  including the initial rates for a new shop; and bill existing customers and branches.
- **A salesperson may not:** delete or cancel sales; deactivate or delete customers, branches or prices
  (removing a rate — sending the shop back to the standard price — is the office's call); set or change a
  customer's opening balance (a shop the salesperson found owes nothing yet); change product master data;
  or reach any office endpoint. Enforced by the API, not the app: the salesperson's only way in is
  `/api/mobile/*`.
- **Salesperson changes travel through the offline sync batch**, like sales, with ids generated on the
  phone. A new shop can be created, priced and billed with no signal, and a retry never makes a second
  customer.
- **Do not duplicate a customer to record another branch.** Finding "Danya Supermarket – Coimbatore" means
  adding or picking the Coimbatore branch under the existing Danya Supermarket. The server refuses an exact
  duplicate customer name; the phone must search existing customers before offering "new customer".
- **The office keeps full control:** add/edit/deactivate customers, branches and rates, see which customers
  the sales team created, see every rate change, and override a rate at any time.
- **Every rate change is recorded** in `sales.CustomerPriceChanges`: customer, product, previous rate, new
  rate, who changed it and when — the office's changes and the salesperson's alike. A recorded bill is still
  never re-priced; a rate change applies from the next bill.

### Confirmed requirements (2026-09-23, invoices and GST)

- **An invoice is an accounting document.** Once finalized it is never edited or deleted; the only change
  allowed is cancellation, which keeps the number. `AppDbContext` enforces this for every code path.
- **Numbers come from the database, never a device.** `GP/26-27/000125`: series, Indian financial year, six
  digits, at most 16 characters as GST requires. Consecutive, restarting each financial year on its own.
  Offline phones never number anything: the server numbers a sale when it syncs.
- **The invoice keeps a snapshot** of the supplier, customer, branch, prices and tax as printed, so later
  master-data changes never alter it.
- **GST is configurable, never hard-coded.** Each product carries HSN, treatment (Taxable / Exempt / Nil rated
  / Non-GST) and rate. Intra-state is CGST + SGST, inter-state IGST, decided by the place of supply - the
  branch for a branch bill. Once the business GSTIN is entered, a product with no treatment or a taxable sale
  with no known state is refused rather than guessed.
- **GST bills only for GST customers** (owner, 2026-09-23). Only pappadam is sold, under one HSN code, and it
  is exempt from GST today. A customer has a "GST registered" tick box that requires its GSTIN; those shops
  get a GST bill (a Bill of Supply while pappadam is exempt), every other shop a normal bill with no GST
  details. Tax follows the product, not the customer: if pappadam ever becomes taxable, shops without GST are
  still charged it.
- **The PDF is made once, stored, fingerprinted and never regenerated**; printing and email use that same
  file. Storage is behind `IInvoiceDocumentStorage` so it can move to Supabase Storage.
- **Email failure never affects the invoice.** Every attempt is logged; the office retries.

**Rule for anything unconfirmed:** mark it TBD / business decision required (§10) instead of assuming.

The full inventory design is in `docs/01-inventory-design.md`; invoice management in `docs/04-invoice-design.md`; reports, shelf life and returns in `docs/05-reports-returns-design.md`.

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

_Last updated: 2026-09-26_

**Phase 1 is complete. Phase 3 is in progress** — a Flutter salesperson app that works offline and
synchronizes with this API, designed in `docs/03-field-sales-design.md` (approved 2026-09-15).
Milestones: M0 environment, **M1 roles and bearer auth (done)**, **M2 customer pricing (done)**,
**M2b stock locations and van loads (done)**, M3 sync foundation, **M4 Flutter foundation (done)**, **M5 shops (done)**, **M6 sale entry (done)**, **M7 offline and sync (done)**,
**M8 payments (done)**, M9 admin field-sales screens, M10 field testing, M11 returns.
There is no phase 2: the owner numbered the mobile work phase 3.

- Solution scaffolded on .NET 10: `GoldenPappadam.sln` with `src/GoldenPappadam.Domain`, `src/GoldenPappadam.Infrastructure`, `src/GoldenPappadam.Api` and `tests/GoldenPappadam.Tests`. No Application project: use-case code lives in the API project in feature folders until it earns its own project. Controllers, not minimal APIs. React client (`client/`) comes once the inventory endpoints exist.
- Inventory entities, EF Core configurations, the `AppDbContext` (audit handling, ledger immutability, UTC DateTime conversion) and Identity with `Guid` keys are in place. Migration `InitialCreate` applied to LocalDB; units KG/PCS/PKT/BOX are seeded.
- Inventory API done and covered by 20 tests against LocalDB: categories, units, products (with source/cycle validation), stock on hand with low-stock flag, movement history with running balance, manual entries (opening/production/damage), count-based adjustments, and packing. Business-rule failures return problem details via `DomainException`.
- Admin login done: cookie authentication, `POST /api/auth/login`, `logout`, `GET /api/auth/me`, `change-password`, and `/api/admin/users` for adding or deactivating admins. Every endpoint requires a signed-in user through a fallback authorization policy; only login and the OpenAPI document are anonymous. Audit fields now record the signed-in user.
- React client done for inventory: login, stock on hand (low-stock flags, add stock, correct after counting), products (with packed-from configuration), packing, stock history with running balance, and settings for categories and units. Stack: Vite, TypeScript, Tailwind v4, shadcn/ui (radix-nova preset), React Router and TanStack Query. `client/` runs on 5173 and proxies `/api` to 5207, so the session cookie stays same-origin.
- Sales done, backend and screens, designed in `docs/02-sales-design.md`: customers with an opening balance and an account statement, bills numbered per Indian financial year that price lines from the product (overridable) and take stock off the ledger, a bill-level discount, cancellation that returns the stock, and payments that settle the oldest bills first or ones you pick, with partial settlement and money on account. 40 tests.
- Dashboard done: five KPI tiles (today, this month, outstanding, product count, stock needing attention), a daily sales trend, sales by product/category, how much of what was billed has come back, stock health, recent bills, what needs restocking and who owes the most — all on the IST business day, with a 7-day / 30-day / this-month range selector.
- UI reworked across every screen for a modern, responsive admin layout: grouped sidebar at 1024px and up with a drawer below it, status colours that only ever carry meaning, skeletons and real empty/error states, priority columns so no list scrolls sideways on a phone, and touch targets that grow on coarse pointers. Charts are Recharts, loaded only with the dashboard route so the other screens do not carry them.
- **M1 done (phase 3):** two roles, `Admin` and `Salesperson`. An endpoint with no authorization attribute is
  **admin-only** through the fallback policy, which is what made every phase-1 controller safe against a
  salesperson token without editing one of them; a bare `[Authorize]` means "anyone signed in" and only the
  shared auth endpoints use it. Identity's bearer scheme runs alongside the cookie, so the React panel keeps
  its session and the phone gets `POST /api/auth/mobile/login` and `mobile/refresh` (1-hour access token,
  30-day refresh). `RoleSeeder` creates the roles and gives every pre-phase-3 account the admin role at
  start-up — without that backfill the new policy would lock the owner out. 36 new tests drive real HTTP
  through the real pipeline (`ApiFactory`, `WebApplicationFactory`), because authorization is wiring rather
  than logic and the only honest check is the status code.
- **M2 done (phase 3):** `sales.CustomerPrices` — one price per shop per product, unique index, admin-only,
  deactivated rather than deleted. The precedence lives in one place, `CustomerPriceService.Resolve`: the
  price typed on the bill line, else the shop's agreed price, else `Product.SellingPrice`, else an error that
  asks a person instead of guessing. A shop with no arrangement keeps paying the product price, so phase-1
  behaviour is unchanged. Changing a price never touches a bill already made, because `InvoiceLine.UnitPrice`
  was always the frozen record of what was charged. The admin sets prices from a card on the customer page.
- **M2b done (phase 3):** stock has a place. `inventory.StockLocations` (seeded `MAIN` and `VAN-1`, fixed ids
  like the units) and `StockMovement.LocationId`, migrated nullable -> backfill to `MAIN` -> required so no row
  ever claimed to be somewhere that did not exist. Verified against the development database: every stock
  figure identical before and after. Stock figures are now **per location**, and callers say which they mean
  rather than inherit a default. Packing and low stock mean the warehouse; a bill with no location still comes
  off the warehouse. `fieldsales.VanLoads` records the morning load and the evening return as `Transfer`
  movement pairs, admin-only. The day's reconciliation is deliberately the ledger's own arithmetic - opening +
  loaded - sold - returned + corrections **is** the van's closing balance - so an unaccounted packet is not a
  report that could disagree with stock, it is stock still sitting on a van that should be empty. It is shown,
  never auto-adjusted: an unrecorded sale and a miscount look identical to arithmetic, and that reconciliation
  is what catches the sale nobody wrote down.
- **M3 done (phase 3):** the sync foundation. `fieldsales.Devices`, `ShopVisits` and `SyncSubmissions`, plus
  `/api/mobile/*` - the salesperson's entire surface, which contains no endpoint that prices, moves stock,
  edits a shop or cancels a bill. `MobileSyncService` owns no business rules: a sale runs through the same
  `InvoiceService` and a payment through the same `PaymentService` the admin panel uses. Idempotency is a
  unique index on `SyncSubmissions.ClientRequestId`, generated once on the device and never regenerated, so a
  retry returns the original bill instead of making a second one. The batch answers per item, so one rejected
  sale does not stop the nine behind it, and a rejection writes nothing, leaving the client id free to succeed
  later. A price changed while the phone was offline is **flagged, not rewritten**. Known gap, commented at
  `RecordSubmissionAsync`: the submission row is committed just after the record rather than inside the same
  transaction, because `InvoiceService` rolls back and retries its own transaction when two bills race for an
  invoice number. A dropped connection is fully covered; a process crash between the two commits is not.
- **M9 done (phase 3):** "Today on the road" - sales, shops visited, cash collected, sold on credit and
  outstanding created today, then the bills with **recorded-on-the-phone against received-by-the-server**,
  which is what makes offline legible rather than mysterious, plus the visits that sold nothing.
- **M4 done (phase 3):** `mobile/` - Flutter, Android target. Drift for the local database, Dio for the API,
  Riverpod for state, connectivity_plus, flutter_secure_storage for tokens, uuid. The cache tables are
  replaced wholesale on every snapshot; `OutboxEntries` is the only local truth, written **before** the screen
  says "saved" and deleted by nothing. Money and quantities are Dart doubles on purpose: the phone never
  decides what anything costs, so these are display values and the exact figures stay in SQL Server's
  decimals. The sync engine pushes then pulls under a single lock, with a 5s/15s/1m/5m/15m backoff that stops
  growing. A refusal is `Failed` and waits for a person; anything else stays `Pending` and is retried.
  12 tests run on the Dart VM against an in-memory drift database - no emulator - and cover the offline
  scenarios in the brief's §23: signal lost mid-sync, ten offline sales in one batch, a repeat treated as
  done, a refusal kept and explained, restart with work outstanding, and sign-out never discarding it.
  `mobile/lib/data/local/database.g.dart` is generated by build_runner and committed, so a fresh clone builds
  without running codegen first.
- **M5-M8 done (phase 3):** the salesperson's screens. Shops with search, a shop page showing the outstanding
  balance **with how stale it is**, the sale sheet, payment collection, and "visited, nothing needed".
  The sale screen lists only products the office has priced for that shop, shows the price as plain text
  beside each one, and has no field to change it - an unpriced product cannot be added at all, which beats an
  invented number. Saving writes the bill, any payment and the visit to the outbox **in one transaction**,
  and the visit names the other two by their client ids because the phone has never spoken to the server.
  The save button closes while saving: two taps would be two sales with two different client ids, which
  idempotency cannot catch. 22 Dart tests.
- **The wire contract is tested**, in `MobileContractTests`: the exact JSON the Flutter app emits, written out
  by hand, posted to the real API. The Dart tests prove the phone builds the right body and the C# tests prove
  the server does the right thing, but neither would notice the two disagreeing about a field name. A rename
  on either side now fails there rather than on a road in Kollam.
- **Salesperson extensions done (2026-09-16):** van stock, a day summary, payment history, and stock
  requests. `fieldsales.StockRequests` (+ lines) is the only new table; `VanLoads.DeviceId` the only new
  column. Everything the phone sends still rides the one outbox and the one client request id, so a stock
  request asked for twice is asked for once. The salesperson's app is four tabs - Today, Shops, Van, Stock -
  because on a doorstep anything more than a tap is too far. The admin gets a **Stock requests** screen with
  two readings of the same data: what to pack, added up per product per day, and the individual requests
  behind it. `/api/mobile/day` is cached on the phone at each sync, so the home screen shows the office's
  figures with the pending count beside them rather than a confident total that quietly omits unsent work.
- **Customer branches done (2026-09-22):** a customer can be a parent company with several physical
  shops, e.g. Danya Supermarket with Kundara and Coimbatore branches. `sales.CustomerBranches` (+
  `Customers.HasMultipleBranches`) and `Invoices.BranchId`, admin-only, deactivated rather than
  deleted so a closed branch's old bills still show it. Pricing is never duplicated per branch -
  `CustomerPrices` stays keyed on the parent customer, so every branch inherits the one agreed
  price. Admin New Bill shows a required branch picker only for a multi-branch customer, clearing
  it the moment the customer changes. Shipped end-to-end, including the phone: the snapshot now
  carries each customer's `hasMultipleBranches` flag and its active branches, `MobileSaleRequest`
  gained an optional `BranchId`, and the sale screen shows the same required picker the admin panel
  does, right after the shop is chosen. 8 new C# tests plus 4 `MobileContractTests`, 6 new Dart
  tests (`new_bill_flow_test`, `sales_repository_test`, `sync_engine_test`). Drift schema bumped to
  v3 (`Branches` table, `Customers.hasMultipleBranches` column).
- **Salesmen onboard shops — backend and admin done (2026-09-23), phone screens next.** Rules in §4. Three
  new sync submission types - `Customer`, `CustomerBranch`, `CustomerPrice` - create-or-update by an id the
  phone generates, so a new shop can be created, given a branch, priced and billed in one offline batch
  (proved end-to-end in `MobileContractTests`). They call the same `CustomerService`,
  `CustomerBranchService` and `CustomerPriceService` as the office, so the rules are the same rules; what
  the phone may not do (opening balance, notes, deactivation, removing a rate) is decided in
  `MobileSyncService`. Every rate change, from anyone, goes to the immutable `sales.CustomerPriceChanges`
  (existing agreed rates backfilled as its first entries). Who created a customer comes from the existing
  `CreatedBy` audit field - no new column. The office sees a "Sales team" badge and filter on Customers, a
  rate history on each customer's price card, and "Rates changed by the sales team" on Today on the road.
  Admin endpoints are unchanged and still admin-only. **Not yet built:** the Flutter screens for adding a
  shop, a branch and a rate - the API is ready for them.
- **Invoice management done (2026-09-23)**, designed in `docs/04-invoice-design.md`. The bill *is* the
  invoice: `sales.Invoices` gained its number parts, document type, a supplier/customer/branch snapshot, the
  tax basis and GST totals; lines gained a line number, HSN, treatment, discount share and CGST/SGST/IGST.
  New tables: `InvoiceSettings` (one row: business details, GSTIN, series, rounding), `InvoiceNumberSequences`
  (the counters), `InvoiceDocuments` (stored PDF + SHA-256), `InvoiceEmailLogs`. Products gained HSN /
  treatment / rate; customers email / GSTIN / state; branches GSTIN / state. Migration
  `AddInvoiceManagement` backfilled the 14 existing bills (series `INV`, snapshot from today's customer)
  with every total and stock figure unchanged. `GstCalculator` is the only place amounts are worked out,
  used by `POST /api/sales/invoices/preview` (New Bill now shows the shop's agreed rate and the tax - it used
  to show the product price), finalizing and the PDF. QuestPDF renders the PDF; MailKit sends; `IEmailSender`
  has None / Pickup / Smtp providers. Admin: invoice history with search, status and "email failed"
  filters and row actions; the invoice page shows PDF and email status, print, download, email and retry,
  email history, who finalized/cancelled and from which phone; Settings has "Invoices and GST".
  49 new tests (239 total): GST arithmetic, amount in words, 25 simultaneous finalizations, two office
  computers plus a phone racing through real HTTP, new-year counter race, counter repair, immutability,
  snapshots, PDF storage, email failure and retry, authorization. The phone app is unchanged; its sales
  get their PDF the first time the office opens them.
- **Phone shows bill numbers and GST vs normal bill (2026-09-23).** The sync result carries
  `documentNumber` for a sale (the same number again on a retry); the phone stores it on the outbox row
  and Home lists "Today's bills" as "waiting to sync" until synced, then with the official number. The
  snapshot carries each shop's `gstin`/`isGstRegistered`; the sale screen and shop page say "GST bill" or
  "Normal bill". Drift schema v4 (`Customers.gstin`, `OutboxEntries.documentNumber`). A shop can no longer
  be marked GST registered before the business GSTIN is in Settings, so the phone never saves a sale the
  server would refuse for that reason. Still not on the phone: adding shops, sharing the PDF.
- **Reports done (2026-09-25)**, designed in `docs/05-reports-returns-design.md`: sales, collections,
  outstanding by age, and a customer statement, each on screen, as PDF and as Excel from one
  `ReportDocument` with server-side totals (`Features/Reports/`, ClosedXML for Excel, QuestPDF house style
  shared through `Common/PdfStyle.cs`). A **Reports** group in the sidebar; **Statement** on the customer
  page.
- **Shelf life, repacking and expiry done (2026-09-26)**, same document. `Product.ShelfLifeDays`; stock age is
  worked out from the ledger first-in-first-out (`StockAgeCalculator`, no batch tables) with van loads
  keeping packing dates; `inventory.RepackEntries` + movement type `Repacking` (exact conversion via the
  loose root product, left-over back to loose, fresh date); expired stock written off only on the office's
  say-so. Inventory gets **Stock age** and **Repacking**, Reports gets **Stock movement**, and the dashboard
  counts expired / expiring / worth-repacking products.
- **Returns in the office done (2026-09-26)**, same document §3. `sales.ReturnNotes` (+ lines), numbered
  `RN/26-27/000001` by the invoice counter, immutable except settlement and cancellation. Returned packets
  never re-enter stock. Settled once, now or later: **replaced free** (`Replacement` movements from the
  warehouse or a van), **credit** (a `Payment` of method `ReturnCredit` that settles the oldest bills, never
  counted as money collected and refused on the payments endpoint), or **nothing**. A credited return cannot
  be cancelled, like a bill with money on it. Admin-only; Sales > **Returns**, Reports > **Returns and
  expiry**, a printable return note.
- **Returns on the phone done (2026-09-26)**, same document §4. Sync submission type `Return`: the
  salesman records packets collected (product, quantity, Expired / Damaged) from the shop page and says
  whether fresh packets went from **his own van** (server picks the van from the device; no van, no
  replacement) - otherwise it waits for the office. No rate or credit ever comes from the phone. The
  sync answer carries the `RN/...` number. The van reconciliation has a **Replaced** figure (admin Van
  screen and the phone's van tab). Also fixed: the shop page's payment history escaped its `$` and never
  showed a payment still waiting to sync. Next in the plan: the owner's daily summary.
- Phase 1 is feature-complete. Remaining work is judgement rather than code: use it on real data, then decide what to correct. Reporting is currently the dashboard plus the date filters and totals on the bills, payments, customers and stock screens; a dedicated printable report has not been built.

Agreed order of work:

1. [x] Finalize phase-1 requirements (done apart from the TBDs in §10).
2. [x] Design the database architecture (conventions in §6).
3. [x] Design the inventory module — `docs/01-inventory-design.md` (approved 2026-09-14).
4. [x] Define products and product types — same document.
5. [x] Define stock-movement logic — same document.
6. [x] Plan how inventory connects to future sales and production modules — sales writes `Sale` and `SaleReversal` movements through the same ledger; production will write `Production` movements the same way.
7. [x] Build the backend incrementally — inventory, login, sales and dashboard.
8. [x] Build the React frontend incrementally — inventory, sales and dashboard screens.

Conventions that emerged while building, worth following in new features:

- Feature folders under `src/GoldenPappadam.Api/Features/<Module>/<Feature>` hold the controller, its DTOs and its service.
- Controllers stay thin; rules the database cannot express live in a small service (`ProductService`, `StockService`, `PackingService`).
- Validate everything **before** mutating a tracked entity, so a rejected request leaves nothing half-changed.
- Business-rule failures throw `DomainException` (400) or `NotFoundException` (404); `DomainExceptionHandler` turns them into problem details.
- Records used as request DTOs put validation attributes on the **constructor parameter**, not `[property: ...]`, which throws on .NET 10.
- Filter and order the entity query **before** projecting to a DTO: SQL Server cannot order by an already-projected record. Keep list projections in a `*Queries` class so a test can run them against real SQL.
- Tests run against a throwaway LocalDB database per test class (`TestDatabase`), not an in-memory provider, so constraints and transactions behave as in production.

Client conventions:

- `client/src/api/` holds typed API functions and DTO types that mirror the server's; `lib/api.ts` is the only place that calls `fetch`, adds `credentials: 'include'` and turns problem details into an `ApiError` message.
- Server state goes through TanStack Query (`useQuery` / `useMutation` with `invalidateQueries`); component state stays in `useState`. No global store.
- Warnings returned by the API (negative stock) surface as a toast, never as a blocked form.
- Quantities, money and dates are formatted only through `lib/format.ts`, which renders dates in IST.

Decisions made:

- 2026-09-11 — Database: Microsoft SQL Server.
- 2026-09-11 — ID, audit-field, data-type and naming conventions (see §6 "Agreed conventions").
- 2026-09-11 — Phase 1 includes simple admin login (ASP.NET Core Identity, no roles).
- Target framework: .NET 10 (SDK 10.0.301 installed). Local SQL Server available: LocalDB (`MSSQLLocalDB`) and SQL Express. Do not touch the `BARTENDER` SQL instance on the owner's laptop.

- 2026-09-14 — Phase-1 requirement answers recorded in §4 "Confirmed requirements" and the build order in §3.
- 2026-09-14 — Inventory design approved: `docs/01-inventory-design.md`. Current stock is computed from the movement ledger (no cache column in phase 1). Stock shortfalls **warn, never block**, so stock may go negative. A packed product's source may be a loose product or another packed product (`SourceProductId`), with no cycles allowed.

- 2026-09-14 — Solution structure (3 projects + tests, feature folders, controllers) and cookie-based login with ASP.NET Core Identity.
- 2026-09-14 — Development database: **SQL Express (`.\SQLEXPRESS`), database `GoldenPappadam`**. Moved off LocalDB, which kept failing to auto-start on this machine; SQL Express runs as a service. Tests use the same instance.
- 2026-09-15 — Charting library: **Recharts**, the only one, loaded through a lazy dashboard route. Recharts paints with SVG presentation attributes, which do not resolve `var()`, so chart colours are read off the document by `lib/chartColors.ts` and passed as resolved values.
- 2026-09-15 — `GET /api/dashboard/product-sales?from&to` added: the invoice list carries no lines, so sales per product and per category cannot be built on the client without a request per bill.

- 2026-09-15 — **Phase 3 approved**: `docs/03-field-sales-design.md`. Flutter/Android salesperson app, offline-first, syncing to this API. Seven business decisions recorded in its Part E.
- 2026-09-15 — Authorization: **fallback policy = Admin**, so a new endpoint is closed until deliberately opened. Salesperson endpoints opt in with `Policies.FieldSales`.
- 2026-09-15 — Mobile authentication: **Identity's bearer token scheme**, not hand-rolled JWT and not a second identity store. The cookie stays for the React panel.
- 2026-09-15 — Integration tests: `Microsoft.AspNetCore.Mvc.Testing` against a throwaway SQL Express database, for things that only real HTTP can prove (authorization, later idempotency).

- 2026-09-14 — Open the solution in **Visual Studio 2026** (18.7). VS 2022 cannot target .NET 10, and the solution stays on .NET 10 because it is the current LTS release.

- 2026-09-23 — **Invoice management**: `docs/04-invoice-design.md`. Numbers from a database counter row
  locked inside the finalizing transaction (gapless, never reused); no saved drafts; GST configurable and
  off until a GSTIN is entered; PDF by QuestPDF, stored once behind `IInvoiceDocumentStorage`; email via
  MailKit behind `IEmailSender`, credentials only in user-secrets / environment.

Pending decisions:

- None blocking. Open business questions remain in §10.

## 11. Running the project locally

```bash
dotnet build
dotnet test
dotnet run --project src/GoldenPappadam.Api
dotnet ef migrations add <Name> -p src/GoldenPappadam.Infrastructure -s src/GoldenPappadam.Api -o Persistence/Migrations
dotnet ef database update -p src/GoldenPappadam.Infrastructure -s src/GoldenPappadam.Api
```

The client needs the API running on 5207 (the `http` launch profile):

```bash
npm install --prefix client
npm run dev --prefix client
```

Then open http://localhost:5173. `.claude/launch.json` defines both servers for tooling.

The first admin account is created at start-up from user secrets, only when the user table is empty. Never put these in a committed file:

```bash
dotnet user-secrets set "Bootstrap:AdminEmail" "you@example.com" --project src/GoldenPappadam.Api
dotnet user-secrets set "Bootstrap:AdminPassword" "<a strong password>" --project src/GoldenPappadam.Api
```

### The salesperson app (`mobile/`)

Flutter 3.47 / Dart 3.13, Android only for now. Open `mobile/` in **Android Studio** (not the solution root),
or use the command line from `mobile/`:

```bash
flutter pub get
flutter test
flutter analyze
dart run build_runner build      # only after changing the drift tables in lib/data/local/database.dart
```

`flutter doctor` reports missing cmdline-tools and unaccepted licences. That does **not** stop a build - the
first `flutter build apk` installs and accepts what it needs. Only install the SDK command-line tools if a
build actually complains. The first Gradle build takes about ten minutes; later ones are far quicker.

**On the emulator.** Start the API with the `http` profile, then run the app with no arguments: the default
`API_BASE_URL` is `http://10.0.2.2:5207`, which is the emulator's alias for the machine hosting it.

```bash
flutter run
```

**On a real phone.** Two things have to change, and both are easy to forget:

1. The API must listen on the network rather than only on loopback. Use the `lan` profile:
   `dotnet run --project src/GoldenPappadam.Api --launch-profile lan` (it binds `0.0.0.0:5207`).
   Windows Firewall will ask to allow it the first time - it has to be allowed on **private** networks.
2. The app must be told where the office is, using the machine's Wi-Fi address from `ipconfig`:

```bash
flutter run --dart-define=API_BASE_URL=http://192.168.1.5:5207
```

Android blocks plain HTTP by default. **Debug builds** (`flutter run`, debug APKs) allow it to any address
through `android/app/src/debug/res/xml/network_security_config.xml`, because the router keeps moving the
laptop's address (192.168.1.2 → .8 → .3) and an exact list failed silently every time it did. **Release
builds** still use the strict list in `src/main/res/xml/network_security_config.xml`, so a release APK needs
the current LAN address added there. When the API is served over HTTPS, both files can go.

**"The phone is not syncing" checklist** (2026-09-22, every cause seen so far):

1. `netstat -ano | findstr :5207` must show `0.0.0.0:5207`. Since 2026-09-22 the `http` and `https` launch
   profiles bind `0.0.0.0:5207` too, so plain F5 works for the phone; `127.0.0.1:5207` means an old build or
   a profile edited back to `localhost`.
0. **McAfee Firewall is installed on the owner's laptop and overrides Windows Firewall.** A Windows allow-rule
   for `goldenpappadam.api.exe` does nothing while McAfee is active. On an untrusted Wi-Fi it drops the
   phone's connections: the PC reaches `http://<lan-ip>:5207` fine, the phone gets nothing. Allow TCP 5207
   (or the exe) inside McAfee's firewall settings. Test from the phone itself with
   `adb shell curl -s -o /dev/null -w '%{http_code}' http://<lan-ip>:5207/openapi/v1.json` — 200 means open.
2. `ipconfig` → the Wi-Fi IPv4 address must match the app's `API_BASE_URL`. Ask the router for a DHCP
   reservation for the laptop so it stops changing.
3. The phone must be on the same Wi-Fi as the laptop.
4. The firewall rule for `goldenpappadam.api.exe` must cover the network's profile (Public or Private).
5. `fieldsales.Devices.LastSeenAt` shows when the phone last reached the server, which tells you whether
   the phone never arrived or arrived and was refused.

The phone needs a **Salesperson** account, created by an admin from the users screen. It cannot use an admin
account's password to reach anything except its own endpoints, which is the point.

First sign-in needs a connection: it registers the handset and pulls the first snapshot. After that the app
works with no signal at all.

### Invoice PDFs and email

PDFs are stored under `src/GoldenPappadam.Api/App_Data/invoice-documents` (git-ignored). In development
email is **not sent**: the `Pickup` provider writes each message as an `.eml` file to
`src/GoldenPappadam.Api/App_Data/mail-outbox` - open one in Outlook to see exactly what a customer would get.
To send for real, configure SMTP, keeping the password out of Git:

```bash
dotnet user-secrets set "Email:Provider" "Smtp" --project src/GoldenPappadam.Api
dotnet user-secrets set "Email:Host" "smtp.example.com" --project src/GoldenPappadam.Api
dotnet user-secrets set "Email:Username" "billing@example.com" --project src/GoldenPappadam.Api
dotnet user-secrets set "Email:Password" "<app password>" --project src/GoldenPappadam.Api
dotnet user-secrets set "Email:FromEmail" "billing@example.com" --project src/GoldenPappadam.Api
```

On a server use environment variables instead (`Email__Provider`, `Email__Password`, ...).

Development uses the **SQL Express** instance `.\SQLEXPRESS` (SQL Server 2022), set in `src/GoldenPappadam.Api/appsettings.json`; the tests create throwaway databases on the same instance. A server overrides the connection with the `ConnectionStrings__GoldenPappadam` environment variable. Inspect the data with SSMS or `sqlcmd -S ".\SQLEXPRESS" -d GoldenPappadam`.

LocalDB is deliberately **not** used: its engine is started on demand by the first process that connects and repeatedly failed to auto-start on this machine ("SQL Server process failed to start", 0x89c5010a). SQL Express runs as a Windows service, so it is always up. The owner's other projects still use LocalDB — leave that instance alone.

Troubleshooting:

- **Login failed / "Cannot open database GoldenPappadam"** on a fresh machine: the database does not exist yet. Run `dotnet ef database update` (see above); the first admin account is then created at start-up from user secrets.
- Never stop the app with a forced process-tree kill (`taskkill /T /F`): it terminates child processes, which can include a database engine. Stop it with Ctrl+C, or Shift+F5 in Visual Studio.
- **MSB3021/MSB3026/MSB3027 "file is locked by" on build.** The app is still running; stop it and build again. These are not compiler errors.

## 10. Open business decisions (TBD — do not assume)

Never design around an assumption for these; ask, or keep the design open.

| # | Question | Status | Affects |
|---|---|---|---|
| ~~1~~ | ~~Do different shops pay different prices?~~ | **Answered 2026-09-15: yes.** See §4 "Confirmed requirements" | sales pricing |
| ~~2~~ | ~~Returns: do shops return damaged/unsold stock, and is it replaced, credited, restocked or discarded?~~ | **Answered 2026-09-25:** only expired or damaged packets come back, never resold; replacement, credit or nothing, decided by the office per shop. Pappadam lasts 20 days from packing; unsold packets are repacked (any size, no loss, fresh 20 days) when the office decides. Built in the office 2026-09-26; phone next - `docs/05-reports-returns-design.md` | inventory + sales |
| 3 | Are discounts given, and at bill level or item level? | TBD — owner to confirm | invoice totals |
| 4 | GST: the exact HSN code, that pappadam is exempt rather than nil-rated, and the "Bill of Supply" heading on GST bills for exempt goods | **Partly answered 2026-09-23:** one HSN, no GST today, GST bills only for GST-registered shops. Accountant to confirm the three details | invoices |
| 5 | Should the `GP` series continue from the old `INV` numbers (15 onward) or start at 1 as it does now? | TBD — owner / accountant | invoice numbering |

Answered on 2026-09-14 and now part of the design: stock shortfall warns instead of blocking; one loose variety can be packed into many packet sizes; a pack can occasionally be made from another pack.
