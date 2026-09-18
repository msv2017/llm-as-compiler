# Flow Compiler examples

A runnable, end-to-end demonstration of the whole pipeline: a small MCP server
(`Flow.Examples.McpServer`) exposing a fixed CRM/billing domain, and eight example scenarios, each
demonstrating one distinct part of the DSL.

Run `./regenerate.sh` (from this directory, or `examples/regenerate.sh` from the repo root) with
`OPENAI_API_KEY` set to reproduce every compiled workflow and execution result from scratch.

| # | Example | DSL feature |
|---|---|---|
| 1 | [01-lookup](01-lookup/) | `Call`, `Return` |
| 2 | [02-chained-lookup](02-chained-lookup/) | Tool chaining |
| 3 | [03-filter-aggregate](03-filter-aggregate/) | `Filter`, `Aggregate` |
| 4 | [04-sort](04-sort/) | `Sort` |
| 5 | [05-conditional-refund](05-conditional-refund/) | `If` |
| 6 | [06-guarded-lookup](06-guarded-lookup/) | `Assert` |
| 7 | [07-bounded-foreach](07-bounded-foreach/) | `Foreach` |
| 8 | [08-ambiguous-prompt](08-ambiguous-prompt/) | Ambiguity detection (`Unresolved`) |

Each folder's own `README.md` has the prompt, the feature it demonstrates, and a pointer to its
compiled/executed artifacts. To run the demo server by itself (e.g. to point `flow-cli` at it by
hand): `dotnet run --project Flow.Examples.McpServer` from this directory, listening on
`http://localhost:5100/mcp`.
