#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
TMP="$(mktemp -d -t pool-serialization.XXXXXX)"
TMP="$(realpath "$TMP")"
case "$TMP" in */pool-serialization.*) ;; *) exit 90 ;; esac
export SYNC="$TMP/sync"
mkdir -p "$TMP/bin" "$SYNC"
launcher=""
trap ': > "$SYNC/resume"; if [[ -n "$launcher" ]]; then wait "$launcher" 2>/dev/null || true; fi; rm -rf -- "$TMP"' EXIT
fail() { echo "FAIL: $*" >&2; exit 1; }
export REAL_GIT="$(command -v git)" REAL_MKDIR="$(command -v mkdir)"
export REAL_RM="$(command -v rm)" REAL_PERL="$(command -v perl)"
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks" WORKTREE_POOL_LOCK_TTL=60
cat > "$TMP/bin/git" <<'EOF'
#!/usr/bin/env bash
"$REAL_GIT" "$@" || exit $?
if [[ "${RACE_ROLE:-}" == slow && "${RACE_STAGE:-}" == eligibility && " $* " == *' status --porcelain '* ]]; then
  : > "$SYNC/ready"
  until [[ -e "$SYNC/resume" ]]; do sleep .02; done
fi
EOF
cat > "$TMP/bin/mkdir" <<'EOF'
#!/usr/bin/env bash
"$REAL_MKDIR" "$@" || exit $?
if [[ "${RACE_ROLE:-}" == slow && "${RACE_STAGE:-}" == publication && "$*" == "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock" ]]; then
  : > "$SYNC/ready"
  until [[ -e "$SYNC/resume" ]]; do sleep .02; done
fi
EOF
cat > "$TMP/bin/rm" <<'EOF'
#!/usr/bin/env bash
"$REAL_RM" "$@" || exit $?
if [[ "${RACE_ROLE:-}" == slow && "${RACE_STAGE:-}" == replacement && " $* " == *" $WORKTREE_POOL_LOCK_ROOT/agent-1.lock "* ]]; then
  : > "$SYNC/ready"
  until [[ -e "$SYNC/resume" ]]; do sleep .02; done
fi
EOF
cat > "$TMP/bin/perl" <<'EOF'
#!/usr/bin/env bash
if [[ "${RACE_ROLE:-}" == slow ]]; then printf '%s\n' "$BASHPID" > "$SYNC/holder"; fi
exec "$REAL_PERL" "$@"
EOF
chmod +x "$TMP/bin/"*
export PATH="$TMP/bin:$PATH"
pool() { bash "$POOL" "$@"; }
git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary"
cd "$TMP/primary"
git config user.email test@example.test
git config user.name Test
echo base > file.txt
git add file.txt && git commit -qm base && git push -q origin main
git worktree add -q -b agent-1 "$TMP/agent-1" main
git worktree add -q -b agent-2 "$TMP/agent-2" main
age_lock() {
  date -u -d "@$(( $(date +%s) - 100 ))" +"%Y-%m-%dT%H:%M:%SZ" > "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/timestamp"
}
start_slow() {
  rm -f "$SYNC/ready" "$SYNC/resume" "$SYNC/holder"
  RACE_ROLE=slow RACE_STAGE="$1" pool acquire slow agent-1 > "$TMP/slow.out" 2>&1 &
  launcher=$!
  local deadline=$((SECONDS + 15))
  until [[ -e "$SYNC/ready" ]]; do
    (( SECONDS < deadline )) || { cat "$TMP/slow.out"; fail "no $1 synchronization event"; }
    sleep .02
  done
}
finish_slow() {
  : > "$SYNC/resume"
  wait "$launcher" || { cat "$TMP/slow.out"; fail 'slow acquisition failed'; }
  launcher=""
}
assert_lease() {
  [[ "$(cat "$WORKTREE_POOL_LOCK_ROOT/agent-1.lock/lease")" == "$1" ]] || fail 'wrong lease record'
  [[ "$(git -C "$TMP/agent-1" config --worktree --get worktree-pool.lease)" == "$1" ]] || fail 'wrong durable lease'
}
pool acquire original agent-1 >/dev/null
mkdir -p "$TMP/no-perl"
for utility in bash git dirname mkdir awk sort cat; do
  printf '#!/usr/bin/bash\nexec %q "$@"\n' "$(command -v "$utility")" > "$TMP/no-perl/$utility"
  chmod +x "$TMP/no-perl/$utility"
done
if PATH="$TMP/no-perl" pool acquire missing-dependency agent-1 > "$TMP/missing-perl.out" 2>&1; then fail 'missing Perl acquired slot'; fi
grep -q 'requires Perl flock support' "$TMP/missing-perl.out" || { cat "$TMP/missing-perl.out"; fail 'missing dependency was not explained'; }
assert_lease original
pool release agent-1 >/dev/null
echo 'PASS: missing Perl refuses without changing the lease'
pool acquire original agent-1 >/dev/null
age_lock
start_slow eligibility
fast_rc=0
pool acquire fast agent-1 > "$TMP/fast.out" 2>&1 || fast_rc=$?
assert_lease original
finish_slow
winners="$(cat "$TMP/slow.out" "$TMP/fast.out" | grep -c '^SLOT=' || true)"
[[ "$winners" == 1 && "$fast_rc" != 0 ]] || { cat "$TMP/fast.out" "$TMP/slow.out"; fail "stale eligibility allowed $winners winners"; }
assert_lease slow
echo 'PASS: delayed stale eligibility admits exactly one owner'
pool release agent-1 >/dev/null

for stage in publication replacement; do
  if [[ "$stage" == replacement ]]; then pool acquire original agent-1 >/dev/null; age_lock; fi
  start_slow "$stage"
  if pool acquire fast agent-1 >/dev/null 2>&1; then fail "$stage admitted another owner"; fi
  if pool release agent-1 >/dev/null 2>&1; then fail "$stage allowed a competing release"; fi
  pool acquire independent agent-2 >/dev/null
  pool release agent-2 >/dev/null
  finish_slow
  assert_lease slow
  pool release agent-1 >/dev/null
  echo "PASS: $stage excludes mutations only on the same slot"
done

pool acquire original agent-1 >/dev/null
age_lock
start_slow eligibility
holder="$(cat "$SYNC/holder")"
kill -KILL "$launcher"
wait "$launcher" 2>/dev/null || true
launcher=""
if pool acquire fast agent-1 >/dev/null 2>&1; then fail 'launcher death exposed surviving mutation'; fi
: > "$SYNC/resume"
deadline=$((SECONDS + 15))
until grep -q '^SLOT=' "$TMP/slow.out"; do
  (( SECONDS < deadline )) || { cat "$TMP/slow.out"; fail 'surviving mutation never finished'; }
  sleep .02
done
assert_lease slow
until ! kill -0 "$holder" 2>/dev/null; do
  (( SECONDS < deadline )) || fail 'surviving mutation never exited'
  sleep .02
done
pool release agent-1 >/dev/null
echo 'PASS: launcher death preserves surviving mutation ownership'

pool acquire original agent-1 >/dev/null
age_lock
start_slow eligibility
holder="$(cat "$SYNC/holder")"
kill -KILL "$holder"
: > "$SYNC/resume"
wait "$launcher" 2>/dev/null || true
launcher=""
deadline=$((SECONDS + 15))
until pool acquire recovered agent-1 > "$TMP/recovered.out" 2>&1; do
  (( SECONDS < deadline )) || { cat "$TMP/recovered.out"; fail 'dead mutation retained its OS lock'; }
  sleep .02
done
assert_lease recovered
pool release agent-1 >/dev/null
[[ -f "$WORKTREE_POOL_LOCK_ROOT/agent-1.mutation" ]] || fail 'release deleted the advisory file'
echo 'PASS: mutation death releases its OS lock without deleting the advisory file'
