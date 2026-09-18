# 06 — Guarded lookup

**DSL feature:** `Assert`.

**Prompt:** "Given a customer id, look up the customer. If the customer is null, fail with
CUSTOMER_NOT_FOUND; otherwise, using the customer found in that same check, return their name and email."

`crm.findCustomerById` is the one demo tool with a nullable (`optional`) return type, specifically
so this example can force a null-check. See `workflow.txt` for exactly how the compiler chose to
prove the subsequent field access safe.
