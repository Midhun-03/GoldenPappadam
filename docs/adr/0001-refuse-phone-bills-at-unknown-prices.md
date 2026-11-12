---
status: accepted
---

# Refuse a phone bill whose price was never the shop's rate

A bill made on the salesman's phone has already happened: the packets are in the shop. Even so, the server refuses the whole bill when any line's price is neither the shop's rate now nor a rate the shop had at some moment since the phone last heard from the office (its last-known rate). The phone never lets a salesman type a price, so such a price can only come from a bug or from tampering, and a finalized bill is an accounting document that can never be edited. A bill at a last-known rate is still accepted as charged and flagged for the office, with no limit on how old that rate is.

## Considered options

- **Accept every price and flag the difference** (the rule until 2026-10-29). Rejected: it turns any price, ₹0 included, into a permanent invoice and relies on someone noticing a badge.
- **Accept the bill but re-price it at the shop's rate.** Rejected: the shop was told an amount on its doorstep, and a recorded price is never rewritten.
- **An age limit on a last-known rate.** Rejected: it would refuse honest bills for delivered goods, and every bill not at the current rate is flagged anyway.

## Consequences

- A refused bill writes nothing on the server. It stays on the phone as failed with the reason shown, and the office re-enters it by hand. The evening van reconciliation shows the unbilled packets, so the refusal cannot hide a delivery.
- A payment taken with a refused bill is still accepted, against the shop's oldest outstanding bills, and the visit is kept without a bill.
- The server must know what every shop's rate used to be, so changes to a product's standard price are recorded the way changes to an agreed rate already are.
- The same reasoning does not extend to the discount: a discount over the cap is stopped on the phone before the bill is saved, so the server's refusal there is only a backstop.
