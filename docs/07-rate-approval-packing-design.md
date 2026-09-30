# Rate-change approval and packing conversion - design

_Approved 2026-09-30. **Both parts built 2026-09-30.** Rules in `CLAUDE.md` §4 "Confirmed requirements
(2026-09-30)", §10 Q11-Q14 answered the same day. Two independent increments, built and committed
separately: **A. packing conversion** first (smaller, office only), then **B. rate-change approval**
(server, office and phone)._

---

## A. Packing conversion

### A1. What changes in the model

Today a packed product carries one figure, `SourceQuantityPerPack`, in the source product's unit - and
the office types the loose quantity used on every packing. The 20-piece packet is set to 0.250 kg, which
at 200 pieces/kg is 2.5 times too much.

| Where | Change |
|---|---|
| `Product.PiecesPerKg` (new, `decimal(18,3)` null) | Loose products counted in kg: the variety's average. Standard 4-inch = 200; a 4.5-inch variety gets its own. Required before a count-based packet can be packed from it. |
| `Product.PiecesPerPack` (new, `int` null) | Count-based packets packed from a loose-kg product: 20, 6, 10 ... |
| `Product.SourceQuantityPerPack` (kept) | Every other packed product: a **weight-based** packet from loose kg (0.250 for 250 g), packets per box (12), pieces when the loose is counted in pieces. |

A packed product has exactly one of `PiecesPerPack` / `SourceQuantityPerPack` - the check constraint
changes from "`SourceQuantityPerPack > 0`" to "exactly one of the two is set and > 0". `PiecesPerPack` is
only allowed when the source is loose and counted in kg (validated in `ProductService`).

**One conversion, one place.** New `PackConversion.SourcePerPack(packed, source)` returns how much source
stock one pack consumes: `PiecesPerPack ÷ source.PiecesPerKg` for a count-based packet, else
`SourceQuantityPerPack`. Packing, repacking (its packed-from chain in `RepackingService.Root`) and the
product screen all call it, so 200 appears nowhere in code - only as data on the loose product. Stored kg
is rounded to 3 decimals (1 g), as every quantity already is: 5,000 pieces ÷ 200 = 25.000 kg exactly; a
170 pieces/kg variety would round each packing to the gram.

### A2. Packing

`POST /api/inventory/packing/preview` runs the same plan the save runs, so the screen shows what will be
recorded. `CreatePackingRequest` loses `SourceQuantityUsed`: the server calculates it (owner, Q11 - for now). In
one transaction, as today, plus two guards:

1. **Lock the source product row** (`UPDLOCK, HOLDLOCK` on `inventory.Products`, the same technique as
   invoice numbering), so two packings of the same loose variety cannot both pass the stock check.
2. **Refuse a shortfall** - source stock in the warehouse must cover the packing, or nothing is written:
   _"Packing 150 × 20-piece packets needs 3,000 pieces (15 kg); the warehouse has 2,000 pieces (10 kg) of
   Loose pappadam (standard)."_ Applied to every packing, including boxes packed from packets, so no packing
   ever takes stock below zero. Sales, van loads, damage and adjustments still warn, not block.
3. **Packed once:** the screen sends a `ClientRequestId` made when it opens; a filtered unique index on
   `PackingEntries.ClientRequestId` makes a double submit return the first entry instead of packing twice.

**Snapshot on the entry** (new nullable columns on `inventory.PackingEntries`): `PiecesPerPack`,
`PiecesPerKg`, `SourcePerPack` (the kg - or packets - one pack used), `SourceOnHandBefore`. "After" is
before − used, derived rather than stored. The existing 5 entries keep nulls: the history shows "-" for
what they never recorded, and their quantities stay as recorded.

### A3. Migration `AddPackingConversion`

Adds the columns and index, and backfills **only what the owner confirmed**: `PiecesPerKg = 200` on the one
loose product counted in kg (the standard pappadam). It does **not** rewrite the packed products, because
their pieces cannot be derived from the wrong kg figure (0.250 × 200 = 50, not 20). Straight after
deploying, the office sets the 20-piece and 6-piece packets to "20 pieces" / "6 pieces" on the product
screen - an audited edit, visible in history, rather than a hidden data fix. Until then they keep packing
at their old kg figure. (A flag on the product list was dropped when built: a real 250 g packet looks the same
as a count-based one not yet corrected, so it would have warned about correct products.)
Stock, stock age and past packing entries are untouched.

### A4. Screens (admin)

- **Product dialog:** a loose kg product gets "Pieces per kg" (prefilled 200 for a new one). A packed
  product from loose kg gets "Packet holds: [n] pieces | [w] kg", showing the conversion beside it
  ("20 pieces = 0.100 kg"). From another packet: "Packets per box", as now.
- **Packing:** the "source used" field goes. A worked panel replaces it: available (kg and pieces) →
  consumed (pieces → kg) → after, updating as packets are typed; Save disabled with the shortfall message
  when stock does not cover it. History gains before / used / after and the conversion.

### A5. Tests

Count-based and weight-based calculation (the owner's examples: 100 × 20 from 50 kg → 40 kg; 100 × 250 g
→ 25 kg; 250 × 20 → 25 kg); a variety with its own pieces per kg; shortfall refused with nothing written;
two concurrent packings of the last stock - one wins; double submit packs once; snapshot unaffected by a
later change to pieces per kg; repacking through the new conversion (5 × 20-piece → 16 × 6-piece, 4 pieces
back to loose as 0.020 kg); box of 12 unchanged; product validation (exactly one content figure, pieces
only from loose kg).

---

## B. Rate-change approval

### B1. Model

New `sales.CustomerRateRequests` (`AuditableEntity`; `AppDbContext` allows only the decision fields to
change after creation, like return notes):

| Column | |
|---|---|
| `Id` | made on the phone, like customers - so the phone can cancel it later |
| `CustomerId`, `ProductId` | |
| `PriceWhenRequested` | the server's agreed rate when the request arrives; null = standard price |
| `RequestedPrice` | > 0 |
| `RequestedAt` | when the salesman asked (the phone's time); `CreatedAt`/`CreatedBy` = received, and who |
| `Reason` | salesman's note, optional |
| `Status` | Pending, Approved, Rejected, Cancelled (string) |
| `DecidedBy`, `DecidedAt`, `DecisionNote` | admin (or the salesman, for his own cancel); note optional |
| `ReplacedById` | set when a newer request replaced this pending one |

Filtered unique index: **one Pending request per customer and product**. `CustomerPriceChanges` gains a
nullable `RateRequestId`, so an approved change names its request (and through it the salesman).

### B2. Where the rule is enforced

The salesman's only way in is `/api/mobile/*`; every office price endpoint is already admin-only by the
fallback policy. On top of that, **in the services**:

- `ICurrentUser` gains `IsInRole`. `CustomerPriceService.SetAsync` (the office path) **refuses a
  salesperson** outright - so no current or future endpoint or sync type can reach it for them.
- `CustomerPriceService.SetInitialRatesAsync(customerId, rates)` is the onboarding path, open to a
  salesperson only for a customer **with no rate history at all** (no `CustomerPriceChanges` row) - i.e.
  one being created right now. It runs in the same transaction as the customer's creation.
- Approval goes through one internal method that sets the rate and records the change with
  `RateRequestId`.

### B3. Sync (phone ↔ server)

| Submission | Change |
|---|---|
| `Customer` | gains `initialRates: [{productId, unitPrice}]`, honoured **only when it creates the customer** (customer + rates in one transaction). An edit carrying rates is refused. |
| `RateRequest` (new) | `{id, customerId, productId, requestedPrice, reason}` → a Pending request; replaces a pending one for the same customer and product (old one → Cancelled, `ReplacedById`). |
| `RateRequestCancel` (new) | `{id}` → Cancelled, only the salesman's own and only while Pending. |
| `CustomerPrice` (existing) | from a phone it **no longer changes a rate**: it becomes a Pending request, so an old app version can neither bypass the rule nor lose what the salesman asked for. |

The snapshot gains `rateRequests`: the salesman's own requests - pending, and decided in the last 30 days
- with status and the office's note. The phone never applies a request to its price cache; it bills at the
current rate until the snapshot brings the approved one.

### B4. Phone

- **New shop:** unchanged to the salesman - the rate cards on the form now travel inside the `Customer`
  submission instead of as separate rates.
- **Shop page, "What this shop pays":** a pending request shows beside its rate ("₹38 requested - with the
  office"), a rejection for a while with the office's note. The Rates screen becomes **Request rate
  changes**, the same product cards (the shared `ProductLineCard`), saving requests; a pending request can
  be cancelled.
- Drift schema v5: a `RateRequests` cache table, filled by the snapshot, plus the outbox as ever.

### B5. Office screens

- **Rate requests** (Sales group, with a count of pending): customer, product, current rate - live, and
  what it was when asked if different - requested rate, salesman, when, reason; **Approve** / **Reject**
  with an optional note. History tab with filters.
- **Customer page price card:** a pending request shows on its product row, with the same two buttons.
- **Today on the road:** kept as "Rates set by the sales team" - a new shop's first rates are still set
  from the phone - with a link to Rate requests for everything else.
- Office rate edits stay direct, as now.

### B6. Tests

Salesperson refused by `SetAsync` whatever the path; onboarding rates accepted with a new customer and
refused once it has rate history; `CustomerPrice` from a phone becomes a request and leaves the rate alone;
approve changes the rate, records the change with the admin and the request, applies from the next bill;
reject leaves it; newer request replaces the pending one; own cancel only while pending; idempotent retries
of each submission; the exact JSON in `MobileContractTests`; salesperson 403 on the office endpoints; Dart
tests for the payloads, the pending display and the request screen.

---

## Not in this change

Recording actual packing loss (Q11 "for now"); branch-specific rates; an admin "approve at a different
rate" (reject, then set it directly); notifying the phone other than through the next snapshot.
