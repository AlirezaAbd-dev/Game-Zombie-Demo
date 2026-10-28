#!/usr/bin/env python3
"""Run the regression suites inside a packaged Linux QA release, outside the source project."""
import argparse
import os
from pathlib import Path
import subprocess

SUITES = ("Environment", "Progression", "Settings", "Controls", "Dialogue", "Audio", "Persistence", "Supply", "Combat", "Timing", "Navigation", "Campaign")
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("binary", type=Path)
parser.add_argument("--logs", type=Path, required=True)
parser.add_argument("--suite", action="append", choices=SUITES)
parser.add_argument("--graphical", action="store_true")
args = parser.parse_args()
binary = args.binary.resolve()
if not binary.is_file() or not os.access(binary, os.X_OK):
    parser.error("binary must be an executable Linux QA export")
logs = args.logs.resolve()
logs.mkdir(parents=True, exist_ok=True)
empty_cwd = logs / "empty-cwd"
empty_cwd.mkdir(exist_ok=True)
if (empty_cwd / "project.godot").exists():
    parser.error("test working directory must not contain a source project")
for suite in args.suite or SUITES:
    env = os.environ.copy()
    env.update({"XDG_DATA_HOME": str(logs / suite / "data"), "XDG_CONFIG_HOME": str(logs / suite / "config"),
                "XDG_CACHE_HOME": str(logs / "runtime-cache"), "DOTNET_ROOT": "/nonexistent", "DOTNET_ROOT_X64": "/nonexistent"})
    command = [str(binary)]
    if args.graphical:
        command += ["--audio-driver", "Dummy", "--rendering-method", "gl_compatibility", "--resolution", "1280x720", "--disable-render-loop"]
    else:
        command += ["--headless"]
    command += ["--", "--qa-suite=" + suite, "--stress-gc"]
    if args.graphical:
        command += ["--capture-playtest"]
    path = logs / (suite + ".log")
    with path.open("w") as output:
        try:
            result = subprocess.run(command, cwd=empty_cwd, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=360)
        except subprocess.TimeoutExpired:
            raise SystemExit(f"FAIL {suite}: exceeded timeout; see {path}")
    content = path.read_text()
    missing_summary = "SMOKE PASS:" not in content and "NAVIGATION SMOKE PASS" not in content
    errors = any(line.startswith(("ERROR:", "WARNING:")) for line in content.splitlines())
    if result.returncode or f"PACKAGED QA SUITE: {suite}" not in content or missing_summary or errors:
        raise SystemExit(f"FAIL {suite}: exit {result.returncode}; see {path}")
    print(next(line for line in content.splitlines() if "SMOKE PASS" in line), flush=True)
print(f"PACKAGED CHECKS PASS; logs: {logs}")
