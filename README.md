# Golden Pappadam — Business Management System

A full-stack ERP-style system built for a real family business: a pappadam manufacturer and distributor in
Kundara, Kollam, Kerala. It runs the business end to end: stock and packing, bills, credit, payments and
returns, an offline-first Android app for the sales team on the road, staff wages and expenses, and the
company's own retail shop.

The office uses it on real data. Most of the business is sold on credit and paid partly, bill to bill, and
the sales team works in places with no mobile signal. Those two facts shaped most of the design.

## What it does

**Office (React admin panel)**
- **Inventory:** loose and packed products in any unit (kg, pieces, packets). Stock is never overwritten:
  every change is a movement in a ledger, and stock on hand is derived from that ledger. Low-stock alerts,
  stock history with running balances, and stock age worked out first-in-first-out from the ledger.
- **Packing:** turns loose stock (kg) into packets in one transaction, using each variety's pieces-per-kg
  figure. Also repacking, and expiry write-offs.
- **Sales and invoicing:** customers with multiple branches, customer-specific prices, gapless invoice
  numbers per Indian financial year, configurable GST (CGST/SGST/IGST by place of supply), and invoices
  that cannot be changed once issued. Each invoice PDF is generated once, stored with a SHA-256 fingerprint,
  and can be emailed.
- **Credit and payments:** partial payments, one payment across several bills, money on account, customer
  ledgers and receivables ageing.
- **Returns:** expired or damaged packets, settled by free replacement, a credit note, or nothing.
- **Staff and expenses:** daily attendance, weekly wages that use each day's wage rate, corrections to a
  paid week carried into the next one, and expenses by category with full edit history.
- **Own shop:** stock transferred from the factory in kg and sold by the piece, within a minimum and
  maximum price.
- **Dashboard and reports:** sales, collections, outstanding balances by age and customer statements, each
  as a screen, a PDF or an Excel file.

**Field sales (Flutter Android app)**
- Works fully offline. Bills, payments, shop visits, returns, new shops and rate-change requests go into a
  local outbox and sync when there is a signal.
- Syncing is idempotent: every record carries an id made on the phone, so a retried request never creates
  a second bill.
- The phone never sets a price. Prices come from the office, and changing a rate goes through an
  office-approved request.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | C#, .NET 10, ASP.NET Core Web API (controllers), Entity Framework Core |
| Database | Microsoft SQL Server |
| Auth | ASP.NET Core Identity: cookie sign-in for the web panel, bearer tokens for the phone, `Admin` / `Salesperson` roles |
| Documents | QuestPDF (invoices, reports), ClosedXML (Excel), MailKit (email) |
| Web client | React 19, TypeScript, Vite, Tailwind CSS v4, shadcn/ui, TanStack Query, React Router, Recharts |
| Mobile | Flutter / Dart (Android), Drift (SQLite), Riverpod, Dio |
| Tests | xUnit against a real SQL Server database per test class, `WebApplicationFactory` for HTTP and authorization tests, Flutter tests on an in-memory Drift database |

## Architecture

A **modular monolith**: one API, split into modules by business area.

```
src/
  GoldenPappadam.Domain/          entities and enums, no framework dependencies
  GoldenPappadam.Infrastructure/  EF Core DbContext, configurations, migrations
  GoldenPappadam.Api/Features/    one folder per module: Inventory, Sales, Payments,
                                  FieldSales, Mobile, Staff, Accounting, OwnShop,
                                  Reports, Dashboard, Auth (payments live in Sales)
tests/GoldenPappadam.Tests/       integration tests against SQL Server
client/                           React admin panel
mobile/                           Flutter salesperson app
docs/                             design documents, one per module
```

Design decisions worth a look:

- **Ledger, not counters.** Stock is the sum of immutable movements (sale, packing, transfer, damage,
  adjustment, and so on), each tied to a location such as the warehouse, a van or the shop. A van's
  end-of-day reconciliation is just the ledger's arithmetic, so a sale nobody recorded shows up as stock
  still on the van.
- **Transactions are never edited.** Invoices, payments and stock movements are corrected with new records:
  a cancellation, a reversal or an adjustment. `AppDbContext` enforces this on every save.
- **Closed by default.** The fallback authorization policy is Admin-only, so a new endpoint cannot be
  reached by the sales team until it is deliberately opened. The phone has its own `/api/mobile/*` surface.
- **One rule, one place.** Price resolution, the GST calculation, the packing conversion and the
  shop's price limits each live in a single service, used by both the web panel and the sync endpoint.
- **GUID keys everywhere**, so the phone can create records offline with no id-mapping layer.
- **The wire contract is tested:** the exact JSON the Flutter app sends is posted to the real API in
  `MobileContractTests`, so a field renamed on either side fails a test instead of failing in the field.

Each module was designed before it was built. The documents in [`docs/`](docs/) record the requirements,
the trade-offs and the open questions.

## Testing

- **353 C# tests**: business rules, concurrency (25 invoices finalized at once must get 25 consecutive
  numbers), authorization through real HTTP, and sync idempotency.
- **84 Flutter tests**: the offline outbox, sync retries and backoff, and screen flows.

```bash
dotnet test
cd mobile && flutter test
```

## Running locally

Requires the .NET 10 SDK, SQL Server (Express or LocalDB), Node.js, and Flutter for the mobile app.

```bash
# database
dotnet ef database update -p src/GoldenPappadam.Infrastructure -s src/GoldenPappadam.Api

# first admin account (stored in user-secrets, never in the repo)
dotnet user-secrets set "Bootstrap:AdminEmail" "you@example.com" --project src/GoldenPappadam.Api
dotnet user-secrets set "Bootstrap:AdminPassword" "<a strong password>" --project src/GoldenPappadam.Api

# API on http://localhost:5207
dotnet run --project src/GoldenPappadam.Api

# web client on http://localhost:5173
npm install --prefix client
npm run dev --prefix client

# mobile app (Android emulator)
cd mobile && flutter pub get && flutter run
```

The connection string is in `src/GoldenPappadam.Api/appsettings.json`; override it with the
`ConnectionStrings__GoldenPappadam` environment variable.

## Author

Built by Midhu, a full-stack .NET developer. <!-- TODO: your full name and LinkedIn / portfolio link -->
