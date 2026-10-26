#!/usr/bin/env python3
"""Exercise real game Quit paths in isolated profiles and verify clean process termination."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--graphical", action="store_true", help="Requires a display; uses compatibility rendering and Dummy audio.")
parser.add_argument("--repeat", type=int, default=1)
parser.add_argument("--logs", type=Path)
args = parser.parse_args()
if args.repeat < 1:
    parser.error("--repeat must be positive")
project_root = Path(__file__).resolve().parents[3]
logs = (args.logs or Path(tempfile.mkdtemp(prefix="foxtrot-exit-"))).resolve()
logs.mkdir(parents=True, exist_ok=True)
for repeat in range(args.repeat):
    for mode in ["menu", "pause", "combat", "window", "dialogue", "transition"]:
        name = f"{repeat + 1}-{mode}"
        env = os.environ.copy()
        env["XDG_DATA_HOME"] = str(logs / name / "data")
        env["XDG_CONFIG_HOME"] = str(logs / name / "config")
        command = ["godot", "--path", str(project_root)]
        if args.graphical:
            command += ["--audio-driver", "Dummy", "--rendering-method", "gl_compatibility", "--resolution", "1280x720"]
        else:
            command += ["--headless"]
        command += ["res://Scenes/Tests/ExitSmoke.tscn", "--", "--exit-" + mode]
        path = logs / (name + ".log")
        with path.open("w") as output:
            try:
                result = subprocess.run(command, cwd=project_root, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=20)
            except subprocess.TimeoutExpired:
                raise SystemExit(f"FAIL {name}: process did not terminate; see {path}")
        content = path.read_text()
        if result.returncode or "EXIT SMOKE REQUEST:" not in content or any(line.startswith(("ERROR:", "WARNING:")) for line in content.splitlines()):
            raise SystemExit(f"FAIL {name}: exit {result.returncode}; see {path}")
        print(f"PASS {name}: clean exit 0", flush=True)
print(f"EXIT CHECKS PASS: {6 * args.repeat} clean exits; logs: {logs}")
