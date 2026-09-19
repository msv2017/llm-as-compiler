# 04 — Sort

**DSL feature:** `Sort`.

**Prompt:** "Given a customer id, list their invoices sorted by amount, highest first, and return
the sorted list."

One `call` fetches the invoices, a `sort` orders them by `amount` descending, and `return` passes
the sorted list straight through — see `workflow.txt` for the compiled shape and `run-output.txt`
for a real execution (`cust-4`'s three invoices come back as `inv-b` 300.00, `inv-c` 125.50,
`inv-a` 50.00).
