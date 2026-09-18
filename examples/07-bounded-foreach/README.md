# 07 — Bounded foreach

**DSL feature:** `Foreach`.

**Prompt:** "Given a customer id, list up to 20 of the customer's active subscriptions, and for each one, send a renewal reminder. Return how many reminders were sent."

The "up to 20" phrasing is what gives the compiled `foreach` its bounded `limit` — `WorkflowExecutor` throws if the real list ever exceeds it, rather than looping unboundedly.
