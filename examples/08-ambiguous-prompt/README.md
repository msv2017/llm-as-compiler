# 08 — Ambiguous prompt

**DSL feature:** ambiguity detection (`Unresolved` / `Interpretations`), not a specific node kind.

**Prompt:** "Given a customer id, find the most appropriate account for this customer and return
its id."

The demo customer for this scenario (`cust-8`) has two equally plausible active accounts with no
stated tie-breaker, so the compiler is expected to refuse to guess rather than silently pick one.
See `compile-output.txt` for the real `Unresolved:` diagnostics from an actual compile attempt.
