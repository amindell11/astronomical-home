#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/drain_pick.sh

# Hermetic regression for scripts/drain_pick.sh: the pick filter (unity label, assignee, open
# blocker, scope block, proposal author), priority-then-age order, a claimed issue's three
# readings by its closing PRs (unfinished, building, pr-closed), claim's assignee re-read and
# two writes, release, every owed verdict and the tried rule, the verify queue, the merge
# queue's facts, landing order and skip reasons, the digest's lists, instruct's checks and
# record, and land-facts' readings of the record, Codex's review, threads and merge order. gh is
# a stub serving REST lists 100 rows a page; every call it does not model fails closed.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRAIN="$SCRIPT_DIR/../drain_pick.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP" 2>/dev/null || true' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export GH_WRITE_LOG="$TMP/gh-writes.log"
export GITHUB_REPOSITORY="owner/repo"
mkdir -p "$FIX" "$TMP/bin"

cat > "$TMP/bin/gh" <<'EOF'
#!/usr/bin/env bash
args="$*"
page() { local n="${args##*&page=}"; n="${n%% *}"; sed -n "$((n * 100 - 99)),$((n * 100))p" "$1"; }
case "$args" in
  "api -X "*) echo "$args" >> "$GH_WRITE_LOG" ;;
  "api user --jq .login") echo me ;;
  "api repos/owner/repo/issues?"*) page "$FIX/issues.txt" ;;
  "api repos/owner/repo/issues/"*"/comments?"*) n="${args#api repos/owner/repo/issues/}"; page "$FIX/comments-${n%%/*}.jsonl" ;;
  "api repos/owner/repo/issues/"*"assignees"*) cat "$FIX/assignees.txt" ;;
  "api repos/owner/repo/issues/"*"labels"*) cat "$FIX/labelled.txt" ;;
  "api repos/owner/repo/pulls?"*) page "$FIX/pulls.jsonl" ;;
  "api repos/owner/repo/pulls/"*"/commits?"*) n="${args#api repos/owner/repo/pulls/}"; page "$FIX/commits-${n%%/*}.txt" ;;
  "api repos/owner/repo/pulls/"*"[.state, .base.ref]"*) n="${args#api repos/owner/repo/pulls/}"; cat "$FIX/pull-${n%% *}.tsv" ;;
  "api repos/owner/repo/pulls/"*) cat "$FIX/pr.json" ;;
  "api graphql "*"pullRequest(number"*) cat "$FIX/pr-graphql.json" ;;
  "api graphql "*"pullRequests("*) cat "$FIX/prs.json" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$TMP/bin/gh"
export PATH="$TMP/bin:$PATH"

# issue <number> <created_at> <labels csv> [assignee] [blocked_by] [body] [comments-json]
ISSUES=()
issue() { local a=("$@" "" "" "" ""); ISSUES+=("${a[@]:0:7}"); }
# pull <number> <open|closed|merged> <body>: a PR the closing-keyword scan reads
PULLS=()
pull() { PULLS+=("$1" "$2" "$3"); }
# One python spawn per write: each file is what drain_pick's gh call prints, in REST order.
write_queue() {
  python3 - "$FIX" "${ISSUES[@]:-}" -- "${PULLS[@]:-}" <<'PY'
import json, sys
fix, args = sys.argv[1], sys.argv[2:]
issues, pulls = args[:args.index("--")], args[args.index("--") + 1:]
with open(f"{fix}/issues.txt", "w") as out:
    for number, created, labels, assignee, blocked, body, comments in zip(*[iter(issues)] * 7):
        out.write(f"{number} " + json.dumps({"number": int(number), "created_at": created, "body": body,
          "labels": [{"name": l} for l in labels.split(",") if l],
          "assignees": [{"login": assignee}] if assignee else [],
          "issue_dependencies_summary": {"blocked_by": int(blocked or 0)}}) + "\n")
        with open(f"{fix}/comments-{number}.jsonl", "w") as c:
            c.writelines(json.dumps(row) + "\n" for row in json.loads(comments or "[]"))
# 100 PRs closing nothing come first, so every closing PR is read from the second page.
rows = [{"number": n, "state": "closed", "merged_at": None, "body": None} for n in range(1, 101)]
rows += [{"number": int(number), "state": "open" if state == "open" else "closed",
          "merged_at": "2026-09-01T00:00:00Z" if state == "merged" else None, "body": body}
         for number, state, body in zip(*[iter(pulls)] * 3)]
with open(f"{fix}/pulls.jsonl", "w") as out:
    out.writelines(json.dumps(row) + "\n" for row in rows)
PY
}
SLICE_BODY=$'## What to build\nThe thing.\n\n## Acceptance criteria\n- it works'
proposal() { printf '[{"user":{"login":"%s"},"html_url":"https://x/c/%s","body":"Ready proposal 2026-09-26\\nScope: s"}]' "$1" "$2"; }

# pr <number> <body> [draft] [paths csv] [statuses: <context>=<STATE>,…] [changedFiles] [title]
#    [comments: JSON list] [unresolved threads] [flags: eyes (Codex's 👀), unseen (a 101st thread)]
# Every PR's history is OLD, then its head SHA.
SHA=76b92040123456789abcdef0123456789abcdef0
OLD=0f12fee0123456789abcdef0123456789abcdef0
PRS=()
pr() { local a=("$@" "" "" "" "" "" "" "" ""); PRS+=("${a[@]:0:10}"); }
# One python spawn per write: prs.json is the open-PR read, pr.json the first PR alone over REST,
# pr-graphql.json the first PR beside every PR's number (land-facts).
write_prs() {
  python3 - "$FIX" "$SHA" "$OLD" "${PRS[@]:-}" <<'PY'
import json, sys
fix, head, old, fields = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4:]
nodes = []
for number, body, draft, paths, statuses, changed, title, comments, unresolved, flags in zip(*[iter(fields)] * 10):
    paths = [p for p in paths.split(",") if p]
    status = {"contexts": [dict(zip(("context", "state"), s.split("="))) for s in statuses.split(",")]}
    threads = [{"isResolved": False}] * int(unresolved or 0) + [{"isResolved": True}]
    nodes.append({"number": int(number), "title": title or f"title {number}", "isDraft": bool(draft),
      "headRefOid": head, "body": body, "changedFiles": int(changed or len(paths)),
      "files": {"nodes": [{"path": p} for p in paths]},
      "commits": {"nodes": [{"commit": {"status": status if statuses else None}}]},
      "history": {"nodes": [{"commit": {"oid": old}}, {"commit": {"oid": head}}]},
      "comments": {"nodes": json.loads(comments or "[]")},
      "reviewThreads": {"totalCount": len(threads) + ("unseen" in flags), "nodes": threads},
      "reactions": {"nodes": [{"user": {"login": "chatgpt-codex-connector[bot]"}}] if "eyes" in flags else []}})
json.dump({"data": {"repository": {"pullRequests": {"nodes": nodes}}}}, open(f"{fix}/prs.json", "w"))
json.dump((nodes or [None])[0], open(f"{fix}/pr.json", "w"))
json.dump({"data": {"repository": {"pullRequest": (nodes or [None])[0],
  "pullRequests": {"nodes": [{"number": n["number"]} for n in nodes]}}}}, open(f"{fix}/pr-graphql.json", "w"))
PY
}
# view <verb>: runs it into $out
view() {
  out="$(bash "$DRAIN" "$1" 2>/dev/null)" || fail "$1 exits 0 (got: $out)"
  [[ ! -s "$GH_WRITE_LOG" ]] || fail "$1 must write nothing"
}

reset() {
  ISSUES=()
  PULLS=()
  PRS=()
  : > "$GH_WRITE_LOG"
  : > "$FIX/assignees.txt"
  echo false > "$FIX/labelled.txt"
}
trailer() { grep -o "^$1=.*" | head -n 1 | cut -d= -f2-; }
skip_of() { grep "^SKIP=$1 " | cut -d' ' -f2-; }

# --- usage -----------------------------------------------------------------------------------
reset
for bad in "" "frob" "pick --force" "claim" "claim abc" "claim 12 lease" "release" "release abc" "owed" "owed abc" \
    "verify-queue 12" "merge-queue 12" "digest 12" "instruct" "instruct 12" "instruct 12@xyz1234" "instruct 12@abc123" \
    "instruct 12@abc1234 13" "land-facts" "land-facts abc" "land-facts 12 13"; do
  rc=0; bash "$DRAIN" $bad > /dev/null 2>&1 || rc=$?
  [[ "$rc" -eq 2 ]] || fail "'$bad' should exit 2 (got $rc)"
done

# --- pick: the filter ------------------------------------------------------------------------
reset
issue 10 2026-09-01T00:00:00Z ready-for-agent "" 0 "$SLICE_BODY"
issue 11 2026-09-02T00:00:00Z ready-for-agent,unity:editor "" 0 "$SLICE_BODY"
issue 12 2026-09-03T00:00:00Z ready-for-agent,unity:none someone 0 "$SLICE_BODY"
issue 13 2026-09-04T00:00:00Z ready-for-agent,unity:none "" 2 "$SLICE_BODY"
issue 14 2026-09-05T00:00:00Z ready-for-agent,unity:none "" 0 $'## What to build\nno acceptance heading'
issue 15 2026-09-06T00:00:00Z ready-for-agent,unity:none "" 0 "no scope" "$(proposal stranger 15)"
issue 16 2026-09-07T00:00:00Z ready-for-agent,unity:none "" 0 "no scope" "$(proposal amindell11 16)"
write_queue
echo '17 {"number": 17, "pull_request": {}}' >> "$FIX/issues.txt"; : > "$FIX/comments-17.jsonl"
out="$(bash "$DRAIN" pick 2>/dev/null)"
! grep -q '=17 ' <<<"$out" || fail "a PR the REST issue list returns is no queue issue (got: $out)"
[[ "$(skip_of 10 <<<"$out")" == no-unity-label ]] || fail "no unity label is skipped (got: $out)"
[[ "$(skip_of 11 <<<"$out")" == unity:editor ]] || fail "unity:editor is skipped by label (got: $out)"
[[ "$(skip_of 12 <<<"$out")" == assigned:someone ]] || fail "assigned is skipped (got: $out)"
[[ "$(skip_of 13 <<<"$out")" == blocked:2 ]] || fail "open blocker is skipped (got: $out)"
[[ "$(skip_of 14 <<<"$out")" == no-scope-block ]] || fail "What to build without acceptance is no scope block (got: $out)"
[[ "$(skip_of 15 <<<"$out")" == no-scope-block ]] || fail "a stranger's proposal is no scope block (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == 16 ]] || fail "the one eligible issue is picked (got: $out)"
[[ "$(trailer SCOPE <<<"$out")" == proposal:https://x/c/16 ]] || fail "SCOPE names the proposal comment (got: $out)"
[[ "$(trailer DRY_RUN <<<"$out")" == 0 ]] || fail "DRY_RUN=0 without the flag"

reset
issue 20 2026-09-01T00:00:00Z ready-for-agent,unity:editor,pri:now someone 1
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(skip_of 20 <<<"$out")" == unity:editor,assigned:someone,blocked:1,no-scope-block ]] || fail "every reason is listed (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == none ]] || fail "nothing eligible picks none (got: $out)"
! grep -q '^SCOPE=' <<<"$out" || fail "no SCOPE when nothing is picked"

# --- pick: one admission rule over the four unity labels -------------------------------------
reset
issue 50 2026-09-04T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY"
issue 51 2026-09-01T00:00:00Z ready-for-agent,unity:editor,drain:approved "" 0 "$SLICE_BODY"
issue 52 2026-09-03T00:00:00Z ready-for-agent,unity:headless "" 0 "$SLICE_BODY"
issue 53 2026-09-02T00:00:00Z ready-for-agent,unity:local-proof "" 0 "$SLICE_BODY"
issue 54 2026-09-01T00:00:00Z ready-for-agent,unity:headless,unity:editor "" 0 "$SLICE_BODY"
issue 55 2026-09-01T00:00:00Z ready-for-agent,unity:none,unity:local-proof "" 0 "$SLICE_BODY"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(skip_of 51 <<<"$out")" == unity:editor ]] || fail "editor is never admitted, whatever else it carries (got: $out)"
[[ "$(skip_of 54 <<<"$out")" == unity:editor,unity:headless,unity-conflict ]] || fail "an editor label beside headless is a conflict (got: $out)"
[[ "$(skip_of 55 <<<"$out")" == unity:local-proof,unity:none,unity-conflict ]] || fail "two admitted unity labels are still a conflict (got: $out)"
[[ "$(grep -c '^SKIP=' <<<"$out")" -eq 3 ]] || fail "none, headless (with no drain:approved) and local-proof are all admitted (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == 53 ]] || fail "the three admitted labels share one priority-then-age order (got: $out)"

# --- pick: priority, then age ------------------------------------------------------------------
reset
issue 30 2026-09-01T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY"
issue 31 2026-09-02T00:00:00Z ready-for-agent,unity:none,pri:later "" 0 "$SLICE_BODY"
issue 32 2026-09-04T00:00:00Z ready-for-agent,unity:none,pri:next "" 0 "$SLICE_BODY"
issue 33 2026-09-03T00:00:00Z ready-for-agent,unity:none,pri:next "" 0 "$SLICE_BODY"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(trailer ISSUE <<<"$out")" == 33 ]] || fail "pri:next beats later/none, older wins within it (got: $out)"
[[ "$(trailer SCOPE <<<"$out")" == body ]] || fail "a slice-shaped body is the scope (got: $out)"
issue 34 2026-09-09T00:00:00Z ready-for-agent,unity:none,pri:now "" 0 "$SLICE_BODY"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(trailer ISSUE <<<"$out")" == 34 ]] || fail "pri:now beats an older pri:next (got: $out)"

reset
issue 40 2026-09-01T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY" '[{"user":{"login":"amindell11"},"html_url":"https://x/c/a","body":"Ready proposal 2026-09-20"},{"user":{"login":"amindell11"},"html_url":"https://x/c/b","body":"Ready proposal 2026-09-25"}]'
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(trailer SCOPE <<<"$out")" == proposal:https://x/c/b ]] || fail "the latest proposal wins over an older one and the body (got: $out)"

# --- pick: --dry-run writes nothing --------------------------------------------------------------
out="$(bash "$DRAIN" pick --dry-run 2>/dev/null)"
[[ "$(trailer DRY_RUN <<<"$out")" == 1 && "$(trailer ISSUE <<<"$out")" == 40 ]] || fail "dry run stamps DRY_RUN=1 and still picks (got: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "pick must write nothing"

# --- pick: a claimed issue reads by the PRs closing it -----------------------------------------
reset
issue 60 2026-09-01T00:00:00Z ready-for-agent,unity:headless,drain:building me 0 "$SLICE_BODY"
issue 61 2026-09-02T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "no scope" "$(proposal amindell11 61)"
issue 62 2026-09-03T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "$SLICE_BODY"
issue 63 2026-09-04T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "$SLICE_BODY"
issue 64 2026-09-05T00:00:00Z ready-for-agent,unity:none,drain:building me 1 "$SLICE_BODY"
issue 65 2026-09-06T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "no scope" "$(proposal amindell11 65)"
issue 66 2026-09-07T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY"
issue 67 2026-09-08T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "$SLICE_BODY"
pull 160 open "Refs #60, not a closing keyword"
pull 161 open "Closes #61"
pull 162 closed "Fixes: #62"
pull 163 closed $'## What changed\n\nresolves #62'
pull 164 closed "closed #63"
pull 165 open "Closes #63"
pull 166 closed "Closes #66"
pull 167 merged "Fixes #67"
write_queue
out="$(bash "$DRAIN" pick 2>/dev/null)"
[[ "$(grep '^UNFINISHED=' <<<"$out")" == $'UNFINISHED=60 body\nUNFINISHED=65 proposal:https://x/c/65' ]] || fail "a claimed issue no PR of any state closes is unfinished, with its scope; a mention without a closing keyword closes nothing (got: $out)"
[[ -z "$(skip_of 60 <<<"$out")" ]] || fail "an unfinished issue has no SKIP line (got: $out)"
[[ "$(skip_of 61 <<<"$out")" == drain:building ]] || fail "a claimed issue with an open PR is building; the assignee is the claim's (got: $out)"
[[ "$(skip_of 62 <<<"$out")" == drain:building,pr-closed:162,pr-closed:163 ]] || fail "only closed-unmerged PRs names each one (got: $out)"
[[ "$(skip_of 63 <<<"$out")" == drain:building ]] || fail "an open PR beside a closed one is building, not pr-closed (got: $out)"
[[ "$(skip_of 64 <<<"$out")" == drain:building,blocked:1 ]] || fail "a claimed issue blocked since its claim is skipped, not unfinished (got: $out)"
[[ "$(skip_of 67 <<<"$out")" == drain:building ]] || fail "a merged PR is not closed-unmerged (got: $out)"
[[ "$(trailer ISSUE <<<"$out")" == 66 ]] || fail "a released issue is picked again whatever its closed PRs (got: $out)"

# --- claim: two writes, the assignee, then the label ----------------------------------------------
reset
out="$(bash "$DRAIN" claim 40 2>/dev/null)"
[[ "$out" == CLAIM=claimed ]] || fail "claim prints one trailer and no slot (got: $out)"
[[ "$(cat "$GH_WRITE_LOG")" == $'api -X POST repos/owner/repo/issues/40/assignees -f assignees[]=me --silent\napi -X POST repos/owner/repo/issues/40/labels -f labels[]=drain:building --silent' ]] || fail "the assignee, then the label (got: $(cat "$GH_WRITE_LOG"))"

# --- claim: assignee changed since pick → taken, nothing written ---------------------------------
reset; echo "someone" > "$FIX/assignees.txt"
rc=0; out="$(bash "$DRAIN" claim 40 2>/dev/null)" || rc=$?
[[ "$rc" -eq 4 && "$out" == CLAIM=taken ]] || fail "an assignee at re-read is taken, exit 4 (rc=$rc: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "taken writes nothing"

# --- release: the assignee, then the label when the issue carries it ------------------------------
reset; echo true > "$FIX/labelled.txt"
out="$(bash "$DRAIN" release 40 2>/dev/null)"
[[ "$out" == RELEASE=released ]] || fail "release prints its trailer (got: $out)"
[[ "$(cat "$GH_WRITE_LOG")" == $'api -X DELETE repos/owner/repo/issues/40/assignees -f assignees[]=me --silent\napi -X DELETE repos/owner/repo/issues/40/labels/drain:building --silent' ]] || fail "the assignee, then the label (got: $(cat "$GH_WRITE_LOG"))"
reset
out="$(bash "$DRAIN" release 40 2>/dev/null)"
[[ "$out" == RELEASE=released && "$(cat "$GH_WRITE_LOG")" == "api -X DELETE repos/owner/repo/issues/40/assignees -f assignees[]=me --silent" ]] \
  || fail "an issue without the label loses only the assignee (got: $out; $(cat "$GH_WRITE_LOG"))"

# --- owed: every verdict ----------------------------------------------------------------------------
# owed_is <verdict> <open unity> <open script> <open eyes> <why> <body> [<tried unity> <tried script> <behind>]
owed_is() {
  PRS=(); pr 77 "$6"; write_prs
  local out rc=0
  out="$(bash "$DRAIN" owed 77 2>"$TMP/err")" || rc=$?
  [[ "$rc" -eq 0 ]] || fail "owed exits 0 on every verdict: $5 (rc=$rc: $(cat "$TMP/err"))"
  [[ "$out" == "OWED=$1"$'\n'"OPEN_UNITY=$2"$'\n'"OPEN_SCRIPT=$3"$'\n'"OPEN_EYES=$4"$'\n'"TRIED_UNITY=${7:-0}"$'\n'"TRIED_SCRIPT=${8:-0}"$'\n'"BEHIND=${9:-0}" ]] || fail "$5 (got: $out)"
}
HEAD=$'## Test status\n\nHosted: green on `76b9204`\n\n### Owed local\n\n'
reset
owed_is absent 0 0 0 "a body with no section is absent" $'## Test status\n\n- [ ] unity: not under the heading\n'
owed_is none 0 0 0 "None. alone is nothing owed" "${HEAD}None."$'\n'
owed_is open 2 1 1 "unticked items count per kind; a tick with no result line is behind head" "${HEAD}"$'- [ ] unity: `run-tests <slot> -WithGraphics`\n- [ ] unity: second boot\n- [x] unity: done\n- [ ] script: `run-script-tests <slot>`\n- [ ] eyes: no bars behind the backdrop\n' 0 0 1
owed_is discharged 0 0 0 "all ticked is discharged; indented lines are an item's result" "${HEAD}"$'- [x] unity: `run-tests <slot>`\n  passed on `76b9204`, 12/12\n    - [ ] eyes: nested text is result prose\n\n- [x] eyes: looks right\n'
owed_is open 1 0 1 "CRLF bodies parse, and the section ends at the next heading" "${HEAD//$'\n'/$'\r\n'}"$'- [ ] unity: boot\r\n  result line\r\n- [ ] eyes: look\r\n\r\n## Merge order\r\n\r\n- after #747 — same file\r\n'
owed_is malformed 0 0 0 "an unknown kind is malformed" "${HEAD}"$'- [ ] unity: boot\n- [ ] manual: something\n'
owed_is malformed 0 0 0 "nothing owed is never a checkbox" "${HEAD}"$'- [ ] None\n'
owed_is malformed 0 0 0 "None. beside items is malformed" "${HEAD}"$'- [x] script: ran\nNone.\n'
owed_is malformed 0 0 0 "None. before an item is malformed, not nothing owed" "${HEAD}None."$'\n- [ ] unity: boot\n'
owed_is malformed 0 0 0 "an empty section is malformed" "${HEAD}"$'## Merge order\n'
owed_is malformed 0 0 0 "prose in the section is malformed" "${HEAD}"$'The hosted run skips graphics.\n- [ ] unity: boot\n'
owed_is malformed 0 0 0 "an indented line under no item is malformed" "${HEAD}"$'  - [ ] unity: boot\n- [ ] unity: boot\n'
owed_is malformed 0 0 0 "two sections are malformed" "${HEAD}None."$'\n\n### Owed local\n\n- [ ] unity: boot\n'
grep -q 'more than one' "$TMP/err" || fail "malformed names why on stderr (got: $(cat "$TMP/err"))"
owed_is open 2 1 1 "an item is tried when a result line names the head, by a short or the full SHA; one naming an older commit is untried, or behind when ticked; eyes is neither" \
  "${HEAD}"$'- [ ] unity: boot A\n  FAIL at `76b9204`, 3 failed\n- [ ] unity: boot B\n  FAIL at `0f12fee`, before the fix (`76b920` is too short)\n- [ ] script: suite\n  FAIL at `'"$SHA"$'`\n- [x] unity: boot C\n  passed on `0f12fee`\n- [x] script: lint\n  passed on `76B9204`\n- [ ] eyes: look\n  seen at `76b9204`\n' 1 1 1
[[ ! -s "$GH_WRITE_LOG" ]] || fail "owed must write nothing"

# --- verify-queue: the untried items of every pipeline PR ---------------------------------------------
OWED=$'## Test status\n\n### Owed local\n\n'
reset
pr 102 "${OWED}"$'- [ ] unity: boot\n  FAIL at `76b9204`\n- [ ] eyes: look\n'
pr 101 $'no section\n- [ ] unity: not an item\n'
write_prs
view verify-queue
[[ "$out" == VERIFY=none ]] || fail "a PR whose open items are all tried, and one with no section, leave the queue empty (got: $out)"
pr 104 "${OWED//$'\n'/$'\r\n'}"$'- [ ] unity: crlf boot\r\n  FAIL at `0f12fee`\r\n'
pr 103 "${OWED}"$'- [ ] unity: boot → graphics\n- [ ] unity: failed boot\n  FAIL at `76b9204`\n- [x] unity: done\n- [ ] script: `run-script-tests <slot>`\n- [ ] eyes: look\n'
write_prs
view verify-queue
[[ "$out" == "VERIFY=103 $SHA unity:1,script:1"$'\nITEM=103 unity boot → graphics\nITEM=103 script `run-script-tests <slot>`\n'"VERIFY=104 $SHA unity:1,script:0"$'\nITEM=104 unity crlf boot' ]] \
  || fail "one block per PR with an untried item, lowest PR first, its untried items in body order (got: $out)"

# --- merge-queue: facts, landing order, skip reasons --------------------------------------------------
NONE="${OWED}None."$'\n'
after() { printf '\n## Merge order\n\n'; printf -- '- after #%s — both edit one file\n' "$@"; }
reset
pr 213 "${OWED}"$'- [ ] unity: boot\n'
pr 211 "$NONE" "" "" merge-proof/headless=FAILURE
write_prs
view merge-queue
[[ "$out" == $'MERGE=none\nSKIP=211 hosted:failure' ]] || fail "with nothing placed, MERGE=none leads the skips (got: $out)"
pr 201 "$NONE" "" scripts/x.sh
pr 202 "${OWED}"$'- [x] unity: boot\n  passed on `0f12fee`\n' draft .github/w.yml
crlf="$NONE$(after 205 999)"; pr 203 "${crlf//$'\n'/$'\r\n'}"
pr 204 "$NONE" "" src/a.cs merge-proof/headless=PENDING,CodeRabbit=FAILURE
pr 205 "$NONE" "" src/a.cs "" 101
pr 206 "$NONE"$'\n## Merge order\n\n- after #204, same file\n'
pr 207 "$NONE$(after 206)"
pr 208 "$NONE$(after 209)"
pr 209 "$NONE$(after 208)"
pr 210 "$NONE$(after 210)"
pr 212 "$NONE" "" "" merge-proof/headless=SUCCESS,merge-proof/resharper=ERROR
pr 214 "$NONE$(after 213)"
pr 215 "no section"
pr 216 "$NONE$(after 215)"
pr 217 "$NONE$(after 204)$(after 204)"
pr 218 "${OWED}"$'- [ ] unity: boot\n\n## Merge order\n\nlands after #204\n'
pr 219 "${OWED}"$'- [ ] unity: boot\n'"$(after 220)"
pr 220 "$NONE$(after 219)"
write_prs
view merge-queue
want="MERGE=204 @ owed:none
MERGE=201 @ owed:none,scripts
MERGE=202 @ owed:discharged,behind-head,draft,github
MERGE=205 @ owed:none,scripts
MERGE=203 @ owed:none
SKIP=206 merge-order-malformed
SKIP=207 after:206
SKIP=208 order-cycle,after:209
SKIP=209 order-cycle,after:208
SKIP=210 order-cycle,after:210
SKIP=211 hosted:failure
SKIP=212 hosted:failure
SKIP=214 after:213
SKIP=216 after:215
SKIP=217 merge-order-malformed
SKIP=218 merge-order-malformed
SKIP=219 order-cycle
SKIP=220 order-cycle,after:219"
[[ "$out" == "${want//@/$SHA}" ]] || fail "a constraint wins, then scripts and .github last, then PR number; a constraint on a PR no longer open is dead; every skip reason; a PR with open items shows only a merge-order fault (got: $out)"

# --- digest: every list ---------------------------------------------------------------------------------
U=https://github.com/owner/repo
under() { awk -v title="**$1**" '$0 == title { on = 1; next } on && /^$/ { exit } on' <<<"$out"; }
reset
issue 10 2026-09-01T00:00:00Z ready-for-agent "" 0 "$SLICE_BODY"
issue 11 2026-09-02T00:00:00Z ready-for-agent,unity:editor "" 0 "$SLICE_BODY"
pr 306 "no section"
write_queue; write_prs
view digest
[[ "$(under 'Ready-labelled issues no cloud batch can build')" == "- [#10]($U/issues/10) — no-unity-label" ]] || fail "an issue no cloud batch can build is listed; a unity:editor one is not (got: $out)"
[[ "$(grep -c '^\*\*' <<<"$out")" -eq 1 ]] || fail "empty lists are omitted (got: $out)"
ISSUES=(); write_queue
view digest
[[ "$out" == "Nothing is waiting in the drain pipeline." ]] || fail "nothing listed says so in one line (got: $out)"
issue 16 2026-09-03T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY"
issue 17 2026-09-04T00:00:00Z ready-for-agent,unity:none "" 0 "$SLICE_BODY"
issue 54 2026-09-05T00:00:00Z ready-for-agent,unity:headless,unity:editor "" 0 "$SLICE_BODY"
issue 60 2026-09-06T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "$SLICE_BODY"
issue 62 2026-09-07T00:00:00Z ready-for-agent,unity:none,drain:building me 0 "$SLICE_BODY"
pull 162 closed "Closes #62"
pull 163 closed "Closes #62"
pr 301 "${OWED}"$'- [ ] unity: boot\n- [ ] eyes: look\n' "" "" "" "" "feat: a → b"
pr 302 "${OWED}"$'- [ ] script: suite\n  FAIL at `76b9204`\n'
pr 303 "${OWED}"$'- [ ] manual: something\n'
pr 304 "$NONE" draft
pr 305 "$NONE$(after 301)"
write_queue; write_prs
view digest
[[ "$(under '`eyes` items to look at')" == "- [#301]($U/pull/301) feat: a → b — 1 unticked" ]] || fail "eyes list, with the PR title in UTF-8 (got: $out)"
[[ "$(under 'Owed items that failed at head')" == "- [#302]($U/pull/302) title 302 — unity:0,script:1" ]] || fail "tried list (got: $out)"
[[ "$(under 'Malformed owed-local checklists')" == "- [#303]($U/pull/303) title 303" ]] || fail "malformed list (got: $out)"
[[ "$(under 'Merge candidates, in landing order')" == "- [#304]($U/pull/304) title 304 — owed:none, draft" ]] || fail "merge candidates with their facts (got: $out)"
[[ "$(under 'Skipped from the merge queue')" == "- [#305]($U/pull/305) title 305 — after [#301]($U/pull/301)" ]] || fail "merge-queue skips with linked reasons (got: $out)"
[[ "$(under 'Claimed issues whose PRs all closed unmerged')" == "- [#62]($U/issues/62) — pr-closed [#162]($U/pull/162), pr-closed [#163]($U/pull/163)" ]] || fail "pr-closed issues name their PRs (got: $out)"
[[ "$(under 'Ready-labelled issues no cloud batch can build')" == "- [#54]($U/issues/54) — unity:editor, unity:headless, unity-conflict" ]] || fail "a unity conflict cannot be built (got: $out)"
[[ "$(under 'Build')" == "- \`pick\` admits 2 issues"$'\n'"- unfinished: [#60]($U/issues/60)" ]] || fail "build list (got: $out)"
[[ "$(under 'Verify')" == "- [#301]($U/pull/301) feat: a → b — unity:1,script:0" ]] || fail "a PR with an untried item and an eyes item is in both lists (got: $out)"

# --- instruct: every argument is checked before one record per PR is written -----------------------------
C1=aaaa1110123456789abcdef0123456789abcdef0
C2=aaaa1111123456789abcdef0123456789abcdef0
C3=bbbb0000123456789abcdef0123456789abcdef0
# pull_of <number> <state> <base> <commit>…: the REST PR read and its commit list
pull_of() { printf '%s\t%s\n' "$2" "$3" > "$FIX/pull-$1.tsv"; printf '%s\n' "${@:4}" > "$FIX/commits-$1.txt"; }
instruct() { rc=0; out="$(bash "$DRAIN" instruct "$@" 2>"$TMP/err")" || rc=$?; }
reset
pull_of 12 open main "$C1" "$C2"
pull_of 13 closed main "$C3"
pull_of 14 open release "$C3"
pull_of 15 open main $(printf 'cccc%036d\n' $(seq 1 100)) "$C3"
instruct 12@aaaa111 13@AAAA1110 14@bbbb000 15@AAAA1110 12@1110123 12@aaaa1110
[[ "$rc" -eq 3 ]] || fail "a refused argument exits 3 (rc=$rc: $out)"
[[ "$out" == $'REFUSED=12 not-a-commit\nREFUSED=13 not-open\nREFUSED=14 base:release\nREFUSED=15 not-a-commit\nREFUSED=12 not-a-commit' ]] \
  || fail "a SHA naming two commits, none, or only the middle of one is no commit; a closed PR and another base refuse (got: $out)"
grep -q "names more than one of its commits" "$TMP/err" || fail "an ambiguous SHA says so (got: $(cat "$TMP/err"))"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "one refused argument writes nothing, not even for a good one"
instruct 12@AAAA1110 15@bbbb0000
[[ "$rc" -eq 0 && "$out" == "INSTRUCTED=12 $C1"$'\n'"INSTRUCTED=15 $C3" ]] || fail "each PR is recorded at the full commit; a commit on the second page is found (rc=$rc: $out)"
today="$(date -u +%F)"
[[ "$(cat "$GH_WRITE_LOG")" == "api -X POST repos/owner/repo/issues/12/comments -f body=Merge instruction $today: \`$C1\` --silent"$'\n'"api -X POST repos/owner/repo/issues/15/comments -f body=Merge instruction $today: \`$C3\` --silent" ]] \
  || fail "one comment per PR, its first line the record (got: $(cat "$GH_WRITE_LOG"))"

# --- land-facts: the record, Codex's review, threads and merge order -------------------------------------
codex() { printf '{"author":{"login":"chatgpt-codex-connector"},"body":"<!-- codex-pull-request-review-summary -->\\n\\n## Codex Review Summary\\n\\n| Review | Status | Commit | Review trigger |\\n| --- | --- | --- | --- |\\n| 📝 **Code Review** | %s | %s | PR opened |\\n"}' "$1" "$2"; }
note() { printf '{"author":{"login":"%s"},"body":"%s"}' "$1" "$2"; }
rec() { note amindell11 "Merge instruction 2026-10-0$1: \`$2\`"; }
DONE='✅ **Completed** <relative-time datetime=\"2026-10-03T06:07:25Z\">2026-10-03</relative-time>'
facts_are() {
  PRS=(); pr 401 "$NONE$(after 402 999)" "" "" "" "" "" "$3" "${4:-0}" "${5:-}"; pr 402 "no section"; write_prs
  out="$(bash "$DRAIN" land-facts 401 2>"$TMP/err")" || fail "land-facts exits 0: $1 (got: $out)"
  [[ "$out" == "$2" ]] || fail "$1 (got: $out)"
}
facts() { printf 'OWED=none\nINSTRUCTION=%s\nREVIEW=%s\nUNRESOLVED=%s\nAFTER=402' "$@"; }
reset
facts_are "the latest record by the author wins; a stranger's, a short SHA and no date are no record; a Completed row names the commit reviewed; threads count unresolved; a constraint on a PR no longer open is dead" \
  "$(facts "$SHA" "completed $SHA" 2)" \
  "[$(rec 1 "$OLD"),$(rec 2 "$SHA"),$(note stranger "Merge instruction 2026-10-03: \`$OLD\`"),$(note amindell11 "Merge instruction 2026-10-04: \`76b9204\`"),$(note amindell11 "Merge instruction: \`$OLD\`"),$(codex "$DONE" '`76b9204`')]" 2
facts_are "the latest record names nothing when its commit is not the PR's; a review of an older commit names that commit" \
  "$(facts none "completed $OLD" 0)" "[$(rec 1 "$SHA"),$(rec 2 "$C3"),$(codex "$DONE" '`0f12fee`')]"
facts_are "a status other than Completed is a running review; a thread past the first 100 counts as unresolved" \
  "$(facts none running 1)" "[$(codex '⏳ **Running**' '`76b9204`')]" 0 unseen
facts_are "Codex's 👀 on the PR is a running review, whatever the row says" "$(facts none running 0)" "[$(codex "$DONE" '`76b9204`')]" 0 eyes
facts_are "a summary someone else posted is no review" "$(facts none absent 0)" \
  "[$(note stranger '<!-- codex-pull-request-review-summary -->\n| **Code Review** | ✅ **Completed** | `76b9204` |')]"
facts_are "a reviewed SHA naming no commit of the PR is unreadable" "$(facts none unreadable 0)" "[$(codex "$DONE" '`cccc000`')]"
PRS=(); pr 401 "$NONE"$'\n## Merge order\n\nafter #402\n'; write_prs
out="$(bash "$DRAIN" land-facts 401 2>/dev/null)"
[[ "$(trailer AFTER <<<"$out")" == malformed && "$(trailer OWED <<<"$out")" == none ]] || fail "a malformed merge order says so (got: $out)"
[[ ! -s "$GH_WRITE_LOG" ]] || fail "land-facts must write nothing"

# --- merge-queue: the facts instructed and class ----------------------------------------------------------
GREEN=merge-proof/headless=SUCCESS,merge-proof/resharper=SUCCESS
REVIEWED="[$(codex "$DONE" '`76b9204`')]"
reset
pr 501 "$NONE" "" src/a.cs "$GREEN" "" "" "[$(rec 1 "$OLD"),$(codex "$DONE" '`76b9204`')]"
pr 502 "$NONE" "" src/a.cs merge-proof/headless=SUCCESS,merge-proof/resharper=PENDING "" "" "$REVIEWED"
pr 503 "$NONE" "" src/a.cs "$GREEN" "" "" "[$(codex "$DONE" '`0f12fee`')]"
pr 504 "$NONE" "" src/a.cs "$GREEN" "" "" "$REVIEWED" 1
pr 505 "$NONE" "" scripts/x.sh "$GREEN" "" "" "$REVIEWED"
pr 506 "${OWED}"$'- [x] unity: boot\n  passed on `76b9204`\n' "" src/a.cs "$GREEN" "" "" "$REVIEWED"
pr 507 "$NONE" "" src/a.cs "$GREEN" "" "" "$REVIEWED" 0 eyes
write_prs
view merge-queue
want="MERGE=501 @ owed:none,instructed,class
MERGE=502 @ owed:none
MERGE=503 @ owed:none
MERGE=504 @ owed:none
MERGE=506 @ owed:discharged
MERGE=507 @ owed:none
MERGE=505 @ owed:none,scripts"
[[ "$out" == "${want//@/$SHA}" ]] || fail "instructed names a record on any commit; class needs nothing owed, no scripts, both statuses green, Codex done on the head and no open thread (got: $out)"

echo "PASS test_drain_pick.sh"
