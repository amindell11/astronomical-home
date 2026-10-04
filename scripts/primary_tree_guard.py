"""PreToolUse hook (Bash, PowerShell): refuse destructive commands aimed at the primary tree.

The primary tree is the repo's main worktree (the first `git worktree list` entry). It holds
the owner's uncommitted work, so agents never clean, reset or bulk-delete there; slots and
other linked worktrees are fair game.

Interface (Claude Code hook contract), exit 0 on well-formed hook JSON:
  stdin   the hook JSON; reads `cwd` and `tool_input.command`.
  stdout  nothing when allowed; when refused, one JSON line with
          hookSpecificOutput.permissionDecision "deny" (binding even in bypass mode) and a
          permissionDecisionReason naming the segment, the directory and the safe shape.

Refused when the segment's effective directory is, or may be, the primary tree:
  git clean (not -n/--dry-run), git rm -r, git checkout --orphan / -f / <pathspec>,
  git restore (unless --staged alone), git reset --hard.
Refused when any target path is, may be, or contains the primary tree:
  recursive rm / Remove-Item (and its aliases), cmd rmdir /s.

The effective directory is the `-C` target resolved against every directory the shell may be
in at that point. A `cd` may fail unless its target exists now and nothing earlier in the
command deletes or moves files; a `;`-joined `cd` that may fail keeps the old directory as a
candidate, while `&&` drops it. `bash -c` / `pwsh -Command` strings are followed. A path that
cannot be resolved (an unknown variable, an unparsable command) counts as the primary tree.
Variables assigned in the same command from a literal path or `$(mktemp ...)` are resolved.
"""

import json
import os
import posixpath
import re
import shlex
import subprocess
import sys

UNKNOWN = "<unresolved>"
TEMP = "<mktemp>"
CD = {"cd", "chdir", "pushd", "set-location", "sl", "push-location"}
RM = {"rm", "remove-item", "ri", "del", "erase", "rmdir", "rd"}
MOVERS = {"mv", "move-item", "mi", "move", "ren", "rename-item"}
SHELLS = {"bash", "sh", "pwsh", "powershell", "powershell.exe", "pwsh.exe", "bash.exe"}
WRAPPERS = {"sudo", "command", "time", "builtin", "exec", "env", "&"}
REDIRECT = re.compile(r"\d*>&\d*-?|&>>?|<&\d*")
REDIRECT_TOKEN = re.compile(r"^\d*(>>?|<)")
PS_SWITCHES = {"-force", "-whatif", "-confirm", "-verbose", "-debug"}
TRIGGER = re.compile(r"\b(clean|rm|checkout|restore|reset|remove-item|ri|del|erase|rmdir|rd)\b", re.I)
ASSIGN = re.compile(r"^([A-Za-z_][A-Za-z0-9_]*)=(.*)$", re.S)
VAR = re.compile(r"\$\{?([A-Za-z_][A-Za-z0-9_:]*)\}?")


def norm(path):
    p = path.replace("\\", "/")
    m = re.match(r"^/([A-Za-z])(/|$)(.*)", p)
    if m:
        p = f"{m.group(1)}:/{m.group(3)}"
    p = posixpath.normpath(p)
    if re.match(r"^[A-Za-z]:", p):
        p = p[0].lower() + p[1:]
        if len(p) == 2:
            p += "/"
    return p.casefold() if os.name == "nt" else p


def is_abs(p):
    return p.startswith("/") or bool(re.match(r"^[A-Za-z]:[\\/]", p)) or p.startswith("\\\\")


def within(child, parent):
    return child == parent or child.startswith(parent.rstrip("/") + "/")


class Tree:
    def __init__(self, primary, others):
        self.primary = primary
        self.others = others

    def in_primary(self, d):
        if d == UNKNOWN:
            return True
        if d == TEMP:
            return False
        return within(d, self.primary) and not any(within(d, o) for o in self.others)

    def target_hits(self, d):
        return self.in_primary(d) or (d not in (UNKNOWN, TEMP) and within(self.primary, d))


def load_tree(cwd):
    for start in (cwd, os.environ.get("CLAUDE_PROJECT_DIR")):
        if not start or not os.path.isdir(start):
            continue
        r = subprocess.run(["git", "-C", start, "worktree", "list", "--porcelain"],
                           capture_output=True, text=True)
        paths = [norm(l[9:]) for l in r.stdout.splitlines() if l.startswith("worktree ")]
        if r.returncode == 0 and paths:
            return Tree(paths[0], [p for p in paths[1:] if p != paths[0]])
    return None


def strip_heredocs(cmd):
    out, lines, i = [], cmd.split("\n"), 0
    while i < len(lines):
        line = lines[i]
        out.append(line)
        m = re.search(r"<<-?\s*(['\"]?)([A-Za-z_][A-Za-z0-9_]*)\1", line)
        i += 1
        if m:
            while i < len(lines) and lines[i].strip() != m.group(2):
                i += 1
            i += 1
    return "\n".join(out)


def tokenize(cmd, powershell):
    text = REDIRECT.sub(" ", strip_heredocs(cmd)).replace("\n", " \n ")
    lex = shlex.shlex(text, posix=True, punctuation_chars=";&|")
    lex.whitespace = " \t\r"
    lex.whitespace_split = True
    if powershell:
        lex.escape = "`"
    return list(lex)


def pipelines(tokens):
    """Yields (op, [argv, ...]): the operator before each pipeline, and its piped commands."""
    pipe, seg, op = [], [], ";"
    for t in tokens + [";"]:
        if t and set(t) <= set(";&|\n"):
            if seg:
                pipe.append(seg)
            seg = []
            if t == "|":
                continue
            if pipe:
                yield op, pipe
            pipe, op = [], (t if t in ("&&", "||") else ";")
        else:
            seg.append(t)


class Scan:
    def __init__(self, tree, powershell=False):
        self.tree = tree
        self.powershell = powershell
        self.moved = False
        self.vars = {}
        self.blocked = None

    def expand(self, word):
        if "$(" in word or "`" in word:
            return UNKNOWN
        def sub(m):
            name = m.group(1)
            if name.lower().startswith("env:"):
                val = os.environ.get(name[4:])
            else:
                val = self.vars[name] if name in self.vars else os.environ.get(name)
            if val is None or val in (UNKNOWN, TEMP):
                raise LookupError(val or UNKNOWN)
            return val
        try:
            return VAR.sub(sub, word) if "$" in word else word
        except LookupError as e:
            return TEMP if e.args[0] == TEMP and VAR.fullmatch(word.split("/")[0]) else UNKNOWN

    def resolve(self, word, cwd):
        w = self.expand(word)
        if w in (UNKNOWN, TEMP):
            return w
        if w.startswith("~"):
            w = os.path.expanduser(w)
        if is_abs(w):
            return norm(w)
        if cwd in (UNKNOWN, TEMP):
            return cwd
        return norm(cwd + "/" + w)

    def assign(self, name, value):
        if re.match(r"^\$\(\s*mktemp\b", value):
            self.vars[name] = TEMP
        else:
            self.vars[name] = self.expand(value)

    def run(self, cmd, cwds):
        try:
            tokens = tokenize(cmd, self.powershell)
        except ValueError:
            if TRIGGER.search(cmd):
                self.blocked = (cmd.strip()[:200], UNKNOWN, "the command could not be parsed")
            return cwds
        states = {(c, "ok") for c in cwds}
        for op, pipe in pipelines(tokens):
            nxt = set()
            for cwd, status in states:
                runs = op == ";" or (op == "&&" and status == "ok") or (op == "||" and status == "fail")
                if not runs:
                    nxt.add((cwd, status))
                    continue
                if len(pipe) == 1:
                    nxt.update(self.segment(pipe[0], cwd))
                else:
                    for argv in pipe:
                        self.segment(argv, cwd)
                    nxt.update({(cwd, "ok"), (cwd, "fail")})
                if self.blocked:
                    return set()
            states = nxt
        return {c for c, _ in states}

    def segment(self, argv, cwd):
        argv = [a.lstrip("(").rstrip(")") for a in argv if a.strip("()") or not a]
        argv = [a for i, a in enumerate(argv)
                if not REDIRECT_TOKEN.match(a) and not (i and re.fullmatch(r"\d*(>>?|<)", argv[i - 1]))]
        while argv and ASSIGN.match(argv[0].lstrip("$")):
            name, value = ASSIGN.match(argv.pop(0).lstrip("$")).groups()
            self.assign(name, value)
        while argv and argv[0].lower() in WRAPPERS | {"export", "local", "declare"}:
            head = argv.pop(0).lower()
            if head in ("export", "local", "declare"):
                for a in argv:
                    m = ASSIGN.match(a)
                    if m:
                        self.assign(*m.groups())
                return [(cwd, "ok"), (cwd, "fail")]
        if not argv:
            return [(cwd, "ok"), (cwd, "fail")]
        name = posixpath.basename(argv[0].replace("\\", "/")).lower()
        args = argv[1:]
        if name.startswith("$") and len(argv) >= 3 and argv[1] == "=":
            self.assign(name[1:], argv[2].strip("'\""))
            return [(cwd, "ok"), (cwd, "fail")]
        if name in CD:
            target = self.resolve(next((a for a in args if not a.startswith("-")), "~"), cwd)
            if target not in (UNKNOWN, TEMP) and os.path.isdir(target) and not self.moved:
                return [(target, "ok")]
            return [(target, "ok"), (cwd, "fail")]
        if name in RM | MOVERS or (name == "git" and "worktree" in args):
            self.moved = True
        if name in SHELLS:
            for i, a in enumerate(args):
                if a.lower() in ("-c", "-command") and i + 1 < len(args):
                    return [(c, s) for c in self.run(" ".join(args[i + 1:]), {cwd}) for s in ("ok", "fail")]
        if name == "git":
            self.git(argv, args, cwd)
        elif name in RM:
            self.rm(argv, name, args, cwd)
        return [(cwd, "ok"), (cwd, "fail")]

    def block(self, argv, where, why):
        if not self.blocked:
            self.blocked = (" ".join(argv)[:200], where, why)

    def git(self, argv, args, cwd):
        where, i = cwd, 0
        while i < len(args) and args[i].startswith("-"):
            a = args[i]
            if a == "-C" and i + 1 < len(args):
                target = args[i + 1]
                where = cwd if target == "" else self.resolve(target, where)
                i += 2
            elif a == "-c":
                i += 2
            elif a.startswith(("--git-dir", "--work-tree")):
                where = UNKNOWN
                i += 2 if "=" not in a else 1
            else:
                i += 1
        if i >= len(args):
            return
        sub, rest = args[i], args[i + 1:]
        flags = [r for r in rest if r.startswith("-")]
        short = "".join(f[1:] for f in flags if not f.startswith("--"))
        why = None
        if sub == "clean" and not ("n" in short or "--dry-run" in flags):
            why = "git clean deletes untracked and ignored files"
        elif sub == "rm" and ("r" in short or "--recursive" in flags):
            why = "git rm -r removes a tree of files"
        elif sub == "checkout":
            paths = rest[rest.index("--") + 1:] if "--" in rest else [r for r in rest if r in (".", ":/", "*")]
            if "--orphan" in flags or "f" in short or "--force" in flags or paths:
                why = "git checkout --orphan / --force / <pathspec> discards the working tree's changes"
        elif sub == "restore" and not ("--staged" in flags and "--worktree" not in flags):
            why = "git restore discards working-tree changes"
        elif sub == "reset" and "--hard" in flags:
            why = "git reset --hard discards working-tree changes"
        if why and self.tree.in_primary(where):
            self.block(argv, where, why)

    def rm(self, argv, name, args, cwd):
        recursive, targets, i = False, [], 0
        while i < len(args):
            a, low = args[i], args[i].lower()
            i += 1
            if low in ("/s", "--recursive") or (low.startswith("-r") and "recurse".startswith(low[1:])):
                recursive = True
            elif not self.powershell and re.fullmatch(r"-[a-z]*r[a-z]*", low):
                recursive = True
            elif self.powershell and low.startswith("-") and ":" not in low and low not in PS_SWITCHES:
                if low in ("-path", "-literalpath") and i < len(args):
                    targets.append(args[i])
                i += 1
            elif not a.startswith("-") and not re.fullmatch(r"/[a-z]", low):
                targets.append(a)
        if not recursive:
            return
        for t in targets or ["."]:
            d = self.resolve(t.rstrip("*").rstrip("/") or t, cwd)
            if self.tree.target_hits(d):
                self.block(argv, d, "a recursive delete reaches into the primary tree")
                return


def deny(reason):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse",
                                             "permissionDecision": "deny",
                                             "permissionDecisionReason": reason}}))
    return 0


def main():
    data = json.load(sys.stdin)
    cmd = (data.get("tool_input") or {}).get("command") or ""
    if not TRIGGER.search(cmd):
        return 0
    try:
        return judge(data, cmd)
    except Exception as e:  # a crash is doubt; fail closed on the commands this guard exists for
        return deny(f"primary-tree guard: could not judge this command ({e!r}); refusing. "
                    "Target a slot with `git -C <literal slot path>`.")


def judge(data, cmd):
    cwd = data.get("cwd") or os.getcwd()
    tree = load_tree(cwd)
    if tree is None:
        return deny(f"primary-tree guard: no git worktree found from {cwd}; refusing a possibly "
                    "destructive command. Run it with `git -C <literal slot path>` from inside the repo.")
    scan = Scan(tree, data.get("tool_name") == "PowerShell")
    scan.run(cmd, {norm(cwd)})
    if not scan.blocked:
        return 0
    seg, where, why = scan.blocked
    place = "an unresolved directory" if where == UNKNOWN else where
    return deny(f"primary-tree guard: refused `{seg}` — {why}, and it would run in {place}, which is or may "
                f"be the primary tree ({tree.primary}). Agents never clean, reset or bulk-delete there "
                "(AGENTS.md, Default workflow). Target a slot with a literal path, `git -C <slot path> ...`, "
                "and chain with `&&` so a failed step stops the command.")


if __name__ == "__main__":
    sys.exit(main())
