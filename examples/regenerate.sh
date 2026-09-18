#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

if [ -z "${OPENAI_API_KEY:-}" ]; then
  echo "OPENAI_API_KEY is not set. Set it and re-run." >&2
  exit 1
fi

echo "Starting Flow.Examples.McpServer..."
dotnet run --project examples/Flow.Examples.McpServer > /tmp/flow-examples-mcp-server.log 2>&1 &
SERVER_PID=$!
trap 'echo "Stopping demo MCP server (pid $SERVER_PID)"; kill "$SERVER_PID" 2>/dev/null || true' EXIT

echo "Waiting for the demo MCP server to become reachable..."
for _ in $(seq 1 30); do
  if curl -s -o /dev/null "http://localhost:5100/mcp"; then
    break
  fi
  sleep 1
done

for dir in examples/[0-9][0-9]-*/; do
  name="${dir%/}"
  scenario="$dir/scenario.json"
  if [ ! -f "$scenario" ]; then
    continue
  fi

  if [ -f "$dir/sample-input.json" ]; then
    echo "Compiling + running $name..."
    dotnet run --project src/Flow.Cli -- compile "$scenario" --save "$dir/workflow.json"
    dotnet run --project src/Flow.Cli -- run "$dir/workflow.json" \
      --mcp-url http://localhost:5100/mcp --input "$dir/sample-input.json" \
      > "$dir/run-output.txt"
  else
    echo "Compiling (no run step) $name..."
    dotnet run --project src/Flow.Cli -- compile "$scenario" > "$dir/compile-output.txt" || true
  fi
done

echo "Done."
