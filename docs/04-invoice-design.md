# Invoice management - design

_Written 2026-09-23. Covers invoice numbering, GST, the invoice snapshot, PDFs, printing and email._
_Builds on `02-sales-design.md` (bills, payments, cancellation) and `03-field-sales-design.md` (the phone)._

## 1. Bill and invoice are one record

The existing `sales.Invoices` row **is** the invoice. It was extended rather than joined to a second
table, because a second table would duplicate every amount and give two things that could disagree.

| Stage | Where it lives |
|---|---|
| Draft | The New Bill form, or a sale waiting in the phone's outbox. Never a database row. |
| Finalizing | One transaction on the server (§3). Not a stored state: it either commits whole or not at all. |
| Finalized | `Status = Issued`. Numbered, stock taken, owed. |
| PDF stored | A row in `sales.InvoiceDocuments`. |
| Emailed / email failed | The latest row in `sales.InvoiceEmailLogs`. |
| Cancelled | `Status = Cancelled`. Number, lines and PDF kept; stock put back. |

**Why no saved drafts.** A draft that holds a number leaves gaps in the series when it is abandoned; one
that does not hold a number is just the form. GST expects a consecutive series, so the number is given
only at the moment of finalizing. Saved drafts can be added later as a separate table without touching
this one.

**Immutability is enforced, not hoped for.** `AppDbContext` refuses any change to an invoice except the
cancellation fields (`Invoice.PropertiesEditableAfterFinalization`), whichever code path tries it. So:

- `CreatedAt` / `CreatedBy` = finalized at / by.
- `UpdatedAt` / `UpdatedBy` = cancelled at / by, since cancelling is the only update ever allowed.
- The phone a synced sale came from is found through `fieldsales.SyncSubmissions` (the client's temporary
  id → the server's invoice), so no column repeats it.

## 2. What an invoice keeps (the snapshot)

Everything printed is copied onto the invoice when it is finalized, so renaming a shop, moving a branch or
changing a GST rate never changes an old invoice:

- **Supplier:** name, address, GSTIN, state code - from `sales.InvoiceSettings` at that moment.
- **Customer:** name, address, phone, GSTIN, state code.
- **Branch:** name, address, phone, GSTIN, state code.
- **Tax basis:** place of supply, intra/inter-state, reverse charge, whether rates included GST.
- **Per line:** line number, description, unit, HSN, quantity, rate, discount share, treatment, GST rate,
  taxable value, CGST, SGST, IGST, cess.
- **Totals:** gross, discount, taxable, CGST, SGST, IGST, cess, round-off, grand total.

Lists and the detail page show the snapshot, not today's customer record.

## 3. Invoice numbers

Format: `GP/26-27/000125` - series, financial year, six digits. Fifteen characters; GST allows sixteen,
which is why the series prefix is limited to three characters.

**How two devices can never get the same number.** `sales.InvoiceNumberSequences` holds one counter per
series per financial year. Inside the transaction that saves the invoice, one statement increments it:

```sql
UPDATE sales.InvoiceNumberSequences WITH (UPDLOCK, HOLDLOCK)
SET @next = LastNumber = LastNumber + 1 ...
IF @@ROWCOUNT = 0 INSERT ... (first invoice of a new year)
```

The update holds an exclusive lock on that row until the transaction ends, so a second device's
finalization waits and then reads the next number. `HOLDLOCK` also locks the key range when the year's row
does not exist yet, so the first two bills of a new year cannot both create a counter.

- **No gaps from failures:** if the invoice fails to save, the increment rolls back with it.
- **Never reused:** a cancelled invoice keeps its number for ever.
- **New financial year:** derived from the invoice date (1 April); the first bill of the year creates its
  counter. Nothing is reset by hand.
- **Second line of defence:** unique indexes on `InvoiceNumber` and on
  `(SeriesCode, FinancialYear, SequenceNumber)`. If one ever fires - only possible if the counter fell
  behind, e.g. a restored backup - the service repairs the counter from the invoices on file and retries.
- **Why not `MAX()+1`:** two readers see the same maximum. **Why not a `SEQUENCE`:** not transactional
  (gaps) and needs one object per year.
- **Offline phones never number anything.** The phone sends its own client id; the server finalizes and
  numbers the sale when it syncs, and answers with the number (`documentNumber` in the sync result - a
  retry answers with the same one). The phone keeps it on the outbox row and Home's "Today's bills" shows
  it; until then the bill reads "waiting to sync".

Bills made before this change keep their numbers (`INV-2026-00001` ... `00014` became series `INV`,
2026-27, numbers 1-14). The `GP` series started at 1.

## 4. GST

**The owner's rule (2026-09-23):** Golden Pappadam sells only pappadam, under one HSN code, which is exempt
from GST today. Shops that are GST registered get a **GST bill**; every other shop gets a **normal bill**.
A customer is marked with a "GST registered" tick box, which requires its GSTIN. Only the GSTIN is stored:
having one *is* being registered.

| Customer | Tax charged? | Document |
|---|---|---|
| GST registered | no - pappadam is exempt | **GST bill: Bill of Supply** - both GSTINs, HSN, place of supply, "Exempt" |
| GST registered | yes (if a product is ever taxable) | **GST bill: Tax Invoice** - as above plus CGST/SGST or IGST |
| Not registered | no | **Normal bill** - no GST details at all |
| Not registered | yes (future) | Tax Invoice, without a customer GSTIN - tax follows the product, never the customer |

- The business GSTIN (Settings) must be entered before a shop can be marked GST registered, and before a
  GST customer can be billed. The first rule keeps a phone from saving a sale offline that the server would
  then refuse; the second covers a GSTIN removed later.
- The phone's snapshot carries each shop's GSTIN, and its sale screen and shop page say "GST bill" or
  "Normal bill". The server still decides what the bill is when it finalizes the sale.
- GST cannot be switched on while any active product has no treatment, and once it is on a product cannot be
  saved without one - otherwise every bill for that product, the phone's included, would stop.
- **Why "Bill of Supply" and not "Tax Invoice" for exempt pappadam:** GST rules have a registered supplier
  of exempt goods issue a bill of supply. It is still the GST document the shop asked for. If the accountant
  wants a different heading, it is one line in `InvoiceService.DocumentTypeFor`.

- Each product carries **HSN**, **treatment** (Taxable, Exempt, Nil rated, Non-GST) and, if taxable, its
  **GST rate**. A product with no treatment **cannot be billed** once GST is on - the app does not guess.
- **Place of supply** is where the goods went: the branch for a branch bill (never the parent company), the
  customer otherwise; the state picked, else the first two digits of the GSTIN. A taxable sale with no
  known place of supply is refused.
- Same state as the supplier → **CGST + SGST** (each half the rate); any other state → **IGST**. A check
  constraint makes it impossible for one invoice to carry both.
- **Rates including GST** (Settings, on by default like an MRP): tax is worked out of the agreed rate, so
  the shop pays exactly that rate. Off: tax is added on top.
- **Bill discount** is shared over the lines in proportion to their amounts and lowers their taxable value.
- **Rounding:** every amount to the paisa, half away from zero, at the line; totals are sums of lines;
  CGST and SGST are the same computed half, so always equal. Rounding the grand total to the rupee is
  optional and printed as the round-off.
- **One calculation** (`GstCalculator`) serves the New Bill preview, the saved invoice and the PDF. The
  client sends no totals; the server works out everything.
- Cess has columns everywhere and is always zero today.

**To confirm with the accountant:** the exact HSN code, that pappadam is exempt (not nil-rated), and the
"Bill of Supply" heading. See §10 of `CLAUDE.md`.

## 5. PDF, storage and printing

- Made with **QuestPDF** (Community licence: free under USD 1M revenue) from the invoice snapshot.
  A4, black-and-white friendly, HSN and tax summary, amount in words (lakh/crore), bank details, terms,
  signatory box, "Original for recipient", page numbers. A cancelled invoice generated after cancelling is
  watermarked.
- Made **once**, after the invoice commits - a PDF failure never undoes a sale. Office bills get it
  straight away; phone sales the first time anyone opens, prints or emails them.
- Stored through `IInvoiceDocumentStorage`. Today `LocalInvoiceDocumentStorage` (folder from
  `InvoiceDocuments:LocalRootPath`, default `App_Data/invoice-documents`, git-ignored). Supabase Storage
  or any object store is a second implementation and one line in `Program.cs`.
- `sales.InvoiceDocuments` records the key, file name, size and **SHA-256**. Every download is checked
  against it; a missing or altered file is reported, never silently regenerated.
- Served only by `GET /api/sales/invoices/{id}/pdf` to a signed-in admin, `Cache-Control: no-store`.
  There is no public URL.
- **Printing** loads that same stored PDF into a hidden frame and opens the browser's print dialog, so the
  printed copy, the emailed copy and the stored copy are one file.

## 6. Email

- `IEmailSender` with three providers chosen by `Email:Provider`: `None` (sending fails with a clear
  reason), `Pickup` (writes `.eml` files to `App_Data/mail-outbox` - the development default) and `Smtp`
  (MailKit, STARTTLS on 587).
- Credentials never live in Git: `dotnet user-secrets set "Email:Password" ...` in development, the
  `Email__Password` environment variable on a server.
- Only a finalized, non-cancelled invoice with a stored, verified PDF is ever sent. The recipient defaults to
  the customer's email and can be typed.
- Every attempt is a row in `sales.InvoiceEmailLogs` (recipient, subject, status, error, attempt number, who).
  A failure is recorded and shown ("Email: Failed - Retry"); the invoice is untouched. The history list can
  filter to invoices whose last email failed.

## 7. Not built (deliberately)

Saved drafts, credit notes, e-invoice (IRN) and e-way bill APIs, multiple active series, cess rates,
UQC codes per unit, opening or sharing the PDF from the phone, adding shops (with the GST tick box) from
the phone, Supabase. None of them needs a change to what is here.
