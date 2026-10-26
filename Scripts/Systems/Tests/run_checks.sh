#!/usr/bin/env bash
# Source-tree checks. Each suite receives a different temporary save/config root.
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "$script_dir/../../.." && pwd)"
qa_root="${1:-$(mktemp -d /tmp/foxtrot-checks.XXXXXX)}"
mkdir -p -- "$qa_root"
qa_root="$(cd -- "$qa_root" && pwd)"
printf 'QA logs: %s\n' "$qa_root"
dotnet build "$project_root/Top Down Demo.csproj" --no-restore --nologo > "$qa_root/build.log" 2>&1
for suite in Environment Progression Settings Controls Dialogue Audio Persistence Supply Combat Timing Navigation Campaign; do
    log="$qa_root/$suite.log"
    if ! timeout 300 env XDG_DATA_HOME="$qa_root/$suite/data" XDG_CONFIG_HOME="$qa_root/$suite/config" \
        godot --headless --path "$project_root" "res://Scenes/Tests/${suite}Smoke.tscn" -- --stress-gc > "$log" 2>&1; then
        tail -n 60 "$log"
        exit 1
    fi
    if ! rg -q 'SMOKE PASS' "$log" || rg -q '^(ERROR|WARNING):' "$log"; then
        tail -n 60 "$log"
        exit 1
    fi
    rg 'SMOKE PASS' "$log"
done
printf 'All source-tree suites passed. Logs retained at %s\n' "$qa_root"
