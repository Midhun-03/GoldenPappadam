# Reports, shelf life and returns - design

_Started 2026-09-25. Plan approved the same day; built step by step, each step recorded here as it lands._

## 1. Reports (built 2026-09-25)

**One report, three outputs.** Every report is a `ReportDocument` built by one small query class in
`Features/Reports/`: a title, the period, summary figures, and sections (tables) whose **totals are worked
out on the server, once**. The same document is:

- JSON for the screen (`client/src/pages/reports/ReportView.tsx` renders any report);
- a PDF (`ReportPdfRenderer`, QuestPDF, the invoice house style via `Common/PdfStyle.cs`; a table wider
  than seven columns turns the page to landscape);
- an Excel sheet (`ReportExcelWriter`, ClosedXML): real numbers and dates, not text, so the accountant
  can add them up.

So the screen, the print and the spreadsheet can never disagree. A new report is a query class, one
controller action and one menu entry - no new screen.

`GET /api/reports/{sales|collections|outstanding|statement/{customerId}}?from&to|asOf&format=json|pdf|xlsx`.
Admin-only through the fallback policy. Dates are IST business days (`ReportPeriod`), today when left out,
at most a year per request.

| Report | What it answers |
|---|---|
| **Sales** | Every bill in the period (as printed: shop, branch, GST or normal, who billed it and whether from the office or a phone, paid so far, still due), then by product, by shop and by who billed. Cancelled bills are listed separately and counted nowhere. |
| **Collections** | Every payment with the bills it settled; by method; by who received it; how much was kept on account. |
| **Outstanding** | On a chosen day, what each shop owes and for how long: before-system balance, bills aged 0–7 / 8–15 / 16–30 / 31–60 / over 60 days, money paid ahead. |
| **Statement** | One shop's account for a period - brought forward, every bill and payment, owed at the end - from the same ledger as the customer page. Opened from the **Statement** button on the customer page. |

**Decisions worth knowing**

- **Money paid without naming a bill** is applied in the outstanding report the way a person would: to the
  before-system balance first, then the oldest bills. Without that, a shop that paid its opening balance in
  cash showed it as still owing, and a bill made after an advance payment looked overdue. Every row still
  adds up to the shop's balance on its statement. Found on the development data.
- **An earlier day reads as it did that evening:** the outstanding report counts only bills and payments
  dated on or before the chosen day.
- **Product values vs bill totals:** product values are after discount, include tax and leave out
  rounding. Bills made before 23 Sep 2026 kept their discount on the whole bill, so where they are in the
  period the report states exactly how much the products exceed the bills (₹110 on the development data).

## 2. Shelf life, repacking and expiry (built 2026-09-26)

**Shelf life** is a product setting (`Product.ShelfLifeDays`, null = does not expire); pappadam is 20 days
from packing. Nothing is hard-coded, and products start with none set - enter it on each product.

**Stock age comes from the ledger, not from batch numbers.** `StockAgeCalculator` replays one product's
movements first-in-first-out, keeping a queue of layers (a quantity packed on one day) per place:

| Movement | Effect on age |
|---|---|
| Packing, production, opening, positive adjustment | a layer dated that day |
| Sale, damage, negative adjustment | take the oldest layers first |
| Van load / van return (transfer) | the oldest layers move **with their packing date** |
| Cancelled bill (sale reversal) | puts back exactly the layers its sale took |
| Repacking | the packets opened lose their oldest layers; new packets are dated the repacking day; loose left-over keeps the oldest opened packet's date |
| Stock below zero | a debt the next arrival pays first |

Bands are relative to the shelf life (`AgeBands`); for 20 days: fresh 0–4, **worth repacking 5–10**, ageing
11–15, **expiring soon 16–20**, **expired 21+**.

**Repacking** (`inventory.RepackEntries`, movement type `Repacking`): the office opens N packets of one
product and packs them as another (or the same) - owner's rules: office decides, any size, nothing lost,
fresh 20 days. Both products are traced to the loose product they are packed from, so the conversion is
exact: 5 × 20-piece = 100 pieces = 10 × 10-piece; 100 pieces into 6-piece packets = 16 packets and 4 pieces
back to loose. Different loose varieties are refused. Warehouse only, like packing; a shortfall warns.

**Expired stock is written off by a person, never automatically** (`POST /api/inventory/stock/age/write-off`):
one `Damage` movement for the expired quantity, which first-in-first-out takes from exactly the expired layers.

**Screens and reports:** Inventory → **Stock age** (per place, with Repack and Write off) and **Repacking**;
Reports → **Stock movement** (opening, made/packed, used for packing, repacked in/out, moved in/out, sold,
damaged, adjusted, closing - minus figures for movements out, so each row adds up); the stock-age report as
PDF/Excel; and the dashboard's stock card says how many products are expired, expiring within 3 days or
worth repacking.

## 3. Returns in the office (built 2026-09-26)

The owner's rules (2026-09-25): shops only return **expired or damaged** packets, returned packets are
**never resold**, and what the shop gets **depends on the shop**, decided by the office.

**The record.** `sales.ReturnNotes` + `sales.ReturnNoteLines`, numbered `RN/26-27/000001` through the same
locked counter as invoices (series `RN`, which the invoice prefix may not take). A note snapshots the
customer and branch names, and each line fixes its rate when recorded: the shop's agreed rate through
`CustomerPriceService.Resolve`, else the product price, overridable by the office. The branch follows the
bill rule (`BranchRule`): a multi-branch customer must name one. Like an invoice, a note is never edited;
`AppDbContext` allows only the settlement and cancellation fields to change.

**A return never adds to stock.** The packets were sold and are now waste; the note lines are the record.

| Settlement | What it does |
|---|---|
| Office to decide (`Pending`) | Only the record, until the office decides - once. |
| Replaced free | `Replacement` movements take the same products and quantities out of the warehouse or a van. The bill is untouched. |
| Credit | A `Payment` with method `ReturnCredit`, referencing the note. It settles the oldest bills first exactly as money would, so balances, ledgers and the outstanding report need no new logic. Defaults to the packets' value; the office may credit a different amount. |
| Nothing given | Only the record. |

**A credit is not money.** `ReturnCredit` cannot be entered on the payments screen, is never offered to
the phone, is left out of the collections report and shows as "Return credit" in the ledger and statement.
The dashboard and the phone's day figures count only phone payments or allocations to bills, so they were
already right.

**Cancelling.** A replacement's movements are mirrored back into the same place. A **credited** return
cannot be cancelled, for the same reason a bill with money on it cannot: payments are never reversed in
this system. If one is ever needed, it is a new design, not a delete.

**Stock age.** A replacement takes the oldest layers, as a sale does; a cancelled replacement puts the same
layers back with their packing date. The stock report has a "Replaced free" column.

**Screens and reports.** Sales > Returns (list with "only those to decide"), New return, and the return
page with Decide, Print (A4 PDF from the report renderer) and Cancel. Reports > Returns and expiry: notes,
by product and reason, by shop, and expired stock written off from the Stock age screen.

## 4. Returns on the phone — next
## 5. The owner's daily summary — next

See the approved plan for the design of 3–5; this document is filled in as each is built.
