# 07 — Bounded foreach

**DSL feature:** `Foreach`.

**Prompt:** "Given a customer id, list up to 20 of the customer's active subscriptions. For each subscription in the list, send a renewal reminder. Count the subscription list and return how many reminders were sent."

The "up to 20" phrasing is what gives the compiled `foreach` its bounded `limit` — `WorkflowExecutor` throws if the real list ever exceeds it, rather than looping unboundedly.

Note: `workflow.txt` also contains a redundant `aggregate` node (`countSent`) that's computed but
never used — the actual returned count comes from a separate `filterSent` → `countTrue` path, which
only counts reminders that succeeded. `remindersSent: 3` is correct either way; this is just
harmless LLM-compiler-output redundancy, left as-is here since re-compiling to remove it isn't free
of risk (see the folder's git history if you're curious what that risk looks like in practice).
`DeadNodeEliminator` (added later in this repo's history) would now prune a redundant node like
`countSent` automatically on any future compile — this committed example simply predates that pass,
and the last recompile attempt hit an unrelated regression before a fresh, already-pruned artifact
could be adopted.
