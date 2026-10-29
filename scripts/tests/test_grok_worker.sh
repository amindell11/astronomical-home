#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/grok_worker.py

# Hermetic regression for the Grok build worker. A temp repo stands in for the primary tree, with a
# slot and a sibling worktree. GROK_WORKER_AGENT_CMD swaps Cursor for a fake agent that plays one
# canned call per launch (exit code, stderr, files to write, tool-call paths, STATUS) and records its
# argv, so each case pins the exit code and trailers the worker derives from Cursor's stream-json.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER="$SCRIPT_DIR/../grok_worker.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }
native() { (cd "$1" && { pwd -W 2>/dev/null || pwd; }); }

git init -q "$TMP/primary"
git -C "$TMP/primary" -c user.name=t -c user.email=t@t commit -q --allow-empty -m init
git -C "$TMP/primary" worktree add -q --detach "$TMP/slot"
git -C "$TMP/primary" worktree add -q --detach "$TMP/sib"
mkdir -p "$TMP/slot/src"
P="$(native "$TMP/primary")"
S="$(native "$TMP/slot")"
SIB="$(native "$TMP/sib")"
T="$(native "$TMP")"
PY="$(python3 -c 'import sys; print(sys.executable.replace("\\", "/"))')"
printf 'Create the files the test asks for.\n' > "$T/brief.md"

cat > "$T/fake_agent.py" <<'EOF'
import json, os, pathlib, sys
plan = pathlib.Path(os.environ["FAKE_PLAN"])
n = len(list(plan.glob("argv-*.json"))) + 1
(plan / f"argv-{n}.json").write_text(json.dumps(sys.argv[1:]))
call = json.loads((plan / f"call-{n}.json").read_text())
ws = sys.argv[sys.argv.index("--workspace") + 1]
for rel in call.get("write", []):
    pathlib.Path(ws, rel).write_text("x")
sys.stderr.write(call.get("stderr", ""))
if call.get("status"):
    print(json.dumps({"type": "system", "subtype": "init", "model": "fake-grok"}))
    for p in call.get("paths", []):
        print(json.dumps({"type": "tool_call", "subtype": "started",
                          "tool_call": {"readToolCall": {"args": {"path": p}}}}))
    for p, content in call.get("edits", []):
        print(json.dumps({"type": "tool_call", "subtype": "started",
                          "tool_call": {"editToolCall": {"args": {"path": p, "streamContent": content}}}}))
    text = f"Done.\n**STATUS: {call['status']}**\nCHANGED: -\nVERIFIED: -\nNOTICED: none"
    print(json.dumps({"type": "result", "subtype": "success", "result": text, "usage": {}}))
sys.exit(call.get("exit", 0))
EOF

# plan <case> <call-json>... : one canned call per launch, in order.
plan() {
  local dir="$T/plan-$1" n=0
  shift
  mkdir -p "$dir"
  for call in "$@"; do
    n=$((n + 1))
    printf '%s' "$call" > "$dir/call-$n.json"
  done
}
calls() { find "$T/plan-$1" -name 'argv-*.json' | wc -l | tr -d ' '; }
# work <case> <worker args>... : sets rc and out (stdout); stderr lands in $TMP/err-<case>.
work() {
  local name="$1"
  shift
  rc=0
  out="$(GROK_WORKER_AGENT_CMD="'$PY' '$T/fake_agent.py'" GROK_WORKER_RETRY_DELAY_S=0 \
    FAKE_PLAN="$T/plan-$name" python3 "$WORKER" --slot "$S" --brief "$T/brief.md" \
    --out "$T/out-$name" "$@" 2>"$TMP/err-$name")" || rc=$?
}
trailer() { sed -n "s/^$1=//p" <<<"$out"; }
expect() {  # expect <case> <rc> <KEY=value>...
  local name="$1" want="$2" kv
  shift 2
  [[ "$rc" == "$want" ]] || fail "$name: exit $rc, want $want; stdout: $out; stderr: $(cat "$TMP/err-$name")"
  for kv in "$@"; do
    [[ "$(trailer "${kv%%=*}")" == "${kv#*=}" ]] || fail "$name: want $kv; stdout: $out"
  done
}

# --- DONE with a passing verify; permissions, argv and exclude ---
plan done '{"status": "DONE", "write": ["src/hello.txt"]}'
work done --verify "test -f src/hello.txt"
expect done 0 GROK_WORKER=done ROUNDS=1 VERIFY=pass OUTSIDE_READS=0 MODEL=fake-grok
[[ "$(trailer TRANSCRIPT)" == *transcript-1.jsonl && -s "$(trailer TRANSCRIPT)" ]] || fail "done: no transcript: $out"
[[ "$(wc -l <<<"$out" | tr -d ' ')" == 7 ]] || fail "done: stdout holds more than the trailers: $out"
grep -q "STATUS: DONE" "$TMP/err-done" || fail "done: Grok's STATUS block missing from stderr"
python3 - "$T/plan-done/argv-1.json" "$S" <<'EOF' || fail "done: Cursor argv is wrong"
import json, sys
argv = json.load(open(sys.argv[1]))
assert "--force" not in argv and "--yolo" not in argv, argv
assert argv[argv.index("--workspace") + 1] == sys.argv[2], argv
assert argv[argv.index("--model") + 1] == "grok-4.7-high", argv
assert argv[-1].startswith("You are the build worker") and argv[-1].endswith("Create the files the test asks for.\n"), argv[-1]
EOF
python3 - "$S/.cursor/cli.json" "$P" "$SIB" "$S" <<'EOF' || fail "done: cli.json permissions are wrong"
import json, sys
perms = json.load(open(sys.argv[1]))["permissions"]
assert "Write(src/**)" in perms["allow"] and "Read(**)" in perms["allow"], perms
for p in sys.argv[2:4]:
    assert f"Read({p}/**)" in perms["deny"], (p, perms)
assert f"Read({sys.argv[4]}/**)" not in perms["deny"], perms
assert {"Shell(gh)", "Shell(git:push*)", "Shell(git:commit*)"} <= set(perms["deny"]), perms
EOF
EXCLUDE="$(git -C "$S" rev-parse --git-path info/exclude)"
[[ "$(grep -cx '\.cursor/' "$EXCLUDE")" == 1 ]] || fail "done: .cursor/ is not in info/exclude once"
[[ "$(git -C "$S" status --porcelain)" == "?? src/" ]] || fail "done: slot status shows more than src/: $(git -C "$S" status --porcelain)"

# --- a refused launch is retried; a second run leaves info/exclude alone ---
plan retry '{"exit": 1, "stderr": "ActionRequiredError: Invalid internal service header\n"}' \
  '{"status": "DONE"}'
work retry
expect retry 0 GROK_WORKER=done ROUNDS=1 VERIFY=none
[[ "$(calls retry)" == 2 ]] || fail "retry: want 2 launches, got $(calls retry)"
grep -q "refused the launch" "$TMP/err-retry" || fail "retry: the refusal was not reported"
[[ "$(grep -cx '\.cursor/' "$EXCLUDE")" == 1 ]] || fail "retry: .cursor/ was appended twice"

# --- refused on every attempt ---
R='{"exit": 1, "stderr": "ActionRequiredError: AI Model Not Found\n"}'
plan refused "$R" "$R" "$R" "$R"
work refused
expect refused 2 GROK_WORKER=refused ROUNDS=1 VERIFY=none
[[ "$(calls refused)" == 4 ]] || fail "refused: want 4 launches, got $(calls refused)"

# --- a failed verify relaunches with its output, and round 2 passes ---
plan round2 '{"status": "DONE"}' '{"status": "DONE", "write": ["src/fixed.txt"]}'
work round2 --verify "echo verify-marker-42; test -f src/fixed.txt"
expect round2 0 GROK_WORKER=done ROUNDS=2 VERIFY=pass
[[ "$(trailer TRANSCRIPT)" == *transcript-2.jsonl ]] || fail "round2: TRANSCRIPT is not round 2's: $out"
python3 - "$T/plan-round2/argv-2.json" <<'EOF' || fail "round2: the retry prompt does not quote the verify output"
import json, sys
prompt = json.load(open(sys.argv[1]))[-1]
assert "verify-marker-42" in prompt and "test -f src/fixed.txt" in prompt, prompt
assert "Create the files the test asks for." in prompt, prompt
EOF

# --- verify still failing after the last round ---
plan exhausted '{"status": "DONE"}'
work exhausted --rounds 1 --verify "test -f src/never.txt"
expect exhausted 1 GROK_WORKER=verify_failed ROUNDS=1 VERIFY=fail

# --- a verify that cannot judge stops without a retry round ---
plan verr '{"status": "DONE"}' '{"status": "DONE"}'
work verr --verify "echo stale-snapshot; exit 3"
expect verr 1 GROK_WORKER=verify_error ROUNDS=1 VERIFY=error
[[ "$(calls verr)" == 1 ]] || fail "verr: relaunched after a verify that could not judge"

# --- a --fail-codes exit is a failed change, so round 2 runs ---
plan failcodes '{"status": "DONE"}' '{"status": "DONE", "write": ["src/compiled.txt"]}'
work failcodes --fail-codes 1,2 --verify "test -f src/compiled.txt || exit 2"
expect failcodes 0 GROK_WORKER=done ROUNDS=2 VERIFY=pass

# --- BLOCKED stops before verify ---
plan blocked '{"status": "BLOCKED"}' '{"status": "DONE"}'
work blocked --verify "true"
expect blocked 1 GROK_WORKER=blocked ROUNDS=1 VERIFY=none
[[ "$(calls blocked)" == 1 ]] || fail "blocked: relaunched after BLOCKED"

# --- tool calls naming paths outside the slot are counted and warned about ---
[[ "$P" =~ ^([A-Za-z]):(.*)$ ]] || fail "outside: the temp dir has no drive letter: $P"
MSYS_P="/${BASH_REMATCH[1],,}${BASH_REMATCH[2]}"
plan outside "$(printf '{"status": "DONE", "paths": ["%s/src/a.cs", "%s/src/b.cs", "%s/x/y.cs", "cat %s/src/b.cs 2>/dev/null"], "edits": [["%s/src/c.cs", "// mirrors %s/src/c.cs"]]}' \
  "$S" "$SIB" "$MSYS_P" "$SIB" "$S" "$P")"
work outside
expect outside 0 GROK_WORKER=done OUTSIDE_READS=2
grep -q "WARNING: Grok's tool calls named 2 path" "$TMP/err-outside" || fail "outside: no loud warning"

echo "PASS: grok worker"
