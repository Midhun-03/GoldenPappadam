# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

(The Flutter Android salesperson app in `mobile/` is covered by this record; the owner chose to keep Platform = web, aimed at the React admin panel in `client/`.)

## Users

- **Primary:** the owner and family members running Golden Pappadam, a family pappadam manufacturer and distributor in Kundara, Kollam, Kerala. They use the admin panel on the Windows 11 office PC during working hours, including the Sundays staff work. Jobs: record stock and packing, bill shops, collect and settle payments, pay weekly wages, log expenses, check what is owed. They are running a business, not administering software.
- **Secondary:** salespeople on the road, using the offline-first Android app over weak signal at shop doorsteps. A tap too many is too far.
- Accountant involvement (GST, retention periods) is an open question, not a confirmed audience.

## Product Purpose

A custom business management system that digitizes the whole business gradually: inventory and packing first, then sales, customers, credit and partial payments, dashboard and reports, plus staff wages, expenses, returns, the own retail shop and a field-sales app. Success: the family can trust the numbers (stock, who owes what, profit) without a notebook.

## Positioning

Built for how this business actually trades: credit and bill-to-bill payments, partial settlement, per-shop prices, parent customers with branches, loose stock converted to many packet sizes, and offline phones. Generic billing software assumes cash sales and fixed products.

## Operating Context

- Most sales are on credit; partial payments are normal; a payment may span several invoices.
- Indian context: ₹ money, IST business day, Indian financial year bills (`GP/26-27/000125`), GST configurable and currently not applicable (pappadam exempt), weekly Saturday wages.
- Production runs on the office PC; phones sync over office Wi-Fi.
- Salespeople work offline and sync later.

## Capabilities and Constraints

- Stack: ASP.NET Core Web API, EF Core, SQL Server; React + TypeScript (Vite, Tailwind v4, shadcn/ui radix-nova, TanStack Query, Recharts); Flutter for the phone.
- Admin panel is responsive (sidebar at 1024px+, drawer below), uses status colors only for meaning, skeleton/empty/error states, larger touch targets on coarse pointers.
- Records are immutable ledgers: stock movements, invoices, wage payments are corrected by new records, never edited.
- Dates and money are formatted only through `client/src/lib/format.ts` (IST).
- Open business decisions (do not assume): GST details, profit definition, record retention (see CLAUDE.md §10).
- Out of scope: full accounting, production/raw-material management.

## Brand Commitments

Business name "Golden Pappadam". The admin panel uses one mark (`client/src/components/Logo.tsx`: a pappadam with three blisters on a dark tile), made in-house on 2026-11-06 and not yet confirmed by the owner as the brand logo. No palette or typography commitments have been made.

## Evidence on Hand

- Screenshots of the admin panel in the README.
- Real workflows documented in `docs/01`–`docs/08` and CLAUDE.md.
- No testimonials or customer proof; do not fabricate any.

## Product Principles

1. **Trust the numbers.** Every figure derives from a ledger and can be traced; warnings are shown, never hidden.
2. **Credit is the default, not the exception.** Balances, partial payments and staleness are first-class on screen.
3. **Fast for the tired and the busy.** Common tasks (bill, pay, count stock) take few steps and read at a glance.
4. **Never decide for a person what only they know.** Show shortfalls and mismatches instead of auto-correcting them.
5. **Plain over clever.** Simple, practical tools for a family business, not enterprise accounting.

## Accessibility & Inclusion

No formal standard was set. Users are non-specialists on shared office hardware, so legibility, clear labels and large touch targets matter. Malayalam text must display correctly in data (per the database conventions); a Malayalam UI was not requested.
