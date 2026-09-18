# 03 — Filter + aggregate

**DSL feature:** `Filter`, `Aggregate`.

**Prompt:** "Given a customer id, list their invoices, keep only the ones with status UNPAID, and
return the sum of their amounts as the outstanding balance."

A `filter` narrows the invoice list to UNPAID ones; an `aggregate` (`Sum`) reduces it to one number.
