#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/evidence_push.sh

# A local origin stands in for GitHub. The slug and the HEAD client are injected,
# so the test never pushes to the real remote and never needs a network.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUSH="$SCRIPT_DIR/../evidence_push.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

git init -q -b main "$TMP/bare" --bare
git init -q -b main "$TMP/repo"
git -C "$TMP/repo" remote add origin "$TMP/bare"
echo seed > "$TMP/repo/README"
git -C "$TMP/repo" add README
git -C "$TMP/repo" -c user.email=test@example.com -c user.name=test commit -qm seed

mkdir -p "$TMP/bin"
cat > "$TMP/bin/curl" <<'EOF'
#!/usr/bin/env bash
code=200
[[ ! -f "$CURL_STATUS_FILE" ]] || code="$(cat "$CURL_STATUS_FILE")"
printf '%s' "$code"
EOF
chmod +x "$TMP/bin/curl"

printf 'one\n' > "$TMP/a.txt"
printf 'two\n' > "$TMP/b.txt"
printf 'three\n' > "$TMP/c.txt"

export EVIDENCE_PUSH_SLUG=amindell11/astronomical-home
export EVIDENCE_PUSH_CURL="$TMP/bin/curl"
export EVIDENCE_PUSH_PLANT_HOOK=1
export CURL_STATUS_FILE="$TMP/status"
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com
export GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com
printf '200' > "$CURL_STATUS_FILE"

out="$(cd "$TMP/repo" && bash "$PUSH" capture-rig "$TMP/a.txt" "$TMP/b.txt" "$TMP/c.txt")" || fail "push should exit 0: $out"
urls="$(printf '%s\n' "$out" | grep '^URL=' || true)"
count="$(printf '%s\n' "$urls" | grep -c '^URL=')"
[[ "$count" -eq 3 ]] || fail "expected 3 URL lines, got: $out"
printf '%s\n' "$out" | grep -q '^SHA=[0-9a-f]\{40\}$' || fail "missing SHA line: $out"
printf '%s\n' "$urls" | grep -q 'raw/.*/a.txt' || fail "a txt file must use the raw URL: $out"

tree="$(git -C "$TMP/bare" ls-tree -r --name-only evidence/capture-rig)"
printf '%s\n' "$tree" | grep -qx 'a.txt' || fail "a.txt was not pushed: $tree"
printf '%s\n' "$tree" | grep -qx 'b.txt' || fail "b.txt was not pushed"
printf '%s\n' "$tree" | grep -qx 'c.txt' || fail "c.txt was not pushed"
printf '%s\n' "$tree" | grep -q 'githooks' && fail "a hook stub was committed: $tree"

printf '404' > "$CURL_STATUS_FILE"
printf 'one-again\n' > "$TMP/a.txt"
rc=0
(cd "$TMP/repo" && bash "$PUSH" capture-rig "$TMP/a.txt") >/dev/null 2>"$TMP/err" || rc=$?
[[ "$rc" -eq 1 ]] || fail "a non-200 HEAD must exit 1, got $rc ($(cat "$TMP/err"))"
grep -q 'returned 404' "$TMP/err" || fail "expected a 404 report, got $(cat "$TMP/err")"

echo "PASS: evidence_push.sh — three URLs, hook stub stripped, non-200 fails"
