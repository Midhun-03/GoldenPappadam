# Employees, wages and expenses - design

_Built 2026-09-28 from the rules in `CLAUDE.md` §4 "Employees, wages and expenses" (owner's answers to §10
Q6-Q9 the same day). Office-only: every endpoint is admin-only through the fallback policy, and nothing is
added to `/api/mobile/*`._

## 1. Tables

Two new schemas, `staff` and `accounting`. Migration `AddStaffAndExpenses` only creates tables; no existing
table or row is touched.

| Table | Kind | What it holds |
|---|---|---|
| `staff.Employees` | master (`AuditableEntity`, `IsActive`) | name, designation, phone, address, joined on. Not a login. |
| `staff.EmployeeWageRates` | immutable (`Entity`) | daily wage + effective-from date. A raise adds a row. |
| `staff.AttendanceStatuses` | master, seeded with fixed ids | Present 1, Half day 0.5, Absent 0, Leave 0 - the fraction is data. |
| `staff.AttendanceRecords` | editable | one per employee per IST date (unique index). No row = not recorded = 0. |
| `staff.WagePayments` | document (cancel only) | one employee, one Sunday-Saturday week: the amounts as handed over. |
| `staff.WagePaymentLines` | immutable | the snapshot: 7 day lines, then correction and carried-balance lines. |
| `accounting.ExpenseCategories` | master, 12 seeded with fixed ids | Employee wages is the system category (`KnownExpenseCategories`). |
| `accounting.Expenses` | editable, cancelled never deleted | money spent; `WagePaymentId` set only on the expense a wage payment made. |
| `accounting.ExpenseChanges` | immutable | the version each edit or cancellation replaced, with who, when and why. |

**Guards in the database, not just the services**

- At most one standing payment per employee per week: unique filtered index
  `(EmployeeId, PeriodStart) WHERE Status = 'Paid'`. A cancelled payment stays and frees the week.
- One expense per wage payment: unique filtered index on `Expenses.WagePaymentId`.
- `AppDbContext` refuses any change to a `WagePayment` except cancelling it, and any change to a wage
  expense except its status - whichever code path tries.
- `Amount > 0` on expenses and wage rates; `Amount >= 0` and `CarriedForward <= 0` on wage payments;
  day fractions between 0 and 1.

## 2. The week and the calculation

`WageWeek` is the one place the week is defined: Sunday to Saturday, paid on the Saturday, the Saturday
included. `WageCalculator` works out a week for every employee, and both the screen and the payment use it,
so the office is shown exactly what gets recorded.

- **Days:** each day's fraction × the rate in effect that day (`WageRates.InEffectOn`: latest start on or
  before the day; two starting the same day - the newer corrected the older). A mid-week raise therefore
  pays each day at its own rate. Money is rounded to the paisa per day.
- **Corrections** (§10 Q8): for every day already paid, the calculator compares the status paid with the
  status recorded now. If they differ, the difference - at the rate the day was *paid* at - becomes a
  correction line on the employee's next unpaid week. Compared by status, not fraction, so changing what
  "Half day" is worth never rewrites paid half days. A correction is settled once: after it is paid, the
  paid state of that day moves on and nothing is pending.
- **Overpayment larger than the week:** the payment is ₹0 with `CarriedForward` negative, no expense is
  made (nothing was handed over), and the next payment picks it up as a carried-balance line.
- **Nothing to pay:** no days worked and no corrections. Such a week needs no payment.

## 3. Paying

`POST /api/staff/wage-payments` pays one or more employees for a week, all or none, in one transaction:
each payment with its lines, and each one with money in it gets one Employee wages expense (dated the
payment date, same method). It is refused when:

- the week's Saturday has not come, or the payment date is outside Saturday..today;
- the week is already paid (and the unique index catches two screens racing);
- a worked day has no wage set;
- **the amount differs from what the office was shown** (`ExpectedAmount`) - attendance changed while the
  screen was open, so nobody records a figure they did not see.

**Cancelling** (`POST .../{id}/cancel`, reason required) is for a payment recorded that never happened. The
payment and its expense stay, marked cancelled, and the week can be paid again. Refused while a later
payment settled a correction to it or took over its carried balance - that one has to go first, or the same
days would be paid twice.

**Wage changes** never touch a paid week (the lines are a snapshot), and a new rate may not start on or
before the last paid Saturday. Entering the same rate from the same day twice adds nothing.

## 4. Expenses

- Categories are data: added, renamed and deactivated from Settings, never deleted. Employee wages cannot be
  chosen, renamed or deactivated - the only way in is paying wages, which is what stops double counting.
- An expense is edited in place, but first its current version is written to `ExpenseChanges`; the history
  dialog reads them back as a timeline. Cancelled expenses are kept and left out of every total.
- `GET /api/accounting/expenses` and `/summary` take the same filters (from, to, category, search); the
  summary totals by category, largest first, cancelled left out. An expense counts in the period of its
  date, so a wage week lands in the month it was paid.
- No receipt attachments yet (rules: optional, later, behind a storage interface).

## 5. Screens

Sidebar groups **Staff** (Attendance, Weekly wages, Employees) and **Accounts** (Expenses); two Settings
cards (Expense categories, Attendance day values).

- **Attendance:** pick a day, tap a status per person, "Everyone else present", save once. Tapping the chosen
  status again clears it (the usual state for someone whose Sunday it is not). Rows whose week is already
  paid are badged, and saving a change there says it will be settled next week.
- **Weekly wages:** a week at a time; per employee the attendance, rate(s), days, wages and status; a row
  opens to the seven days and any corrections. Pay one, or "Pay all pending" through one confirmation.
- **Employees / employee page:** details, current wage, "Change wage" from a date, the full wage history,
  and every wage payment.
- **Expenses:** Today / This week / This month / Custom, category, search, show cancelled; totals by
  category beside the list.

## 6. Not built

- The dashboard's **profit or loss for the month**: its definition is still to be confirmed (`CLAUDE.md`
  §10 Q10). Everything it needs - expenses by date, wages included - is now recorded.
- Overtime, bonuses, advances, deductions: expected to become further `WagePaymentLineType` values.
