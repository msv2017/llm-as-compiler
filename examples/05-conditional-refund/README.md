# 05 — Conditional refund

**DSL feature:** `If`.

**Prompt:** "Given a customer id, compute their outstanding balance as the sum of UNPAID invoice
amounts. If the balance is less than 100, refund the oldest unpaid invoice by calling the refund
tool with that invoice's id. Return whether a refund was made and the outstanding balance."

The `if` branch performs a real write (`billing.createRefund`) conditionally on a computed value.
