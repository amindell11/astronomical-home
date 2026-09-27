#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/sweep_leads.sh

# Hermetic regression for scripts/sweep_leads.sh: each lead kind, the --since cut (day and
# timestamp), a fenced #N or path is not a lead, a quiet issue lands in QUIET=, the --out packet
# layout (citing paragraphs, list items and table rows only), and the exit codes. gh is a stub on PATH answering from fixtures; the dead-path lookup
# runs against origin/main of a throwaway git tree.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LEADS="$SCRIPT_DIR/../sweep_leads.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export FIX="$TMP/fix"
export GH_CALL_LOG="$TMP/gh-calls.log"
mkdir -p "$FIX"

export STUB_BIN="$TMP/bin"
mkdir -p "$STUB_BIN"
cat > "$STUB_BIN/gh" <<'EOF'
#!/usr/bin/env bash
args="$*"
echo "$args" >> "$GH_CALL_LOG"
[[ -z "${GH_FAIL:-}" ]] || { echo "gh: HTTP 502" >&2; exit 1; }
case "$args" in
  "issue list --state open "*) cat "$FIX/open.json" ;;
  "issue list --state closed "*) cat "$FIX/closed.json" ;;
  "pr list --state merged "*) cat "$FIX/merged.json" ;;
  *) echo "gh stub: unmodelled call: $args" >&2; exit 97 ;;
esac
EOF
chmod +x "$STUB_BIN/gh"
export PATH="$STUB_BIN:$PATH"

TREE="$TMP/tree"
mkdir -p "$TREE/doc" "$TREE/src/Game/Assets/Ships"
git -C "$TREE" init -q
git -C "$TREE" config core.autocrlf false
echo x > "$TREE/doc/here.md"; echo x > "$TREE/src/Game/Assets/Ships/Scout.cs"
git -C "$TREE" add -A
git -C "$TREE" -c user.name=t -c user.email=t@t commit -q -m init
git -C "$TREE" update-ref refs/remotes/origin/main HEAD

# Fixtures: issues 1–7 open, 20/21 closed, PRs 100–104 merged. --since is 2026-09-22.
python3 - "$FIX" <<'PY'
import json, os, sys
fix = sys.argv[1]
old, new = "2026-09-20T10:00:00Z", "2026-09-23T10:00:00Z"
def issue(n, body, updated=old):
    return {"number": n, "title": f"Issue {n}", "labels": [{"name": "tooling"}], "assignees": [], "updatedAt": updated, "body": body}
FENCED = "```\n#7 lives in a fence, as does doc/fenced-gone.md\n```"
open_issues = [
    issue(1, "Quiet: names `doc/here.md`, `doc/`, Assets/Ships/Scout.cs and https://example.com/doc/url-gone.md."),
    issue(2, "Cited by a PR title."),
    issue(3, "Cited in a PR body paragraph."),
    issue(4, "Closed by a PR's closing refs."),
    issue(5, "Parent of #20 (closed in window) and #21 (closed before it)."),
    issue(6, "Points at doc/gone.md and `gonedir/`.\n\n" + FENCED),
    issue(7, "Updated in window; cited only inside a PR's code fence.", updated=new),
]
closed = [{"number": 20, "closedAt": "2026-09-24T00:00:00Z"}, {"number": 21, "closedAt": "2026-09-21T23:59:59Z"}]
def pr(n, title, body, closes=(), merged=new):
    return {"number": n, "title": title, "mergedAt": merged, "body": body,
            "closingIssuesReferences": [{"number": c} for c in closes]}
merged = [
    pr(100, "feat: thing (#2)", "No refs here."),
    pr(101, "feat: other", "Intro paragraph.\n\nAdvances #3 by a step.\n\nUnrelated closing paragraph.\n\n```\n#7 in a fence\n```\n\n"
       "Items:\n- one for #3\n  continued\n- two for #9\n\n| # | verdict |\n|---|---|\n| #3 | keep |\n| #9 | done |"),
    pr(102, "feat: closer", "Body without refs.", closes=[4]),
    pr(103, "feat: early", "Advances #1.", merged="2026-09-21T10:00:00Z"),
    pr(104, "feat: same day", "Advances #1 too.", merged="2026-09-22T10:00:00Z"),
]
for name, rows in (("open", open_issues), ("closed", closed), ("merged", merged)):
    json.dump(rows, open(os.path.join(fix, f"{name}.json"), "w", encoding="utf-8"))
PY

run() { (cd "$TREE" && bash "$LEADS" "$@"); }
trailer() { grep -o "^$1=.*" | head -n 1 | cut -d= -f2-; }

# --- usage ---------------------------------------------------------------------------------
for args in "" "--since 2026-09-22" "--out $TMP/o" "--since yesterday --out $TMP/o" \
            "--since 2026-09-22T10:00:00 --out $TMP/o" "--since 2026-09-22 --out $TMP/o --bogus"; do
  rc=0; run $args > /dev/null 2>&1 || rc=$?
  [[ "$rc" -eq 2 ]] || fail "usage '$args' should exit 2 (got $rc)"
done
mkdir -p "$TMP/full"; touch "$TMP/full/stale.md"
rc=0; run --since 2026-09-22 --out "$TMP/full" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 2 ]] || fail "a non-empty --out should exit 2 (got $rc)"

# --- infra ---------------------------------------------------------------------------------
rc=0; GH_FAIL=1 run --since 2026-09-22 --out "$TMP/o-fail" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 1 ]] || fail "a gh failure should exit 1 (got $rc)"
cp "$FIX/closed.json" "$TMP/closed.bak"
python3 -c 'import json,sys; json.dump([{"number": 1000 + i, "closedAt": "2026-09-24T00:00:00Z"} for i in range(1000)], open(sys.argv[1], "w"))' "$FIX/closed.json"
rc=0; run --since 2026-09-22 --out "$TMP/o-limit" > /dev/null 2>&1 || rc=$?
[[ "$rc" -eq 1 ]] || fail "a listing at its limit should exit 1 (got $rc)"
cp "$TMP/closed.bak" "$FIX/closed.json"

# --- leads by day --------------------------------------------------------------------------
: > "$GH_CALL_LOG"
OUT="$TMP/day"
out="$(run --since 2026-09-22 --out "$OUT" 2>/dev/null)"
grep -q -- '--search closed:>=2026-09-22 ' "$GH_CALL_LOG" || fail "closed search narrows by day (got: $(cat "$GH_CALL_LOG"))"
grep -q -- '--search merged:>=2026-09-22 ' "$GH_CALL_LOG" || fail "merged search narrows by day"
[[ "$(trailer LED <<<"$out")" == "1,2,3,4,5,6,7" ]] || fail "day LED (got: $out)"
[[ "$(trailer QUIET <<<"$out")" == "" ]] || fail "day QUIET (got: $out)"
grep -q '^- cited: PR #104' "$OUT/issue-1.md" || fail "a PR merged on the --since day cites"
! grep -q 'PR #103' "$OUT/issue-1.md" || fail "a PR merged before --since does not cite"
[[ "$(trailer CITED <<<"$out")" == 4 && "$(trailer CHILD_CLOSED <<<"$out")" == 1 ]] || fail "day kind counts (got: $out)"
[[ "$(trailer DEAD_PATH <<<"$out")" == 1 && "$(trailer UPDATED <<<"$out")" == 1 ]] || fail "day kind counts (got: $out)"

# --- leads by timestamp: PR 104 falls outside, so issue 1 goes quiet ------------------------
OUT="$TMP/ts"
out="$(run --since 2026-09-22T12:00:00Z --out "$OUT" 2>/dev/null)"
[[ "$(trailer LED <<<"$out")" == "2,3,4,5,6,7" ]] || fail "timestamp LED (got: $out)"
[[ "$(trailer QUIET <<<"$out")" == "1" ]] || fail "a quiet issue lands in QUIET= (got: $out)"
[[ "$(ls "$OUT" | tr '\n' ' ')" == "issue-2.md issue-3.md issue-4.md issue-5.md issue-6.md issue-7.md " ]] \
  || fail "--out holds one packet per led issue (got: $(ls "$OUT"))"

# --- each lead kind ------------------------------------------------------------------------
grep -q '^- cited: PR #100 — feat: thing (#2) (merged 2026-09-23T10:00:00Z)$' "$OUT/issue-2.md" || fail "cited by title (got: $(cat "$OUT/issue-2.md"))"
grep -q '^- cited: PR #101' "$OUT/issue-3.md" || fail "cited by body prose"
grep -q '^- cited: PR #102' "$OUT/issue-4.md" || fail "cited by closing refs"
grep -q '^mergedAt: 2026-09-23T10:00:00Z · closes: #4$' "$OUT/issue-4.md" || fail "closing refs line"
grep -q '^(no body paragraph cites #4)$' "$OUT/issue-4.md" || fail "a close-only citation says so"
grep -q '^- child-closed: #20 (closed 2026-09-24T00:00:00Z)$' "$OUT/issue-5.md" || fail "child-closed (got: $(cat "$OUT/issue-5.md"))"
! grep -q '^- child-closed: #21' "$OUT/issue-5.md" || fail "a child closed before --since is no lead"
[[ "$(grep -c '^- dead-path:' "$OUT/issue-6.md")" -eq 2 ]] || fail "two dead paths (got: $(cat "$OUT/issue-6.md"))"
grep -q '^- dead-path: `doc/gone.md`' "$OUT/issue-6.md" || fail "a missing file is a dead path"
grep -q '^- dead-path: `gonedir/`' "$OUT/issue-6.md" || fail "a missing backticked dir is a dead path"
! grep -q 'fenced-gone' <(grep '^- ' "$OUT/issue-6.md") || fail "a fenced path is no lead"
grep -q '^- updated: 2026-09-23T10:00:00Z$' "$OUT/issue-7.md" || fail "updated"
[[ "$(grep -c '^- ' "$OUT/issue-7.md")" -eq 1 ]] || fail "a #N inside a PR's code fence is not a cited lead (got: $(cat "$OUT/issue-7.md"))"

# --- packet layout -------------------------------------------------------------------------
P="$OUT/issue-3.md"
[[ "$(head -n 1 "$P")" == "# #3 — Issue 3" ]] || fail "packet heading (got: $(head -n 1 "$P"))"
grep -q '^labels: tooling · assignees: none · updatedAt: 2026-09-20T10:00:00Z$' "$P" || fail "packet metadata line"
grep -q '^<issue-body number=3>$' "$P" && grep -q '^</issue-body>$' "$P" || fail "issue body is delimited"
grep -q '^### PR #101 — feat: other$' "$P" || fail "citing PR heading"
[[ "$(sed -n '/^<pr-body number=101>$/,/^<\/pr-body>$/p' "$P")" == "$(printf '<pr-body number=101>\nAdvances #3 by a step.\n\n- one for #3\n  continued\n\n| # | verdict |\n| #3 | keep |\n</pr-body>')" ]] \
  || fail "only the citing paragraphs, list items and table rows are carried (got: $(cat "$P"))"

echo "test_sweep_leads: PASS"
