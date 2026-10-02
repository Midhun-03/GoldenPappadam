# Own shop sales and stock - design

_Approved 2026-09-30 with the owner's answers (below). **Built 2026-09-30**, admin only. Rules in `CLAUDE.md`
§4 "Confirmed requirements (2026-09-30, own shop)"._

Golden Pappadam runs its own retail shop. The factory sends it loose pappadam in kg; the shop sells it by
the piece, over the counter, to walk-in customers and to caterers and other shops. The admin runs the shop
for now, so the whole module is office-only.

## 1. Owner's answers (2026-09-30)

| # | Question | Answer |
|---|---|---|
| 1 | Do caterers / wholesale buyers take pieces on credit? | **No** - they pay at the time of buying, like everyone at the shop |
| 2 | The minimum rate | **₹1.30** for the normal pappadam, **editable by the admin** (data on the product, not code) |
| 3 | May the rate go above the default? | **No** - the default rate (₹1.60) is also the most a piece sells for |
| 4 | Kg × pieces per kg not a whole number | **Round to the nearest whole piece** |
| 5-11 | One variety per transfer; per-piece agreed rates reuse `CustomerPrices`; `OS/26-27/000001` numbering, no printed receipt yet; unsold shop stock is sold or written off, never sent back; no shelf life at the shop yet; the admin records transfers; the dashboard tile comes later | Defaults as proposed |

## 2. How it fits the existing inventory

Nothing is a second inventory. The shop is a **stock location** and its stock is the same movement ledger.

- `inventory.StockLocations` gains `SHOP` ("Own shop", kind **`Shop`**, fixed id `KnownStockLocations.OwnShopId`).
- A product has one unit, and the shop counts pieces, so each loose variety gets **one pieces product**:
  `Product.Kind = Pieces`, unit **PCS**, `SourceProductId` = the loose-kg variety. It is an internal
  representation, one per variety - never one per bundle size. The 15/30/50/100-piece bundles the shop makes
  up are still these pieces: making them up moves no stock; only a sale does.
- Why not keep the shop's stock in kg on the loose product and convert each sale? 0.001 kg cannot hold one
  piece exactly when the factor does not divide 1,000 (1/170 kg), so counts would drift; and changing a
  variety's pieces per kg would silently change how many pieces sit on the shelf.
- The conversion figure stays where it already is - the loose variety's `PiecesPerKg` (200 for the standard
  4-inch). A larger variety is a new loose product with its own figure, plus its own pieces product with its
  own rates. Nothing is hard-coded.

**Kept apart.** A pieces product is only ever at the shop, and the shop holds nothing else. Refused: on an
invoice (office New Bill and the phone), a van load, a return, a stock request, a rate-change request, a new
shop's initial rates from the phone; as the source of a packed product; stock entries of it anywhere but the
shop, or of anything else at the shop. The phone snapshot leaves pieces products and their rates out. The
warehouse stock list (and so the dashboard's low stock) no longer shows them as "0, low".

## 3. Pricing

- `Product.SellingPrice` of a pieces product = the rate per piece (₹1.60), **required**, and the ceiling.
- New `Product.MinimumSellingPrice` (pieces products only; ≤ the rate) = the floor (₹1.30). Null means the
  rate cannot be lowered at all.
- A sale line starts at `CustomerPriceService.Resolve(typed, customer's agreed rate, product rate)`. An agreed
  per-piece rate lives in the existing `CustomerPrices` (per product, so it never touches the customer's
  packet rates) and must itself lie in the band. The final rate is checked by `ShopRates.EnsureAllowed` in the
  service - the screen only mirrors it - and by a check constraint on the line.
- Each line keeps the rate charged, the default and the minimum of the moment, so wholesale sales stay visible
  after the rates change.

## 4. Records

| Table (`ownshop` schema) | What |
|---|---|
| `ShopTransfers` | Immutable. Source loose product, pieces product, from/to location, kg, pieces per kg used, pieces received, factory and shop stock before, time, `ClientRequestId`, notes, who (`CreatedBy`). |
| `ShopSales` | The counter sale. `OS/26-27/000001` from the invoice counter in its own series (`InvoiceSettingsService.OwnShopSeries`, which invoices may not use), IST sale date, location, optional customer + name snapshot (null = walk-in), payment method (never `ReturnCredit`), total, status `Completed`/`Cancelled`, notes, `ClientRequestId`. Only the cancellation fields may change (`AppDbContext`). |
| `ShopSaleLines` | Pieces product, name snapshot, whole pieces, rate charged, default and minimum at the time, line total. |

A shop sale is **not an `Invoice`**: no customer ledger, no credit, no GST document, no walk-in "customer" row.
Everyone pays at the counter (answer 1).

## 5. Stock movements

| Operation | Movements (one transaction) | Blocks when |
|---|---|---|
| Transfer 30 kg | `ShopTransfer` −30 KG loose at MAIN; `ShopTransfer` +6,000 PCS pieces at SHOP | the warehouse has less than the kg |
| Sale 47 pcs | `Sale` −47 at SHOP, reference `ShopSale` | the shop has fewer pieces |
| Cancel sale | `SaleReversal` +47 at SHOP | - |
| Damage at the shop | `Damage` (existing entry, location SHOP) | more than the shop has |
| Count at the shop | `Adjustment` (existing), whole pieces | - |

`ShopTransfer` is its own movement type because a `Transfer` pair is one product whose halves cancel out;
this pair changes product and unit. The stock-movement report counts it under Moved in / Moved out.

Checks and writes run under `StockService.LockProductsAsync` (UPDLOCK, HOLDLOCK on the product rows, in id
order). Packing now uses the same helper, so a packing and a transfer cannot both take the last kilogram, and
two sales cannot both take the last pieces. A repeated Save with the same `ClientRequestId` returns the first
record.

## 6. API (admin-only through the fallback policy)

- `GET /api/own-shop/stock?customerId=` - pieces on the shelf, rate band, factory kg; `rate` = where a sale starts.
- `POST /api/own-shop/transfers/preview`, `POST /api/own-shop/transfers`, `GET /api/own-shop/transfers?from&to`
- `GET /api/own-shop/sales?from&to&customerId&productId`, `GET /api/own-shop/sales/{id}`,
  `POST /api/own-shop/sales`, `POST /api/own-shop/sales/{id}/cancel`
- Existing: products (kind `Pieces`, `minimumSellingPrice`), stock entries and adjustments with the SHOP location.

## 7. Screens (sidebar group "Own shop")

Shop stock (count, opening stock / damage, history), New shop sale (lines with 15/30/50/100 quick buttons,
rate prefilled and bounded, walk-in or customer, paid by), Shop sales (date range, totals, "lower rate" badge)
and the sale page with Cancel, Receive from factory (kg → pieces worked out by the server, shortfall shown,
history). The product dialog gains the "Shop pieces" type.

## 8. Tests

45 new test cases - `ShopTransferTests` (12), `ShopSaleTests` (21), `OwnShopProductTests` (12) - plus the three new
endpoints in `AuthorizationTests` (salesperson 403, admin 200, anonymous 401).

## 9. Not built - later phases

Dashboard tile and profit (Q10) including shop sales; reports (shop sales by variety and date, pieces sold,
sales below the standard rate); a printed receipt; returning shop stock to the factory; shelf life at the
shop; credit sales at the shop (would be an `Invoice` from the SHOP location); GST treatment of counter sales,
for the accountant once a GSTIN is entered; a salesperson/shop-user role.
