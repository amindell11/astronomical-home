"""Run Grok through the Cursor CLI in a pool slot from a build brief.

The default build worker (agent-worktree-pr-loop, Step 2): Grok edits the slot's tree, never
commits, and ends with a STATUS block; this script runs the caller's verify command after each
DONE round and relaunches with the verify output until it passes or the rounds run out. When
Grok can't run, the script says so within seconds so the caller falls back to an Opus subagent.

Usage:
  python scripts/grok_worker.py --slot <agent-N | path> --brief <file>
      [--verify "<shell cmd>"] [--fail-codes 1] [--rounds 2] [--model grok-4.7-high]
      [--stall-minutes 8] [--max-minutes 45] [--recheck] [--out <dir>]

  --slot    a pool slot name (resolved through `agent_worktree_pool.sh status --porcelain`)
            or the path of a git worktree root.
  --verify  run with Git Bash in the slot after each DONE round. Its exit: 0 = pass;
            a --fail-codes member = the change is wrong, so Grok gets the next round with
            its output; any other = the check could not judge, so the run stops with no retry.
  --fail-codes  comma list of verify exits that mean the change is wrong; default 1.
            0 is a usage error.
  --stall-minutes  kill a round whose transcript has not grown for this long (stalled).
  --max-minutes    cap on the whole run (timeout), checked during rounds and before each
            launch; a running verify is never killed.
  --recheck skip the health record and run the preflight.
  --out     where transcripts and logs go; default a fresh dir under the system temp dir.
            Never inside the slot.

Before the first launch: the health record (below), then a preflight, Cursor's `models` command
(PREFLIGHT_TIMEOUT_S), which must succeed and list --model. Either one failing is unavailable.

Exit codes:
  0  done: Grok reported DONE and --verify passed, or no --verify was given.
  1  Grok ran but didn't finish cleanly: blocked (BLOCKED or no STATUS line), verify_failed,
     verify_error, stalled or timeout. The tree may hold partial edits (DIRTY=).
  2  usage error (no trailers).
  3  Grok never started: unavailable or refused. The tree is untouched.

Machine channel: these KEY=value trailers on stdout, in this order, and nothing else there.
  GROK_WORKER=done|blocked|verify_failed|verify_error|stalled|timeout|unavailable|refused
  ROUNDS=<rounds launched>
  WALL_S=<whole seconds>
  VERIFY=pass|fail|error|none  the last verify run (exit 0 | a fail code | other); none
                             when none ran
  OUTSIDE_READS=<count>      distinct absolute paths outside the slot in Grok's tool-call args
                             (an edit counts only its target path)
  MODEL=<model>              as the transcript's init event names it; unknown when absent
  TRANSCRIPT=<path>          the last round's stream-json transcript; empty when none launched
  FALLBACK=none|fix-verify|opus  none on done, fix-verify on verify_error, opus otherwise
  DIRTY=<comma list>         slot files whose git status or content changed during the run
Progress, Grok's STATUS block and warnings go to stderr. OUTSIDE_READS > 0 never changes the
exit code; it prints a warning, because Grok has read sibling trees' solutions when stuck.

A launch that exits non-zero before any tool call failed to start. Its output is matched:
  PERSISTENT  no retry; unavailable.
  TRANSIENT   the backend refusing under load: TRANSIENT_RETRIES retries.
  other       OTHER_RETRIES retry.
Retry k sleeps RETRY_DELAY_S x k; out of retries is refused. A round after the first that fails
to start ends the run on the previous round's verify_failed, since the tree holds its edits.

Health record: HEALTH_FILE (%LOCALAPPDATA%/grok-worker/health.json), owned by this script:
  {"outcome": "unavailable"|"refused", "reason": <str>, "at": <ISO-8601 UTC>, "model": <slug>}
A run that ends unavailable or refused, or whose later round fails to start, writes it; every
other run that passes the preflight deletes it. While it is younger than HEALTH_TTL_S, a run
exits unavailable within a second, before any slot check or preflight, unless --recheck.

Files: <out>/transcript-<round>.jsonl (Cursor stdout), <out>/cursor-<round>.log (Cursor stderr,
every attempt), <out>/verify-<round>.log. In the slot: .cursor/cli.json (the permission
allowlist, rewritten each run) and a `.cursor/` line in the shared info/exclude.

Environment (test seams):
  GROK_WORKER_AGENT_CMD      replaces the Cursor command (split shell-style; Cursor's
                             arguments are appended, `models` for the preflight)
  GROK_WORKER_RETRY_DELAY_S  replaces RETRY_DELAY_S
  GROK_WORKER_MINUTE_S       seconds in a minute for --stall-minutes and --max-minutes
  GROK_WORKER_HEALTH_FILE    replaces HEALTH_FILE
"""

import argparse
import hashlib
import json
import os
import posixpath
import re
import shlex
import shutil
import subprocess
import sys
import tempfile
import time
from datetime import datetime, timezone
from pathlib import Path

PREFLIGHT_TIMEOUT_S = 20
HEALTH_TTL_S = 30 * 60
HEALTH_FILE = Path(os.environ.get("LOCALAPPDATA") or tempfile.gettempdir(), "grok-worker", "health.json")
TRANSIENT_RETRIES = 2
OTHER_RETRIES = 1
RETRY_DELAY_S = 15
POLL_S = 0.5
VERIFY_TAIL_LINES = 60
VERIFY_TAIL_CHARS = 4000
# Windows caps a command line at 32,767 chars; the retry prompt adds the verify tail.
MAX_PROMPT_CHARS = 26000

PERSISTENT = re.compile(r"not logged in|not authenticated|authentication required|unauthori[sz]ed"
                        r"|(usage|plan) limit|quota|billing", re.I)
TRANSIENT = re.compile(r"ActionRequiredError|Invalid internal service header|AI Model Not Found", re.I)

POOL_SLOT = re.compile(r"^agent-\d+$")
CURSOR_VERSION = re.compile(r"^(\d{4})\.(\d{1,2})\.(\d{1,2})(?:-(\d{2})-(\d{2})-(\d{2}))?-[a-f0-9]+$")
ABS_PATH = re.compile(r"(?<![\w./~-])(?:[A-Za-z]:[\\/]|/[A-Za-z]/)[^\s\"'`<>|*?;,()\[\]{}]*")
STATUS_LINE = re.compile(r"^STATUS:\s*(DONE|BLOCKED)\b")
ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")

ALLOW = ["Read(**)", "Write(src/**)", "Shell(git:status*)", "Shell(git:diff*)", "Shell(git:log*)",
         "Shell(dotnet)"]
DENY = ["Shell(gh)", "Shell(git:push*)", "Shell(git:commit*)", "Shell(git:remote*)",
        "Shell(git:checkout*)", "Shell(git:reset*)", "Shell(git:stash*)"]

PREAMBLE = """You are the build worker for one change in the git worktree {slot}.
- Work only inside this workspace: read and write nothing outside it.
- Leave every change uncommitted: no commit, branch, checkout, stash, reset or push.
- Follow AGENTS.md in this workspace.
- End your final message with exactly these four lines:
STATUS: DONE | BLOCKED
CHANGED: <files you changed>
VERIFIED: <what you ran, and its result>
NOTICED: <anything surprising, or none>
"""

RETRY = """
A previous round already changed this workspace (see `git status` and `git diff`). Its verify
command, `{verify}`, then failed. The end of its output:

```
{tail}
```

Fix the cause of that failure within the brief's constraints.
"""


def say(msg):
    print(f"grok_worker: {msg}", file=sys.stderr, flush=True)


def usage_error(msg):
    say(msg)
    sys.exit(2)


def norm(path):
    p = path.replace("\\", "/")
    m = re.match(r"^/([A-Za-z])(/|$)", p)
    if m:
        p = f"{m.group(1)}:/{p[3:]}"
    p = posixpath.normpath(p)
    return p.casefold() if os.name == "nt" else p


def last_line(text):
    lines = [l.strip() for l in ANSI.sub("", text).splitlines() if l.strip()]
    return lines[-1][:300] if lines else ""


def git(slot, *args):
    return subprocess.run(["git", "-C", slot, *args], capture_output=True, text=True, encoding="utf-8",
                          errors="replace")


def find_bash():
    bash = shutil.which("bash")
    # From PowerShell, PATH's first bash is WSL's launcher, which runs commands in Linux.
    if bash is None or (os.name == "nt" and re.search(r"[\\/](windowsapps|system32)[\\/]", bash, re.I)):
        usage_error(f"no Git Bash on PATH (found {bash}); run grok_worker from Git Bash")
    return bash


def resolve_slot(arg, bash):
    if POOL_SLOT.match(arg):
        pool = Path(__file__).with_name("agent_worktree_pool.sh")
        r = subprocess.run([bash, str(pool), "status", "--porcelain"], capture_output=True, text=True)
        current = None
        for line in r.stdout.splitlines():
            key, _, value = line.partition("=")
            if key == "slot":
                current = value
            elif key == "path" and current == arg:
                arg = value
                break
        else:
            usage_error(f"the pool has no slot {arg} (status --porcelain exit {r.returncode})")
    r = git(arg, "rev-parse", "--show-toplevel")
    if r.returncode != 0 or norm(r.stdout.strip()) != norm(arg):
        usage_error(f"--slot {arg} is not a git worktree root")
    return r.stdout.strip()


def cursor_command(env):
    override = os.environ.get("GROK_WORKER_AGENT_CMD")
    if override:
        return shlex.split(override)
    local = os.environ.get("LOCALAPPDATA")
    versions = Path(local, "cursor-agent", "versions") if local else None
    found = []
    if versions and versions.is_dir():
        for d in versions.iterdir():
            m = CURSOR_VERSION.match(d.name)
            if m and (d / "node.exe").is_file():
                found.append((tuple(int(g or 0) for g in m.groups()), d))
    if not found:
        usage_error(f"no Cursor CLI version under {versions}")
    newest = max(found)[1]
    env["NODE_COMPILE_CACHE"] = str(Path(local, "cursor-compile-cache"))
    # The .cmd shim mangles multi-line prompts, so node runs index.js directly.
    return [str(newest / "node.exe"), str(newest / "index.js")]


def health_path():
    return Path(os.environ.get("GROK_WORKER_HEALTH_FILE") or HEALTH_FILE)


def recent_failure(path):
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
        age = (datetime.now(timezone.utc) - datetime.fromisoformat(record["at"])).total_seconds()
    except (OSError, ValueError, KeyError, TypeError):
        return None
    return record if 0 <= age < HEALTH_TTL_S else None


def record_health(path, failure, model):
    if failure is None:
        path.unlink(missing_ok=True)
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    outcome, reason = failure
    tmp = path.with_suffix(".tmp")
    tmp.write_text(json.dumps({"outcome": outcome, "reason": reason, "model": model,
                               "at": datetime.now(timezone.utc).isoformat(timespec="seconds")}),
                   encoding="utf-8")
    os.replace(tmp, path)


def preflight(cmd, env, slot, model):
    try:
        r = subprocess.run(cmd + ["models"], cwd=slot, env=env, stdin=subprocess.DEVNULL, capture_output=True,
                           text=True, encoding="utf-8", errors="replace", timeout=PREFLIGHT_TIMEOUT_S)
    except subprocess.TimeoutExpired:
        return f"`models` gave no answer in {PREFLIGHT_TIMEOUT_S}s"
    if r.returncode != 0:
        return f"`models` exited {r.returncode}: {last_line(r.stdout + r.stderr)}"
    slugs = {line.split()[0] for line in ANSI.sub("", r.stdout).splitlines() if " - " in line}
    return None if model in slugs else f"`models` does not list {model}"


def write_permissions(slot):
    listed = git(slot, "worktree", "list", "--porcelain").stdout.splitlines()
    others = [l[9:] for l in listed if l.startswith("worktree ") and norm(l[9:]) != norm(slot)]
    deny = DENY + [f"Read({p.rstrip('/')}/**)" for p in others]
    cursor_dir = Path(slot, ".cursor")
    cursor_dir.mkdir(exist_ok=True)
    with open(cursor_dir / "cli.json", "w", encoding="utf-8") as f:
        json.dump({"permissions": {"allow": ALLOW, "deny": deny}}, f, indent=2)
    if git(slot, "check-ignore", "-q", ".cursor/cli.json").returncode != 0:
        exclude = Path(slot, git(slot, "rev-parse", "--git-path", "info/exclude").stdout.strip())
        exclude.parent.mkdir(parents=True, exist_ok=True)
        text = exclude.read_text(encoding="utf-8") if exclude.exists() else ""
        with open(exclude, "a", encoding="utf-8") as f:
            f.write(("" if text.endswith("\n") or not text else "\n") + ".cursor/\n")


def tree_state(slot):
    entries = git(slot, "status", "--porcelain=v1", "-z", "--untracked-files=all").stdout.split("\0")
    state, i = {}, 0
    while i < len(entries):
        entry = entries[i]
        i += 1
        if len(entry) < 4:
            continue
        if entry[0] in "RC":
            i += 1
        try:
            digest = hashlib.sha1(Path(slot, entry[3:]).read_bytes()).hexdigest()
        except OSError:
            digest = None
        state[entry[3:]] = (entry[:2], digest)
    return state


def read_events(transcript):
    events = []
    with open(transcript, encoding="utf-8", errors="replace") as f:
        for line in f:
            line = line.strip()
            if line.startswith("{"):
                try:
                    events.append(json.loads(line))
                except json.JSONDecodeError:
                    pass
    return events


def kill_tree(proc):
    if os.name == "nt":
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(proc.pid)], capture_output=True)
    else:
        proc.kill()
    proc.wait()


def launch(cmd, env, slot, transcript, log, stall_s, deadline):
    """Returns Cursor's exit code, or "stalled" / "timeout" after killing its process tree."""
    with open(transcript, "wb") as stdout, open(log, "ab") as stderr:
        proc = subprocess.Popen(cmd, cwd=slot, env=env, stdin=subprocess.DEVNULL, stdout=stdout, stderr=stderr)
        size, grew = 0, time.monotonic()
        while True:
            try:
                return proc.wait(timeout=POLL_S)
            except subprocess.TimeoutExpired:
                pass
            now = time.monotonic()
            current = os.fstat(stdout.fileno()).st_size
            if current != size:
                size, grew = current, now
            if now >= deadline or now - grew >= stall_s:
                kill_tree(proc)
                return "timeout" if now >= deadline else "stalled"


class Round:
    def __init__(self, state, transcript, events=(), reason=""):
        self.state = state
        self.transcript = transcript
        self.events = list(events)
        self.reason = reason


def run_round(n, cmd, env, slot, out, delay, stall_s, deadline):
    transcript, log = out / f"transcript-{n}.jsonl", out / f"cursor-{n}.log"
    retries = 0
    while True:
        say(f"round {n}: launching Cursor (retry {retries})" if retries else f"round {n}: launching Cursor")
        with open(log, "ab") as f:
            offset = f.tell()
        rc = launch(cmd, env, slot, transcript, log, stall_s, deadline)
        events = read_events(transcript)
        if rc in ("stalled", "timeout"):
            why = "its transcript stopped growing" if rc == "stalled" else "the run hit --max-minutes"
            say(f"round {n}: {why}; killed Cursor's process tree")
            return Round(rc, transcript, events)
        if rc == 0 or any(e.get("type") == "tool_call" for e in events):
            if rc != 0:
                say(f"round {n}: Cursor exited {rc} after starting work (log: {log.as_posix()})")
            return Round("ran", transcript, events)
        with open(log, "rb") as f:
            f.seek(offset)
            text = transcript.read_text(encoding="utf-8", errors="replace") + "\n" + \
                f.read().decode("utf-8", errors="replace")
        reason = last_line(text) or f"exit {rc}"
        if PERSISTENT.search(text):
            say(f"round {n}: Cursor can't run Grok (exit {rc}): {reason}")
            return Round("unavailable", transcript, reason=reason)
        allowed = TRANSIENT_RETRIES if TRANSIENT.search(text) else OTHER_RETRIES
        say(f"round {n}: Cursor refused the launch (exit {rc}): {reason}")
        if retries >= allowed:
            return Round("refused", transcript, reason=reason)
        retries += 1
        time.sleep(delay * retries)


def grok_status(events):
    text = next((e.get("result") or "" for e in reversed(events) if e.get("type") == "result"), "")
    lines = text.splitlines()
    for i in range(len(lines) - 1, -1, -1):
        m = STATUS_LINE.match(re.sub(r"[*`_]", "", lines[i]).strip())
        if m:
            return m.group(1), "\n".join(lines[i:])
    return None, text[-1500:]


def outside_paths(events, slot):
    root = norm(slot)
    found = set()

    def walk(value):
        if isinstance(value, str):
            for m in ABS_PATH.finditer(value):
                p = norm(m.group(0).rstrip(".:"))
                if p != root and not p.startswith(root.rstrip("/") + "/"):
                    found.add(p)
        elif isinstance(value, dict):
            for v in value.values():
                walk(v)
        elif isinstance(value, list):
            for v in value:
                walk(v)

    for e in events:
        if e.get("type") != "tool_call" or e.get("subtype") != "started":
            continue
        for name, call in (e.get("tool_call") or {}).items():
            args = call.get("args") if isinstance(call, dict) else None
            if isinstance(args, dict):
                # An edit's args carry the new file content, whose paths are text, not reads.
                walk(args.get("path") if name == "editToolCall" else args)
    return found


def run_verify(bash, slot, command, log):
    r = subprocess.run([bash, "-c", command], cwd=slot, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                       stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    log.write_text(r.stdout, encoding="utf-8")
    tail = "\n".join(r.stdout.splitlines()[-VERIFY_TAIL_LINES:])[-VERIFY_TAIL_CHARS:]
    return r.returncode, tail


def fail_codes(text):
    try:
        codes = {int(c) for c in text.split(",")}
    except ValueError:
        raise argparse.ArgumentTypeError(f"not a comma list of exit codes: {text!r}")
    if 0 in codes:
        raise argparse.ArgumentTypeError("0 is a pass, never a fail code")
    return codes


def parse_args():
    ap = argparse.ArgumentParser(description="Run Grok through the Cursor CLI in a pool slot.")
    ap.add_argument("--slot", required=True)
    ap.add_argument("--brief", required=True)
    ap.add_argument("--verify")
    ap.add_argument("--fail-codes", type=fail_codes, default="1")
    ap.add_argument("--rounds", type=int, default=2)
    ap.add_argument("--model", default="grok-4.7-high")
    ap.add_argument("--stall-minutes", type=float, default=8)
    ap.add_argument("--max-minutes", type=float, default=45)
    ap.add_argument("--recheck", action="store_true")
    ap.add_argument("--out")
    args = ap.parse_args()
    if args.rounds < 1:
        ap.error("--rounds must be at least 1")
    if args.stall_minutes <= 0 or args.max_minutes <= 0:
        ap.error("--stall-minutes and --max-minutes must be positive")
    return args


class Outcome:
    def __init__(self):
        self.state, self.rounds, self.verify, self.model = "blocked", 0, "none", None
        self.outside, self.transcript, self.dirty = set(), None, []


def finish(o, started):
    fallback = {"done": "none", "verify_error": "fix-verify"}.get(o.state, "opus")
    if o.outside:
        listed = "\n  ".join(sorted(o.outside)[:10])
        say(f"WARNING: Grok's tool calls named {len(o.outside)} path(s) outside the slot:\n  {listed}")
    print(f"GROK_WORKER={o.state}")
    print(f"ROUNDS={o.rounds}")
    print(f"WALL_S={int(time.monotonic() - started)}")
    print(f"VERIFY={o.verify}")
    print(f"OUTSIDE_READS={len(o.outside)}")
    print(f"MODEL={o.model or 'unknown'}")
    print(f"TRANSCRIPT={o.transcript.as_posix() if o.transcript else ''}")
    print(f"FALLBACK={fallback}")
    print(f"DIRTY={','.join(o.dirty)}")
    return 0 if o.state == "done" else 3 if o.state in ("unavailable", "refused") else 1


def main():
    args = parse_args()
    started = time.monotonic()
    minute = float(os.environ.get("GROK_WORKER_MINUTE_S", 60))
    stall_s, deadline = args.stall_minutes * minute, started + args.max_minutes * minute
    try:
        brief = Path(args.brief).read_text(encoding="utf-8")
    except OSError as e:
        usage_error(f"cannot read --brief: {e}")
    o, health = Outcome(), health_path()
    if not args.recheck:
        record = recent_failure(health)
        if record:
            say(f"Grok was {record.get('outcome')} at {record.get('at')} ({record.get('reason')}); "
                f"--recheck runs the preflight now (record: {health.as_posix()})")
            o.state = "unavailable"
            return finish(o, started)
    bash = find_bash() if args.verify or POOL_SLOT.match(args.slot) else None
    slot = resolve_slot(args.slot, bash)
    out = Path(args.out) if args.out else Path(tempfile.mkdtemp(prefix="grok-worker-"))
    if norm(str(out.resolve())).startswith(norm(slot).rstrip("/") + "/"):
        usage_error("--out must be outside the slot")
    out.mkdir(parents=True, exist_ok=True)
    preamble = PREAMBLE.format(slot=slot)
    prompt = f"{preamble}\n# Brief\n\n{brief}"
    if len(prompt) > MAX_PROMPT_CHARS:
        usage_error(f"the brief makes a {len(prompt)}-char prompt; the limit is {MAX_PROMPT_CHARS}")
    env = dict(os.environ, CURSOR_INVOKED_AS="agent")
    cursor = cursor_command(env)
    reason = preflight(cursor, env, slot, args.model)
    if reason:
        say(f"preflight: {reason}")
        record_health(health, ("unavailable", reason), args.model)
        o.state = "unavailable"
        return finish(o, started)
    cmd = cursor + ["-p", "--output-format", "stream-json", "--model", args.model, "--trust", "--workspace", slot]
    write_permissions(slot)
    before = tree_state(slot)
    delay = float(os.environ.get("GROK_WORKER_RETRY_DELAY_S", RETRY_DELAY_S))
    say(f"slot {slot}; transcripts in {out.as_posix()}")

    failure = None
    for n in range(1, args.rounds + 1):
        if time.monotonic() >= deadline:
            say("the run hit --max-minutes before the next round")
            o.state = "timeout"
            break
        o.rounds = n
        rnd = run_round(n, cmd + [prompt], env, slot, out, delay, stall_s, deadline)
        o.transcript = rnd.transcript
        o.outside |= outside_paths(rnd.events, slot)
        init = next((e for e in rnd.events if e.get("type") == "system" and e.get("subtype") == "init"), {})
        o.model = init.get("model") or o.model
        if rnd.state in ("unavailable", "refused"):
            failure = (rnd.state, rnd.reason)
            if n == 1:
                o.state = rnd.state
            else:
                say(f"round {n} did not start; the run ends on round {n - 1}'s failed verify")
            break
        if rnd.state != "ran":
            o.state = rnd.state
            break
        status, block = grok_status(rnd.events)
        say(f"round {n}: Grok's final message ends:\n{block}")
        if status != "DONE":
            if status is None:
                say(f"round {n}: no STATUS line in Grok's final message; treating it as BLOCKED")
            o.state = "blocked"
            break
        if not args.verify:
            o.state = "done"
            break
        log = out / f"verify-{n}.log"
        rc, tail = run_verify(bash, slot, args.verify, log)
        o.verify = "pass" if rc == 0 else "fail" if rc in args.fail_codes else "error"
        say(f"round {n}: verify {o.verify} (exit {rc}; log: {log.as_posix()})")
        if rc == 0:
            o.state = "done"
            break
        if rc not in args.fail_codes:
            o.state = "verify_error"
            say(f"verify could not judge the change; output ends:\n{tail}")
            break
        o.state = "verify_failed"
        if n < args.rounds:
            prompt = f"{preamble}{RETRY.format(verify=args.verify, tail=tail)}\n# Brief\n\n{brief}"
        else:
            say(f"verify output ends:\n{tail}")

    after = tree_state(slot)
    o.dirty = sorted(p for p in before.keys() | after.keys() if before.get(p) != after.get(p))
    record_health(health, failure, args.model)
    return finish(o, started)


if __name__ == "__main__":
    sys.exit(main())
