# 02 — Chained lookup

**DSL feature:** tool chaining — one tool's result feeds the next tool's argument.

**Prompt:** "Given a customer's email, find the customer, then look up their loyalty tier using
the customer's id. Return the tier."

Two sequential `call` nodes: the second's `customerId` argument is a path into the first's result.
