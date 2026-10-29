"""Run Grok through the Cursor CLI in a pool slot from a build brief.

The default build worker (agent-worktree-pr-loop, Step 2): Grok edits the slot's tree, never
commits, and ends with a STATUS block; this script runs the caller's verify command after each
DONE round and relaunches with the verify output until it passes or the rounds run out.

Usage:
  python scripts/grok_worker.py --slot <agent-N | path> --brief <file>
      [--verify "<shell cmd>"] [--rounds 2] [--model grok-4.7-high] [--out <dir>]

  --slot    a pool slot name (resolved through `agent_worktree_pool.sh status --porcelain`)
            or the path of a git worktree root.
  --verify  run with Git Bash in the slot after each DONE round; exit 0 = pass.
  --out     where transcripts and logs go; default a fresh dir under the system temp dir.
            Never inside the slot.

Exit codes:
  0  Grok reported DONE and --verify passed, or no --verify was given.
  1  Grok reported BLOCKED or gave no STATUS line, verify still failed after the last round,
     or a round passed ROUND_TIMEOUT_S (its process tree is killed).
  2  usage error (no trailers), or Cursor refused every launch of a round.

Machine channel: these KEY=value trailers on stdout, in this order, and nothing else there.
  GROK_WORKER=done|blocked|verify_failed|timeout|refused
  ROUNDS=<rounds launched>
  WALL_S=<whole seconds>
  VERIFY=pass|fail|none      the last verify run; none when no verify ran
  OUTSIDE_READS=<count>      distinct absolute paths outside the slot in Grok's tool-call args
                             (an edit counts only its target path)
  MODEL=<model>              as the transcript's init event names it; unknown when absent
  TRANSCRIPT=<path>          the last round's stream-json transcript
Progress, Grok's STATUS block and warnings go to stderr. OUTSIDE_READS > 0 never changes the
exit code; it prints a warning, because Grok has read sibling trees' solutions when stuck.

Files: <out>/transcript-<round>.jsonl (Cursor stdout), <out>/cursor-<round>.log (Cursor stderr,
every attempt), <out>/verify-<round>.log. In the slot: .cursor/cli.json (the permission
allowlist, rewritten each run) and a `.cursor/` line in the shared info/exclude.

A launch that exits non-zero before any tool call is a refusal (Cursor's backend refuses
under load); it is retried up to LAUNCH_ATTEMPTS times, sleeping RETRY_DELAY_S x attempt.

Environment (test seams):
  GROK_WORKER_AGENT_CMD      replaces the Cursor command (split shell-style; Cursor's
                             arguments are appended)
  GROK_WORKER_RETRY_DELAY_S  replaces RETRY_DELAY_S
"""

import argparse
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
from pathlib import Path

ROUND_TIMEOUT_S = 3600
LAUNCH_ATTEMPTS = 4
RETRY_DELAY_S = 20
VERIFY_TAIL_LINES = 60
VERIFY_TAIL_CHARS = 4000
# Windows caps a command line at 32,767 chars; the retry prompt adds the verify tail.
MAX_PROMPT_CHARS = 26000

POOL_SLOT = re.compile(r"^agent-\d+$")
CURSOR_VERSION = re.compile(r"^(\d{4})\.(\d{1,2})\.(\d{1,2})(?:-(\d{2})-(\d{2})-(\d{2}))?-[a-f0-9]+$")
ABS_PATH = re.compile(r"(?<![\w./~-])(?:[A-Za-z]:[\\/]|/[A-Za-z]/)[^\s\"'`<>|*?;,()\[\]{}]*")
STATUS_LINE = re.compile(r"^STATUS:\s*(DONE|BLOCKED)\b")

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


def git(slot, *args):
    return subprocess.run(["git", "-C", slot, *args], capture_output=True, text=True)


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


class Round:
    def __init__(self, state, events=(), transcript=None):
        self.state = state
        self.events = list(events)
        self.transcript = transcript


def run_round(n, cmd, env, slot, prompt, out, delay):
    transcript, log = out / f"transcript-{n}.jsonl", out / f"cursor-{n}.log"
    for attempt in range(1, LAUNCH_ATTEMPTS + 1):
        say(f"round {n}: launching Cursor (attempt {attempt}/{LAUNCH_ATTEMPTS})")
        with open(transcript, "wb") as stdout, open(log, "ab") as stderr:
            stderr.write(f"--- attempt {attempt}\n".encode())
            stderr.flush()
            proc = subprocess.Popen(cmd + [prompt], cwd=slot, env=env, stdin=subprocess.DEVNULL,
                                    stdout=stdout, stderr=stderr)
            try:
                rc = proc.wait(timeout=ROUND_TIMEOUT_S)
            except subprocess.TimeoutExpired:
                kill_tree(proc)
                say(f"round {n}: no result after {ROUND_TIMEOUT_S}s; killed Cursor's process tree")
                return Round("timeout", read_events(transcript), transcript)
        events = read_events(transcript)
        if rc == 0 or any(e.get("type") == "tool_call" for e in events):
            if rc != 0:
                say(f"round {n}: Cursor exited {rc} after starting work (log: {log.as_posix()})")
            return Round("ran", events, transcript)
        last = log.read_text(encoding="utf-8", errors="replace").strip().splitlines()[-1:]
        say(f"round {n}: Cursor refused the launch (exit {rc}): {' '.join(last)}")
        if attempt < LAUNCH_ATTEMPTS:
            time.sleep(delay * attempt)
    return Round("refused", transcript=transcript)


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


def parse_args():
    ap = argparse.ArgumentParser(description="Run Grok through the Cursor CLI in a pool slot.")
    ap.add_argument("--slot", required=True)
    ap.add_argument("--brief", required=True)
    ap.add_argument("--verify")
    ap.add_argument("--rounds", type=int, default=2)
    ap.add_argument("--model", default="grok-4.7-high")
    ap.add_argument("--out")
    args = ap.parse_args()
    if args.rounds < 1:
        ap.error("--rounds must be at least 1")
    return args


def main():
    args = parse_args()
    started = time.monotonic()
    bash = find_bash() if args.verify or POOL_SLOT.match(args.slot) else None
    slot = resolve_slot(args.slot, bash)
    try:
        brief = Path(args.brief).read_text(encoding="utf-8")
    except OSError as e:
        usage_error(f"cannot read --brief: {e}")
    out = Path(args.out) if args.out else Path(tempfile.mkdtemp(prefix="grok-worker-"))
    if norm(str(out.resolve())).startswith(norm(slot).rstrip("/") + "/"):
        usage_error("--out must be outside the slot")
    out.mkdir(parents=True, exist_ok=True)
    preamble = PREAMBLE.format(slot=slot)
    prompt = f"{preamble}\n# Brief\n\n{brief}"
    if len(prompt) > MAX_PROMPT_CHARS:
        usage_error(f"the brief makes a {len(prompt)}-char prompt; the limit is {MAX_PROMPT_CHARS}")
    env = dict(os.environ, CURSOR_INVOKED_AS="agent")
    cmd = cursor_command(env) + ["-p", "--output-format", "stream-json", "--model", args.model,
                                 "--trust", "--workspace", slot]
    write_permissions(slot)
    delay = float(os.environ.get("GROK_WORKER_RETRY_DELAY_S", RETRY_DELAY_S))
    say(f"slot {slot}; transcripts in {out.as_posix()}")

    state, verify, model, outside, transcript, rounds = "blocked", "none", None, set(), None, 0
    for n in range(1, args.rounds + 1):
        rounds = n
        rnd = run_round(n, cmd, env, slot, prompt, out, delay)
        transcript = rnd.transcript
        outside |= outside_paths(rnd.events, slot)
        init = next((e for e in rnd.events if e.get("type") == "system" and e.get("subtype") == "init"), {})
        model = init.get("model") or model
        if rnd.state != "ran":
            state = rnd.state
            break
        status, block = grok_status(rnd.events)
        say(f"round {n}: Grok's final message ends:\n{block}")
        if status != "DONE":
            if status is None:
                say(f"round {n}: no STATUS line in Grok's final message; treating it as BLOCKED")
            state = "blocked"
            break
        if not args.verify:
            state = "done"
            break
        log = out / f"verify-{n}.log"
        rc, tail = run_verify(bash, slot, args.verify, log)
        verify = "pass" if rc == 0 else "fail"
        say(f"round {n}: verify {verify} (exit {rc}; log: {log.as_posix()})")
        if rc == 0:
            state = "done"
            break
        state = "verify_failed"
        if n < args.rounds:
            prompt = f"{preamble}{RETRY.format(verify=args.verify, tail=tail)}\n# Brief\n\n{brief}"
        else:
            say(f"verify output ends:\n{tail}")

    if outside:
        listed = "\n  ".join(sorted(outside)[:10])
        say(f"WARNING: Grok's tool calls named {len(outside)} path(s) outside the slot:\n  {listed}")
    print(f"GROK_WORKER={state}")
    print(f"ROUNDS={rounds}")
    print(f"WALL_S={int(time.monotonic() - started)}")
    print(f"VERIFY={verify}")
    print(f"OUTSIDE_READS={len(outside)}")
    print(f"MODEL={model or 'unknown'}")
    print(f"TRANSCRIPT={transcript.as_posix()}")
    return {"done": 0, "refused": 2}.get(state, 1)


if __name__ == "__main__":
    sys.exit(main())
