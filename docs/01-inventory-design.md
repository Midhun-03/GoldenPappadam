# Inventory module — design proposal

_Status: APPROVED 2026-09-14, amended the same day with the owner's answers in §7._
_Covers plan steps 3, 4 and 5 (inventory module, products and product types, stock-movement logic)._

## 1. What this design has to satisfy

- Loose/bulk stock and packaged stock, tracked separately per variety.
- Different units per product (kg, pieces, packets). No assumption that all products share a unit.
- Packing converts stock of one product into another: normally loose into packets, and occasionally packets into a larger pack such as a box of 12. Recorded with history, with actual quantities that may differ from the theoretical ones (packing loss).
- Every stock change is a movement with a reason; current stock is reconcilable with that history.
- New product types must be addable without redesigning the schema.
- Nothing built for unconfirmed business rules (pricing per shop, returns, discounts, GST). See §7.

## 2. Tables

All tables live in the SQL Server schema `inventory` and follow the conventions in `CLAUDE.md` §6
(`Guid Id`, audit fields from `Entity` / `AuditableEntity`, `decimal(18,2)` money, `decimal(18,3)` quantities, enums stored as strings).

### `inventory.ProductCategories` — `AuditableEntity`

| Column | Type | Notes |
|---|---|---|
| Name | nvarchar(100), unique | e.g. "Pappadam", "Special" |
| IsActive | bit | deactivate, never delete |

### `inventory.UnitOfMeasures` — `AuditableEntity`

| Column | Type | Notes |
|---|---|---|
| Code | nvarchar(10), unique | KG, PCS, PKT |
| Name | nvarchar(50) | Kilogram, Piece, Packet |
| IsActive | bit | |

A table rather than a C# enum, so new units can be added by an admin without a code change and migration.
There is deliberately **no unit-conversion engine** (no "1 kg = 1000 g" matrix). Conversion happens in exactly one
place — packing — and is stored on the packed product (below).

### `inventory.Products` — `AuditableEntity`

One table for both loose and packaged products. A single table means sales lines, stock movements and reports all
point at one `ProductId`, and a new product type is a new row, not a new table.

| Column | Type | Notes |
|---|---|---|
| ProductCode | nvarchar(30), unique | human-facing code/SKU |
| Name | nvarchar(150) | Malayalam text supported (`nvarchar`) |
| CategoryId | FK to ProductCategories | |
| Kind | string enum: `Loose`, `Packed` | |
| UnitOfMeasureId | FK to UnitOfMeasures | the unit this product's stock is counted in |
| SourceProductId | FK to Products, null | `Packed` only: the product it is packed from — usually a loose product, occasionally another packed product (a box of 12 packets) |
| SourceQuantityPerPack | decimal(18,3), null | `Packed` only: source stock consumed by one pack, **in the source product's unit** (0.250 when the source is loose kg; 20 when it is loose pieces; 12 when the source is packets) |
| SellingPrice | decimal(18,2), null | default price; null = not normally sold as-is |
| LowStockThreshold | decimal(18,3), null | null = no alert for this product |
| IsActive | bit | |

**Validation rules (enforced in code):**

- `Kind = Packed` requires `SourceProductId` and `SourceQuantityPerPack`; `SourceQuantityPerPack > 0`. The source
  may be a loose product or another packed product.
- `Kind = Loose` requires both of those columns to be null.
- A product may not be its own source, directly or through a chain of sources (no cycles). Checked when a product is
  created or edited; these chains are one or two levels deep in practice.
- Several packed products may share one source, which is the normal case: 6-piece, 20-piece and 25-piece packets all
  packed from the same loose variety.
- `Kind` cannot be changed after creation.
- A product that already has stock movements cannot change its `UnitOfMeasureId`.

**Why a self-reference instead of separate loose and packed tables:** separate tables would duplicate every common
column and force sales lines and the stock ledger to carry two nullable foreign keys. One table with a `Kind`
discriminator keeps every query simple. The cost is two nullable columns on loose rows, which is acceptable.

### `inventory.StockMovements` — `Entity` (immutable, never edited or deleted)

| Column | Type | Notes |
|---|---|---|
| ProductId | FK to Products | |
| MovementType | string enum | see table below |
| Quantity | decimal(18,3), not 0 | **signed**: positive adds stock, negative removes it |
| OccurredAt | datetime2 (UTC) | business time of the movement; may differ from `CreatedAt` |
| ReferenceType | string enum, null | `PackingEntry`, `Invoice`, ... |
| ReferenceId | Guid, null | id of that record |
| Notes | nvarchar(300), null | reason for damage or adjustment |

Indexes: `(ProductId, OccurredAt)` and `(ReferenceType, ReferenceId)`.

| MovementType | Sign | Created by |
|---|---|---|
| `Opening` | + | one-time opening stock entry |
| `Production` | + | loose stock produced (phase 1: a manual entry) |
| `Packing` | − and + | packing: one negative row for loose, one positive row for packs |
| `Sale` | − | saving an invoice |
| `SaleReversal` | + | cancelling an invoice |
| `Damage` | − | manual entry |
| `Adjustment` | ± | manual stock correction after a physical count |

New types (returns, production batches) are added to the enum later without touching the table.

**Why `ReferenceType` + `ReferenceId` instead of a real foreign key per document type:** a column per module
(`InvoiceId`, `PackingEntryId`, `ProductionBatchId`, `StockReturnId`, ...) grows forever and makes the inventory
module depend on every other module. A type and id pair keeps the inventory module self-contained, which is the
point of a modular monolith. The trade-off is honest: the database cannot enforce that reference, so the code must.
Since transactions are never deleted, dangling references should not occur.

### `inventory.PackingEntries` — `Entity` (immutable)

Records one packing operation: source stock consumed, packs produced.

| Column | Type | Notes |
|---|---|---|
| PackedProductId | FK to Products | must be `Kind = Packed` |
| SourceProductId | FK to Products | must equal `PackedProduct.SourceProductId` (stored on the entry so history survives a later change to the product) |
| PacksProduced | decimal(18,3), > 0 | |
| SourceQuantityUsed | decimal(18,3), > 0 | **actual** source stock consumed, not the theoretical figure |
| OccurredAt | datetime2 (UTC) | |
| Notes | nvarchar(300), null | |

The screen suggests `PacksProduced × SourceQuantityPerPack` and the user can overwrite it with what was actually
used, so packing loss is reflected in real stock instead of being hidden. Saving one entry writes, in a single
transaction:

1. the `PackingEntry` row,
2. a `Packing` movement on the source product of `−SourceQuantityUsed`,
3. a `Packing` movement on the packed product of `+PacksProduced`,

both movements carrying `ReferenceType = PackingEntry` and the entry's id.

Worked example (your numbers): loose 100 kg; pack 40 packets of 250 g using 10 kg. That writes a loose movement of
−10.000 and a packet movement of +40.000, leaving 90 kg loose and 40 packets.

Packing a box works the same way with no extra code: a box of 12 packets has the packet product as its source, so
one entry writes −12.000 on the packet product and +1.000 on the box.

## 3. How current stock is calculated

`SUM(Quantity)` over `StockMovements`, grouped by product. **No cached stock column in phase 1.**

- One source of truth, so stock can never silently drift out of step with its history.
- At this business's volume (roughly tens of thousands of movements a year), an indexed `GROUP BY` is fast.
- If it ever becomes slow, a `StockBalances` cache table updated in the same transaction can be added later. That is
  a well-understood optimisation and does not change the ledger design.

**Stock may go negative, and that is allowed.** When a sale or packing entry exceeds available stock, the app shows
a warning and still saves, because deliveries sometimes run ahead of data entry (owner's decision, 2026-09-14).
Negative balances are highlighted on the stock screen so they get corrected with an `Adjustment` movement rather
than quietly ignored.

## 4. Phase 1 inventory screens

1. Categories and units: list, add, edit, deactivate.
2. Products: list with current stock, filter by category and kind, add, edit, deactivate.
3. Stock on hand: current stock per product, low-stock products highlighted.
4. Add stock: opening stock and production entry.
5. Packing: choose the packed product, enter packs produced, accept or correct the suggested source quantity.
6. Adjustment and damage entry, with a mandatory note.
7. Stock history per product: every movement with its type, reference and running balance.

## 5. How this connects to later modules

- **Sales** references `ProductId` and writes `Sale` movements. No change to the inventory tables.
- **Production** gets its own tables (raw materials, batches) and writes `Production` movements, replacing the
  manual production entry. The ledger does not change.
- **Returns** will add movement types (`SaleReturn`, `ReturnDiscarded`) and a returns document that references them.
- **A salesperson app** creates records with its own GUIDs and syncs them, as agreed in `CLAUDE.md` §6.

## 6. Deliberately not included

Batch and expiry tracking, multiple warehouses or locations, purchase orders for raw materials, a unit-conversion
engine, stock reservations, costing and valuation, a cached stock column, a returns workflow, a pricing engine.
Each of these can be added later without changing the tables above.

## 7. Answers from the owner (2026-09-14)

1. **Stock shortfall: warn, do not block.** The sale saves and stock may go negative (see §3).
2. **One loose variety can be packed into many packet sizes** — 6-piece, 20-piece, 25-piece and so on. Several
   packed products simply share the same `SourceProductId`.
3. **A pack can be made from another pack**, rarely (a box of 12 packets). Hence `SourceProductId` rather than a
   loose-only source, and the no-cycle rule in §2.

Remaining TBDs live in `CLAUDE.md` §10 (pricing per shop, returns process, discounts, GST).
