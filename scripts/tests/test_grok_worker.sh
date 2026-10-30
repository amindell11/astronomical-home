#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/grok_worker.py

# Hermetic regression for the Grok build worker. A temp repo stands in for the primary tree, with a
# slot and a sibling worktree. GROK_WORKER_AGENT_CMD swaps Cursor for a fake agent that answers the
# `models` preflight from the case's models.json (a Grok listing by default) and plays one canned
# call per launch (exit code, stderr, files to write, tool-call paths, a hang, STATUS), recording
# its argv, so each case pins the exit code and trailers the worker derives from Cursor's output.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKER="$SCRIPT_DIR/../grok_worker.py"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }
native() { (cd "$1" && { pwd -W 2>/dev/null || pwd; }); }
now_ms() { python3 -c 'import time; print(int(time.time() * 1000))'; }

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
import json, os, pathlib, sys, time
sys.stdout.reconfigure(encoding="utf-8")
plan = pathlib.Path(os.environ["FAKE_PLAN"])
if sys.argv[1:] == ["models"]:
    (plan / f"models-{len(list(plan.glob('models-*')))}").write_text("")
    spec = json.loads((plan / "models.json").read_text()) if (plan / "models.json").exists() else {}
    print(spec.get("out", "Available models\n\nauto - Auto (default)\ngrok-4.7-high - Grok 4.7  High​"))
    sys.exit(spec.get("exit", 0))
n = len(list(plan.glob("argv-*.json"))) + 1
(plan / f"argv-{n}.json").write_text(json.dumps(sys.argv[1:]))
call = json.loads((plan / f"call-{n}.json").read_text())
ws = sys.argv[sys.argv.index("--workspace") + 1]
for rel in call.get("write", []):
    pathlib.Path(ws, rel).write_text("x")
for rel in call.get("append", []):
    with open(pathlib.Path(ws, rel), "a") as f:
        f.write("y")
sys.stderr.write(call.get("stderr", ""))
if call.get("status"):
    print(json.dumps({"type": "system", "subtype": "init", "model": "fake-grok"}))
    for p in call.get("paths", []):
        print(json.dumps({"type": "tool_call", "subtype": "started",
                          "tool_call": {"readToolCall": {"args": {"path": p}}}}))
    for p, content in call.get("edits", []):
        print(json.dumps({"type": "tool_call", "subtype": "started",
                          "tool_call": {"editToolCall": {"args": {"path": p, "streamContent": content}}}}))
    sys.stdout.flush()
    time.sleep(call.get("hang", 0))
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
preflights() { find "$T/plan-$1" -name 'models-*' | wc -l | tr -d ' '; }
# work <case> <worker args>... : sets rc and out (stdout); stderr lands in $TMP/err-<case>.
# HEALTH (the health record) defaults to one per case; MINUTE sets GROK_WORKER_MINUTE_S.
work() {
  local name="$1"
  shift
  rc=0
  out="$(GROK_WORKER_AGENT_CMD="'$PY' '$T/fake_agent.py'" GROK_WORKER_RETRY_DELAY_S=0 \
    GROK_WORKER_HEALTH_FILE="${HEALTH:-$T/health-$name.json}" GROK_WORKER_MINUTE_S="${MINUTE:-60}" \
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
expect done 0 GROK_WORKER=done ROUNDS=1 VERIFY=pass OUTSIDE_READS=0 MODEL=fake-grok FALLBACK=none DIRTY=src/hello.txt
[[ "$(trailer TRANSCRIPT)" == *transcript-1.jsonl && -s "$(trailer TRANSCRIPT)" ]] || fail "done: no transcript: $out"
[[ "$(wc -l <<<"$out" | tr -d ' ')" == 9 ]] || fail "done: stdout holds more than the trailers: $out"
[[ "$(preflights done)" == 1 ]] || fail "done: want one preflight, got $(preflights done)"
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

# --- a transient refusal is retried; a second run leaves info/exclude alone ---
plan retry '{"exit": 1, "stderr": "ActionRequiredError: Invalid internal service header\n"}' \
  '{"status": "DONE"}'
work retry
expect retry 0 GROK_WORKER=done ROUNDS=1 VERIFY=none DIRTY=
[[ "$(calls retry)" == 2 ]] || fail "retry: want 2 launches, got $(calls retry)"
grep -q "refused the launch" "$TMP/err-retry" || fail "retry: the refusal was not reported"
[[ "$(grep -cx '\.cursor/' "$EXCLUDE")" == 1 ]] || fail "retry: .cursor/ was appended twice"

# --- transient refusals stop after two retries; other failures after one ---
R='{"exit": 1, "stderr": "ActionRequiredError: AI Model Not Found\n"}'
plan refused "$R" "$R" "$R" "$R"
work refused
expect refused 3 GROK_WORKER=refused ROUNDS=1 VERIFY=none FALLBACK=opus DIRTY=
[[ "$(calls refused)" == 3 ]] || fail "refused: want 3 launches, got $(calls refused)"
[[ -s "$T/health-refused.json" ]] || fail "refused: no health record"
O='{"exit": 1, "stderr": "Error: socket hang up\n"}'
plan other "$O" "$O" "$O"
work other
expect other 3 GROK_WORKER=refused FALLBACK=opus
[[ "$(calls other)" == 2 ]] || fail "other: want 2 launches, got $(calls other)"

# --- an error that won't clear by itself is not retried ---
plan persistent '{"exit": 1, "stderr": "ActionRequiredError: You have hit your usage limit for this plan.\n"}' '{"status": "DONE"}'
work persistent
expect persistent 3 GROK_WORKER=unavailable FALLBACK=opus DIRTY=
[[ "$(calls persistent)" == 1 ]] || fail "persistent: retried a usage-limit error"

# --- a failed preflight launches nothing; the health record answers the next call; --recheck bypasses it ---
plan preflight '{"status": "DONE"}'
printf '%s' '{"exit": 1, "out": "Failed to load models: [unavailable] connect ECONNREFUSED"}' > "$T/plan-preflight/models.json"
HEALTH="$T/health-shared.json" work preflight
expect preflight 3 GROK_WORKER=unavailable ROUNDS=0 FALLBACK=opus DIRTY= TRANSCRIPT=
[[ "$(calls preflight)" == 0 ]] || fail "preflight: launched after a failed preflight"
grep -q "ECONNREFUSED" "$T/health-shared.json" || fail "preflight: the health record lacks the reason"
t0="$(now_ms)"
HEALTH="$T/health-shared.json" work preflight
elapsed=$(( $(now_ms) - t0 ))
expect preflight 3 GROK_WORKER=unavailable ROUNDS=0 FALLBACK=opus
[[ "$(preflights preflight)" == 1 && "$(calls preflight)" == 0 ]] || fail "memory: the agent ran while the record was fresh"
(( elapsed < 3000 )) || fail "memory: took ${elapsed}ms"
rm "$T/plan-preflight/models.json"
HEALTH="$T/health-shared.json" work preflight --recheck
expect preflight 0 GROK_WORKER=done
[[ "$(preflights preflight)" == 2 && "$(calls preflight)" == 1 ]] || fail "recheck: want a preflight and a launch"
[[ ! -e "$T/health-shared.json" ]] || fail "recheck: a done run left the health record"
plan nomodel '{"status": "DONE"}'
printf '%s' '{"out": "Available models\n\nauto - Auto (default)"}' > "$T/plan-nomodel/models.json"
work nomodel
expect nomodel 3 GROK_WORKER=unavailable
grep -q "does not list grok-4.7-high" "$TMP/err-nomodel" || fail "nomodel: the reason does not name the model"

# --- a stalled round is killed, and its partial edits are named ---
plan stall '{"status": "DONE", "write": ["src/partial.txt"], "append": ["src/hello.txt"], "hang": 60}'
t0="$(now_ms)"
MINUTE=1 work stall --stall-minutes 2
elapsed=$(( $(now_ms) - t0 ))
expect stall 1 GROK_WORKER=stalled FALLBACK=opus DIRTY=src/hello.txt,src/partial.txt
(( elapsed < 20000 )) || fail "stall: took ${elapsed}ms"
plan cap '{"status": "DONE", "hang": 60}'
MINUTE=1 work cap --stall-minutes 30 --max-minutes 3
expect cap 1 GROK_WORKER=timeout FALLBACK=opus

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
expect exhausted 1 GROK_WORKER=verify_failed ROUNDS=1 VERIFY=fail FALLBACK=opus

# --- a verify that cannot judge stops without a retry round ---
plan verr '{"status": "DONE", "write": ["src/verr.txt"]}' '{"status": "DONE"}'
work verr --verify "echo stale-snapshot; exit 3"
expect verr 1 GROK_WORKER=verify_error ROUNDS=1 VERIFY=error FALLBACK=fix-verify DIRTY=src/verr.txt
[[ "$(calls verr)" == 1 ]] || fail "verr: relaunched after a verify that could not judge"

# --- a --fail-codes exit is a failed change, so round 2 runs ---
plan failcodes '{"status": "DONE"}' '{"status": "DONE", "write": ["src/compiled.txt"]}'
work failcodes --fail-codes 1,2 --verify "test -f src/compiled.txt || exit 2"
expect failcodes 0 GROK_WORKER=done ROUNDS=2 VERIFY=pass

# --- BLOCKED stops before verify ---
plan blocked '{"status": "BLOCKED"}' '{"status": "DONE"}'
work blocked --verify "true"
expect blocked 1 GROK_WORKER=blocked ROUNDS=1 VERIFY=none FALLBACK=opus
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
