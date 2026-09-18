# 07 — Bounded foreach

**DSL feature:** `Foreach`.

**Prompt:** "Given a customer id, list up to 20 of the customer's active subscriptions. For each subscription in the list, send a renewal reminder. Count the subscription list and return how many reminders were sent."

The "up to 20" phrasing is what gives the compiled `foreach` its bounded `limit` — `WorkflowExecutor` throws if the real list ever exceeds it, rather than looping unboundedly.

The compiled workflow also includes a redundant, unused `aggregate` node (`countSent`, counting the
`foreach` results directly) alongside the one actually used for the returned count (`filterSent` →
`countTrue`, which counts only the reminders that actually succeeded). `remindersSent: 3` is correct
either way, but only the `filterSent`/`countTrue` path is wired to the output. This is the same kind
of LLM-compiler-output redundancy discussed elsewhere in this repo — harmless, but not re-compiled
away here (a re-run produced a workflow that traded this away for a different regression: it
replaced the per-iteration success flag with a hardcoded constant, so the artifact was kept as-is).
