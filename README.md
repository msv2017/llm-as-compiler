# Flow Compiler

Ask an LLM-based agent to do the same task twice and you can get two different answers. Same
prompt, same tools — but the model re-reasons the whole thing from scratch every run. That's fine
when the task is genuinely novel each time. It's wasteful when it isn't.

A lot of what gets called "AI automation" is actually the same handful of workflows running over
and over: look up a customer, sum some invoices, decide whether to refund. Once a workflow's
meaning is established, there's no reason to keep paying a model to re-derive it.

Flow Compiler works from that idea. Use a model once, at compile time, to turn a prompt and a
catalog of MCP tools into a small, typed, deterministic workflow. After that, a plain .NET executor
runs it — no further LLM calls, same behavior every time. When a prompt doesn't actually reduce to
something deterministic, compilation fails instead of guessing.

> **Use AI to formalize the workflow once; use ordinary software to execute it forever.**

## Contents

- [Example](#example)
- [Examples](#examples)
- [How it works](#how-it-works)
- [Project layout](#project-layout)
- [Getting started](#getting-started)
- [Using Flow.Cli](#using-flowcli)
- [Providers](#providers)
- [Known limitations](#known-limitations)
- [License](#license)

## Example

Prompt in:

> *"Given a customer id, look up and return the customer's name."*

Compiled workflow out:

```
$ dotnet run --project src/Flow.Cli -- compile scenario.json

Status: Success

Assumptions:
  - The tool crm.getCustomerById returns a Customer entity with a 'name' field representing the customer's name.

Workflow:
  call getCustomer = crm.getCustomerById(id: input.customerId)
  return { customerName: getCustomer.name }
```

One LLM call produced that. Run it again tomorrow and it's the same workflow — no further LLM calls
involved. Scenario-file format and full CLI usage below, under [Using Flow.Cli](#using-flowcli).

## Examples

The `examples/` folder is a second, larger demonstration: a real MCP server plus eight scenarios,
each showing one distinct part of the DSL end-to-end (compile *and* execute), not just the one
lookup shown above. See [examples/README.md](examples/README.md).

## How it works

```
prompt + tool catalog + input/output types
                │
                ▼
      AI-assisted compiler (OpenAI or Anthropic)
                │
                ▼
      candidate workflow (JSON)
                │
                ▼
      8-pass deterministic validator ──fails──► repair loop (re-prompt with diagnostics, bounded retries)
                │ passes
                ▼
      typed workflow IR
                │
                ▼
      WorkflowExecutor  ──►  MCP tool calls  ──►  output
```

The model only participates in the top half of this diagram. Once a candidate passes validation,
everything below is ordinary deterministic code — no LLM involved in execution.

Between IR conversion and validation, a deterministic (non-LLM) pass, `DeadNodeEliminator`, prunes IR
nodes that are computed but never referenced downstream, as long as removing them is provably safe
(no tool-call side effect — see `src/Flow.Compiler/DeadNodeEliminator.cs` for the exact rules).
Because this runs *before* validation, a workflow that would previously have failed validation only
because of a problem in an unreferenced, now-pruned node instead validates successfully — a
deliberate behavior change, since validation only ever sees the already-cleaned-up workflow.
Likewise, a call to a dead tool that would have failed at runtime no longer runs at all, since the
call itself is removed.

**Validation** (`Flow.Validation`, run as one fixed pipeline via `WorkflowValidator.CreateDefault()`):

| Pass | Checks |
|---|---|
| `ToolResolutionValidator` | every called tool exists in the catalog |
| `DataflowValidator` | every referenced name is actually defined by a prior node or `input` |
| `TypeValidator` | structural type compatibility, including unresolvable paths (`T104`) |
| `NullabilityValidator` | optional values are proven non-null (via an `if` node's own bare `!= null`/`== null` condition) before being dereferenced |
| `OutputValidator` | the workflow's `return` actually satisfies the declared output type |
| `BoundednessValidator` | loops (`foreach`) carry an explicit, finite iteration limit |
| `ConstantProvenanceValidator` | every literal the model introduces declares where it came from (prompt / tool schema / policy) |
| `SemanticBindingValidator` | semantic types (e.g. `CustomerId` vs `InvoiceId`) aren't cross-wired even when structurally compatible |

If a candidate fails, `RepairLoop` re-prompts the model with the exact diagnostics and retries (3
attempts by default). If it still can't produce a valid candidate — or the model itself flags the
prompt as ambiguous — compilation reports `Uncompilable` with the diagnostics/unresolved reasons,
rather than executing something nobody actually asked for.

## Project layout

| Project | Purpose |
|---|---|
| `src/Flow.TypeSystem` | The `FlowType` hierarchy (primitive, object, list, optional, semantic, enum) and structural compatibility rules |
| `src/Flow.Contracts` | `ToolDefinition`/`ToolCatalog` — the MCP tool contract shape the compiler and executor both consume |
| `src/Flow.IR` | The canonical intermediate representation: `WorkflowDefinition` and its node/expression types |
| `src/Flow.Validation` | The 8-pass validator pipeline described above |
| `src/Flow.Analysis` | Static analysis over compiled IR — which tools a workflow calls (`CapabilityAnalyzer`) and its read/write/delete/external effect surface (`EffectAnalyzer`) |
| `src/Flow.Runtime` | `WorkflowExecutor` — runs a validated `WorkflowDefinition` against an `IMcpInvoker`, no LLM involved |
| `src/Flow.Compiler` | The compiler itself: candidate generation/repair loop, prompt building, and the two model providers (`Providers/OpenAi*`, `Providers/Anthropic*`) |
| `src/Flow.Cli` | Console app with three subcommands: `compile` (a JSON scenario file → workflow, optionally saved to a file), `run` (a saved workflow → executed against a live MCP server), and `scaffold` (discover a live MCP server's tools into a scenario file's `tools` array) |
| `tests/*.Tests` | Deterministic unit tests (scripted-fake model responses; no network) |
| `tests/Flow.IntegrationTests` | Live tests against the real OpenAI/Anthropic APIs, skipped automatically when the relevant API key isn't set |
| `examples/` | A runnable demo MCP server plus eight end-to-end example scenarios (see [examples/README.md](examples/README.md)) |

## Getting started

Requires the .NET 9 SDK (the `src/`/`tests/` projects target `net8.0`, but
`examples/Flow.Examples.McpServer` targets `net9.0` — see [examples/README.md](examples/README.md)
— so building the full solution via `dotnet build` needs the 9 SDK).

```bash
dotnet build
dotnet test --filter "FullyQualifiedName!~Flow.IntegrationTests"   # deterministic suite, no API key needed, ~261 tests
```

To also run the live integration tests, set an API key first:

```bash
OPENAI_API_KEY=sk-...      dotnet test tests/Flow.IntegrationTests --filter "FullyQualifiedName!~Anthropic"
ANTHROPIC_API_KEY=sk-ant-... dotnet test tests/Flow.IntegrationTests --filter "FullyQualifiedName~Anthropic"
```

Each integration test simply returns early (not a hard failure) if its provider's key isn't set, so
`dotnet test tests/Flow.IntegrationTests` with no keys at all is safe to run — it just won't
exercise anything.

## Using Flow.Cli

`Flow.Cli` has three subcommands:

- `compile` turns a JSON **scenario file** — a prompt, its input/output types, and the tool catalog
  — into a workflow and prints the result. Add `--save <path>` to also persist the compiled
  workflow to a file `run` can load later. This also writes a second, human-readable rendering of
  the workflow next to it, at a path derived from `--save`'s own path (the extension swapped to
  `.txt`; if `--save`'s path already ends in `.txt`, the derived name is `<path>.readable.txt`
  instead, so it doesn't overwrite the JSON file). That derived path isn't something you choose —
  if a file already exists there, it's silently overwritten — and if writing it fails, that's only
  a warning; the JSON file and `compile`'s exit code are unaffected. For `--save workflow.json`,
  `workflow.txt` holds just the node listing — no JSON, no status/assumptions wrapper — for example,
  for the scenario behind the [Example](#example) above:

  ```
    call getCustomer = crm.getCustomerById(id: input.customerId)
    return { customerName: getCustomer.name }
  ```
- `run` loads a workflow file previously written by `compile --save` and executes it against a
  live MCP server — no LLM calls, no scenario file needed at this stage.
- `scaffold` discovers a live MCP server's tools and writes most of that scenario file for you (see
  below).

```bash
dotnet run --project src/Flow.Cli -- compile scenario.json --save workflow.json [--provider openai|anthropic]
```

`--provider` defaults to `openai`. Exit codes: `0` compiled successfully, `1` compiled to
`Uncompilable` (the compiler correctly rejected the prompt), `2` a usage/setup problem (bad
arguments, missing file, malformed scenario JSON, missing API key, or the JSON `--save` file itself
failing to write). A failure to write the derived human-readable `.txt` sibling is not one of the
`2` cases — it's reported as a warning on stderr and doesn't change the exit code.

```bash
dotnet run --project src/Flow.Cli -- run workflow.json --mcp-url http://localhost:3001/mcp --input-json '{"customerId":"c1"}'
# or, reading the input from a file instead:
dotnet run --project src/Flow.Cli -- run workflow.json --mcp-url http://localhost:3001/mcp --input input.json
```

Example scenario file — the one behind the [Example](#example) output above:

```json
{
  "prompt": "Given a customer id, look up and return the customer's name.",
  "inputType": { "kind": "object", "name": "Request", "fields": {
    "customerId": { "kind": "primitive", "name": "String" }
  } },
  "outputType": { "kind": "object", "name": "Result", "fields": {
    "customerName": { "kind": "primitive", "name": "String" }
  } },
  "tools": [
    {
      "name": "crm.getCustomerById",
      "effect": "Read",
      "retry": "Safe",
      "inputType": { "kind": "object", "name": "In", "fields": {
        "id": { "kind": "primitive", "name": "String" }
      } },
      "outputType": { "kind": "object", "name": "Customer", "fields": {
        "name": { "kind": "primitive", "name": "String" }
      } }
    }
  ]
}
```

A type is one of six `kind`s:

- `primitive` (`name`: `Null`/`Bool`/`Int`/`Decimal`/`String`/`Date`/`DateTime`/`Duration`)
- `object` (`name` + `fields`)
- `list` (`elementType`)
- `optional` (`innerType`)
- `semantic` (`name` + `underlying`)
- `enum` (`name` + `values`)

`effect` is one of `Pure`/`Read`/`Write`/`Delete`/`External`/`Unknown`; `retry` is one of
`Never`/`Safe`/`Idempotent`/`IdempotentWithKey`.

### Scaffolding a scenario from a live MCP server

Hand-authoring the `tools` array is the tedious part of a scenario file. `scaffold` discovers a
real MCP server's tools (over Streamable HTTP) and writes a complete scenario file for you —
except `inputType`/`outputType`, which describe your workflow's contract, not any tool's, so
nothing can discover them:

```bash
dotnet run --project src/Flow.Cli -- scaffold out.json --mcp-url https://your-mcp-server/mcp --prompt "Your prompt here"
```

It prints a "needs manual review" list of everything it couldn't determine:

- `inputType`/`outputType` (always — these describe your workflow, not any tool)
- each tool's `effect`/`retry` (MCP doesn't carry this, so every tool defaults to `Unknown`/`Never`)
- any per-field schema shape it couldn't convert (`oneOf`/`anyOf`/`$ref`, or a missing output
  schema) — those fields get a `String` or empty-object placeholder instead of a silent guess

### Authenticating to a protected MCP server

Both `scaffold` and `run` connect to an MCP server the same way, and both support the same single
static credential: set `MCP_AUTH_TOKEN` to the full header value the server expects (e.g.
`Bearer sk-...`), and it's sent as the `Authorization` header on every request. If the server needs
a different header name, add `--mcp-auth-header <Name>` (e.g. `--mcp-auth-header X-Api-Key`) to
either command.

```bash
MCP_AUTH_TOKEN="Bearer sk-..." dotnet run --project src/Flow.Cli -- scaffold out.json --mcp-url https://your-mcp-server/mcp --prompt "..."
MCP_AUTH_TOKEN="secret-key" dotnet run --project src/Flow.Cli -- run workflow.json --mcp-url https://your-mcp-server/mcp --mcp-auth-header X-Api-Key --input-json '{}'
```

Passing `--mcp-auth-header` without `MCP_AUTH_TOKEN` set is a usage error (exit 2) — the flag alone
has nothing to send. An invalid header name (not a valid HTTP header token) or a token containing
control characters (e.g. a stray trailing newline) are also rejected before any network I/O, with
the same exit code 2.

## Providers

Two `ISemanticCompilerModel` implementations exist, both going through the same provider-neutral
generate/repair/parse logic (`GenericSemanticCompilerModel`):

| Provider | Env var | Default model | Structured output |
|---|---|---|---|
| `OpenAiSemanticCompilerModel` | `OPENAI_API_KEY` | `gpt-4.1-mini` | Enforced via OpenAI's strict `json_schema` response format |
| `AnthropicSemanticCompilerModel` | `ANTHROPIC_API_KEY` | `claude-haiku-4-5` | Not enforced — Anthropic's structured-output schema validator rejects the IR schema's recursive `$ref`s (expressions/nodes containing themselves), so the schema is described as text in the prompt instead, relying on the repair loop for malformed responses |

Both are exercised end-to-end against the real APIs by `tests/Flow.IntegrationTests` (skipped
without a key).

Compilation success is inherently probabilistic for an LLM-based compiler bounded by a fixed number
of repair attempts — the same prompt can occasionally compile on one run and hit `Uncompilable` on
another, particularly for prompts requiring multi-step reasoning (filter + sort + aggregate + a
null-safety guard, all in the right shape at once).

## Known limitations

This project's own rule is to say what it doesn't know rather than guess — the same rule applies
here, to itself:

- **Compilation is not 100% reliable for harder prompts.** "Refund the oldest unpaid invoice"
  (filter → sort → aggregate-first → null-guard → conditional write) passes most of the time on
  both providers but has been observed to fail occasionally within the default 3 repair attempts.
- **`run` requires structured MCP tool output.** A tool that only returns unstructured text content
  (no `structuredContent` in its `CallToolResult`) can't be used by a compiled workflow — Flow
  Runtime has no way to interpret free text as a typed value.
- **`run` has the same no-timeout/URL-echo gaps on its MCP connection that `scaffold` already has**
  (see below).
- **`run` never re-validates.** Validation happens once, at `compile` time. If a compiled workflow
  file is hand-edited into something invalid, `run` has no validator pass to catch it — it will
  simply fail at execution time in whatever way the invalid IR causes.
- **Anthropic responses aren't schema-enforced**, only prompted for — see the Providers table above.
- **`SemanticBindingValidator`** can only check a binding sourced from a `Filter`/`Sort` node's list
  output once it's been reduced to a scalar via `AggregateNode(First)` — a path can never select a
  field directly off a list.
- **`CandidateWorkflowAst.Interpretations`** is surfaced on `CompilationResult` but currently only
  ever populated when the model chooses to record one; there's no requirement that it explain every
  non-trivial choice.
- **Auth support is a single static header only** — `--mcp-auth-header`/`MCP_AUTH_TOKEN` (see
  "Authenticating to a protected MCP server" above) covers a static API key or bearer token. There's
  no OAuth flow — the MCP SDK has one, but it needs a browser and is a mismatch for a CLI meant to
  run in scripts/pipelines. The header is sent in cleartext over whatever scheme `--mcp-url` uses —
  an `http://` URL transmits the token unencrypted; use `https://` for anything real.
- **`scaffold` has no request timeout** — a slow or wedged MCP endpoint hangs the CLI indefinitely
  (Ctrl+C is the only way out).
- **`scaffold` echoes the MCP URL verbatim** in its output and in any connection-failure message —
  credentials embedded in the URL (`https://user:token@host/mcp`) will appear in your terminal/logs.
- **A tool whose JSON Schema has an empty `enum: []`** converts to an enum type with no values,
  which `compile` then rejects outright — the one schema shape `scaffold` doesn't yet degrade
  gracefully for, unlike every other unsupported shape (which gets a placeholder plus a warning
  instead of a hard failure downstream).

## License

[MIT](LICENSE)
