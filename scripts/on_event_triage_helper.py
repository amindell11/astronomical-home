"""The JSON pass of scripts/on_event_triage.sh, run as its one coprocess per triage run.

Usage: on_event_triage_helper.py <tmp-dir> <event-json-path> <prompt-file>
Each stdin line is one request, `<op>\t<arg>...`. The helper writes the op's shell assignments
to <tmp-dir>/reply.sh, then answers `ok` on stdout; the script sources the file. Paths travel
only in argv, where Git Bash converts them for a native Python; a crash ends the helper, and
the script reads the EOF as infra.

Ops: `op <args>` - reads (bare names are in <tmp-dir>) -> assignments; extra files written.
  event                            <event-json> -> ACTION NUMBER TITLE_CHANGED HAS_OLD_BODY;
                                   old_body.txt when HAS_OLD_BODY=1
  issue                            issue.json -> STATE AUTHOR NODE_ID LABELS ASSIGNED
  keep <pri-label>...              events.json -> keep (the label added last)
  board <project-id>               board.json -> ITEM_ID CURRENT_OPTION, only when the issue is
                                   on that project (else nothing is assigned)
  gate                             issue.json, old_body.txt -> gate_open (1 = the edit adds a
                                   path-like token)
  prompt                           <prompt-file>, issue.json, closed.json -> nothing; prompt.md
  verdict <date> <assigned> <marker>
                                   claude.json -> CLAUDE_ERROR alone on bad output; else
                                   TOKENS_IN TOKENS_OUT TOKENS_CACHE COST_USD FINDINGS
                                   VERDICT_JSON, plus note.md when FINDINGS > 0
  prior <bot-login> <marker>       comments.json -> PRIOR_ID (empty when none)
Failure: past verdict's CLAUDE_ERROR, an unknown op or a missing/malformed input raises; the
helper exits non-zero and the caller's read hits EOF. Exit 0 only when stdin closes.
"""
import json
import os
import re
import shlex
import sys

TMP, EVENT, PROMPT_FILE = sys.argv[1:4]


def tmp(name):
    return os.path.join(TMP, name)


def load(name):
    return json.load(open(tmp(name), encoding="utf-8"))


def write(path, text):
    open(path, "w", encoding="utf-8", newline="\n").write(text)


def event():
    ev = json.load(open(EVENT, encoding="utf-8"))
    changes = ev.get("changes") or {}
    if "issue" in ev:
        action = ev.get("action") or ""
        number = ev["issue"]["number"]
    else:
        action = "dispatch"
        number = int((ev.get("inputs") or {})["issue_number"])
    has_old = "body" in changes
    if has_old:
        write(tmp("old_body.txt"), (changes["body"] or {}).get("from") or "")
    return [("ACTION", action), ("NUMBER", number),
            ("TITLE_CHANGED", 1 if "title" in changes else 0), ("HAS_OLD_BODY", 1 if has_old else 0)]


def issue():
    issue = load("issue.json")
    labels = [l["name"] for l in issue.get("labels") or []]
    return [("STATE", (issue.get("state") or "").upper()),
            ("AUTHOR", (issue.get("author") or {}).get("login") or ""),
            ("NODE_ID", issue.get("id") or ""),
            ("LABELS", " ".join(labels)),
            ("ASSIGNED", 1 if issue.get("assignees") else 0)]


def keep(*present):
    events = [e for page in load("events.json") for e in page]
    last = {}
    for e in events:
        if e.get("event") == "labeled" and (e.get("label") or {}).get("name") in present:
            last[e["label"]["name"]] = e.get("created_at") or ""
    return [("keep", max(present, key=lambda p: last.get(p, "")))]


def board(project_id):
    data = load("board.json")
    nodes = ((data.get("data") or {}).get("node") or {}).get("projectItems", {}).get("nodes") or []
    out = []
    for n in nodes:
        if (n.get("project") or {}).get("id") == project_id:
            out += [("ITEM_ID", n["id"]),
                    ("CURRENT_OPTION", ((n.get("fieldValueByName") or {}).get("optionId")) or "")]
    return out


SEG = r"\.?[\w@-]+(?:\.[\w@-]+)*"
PATH_RE = re.compile(rf"(?<![\w/.])(?:\./)?(?:{SEG}/)+{SEG}|(?<![\w/.])\.?[\w-]+\.(?:cs|md|sh|ps1|py|yml|yaml|json|asmdef|unity|prefab|asset|mat|shader|hlsl|cginc|txt)\b")


def gate():
    new = load("issue.json").get("body") or ""
    old = open(tmp("old_body.txt"), encoding="utf-8").read()
    return [("gate_open", 1 if set(PATH_RE.findall(new)) - set(PATH_RE.findall(old)) else 0)]


def prompt():
    prompt = open(PROMPT_FILE, encoding="utf-8").read()
    issue = load("issue.json")
    closed = load("closed.json")
    n = issue["number"]
    labels = ", ".join(l["name"] for l in issue.get("labels") or []) or "none"
    assignees = ", ".join(a["login"] for a in issue.get("assignees") or []) or "none"
    out = [prompt.rstrip(), "", "## Packet", "",
           f"Issue #{n} · author {issue['author']['login']} · labels: {labels} · assignees: {assignees}", "",
           f"<issue-body number={n}>", f"# {issue['title']}", "", issue.get("body") or "", f"</issue-body>", "",
           "<closed-issues>"]
    out += [f"#{c['number']} · {c['title']} · {(c.get('closedAt') or '')[:10]}" for c in closed]
    out += ["</closed-issues>", ""]
    write(tmp("prompt.md"), "\n".join(out))
    return []


def verdict(date, assigned, marker):
    try:
        out = load("claude.json")
    except Exception as e:
        return [("CLAUDE_ERROR", f"output is not JSON: {e}")]
    if not isinstance(out, dict) or out.get("is_error"):
        return [("CLAUDE_ERROR", "claude reported an error: " + str(out.get("result") if isinstance(out, dict) else out)[:300])]
    v = out.get("structured_output")
    if v is None:
        try:
            v = json.loads(out.get("result") or "")
        except Exception:
            v = None
    required = {"retry_of": (int, type(None)), "retry_evidence": str, "premise_holds": bool,
                "premise_evidence": str, "dead_pointers": list}
    if not isinstance(v, dict) or any(k not in v or not isinstance(v[k], t) for k, t in required.items()) \
            or any(not isinstance(d, dict) or "path" not in d for d in v["dead_pointers"]):
        return [("CLAUDE_ERROR", "verdict does not match the schema: " + json.dumps(v)[:300])]
    usage = out.get("usage") or {}
    reply = [("TOKENS_IN", int(usage.get("input_tokens") or 0)),
             ("TOKENS_OUT", int(usage.get("output_tokens") or 0)),
             ("TOKENS_CACHE", int(usage.get("cache_creation_input_tokens") or 0) + int(usage.get("cache_read_input_tokens") or 0)),
             ("COST_USD", out.get("total_cost_usd") or 0)]
    lines = []
    if v["retry_of"] is not None:
        lines.append(f"Retry of #{v['retry_of']} — {v['retry_evidence']}")
    if not v["premise_holds"]:
        lines.append(f"Premise not in the tree — {v['premise_evidence']}")
    for d in v["dead_pointers"]:
        rep = d.get("replacement")
        lines.append(f"Dead pointer: `{d['path']}` → " + (f"`{rep}`" if rep else "no replacement found"))
    reply += [("FINDINGS", len(lines)), ("VERDICT_JSON", json.dumps(v, ensure_ascii=False))]
    if lines:
        if assigned == "1":
            lines.append("In flight (assigned)")
        write(tmp("note.md"), f"{marker}\nOn-event triage {date}\n" + "\n".join(lines) + "\n")
    return reply


def prior(bot_login, marker):
    comments = [c for page in load("comments.json") for c in page]
    ids = [c["id"] for c in comments if (c.get("user") or {}).get("login") == bot_login and marker in (c.get("body") or "")]
    return [("PRIOR_ID", ids[-1] if ids else "")]


OPS = {"event": event, "issue": issue, "keep": keep, "board": board, "gate": gate,
       "prompt": prompt, "verdict": verdict, "prior": prior}

for line in sys.stdin:
    op, *args = line.rstrip("\n").split("\t")
    reply = OPS[op](*args)
    write(tmp("reply.sh"), "".join(f"{k}={shlex.quote(str(v))}\n" for k, v in reply))
    print("ok", flush=True)
