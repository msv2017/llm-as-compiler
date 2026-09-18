# 04 — Sort

**DSL feature:** `Sort`.

**Prompt:** "Given a customer id, list their invoices sorted by amount, highest first, and return
the sorted list."

No existing *live-compilation* test in this repo exercised `SortNode` end-to-end before this
example — the runtime unit tests cover the executor directly, but no integration test ever compiled
a prompt into one.
