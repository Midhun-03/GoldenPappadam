# Phase 3 — salesperson app and field sales: audit and design proposal

Status: **approved 2026-09-15, in progress.** M1, M2 and M2b are built; the milestone table in Part C says
where things stand. Seven business decisions were taken on 2026-09-15 and are recorded in Part E.

---

## Part A — audit of what exists today

### A1. Solution shape

```
GoldenPappadam.sln
  src/GoldenPappadam.Domain          entities only, no dependencies
  src/GoldenPappadam.Infrastructure  AppDbContext, EF configurations, migrations, Identity
  src/GoldenPappadam.Api             controllers + feature services (Features/<Module>/<Feature>)
  tests/GoldenPappadam.Tests         40 tests, a real SQL Express database per test class
  client/                            React 19 + Vite + Tailwind v4 + shadcn/ui + TanStack Query
```

Two schemas hold the business data (`inventory`, `sales`) and one holds Identity (`identity`).

### A2. What Phase 3 can reuse as-is

This is the important finding: **most of the salesperson's work is already implemented on the server.**

| Existing piece | What it already does | Phase 3 use |
|---|---|---|
| `InvoiceService.CreateAsync` | validates the customer, prices each line from the product (overridable), rounds, applies a bill discount, allocates an FY invoice number with a retry on collision, writes `Sale` stock movements — all in one transaction — and returns negative-stock warnings | a mobile sale is **this method**, called with a smaller DTO |
| `PaymentService.CreateAsync` | records money, allocates oldest-bill-first or to bills you name, leaves the remainder as money on account, returns the new balance | a mobile payment is **this method** |
| `PaymentService.GetBalanceAsync` | opening balance + issued bills − payments | the balance the salesperson sees |
| `CustomerService.GetLedgerAsync` / `GetOutstandingInvoicesAsync` | statement and open bills | the shop detail screen in the app |
| `StockService` | stock is the **sum of the movement ledger**, nothing caches it; shortfalls warn, never block | the ledger shape carries straight over; it gains a location (B2.1) and van sales are `Sale` movements at the van |
| `Entity` / `AuditableEntity` + `AppDbContext.ApplyAuditRules` | stamps `CreatedAt`/`CreatedBy`, refuses edits and deletes of immutable rows | `CreatedBy` on an invoice **already records which salesperson made it** — no new column needed |
| `DomainException` / `NotFoundException` + `DomainExceptionHandler` | business failures become RFC problem details (400/404) | mobile error messages come out correct for free |
| `IndiaTime` | one place that turns UTC into the IST business day | "today's sales" on the admin screen |
| ASP.NET Core Identity, `Guid` keys, `.AddRoles<IdentityRole<Guid>>()` | **already registered**; the role tables exist and are simply unused | a salesperson role costs a seeder, not a redesign |
| `TestDatabase` | throwaway SQL Express database per test class | sync and pricing tests run against real SQL Server |
| `client/src/lib/api.ts`, TanStack Query, `AppLayout` nav groups | typed fetch layer, problem-detail handling, grouped sidebar | the admin field-sales screens slot straight in |

Conventions worth repeating in new code: controllers stay thin, rules live in a small feature service, everything is validated **before** a tracked entity is mutated, list projections live in a `*Queries` class, and request DTO records put validation attributes on the constructor parameter (not `[property: ...]`, which throws on .NET 10).

### A3. Gaps Phase 3 has to fill

1. **No customer-specific pricing.** `Product.SellingPrice` is the only price; `InvoiceLine.UnitPrice` can be overridden freely per bill. §10 question 1 of CLAUDE.md was still open — your brief now answers it.
2. **No roles.** The fallback policy is `RequireAuthenticatedUser()`; every signed-in user can do everything, including `/api/admin/users`. A salesperson account created today would be a full admin.
3. **Cookie authentication only.** `goldenpappadam.auth`, 7-day sliding. Usable from Dart but awkward; there is no bearer/refresh-token path.
4. **No idempotency.** Post the same invoice twice today and you get two bills, two invoice numbers and two sets of stock movements.
5. **No visit record.** There is nowhere to say "visited the shop, took nothing".
6. **No device or origin metadata** on a transaction.
7. **No stock location.** Stock is one global figure per product, so there is nowhere for van stock to live. This is the gap the van-load decision opens (B2.1).
8. **No returns/replacement workflow** — deliberately, and it stays deliberate until Part E Q5 is answered.

---

## Part B — Phase 3 design proposal

> **Decisions taken 2026-09-15.** Price fallback: a shop with no price row pays `Product.SellingPrice`.
> Offline price change: save at the device's price and flag the mismatch (B6).
> Salesperson accounts: one per person from the start.
> **Van stock: stock leaves the warehouse when the van is loaded** — this is the one answer that changes
> the architecture, and B2.1 is its design.

### B1. Principle

The mobile app is a **cache plus an outbox**. It never computes anything that matters. It does not total a customer's balance, does not decide a price, does not allocate a payment to bills. It shows the last figures the server gave it, collects what the salesperson did, and hands that to the server, which runs the same `InvoiceService` and `PaymentService` the admin panel runs. One source of truth, one copy of the business rules.

### B2. Database changes

Six new tables, and **one column added to an existing table** — `StockMovement.LocationId`, forced by the
van-stock decision. Everything else in Phase 1 is untouched.

#### `sales.CustomerPrices` — `AuditableEntity`

| Column | Type | Notes |
|---|---|---|
| Id | uniqueidentifier | |
| CustomerId | uniqueidentifier | FK, restrict |
| ProductId | uniqueidentifier | FK, restrict |
| UnitPrice | decimal(18,2) | what this shop pays |
| IsActive | bit | deactivated, never deleted |

Unique index on `(CustomerId, ProductId)`. Admin-maintained only.

Price precedence when a bill line is built becomes: **explicit override (admin screens only) → customer price → `Product.SellingPrice` → error**. That is one change inside `InvoiceService.BuildLinesAsync`; behaviour is unchanged for customers with no price row (confirmed 2026-09-15: they simply pay the product price), so the current 40 tests stay green.

### B2.1. Stock locations — the van-load decision

Today `StockMovement` is `(ProductId, Quantity)` and current stock is one global sum per product. Selling
from the van means there are two answers to "how much 20-piece packet is there" — in the warehouse and in
the van — so the ledger has to say **where**.

#### `inventory.StockLocations` — `AuditableEntity`

| Column | Notes |
|---|---|
| Code, Name | `MAIN`, `VAN-1` |
| Kind | `Warehouse` or `Van`, stored as text, extensible |
| IsActive | deactivated, never deleted |

One `MAIN` warehouse and one `VAN-1` are seeded. More vans later cost a row, not a redesign.

#### `inventory.StockMovements.LocationId` — the one Phase 1 column change

Added nullable, backfilled to `MAIN` for every existing row, then made required. A standard three-step
migration; no existing number changes, because everything that has happened so far happened at the warehouse.

Current stock becomes **stock per product per location**, and the sum across locations is what the business
owns. Both figures are useful and the screens must be explicit about which one they show (B2.2).

#### `fieldsales.VanLoads` — `Entity` (immutable), with lines

A morning loading: which van, which products, how many. Each line writes **two movements** — negative at
`MAIN`, positive at the van — with movement type `Transfer`, the same shape `PackingEntry` already uses for
its pair of movements. The evening return is another `VanLoads` row in the opposite direction, so unsold
stock goes back and the van's balance returns to zero.

Confirmed 2026-09-15:

- **The admin records the load**, in the panel, before the van leaves. The salesperson still never moves
  stock, so the permission rule in B4 survives intact and the phone needs no stock-writing endpoint.
- **The van empties every evening.** Unsold stock returns to `MAIN`, so the van balance is always either
  today's load or nothing, and reconciliation is a daily question rather than a periodic count.
- **A discrepancy is shown, never auto-corrected.** When loaded − sold − returned is not zero, the
  reconciliation screen flags it and the admin decides what it was — damage, a missed sale, a miscount —
  and records the matching movement. This is the Phase 1 rule that corrections are deliberate new records,
  and it is what stops a forgotten sale from quietly becoming shrinkage. It is also the mechanism that
  catches the failure this whole phase exists to prevent.

New movement type: `Transfer`. New reference type: `VanLoad`. Both enums are already stored as text and
designed to be extended, so this is additive.

#### Where a sale takes its stock from

`Devices.LocationId` says which van the phone rides in. A sale submitted from the mobile app writes its
`Sale` movements **at that van**; a bill created in the admin panel writes them at `MAIN`, exactly as today.
The invoice itself gains no column — the location belongs on the movement, not on the bill.

If the salesperson sells something that was never loaded, the van goes negative. That **warns and does not
block**, which is the rule the inventory module has had since Phase 1 and is exactly right here: the packet
physically left the van whatever the system thinks.

#### What this buys and what it costs

It buys a real answer to "what is still on the van at 4 pm", a morning load sheet, and an evening
reconciliation that shows loaded − sold − returned = 0 or a discrepancy worth asking about. It costs a
migration on the ledger table, a decision on every stock screen about which location it means, and roughly
one extra milestone. It is the correct long-term design — vans and godowns are locations in every inventory
system — but it is the single largest change in this plan and the only one that reaches into Phase 1.

### B2.2. Existing screens after locations

| Screen | Shows |
|---|---|
| Stock on hand | `MAIN` by default, with a location selector; low-stock alerts stay on `MAIN`, because that is what packing draws from |
| Stock history | movements at the selected location, with the location shown on each row |
| Packing | consumes and produces at `MAIN` only |
| Dashboard "stock needing attention" | `MAIN`, unchanged in meaning |
| New: Van stock | what is on the van right now |

#### `fieldsales.Devices` — `AuditableEntity`

Id, `UserId` (the salesperson), `Name`, `Platform`, `LocationId` (the van this phone rides in — see B2.1), `FirstSeenAt`, `LastSeenAt`, `IsActive`. Answers "which phone sent this" and lets you cut off a lost phone.

#### `fieldsales.ShopVisits` — `Entity` (immutable)

| Column | Notes |
|---|---|
| CustomerId | the shop |
| DeviceId | which phone |
| VisitedAt | UTC, **as recorded on the device** |
| Outcome | `Sold`, `NoOrder`, `Closed`, `Skipped` — stored as text, extensible |
| InvoiceId | nullable FK — the bill this visit produced |
| PaymentId | nullable FK — money collected during this visit |
| Notes | nullable |

`CreatedBy` already carries the salesperson. The FK direction matters: the new module points at `sales`, never the other way round, so `sales` stays independent of it.

#### `fieldsales.SyncSubmissions` — `Entity` (immutable)

The whole idempotency mechanism.

| Column | Notes |
|---|---|
| ClientRequestId | uniqueidentifier, **unique index** — generated on the phone, once, per transaction |
| DeviceId | |
| SubmissionType | `Invoice`, `Payment`, `Visit` (extensible) |
| RecordedAt | UTC, when the salesperson entered it on the phone |
| ReceivedAt | UTC, when it reached the server |
| CreatedRecordId | the invoice / payment / visit that was created |
| PriceMismatch | bit — see B6 |

One table rather than three sets of columns on `Invoices` and `Payments`: the sales tables stay exactly as Phase 1 designed them, and visits — and later returns — get the same guarantee without repeating the pattern.

### B3. Authentication

Add ASP.NET Core Identity's **bearer token scheme** alongside the existing cookie:

```csharp
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies()
    .AddBearerToken(IdentityConstants.BearerScheme);   // new
```

`POST /api/auth/mobile/login` calls the same `SignInManager` with the bearer scheme and returns an access token (about an hour) plus a refresh token (about two weeks); `POST /api/auth/mobile/refresh` renews it. No hand-rolled JWT code, no second identity store, and the React app is untouched — it keeps its cookie.

The fallback policy then accepts either scheme:

```csharp
new AuthorizationPolicyBuilder(IdentityConstants.ApplicationScheme, IdentityConstants.BearerScheme)
    .RequireAuthenticatedUser()
    .RequireRole(Roles.Admin)      // default: admin only
    .Build()
```

### B4. Permissions

Two roles, seeded at start-up: `Admin` and `Salesperson`. A one-time seeder gives every existing user `Admin`, so nothing that works today stops working.

- **Default policy = Admin.** Every existing controller becomes admin-only without touching a single controller file.
- Mobile controllers carry `[Authorize(Roles = Roles.Salesperson + "," + Roles.Admin)]` explicitly, so an admin can always test the mobile endpoints.
- The salesperson's endpoints are a **separate, small surface** (`/api/mobile/...`). The enforcement is not "hide the button": the salesperson has no endpoint that can set a price, touch stock, edit a customer, or cancel a bill.
- The mobile create-sale DTO has **no price field at all**. There is nothing to send, so there is nothing to validate away.

### B5. APIs

| Endpoint | Purpose |
|---|---|
| `POST /api/auth/mobile/login`, `/refresh` | bearer tokens |
| `POST /api/mobile/devices/register` | register or refresh this phone; returns its `DeviceId` |
| `GET /api/mobile/sync/snapshot` | everything needed to work offline: active customers with balance and outstanding-bill summary, sellable products with their unit, the customer price list, payment methods, `serverTime`, `pricesAsOf` |
| `POST /api/mobile/sync/submissions` | **batch** upload; per-item result, partial success |
| `GET /api/mobile/day` | the salesperson's own day: totals and the sales they made |

The snapshot is a full snapshot, not a delta. There are tens of customers and tens of products; a delta protocol would be more moving parts than the payload is worth. If it ever grows, a watermark parameter can be added without changing the shape.

The submission batch returns, per item:

| Result | Meaning | The phone then |
|---|---|---|
| `Accepted` | created; the server record id is returned | marks it **Synced** |
| `AlreadyAccepted` | that `ClientRequestId` already exists; the original id is returned | marks it **Synced** |
| `Rejected` | a business rule refused it (inactive customer, unknown product), with the problem-detail message | marks it **Failed**, shows the salesperson, keeps the row |

Anything else — network dropped, 500, timeout — is not an answer at all: the row stays **Pending** and is retried.

### B6. The offline price-change question (§3 of your brief)

Phase 1 already has a rule and it should be preserved: **`InvoiceLine.UnitPrice` is the price actually charged, frozen on the line, and a transaction is never edited.** The sale on the road is a real transaction that already happened at ₹35.

Proposed rule, needs your confirmation (Part E Q3):

1. The phone sends the price it used and the timestamp of the price list it used (`pricesAsOf`).
2. The server **saves the sale at the device's price**. It does not rewrite history.
3. If that customer's price changed after `pricesAsOf`, the server sets `PriceMismatch` on the submission and the bill appears flagged on the admin day screen: *"charged ₹35, current price ₹36"*.
4. You decide per bill: leave it, or cancel and re-bill at the new price — both already exist in Phase 1.

This is deliberately small: one timestamp comparison, no price-history table, and the policy can be swapped later (reject instead of flag, or re-price instead of flag) by changing one method.

A guard against a tampered client: the accepted price must match either the customer's current price or `Product.SellingPrice`, or else be explainable by a stale snapshot. The salesperson cannot invent an arbitrary number even by editing the app, because the server only accepts a price it can recognise.

### B7. Flutter architecture

```
mobile/
  lib/
    core/        config, result types, formatting (IST, rupees)
    data/
      local/     Drift database: customers, products, prices, outbox, meta
      remote/    Dio client, auth interceptor, API DTOs
      repos/     one repository per concern; the only thing the UI talks to
    sync/        SyncEngine (snapshot pull + outbox push), connectivity watcher
    features/
      auth/  shops/  sale/  payment/  day/
```

Packages — one per job, each earning its place:

| Package | Why this one |
|---|---|
| `drift` | typed SQL over SQLite with real migrations; raw SQLite would mean hand-written SQL strings. This is the one local-database choice and there will be no second |
| `dio` | interceptors put token refresh and retry in one place |
| `connectivity_plus` | connectivity change events to trigger sync |
| `flutter_secure_storage` | tokens in the Android keystore, never in shared preferences |
| `riverpod` | state; small, testable, no code generation required |
| `uuid` | the client request id |

iOS is not a target now, but every one of those supports it and nothing here is Android-specific, so iOS later is a build configuration rather than a rewrite.

### B8. Local database

| Table | Kind | Contents |
|---|---|---|
| `customers`, `products`, `customer_prices` | server cache | overwritten wholesale on each snapshot |
| `meta` | local | last sync time, `pricesAsOf`, device id, salesperson |
| `outbox` | **local truth** | one row per thing the salesperson did |

An `outbox` row: `clientRequestId` (generated once, never regenerated), `type`, `payloadJson`, `recordedAt`, `status` (`Pending` / `Syncing` / `Synced` / `Failed`), `attemptCount`, `nextAttemptAt`, `lastError`, `serverRecordId`.

The row is written **before** the UI says "saved", and nothing deletes it. `Synced` rows are pruned only after a later snapshot confirms them, and even then only after 30 days.

### B9. Sync engine

- **Triggers:** app start, connectivity regained, every few minutes while online, after every save, and the manual **Sync now** button.
- **One at a time:** a single worker holding a lock, so a manual tap during an automatic run cannot send the same row twice.
- **Order:** push the outbox first, oldest first — a payment must not land before the bill it pays — then pull the snapshot, so balances reflect what was just sent.
- **Backoff:** 5 s, 15 s, 1 min, 5 min, 15 min, then every 15 min. `Failed` rows (a business rejection) are not retried automatically; they wait for the salesperson.
- **Status is always visible:** a small bar — "All synced, 2 min ago" / "3 waiting" / "1 failed, tap to see".

### B10. Conflict handling

There is very little to handle, by design:

| Situation | Resolution |
|---|---|
| Master data changed on the server | the server wins, always; the phone's copy is a cache |
| Two devices sell to the same shop | no conflict — both are appended; the balance is recomputed server-side |
| The balance is stale on the phone | shown with "as of 10:42" and recomputed on every sync; the phone never writes a balance |
| The same sale sent twice | `ClientRequestId` unique index; the second attempt returns the first result |
| A price changed while the phone was offline | B6 |
| Stock went negative | warns, exactly as Phase 1 already does; it never blocks a delivery that physically happened |

### B11. Admin panel changes

A new sidebar group **Field sales**, additive only:

- **Today** — total sales, shops visited, transactions, cash collected, credit sales, outstanding generated today; then the table you specified: Time | Shop | Products | Amount | Payment | Salesperson | Sync status. Sync status shows *when the phone recorded it* against *when the server received it*, which is what makes the offline behaviour legible: "recorded 11:20, received 14:05 — the phone was out of signal".
- **Visits** — including shops visited with no sale, a number that does not exist today.
- **Customer prices** — a tab on the existing customer page, admin-only, the only place a price can be set.

Everything uses the existing `lib/api.ts`, TanStack Query, `PageHeader`, `FilterBar`, `TableSkeleton` and `EmptyState`, and the day screen follows the dashboard's IST business-day convention.

---

## Part C — milestones

Each milestone ends with a build, the full test suite green, and the admin panel still working. Nothing is merged half-done.

| # | Milestone | Deliverable |
|---|---|---|
| M0 | Environment | Flutter SDK and Android toolchain installed (**not present on this machine**), API reachable from the phone over the LAN |
| M1 | Roles and bearer auth (done) | `Admin`/`Salesperson` roles, seeder, default policy = admin, bearer scheme, mobile login and refresh. Tests: a salesperson token gets 403 on every admin endpoint |
| M2 | Customer pricing (done) | `sales.CustomerPrices`, price precedence in `InvoiceService`, admin price screen. Tests: two shops, two prices, one product |
| M2b | **Stock locations** (done) | `inventory.StockLocations`, the `LocationId` migration and backfill, `Transfer` movements, `fieldsales.VanLoads`, admin load and evening-return screens, the day's reconciliation with its discrepancy line, location selector on the stock screens. Tests: loading moves stock between locations; existing stock figures are unchanged after the backfill |
| M3 | Sync foundation | `fieldsales` schema, devices, submissions, snapshot endpoint, batch submission endpoint with idempotency. Tests: the same `ClientRequestId` twice creates one bill |
| M4 | Flutter foundation | project, Drift schema, Dio client with token refresh, login, connectivity, an empty sync engine |
| M5 | Shops | shop list, search, shop detail with balance and applicable prices, all from the local database |
| M6 | Sale entry | shop → products → quantity → total → credit/paid → save, written to the outbox |
| M7 | Offline and sync | the full sync engine, status UI, retry, manual sync; the offline scenarios in your §23 |
| M8 | Payments | collecting against old outstanding, separate from today's sale |
| M9 | Admin field-sales section | Today screen, visits, sync visibility |
| M10 | Field testing | a real route, on a real phone, with real shops |
| M11 | Returns and replacement | **only after Part E Q5 is answered** |

---

## Part D — what Phase 1 has to change

A short list, and deliberately so:

| Change | Risk |
|---|---|
| `Program.cs`: bearer scheme, and the default policy becomes admin-only | low; one place, covered by a test per existing controller group |
| Role seeder assigns `Admin` to existing users | must run before the policy change takes effect, or you are locked out — so it is the same start-up step |
| `InvoiceService.BuildLinesAsync`: customer price joins the precedence chain | low; no behaviour change when no price row exists |
| `Schemas.cs`: add `fieldsales` | none |
| **`StockMovement.LocationId`** + backfill to `MAIN` | **the real one.** It touches the ledger table every stock figure is derived from. Mitigated by: nullable → backfill → required in one migration, and a test that asserts every existing stock figure is identical before and after |
| **`StockService` queries gain a location filter** | medium; `GetQuantityOnHandAsync`, `GetOnHandAsync` and `GetMovementsAsync` all need to say which location they mean. Existing callers default to `MAIN`, which preserves today's behaviour |
| **`InvoiceService` writes `Sale` movements at a location** | low; defaults to `MAIN`, the mobile path passes the van |
| Stock and packing screens gain a location selector | low; additive, default `MAIN` |
| React client, elsewhere | additive only; no existing screen changes |

Not changing: every entity except `StockMovement`, invoice numbering, payment allocation, the packing
workflow, the customer ledger, the dashboard's meaning.

---

## Part E — decisions

### Answered 2026-09-15

| # | Question | Answer |
|---|---|---|
| 1 | Customer prices, and the fallback for a shop with no price row | Confirmed as a real business rule; closes CLAUDE.md §10 Q1. No price row → `Product.SellingPrice` |
| 3 | Offline price change | Save at the device's price and flag the mismatch (B6) |
| 6 | Van stock | **Stock leaves the warehouse at van loading.** See B2.1 |
| 7 | Salesperson accounts | One account per person from the start |
| 9 | Who records the van load | The admin, in the panel, before the van leaves |
| 10 | Evening return | Unsold stock returns to `MAIN` every evening; the van goes to zero |
| 11 | Load-sheet reconciliation | Show the discrepancy to the admin; never auto-adjust |

Nothing blocks the start of work.

### Still open — can be answered when their milestone arrives

| # | Question | Blocks |
|---|---|---|
| 2 | **Bill discount on the road** — Phase 1 has a bill-level discount. May the salesperson apply one, or is the price the whole story? | M6 |
| 4 | **Cash handling** — the salesperson collects cash all day. Should the app record a day-end cash handover to the office, or is "payments recorded" enough for now? | M8 |
| 5 | **Expired returns** — the blocker for M11. Four parts: (a) is the replacement free, or is the old stock credited to the account? (b) do returned packets go back into sellable stock, or into a separate written-off bucket? (c) is there a limit or a time window? (d) does the shop's bill change, or is the replacement a separate zero-value document? | M11 |
| 8 | **Phone reaching the server** — during testing the phone needs the API over the LAN or a tunnel. Is the API going to be hosted somewhere, or is this LAN-only for now? | M0 |
