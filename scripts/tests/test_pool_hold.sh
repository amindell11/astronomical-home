#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/agent_worktree_pool.sh

# hold / resume: the round trip keeps the lease, unpushed commits and the dirty tree; --local never
# pushes; prepare accepts an unpushed tip only when a held/* branch reaches it.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POOL="$SCRIPT_DIR/../agent_worktree_pool.sh"
pool() { bash "$POOL" "$@"; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
export WORKTREE_POOL_LOCK_ROOT="$TMP/locks"
fail() { echo "FAIL: $1" >&2; exit 1; }

git init -q --bare -b main "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/primary" 2>/dev/null
cd "$TMP/primary"
git config user.email pool-test@example.test
git config user.name "Pool Test"
printf 'results/\n' > .gitignore
echo base > a.txt
echo keep > b.txt
git add . && git commit -qm init && git push -q origin main
git worktree add -q -b agent-1 "$TMP/agent-1" main
git worktree add -q -b agent-2 "$TMP/agent-2" main
A1="$TMP/agent-1"
A2="$TMP/agent-2"

lock_dir() { printf '%s/%s.lock' "$WORKTREE_POOL_LOCK_ROOT" "$1"; }
age_lock() { date -u -d "@$(( $(date -u +%s) - $2 ))" +"%Y-%m-%dT%H:%M:%SZ" > "$(lock_dir "$1")/timestamp"; }
pick_held() { awk -v RS= -v want="held=$1" '{ split($0, l, "\n"); if (l[1] == want) print }'; }
# Direct call to the collector; the one real `status --porcelain` read below guards the machine channel.
held_record() { (source "$POOL"; collect_held_records) | pick_held "$1"; }
# Any push fails while the push URL points nowhere, so a verb that must not push cannot pass by accident.
block_push() { git remote set-url --push origin "$TMP/no-such-origin.git"; }
allow_push() { git remote set-url --push origin "$TMP/origin.git"; }
main_sha() { git rev-parse origin/main; }

# --- prepare: an unheld unpushed tip refuses; one a held/* branch reaches is safe -------------
pool acquire guard agent-1 >/dev/null
echo wip > "$A1/wip.txt"
git -C "$A1" add wip.txt && git -C "$A1" commit -qm wip
wip="$(git -C "$A1" rev-parse HEAD)"
if pool prepare agent-1 > "$TMP/prepare.log" 2>&1; then fail "prepare must refuse an unheld unpushed tip"; fi
grep -qF 'hold agent-1 --local' "$TMP/prepare.log" || fail "the prepare refusal must name 'hold <slot> --local'"
git branch held/guard "$wip"
pool prepare agent-1 >/dev/null 2>&1 || fail "prepare must accept a tip a held/* branch reaches"
[[ "$(git -C "$A1" rev-parse HEAD)" == "$(main_sha)" ]] || fail "prepare must reset the slot"
[[ "$(git rev-parse held/guard)" == "$wip" ]] || fail "the held branch must survive prepare"
git branch -q -D held/guard
pool release agent-1 >/dev/null
echo "PASS: prepare refuses an unheld unpushed tip and accepts a held one"

# --- round trip: held from agent-2, so preferring the left slot differs from auto-pick ---------
pool acquire trip agent-2 >/dev/null
echo committed > "$A2/c.txt"
git -C "$A2" add c.txt && git -C "$A2" commit -qm unpushed
head="$(git -C "$A2" rev-parse HEAD)"
echo edit >> "$A2/a.txt"
git -C "$A2" rm -q b.txt
echo new > "$A2/new.txt"
mkdir -p "$A2/results" && echo ignored > "$A2/results/x"
out="$(pool hold agent-2 2>"$TMP/hold.log")" || { cat "$TMP/hold.log"; fail "hold must succeed on a locked slot"; }
[[ "$out" == 'HELD=trip RESUME="agent_worktree_pool.sh resume trip"' ]] || fail "hold trailer: $out"
[[ ! -d "$(lock_dir agent-2)" ]] || fail "hold must release the slot"
[[ -z "$(git -C "$A2" status --porcelain)" ]] || fail "hold must leave the slot clean"
[[ "$(git -C "$A2" rev-parse HEAD)" == "$(main_sha)" ]] || fail "hold must prepare the slot to origin/main"
[[ "$(git rev-parse held/trip^1)" == "$head" ]] || fail "the snapshot's only parent must be the held HEAD"
[[ "$(git ls-remote origin refs/heads/held/trip | cut -f1)" == "$(git rev-parse held/trip)" ]] || fail "hold must push held/trip"
[[ -z "$(git ls-remote origin refs/heads/task/trip)" ]] || fail "hold must never touch task/<lease>"
rec="$(pool status --porcelain | pick_held trip)"
{ grep -qx 'branch=held/trip' <<< "$rec" && grep -qx 'left_slot=agent-2' <<< "$rec" && grep -qx 'pushed=1' <<< "$rec"; } \
  || fail "porcelain held record: $rec"
pool status | grep -q '^held/trip | HELD' || fail "plain status must list held work"

out="$(pool resume trip 2>"$TMP/resume.log")" || { cat "$TMP/resume.log"; fail "resume must succeed"; }
[[ "$out" == "SLOT=agent-2 PATH="*" RESUMED=trip" ]] || fail "resume must prefer the slot the work left: $out"
[[ "$(git -C "$A2" config --worktree --get worktree-pool.lease)" == trip ]] || fail "resume must restore the lease"
[[ "$(git -C "$A2" rev-parse HEAD)" == "$head" ]] || fail "resume must restore the unpushed commit as HEAD"
dirty="$(git -C "$A2" status --porcelain | LC_ALL=C sort | tr '\n' '|')"
[[ "$dirty" == ' D b.txt| M a.txt|?? new.txt|' ]] || fail "resume must restore the dirty tree as uncommitted changes: $dirty"
[[ -f "$A2/results/x" ]] || fail "ignored files stay with the slot"
if git rev-parse -q --verify refs/heads/held/trip >/dev/null; then fail "resume must delete the local held branch"; fi
[[ -z "$(git ls-remote origin refs/heads/held/trip)" ]] || fail "resume must delete origin's held branch"
[[ -z "$(held_record trip)" ]] || fail "a resumed lease must leave the porcelain"
pool prepare agent-2 origin/main --force >/dev/null 2>&1
pool release agent-2 >/dev/null
echo "PASS: hold/resume round trip keeps lease, commits and dirty tree"

# --- --local never pushes, and resume works from the local branch ------------------------------
pool acquire quiet agent-1 >/dev/null
echo secret > "$A1/s.txt"
git -C "$A1" add s.txt && git -C "$A1" commit -qm secret
head="$(git -C "$A1" rev-parse HEAD)"
echo dirty >> "$A1/a.txt"

block_push
if pool hold agent-1 >/dev/null 2>"$TMP/hold.log"; then fail "hold must fail when its push fails"; fi
grep -qF 'hold agent-1 --local' "$TMP/hold.log" || fail "a failed push must name --local"
if git rev-parse -q --verify refs/heads/held/quiet >/dev/null; then fail "a failed push must remove the local held branch"; fi
[[ -d "$(lock_dir agent-1)" ]] || fail "a failed push must leave the slot locked"
[[ "$(git -C "$A1" status --porcelain)" == ' M a.txt' ]] || fail "a failed push must leave the work on the slot"

out="$(pool hold agent-1 --local 2>"$TMP/hold.log")" || { cat "$TMP/hold.log"; fail "hold --local must not need a push"; }
[[ "$out" == 'HELD=quiet RESUME="agent_worktree_pool.sh resume quiet"' ]] || fail "hold --local trailer: $out"
[[ -z "$(git ls-remote origin refs/heads/held/quiet)" ]] || fail "--local must never push"
grep -qx 'pushed=0' <<< "$(held_record quiet)" || fail "porcelain must show a local hold as pushed=0"
out="$(pool resume quiet agent-1 2>"$TMP/resume.log")" || { cat "$TMP/resume.log"; fail "resuming a local hold must not need a push"; }
[[ "$out" == "SLOT=agent-1 PATH="*" RESUMED=quiet" ]] || fail "resume into a named slot: $out"
[[ "$(git -C "$A1" rev-parse HEAD)" == "$head" ]] || fail "resume must restore the local commit"
[[ "$(git -C "$A1" status --porcelain)" == ' M a.txt' ]] || fail "resume must restore the local dirty edit"
if git rev-parse -q --verify refs/heads/held/quiet >/dev/null; then fail "resume must delete the local held branch"; fi
allow_push
echo "PASS: hold --local never pushes; a failed push rolls back; resume works from the local branch"

# --- hold refusals: existing held branch, open merge phase, free slot --------------------------
git branch held/quiet main
if pool hold agent-1 >/dev/null 2>&1; then fail "hold must refuse an existing held/<lease>"; fi
git branch -q -D held/quiet
git push -q origin main:refs/heads/held/quiet
if pool hold agent-1 --local >/dev/null 2>&1; then fail "hold --local must refuse a held/<lease> known on origin"; fi
git push -q origin --delete held/quiet
# A running merge gate holds the slot's .merge flock for its whole run.
perl -e 'use Fcntl qw(LOCK_EX); open my $l, ">>", $ARGV[0] or die "$!"; flock($l, LOCK_EX) or die "$!";
  open my $r, ">", $ARGV[1] or die "$!"; close $r; select(undef, undef, undef, 0.1) until -e $ARGV[2];' \
  "$WORKTREE_POOL_LOCK_ROOT/agent-1.merge" "$TMP/gate.ready" "$TMP/gate.go" &
gate=$!
for _ in $(seq 1 100); do [[ -f "$TMP/gate.ready" ]] && break; sleep 0.1; done
[[ -f "$TMP/gate.ready" ]] || fail "fixture: the stand-in gate never took the .merge flock"
held_anyway=0
pool hold agent-1 >/dev/null 2>"$TMP/hold.log" && held_anyway=1
touch "$TMP/gate.go"
wait "$gate"
[[ "$held_anyway" == 0 ]] || fail "hold must refuse while a merge gate runs"
grep -qF 'merge-progress agent-1' "$TMP/hold.log" || fail "the merge refusal must name merge-progress"
[[ "$(git -C "$A1" status --porcelain)" == ' M a.txt' ]] || fail "a refused hold must leave the work on the slot"
pool hold agent-1 --local >/dev/null 2>&1 || fail "hold must proceed once the gate has finished"
if pool hold agent-1 >/dev/null 2>&1; then fail "hold must refuse a free slot"; fi
echo "PASS: hold refuses an existing held branch, a running merge gate and a free slot"

# --- resume: refuses a slot holding work; never reclaims the left slot while one is free;
#     falls back to origin when the local branch is gone ---------------------------------------
echo stray > "$A1/stray.txt"
if pool resume quiet agent-1 >/dev/null 2>"$TMP/resume.log"; then fail "resume must refuse a slot holding uncommitted work"; fi
[[ -f "$A1/stray.txt" && ! -d "$(lock_dir agent-1)" ]] || fail "a refused resume must release the slot untouched"
git rev-parse -q --verify refs/heads/held/quiet >/dev/null || fail "a refused resume must keep the held branch"
rm "$A1/stray.txt"
pool acquire other agent-1 >/dev/null
age_lock agent-1 100
git push -q origin held/quiet
git branch -q -D held/quiet
git update-ref -d refs/remotes/origin/held/quiet
out="$(WORKTREE_POOL_LOCK_TTL=60 pool resume quiet 2>"$TMP/resume.log")" || { cat "$TMP/resume.log"; fail "resume must fall back to origin/held/<lease>"; }
[[ "$out" == "SLOT=agent-2 PATH="*" RESUMED=quiet" ]] || fail "resume must take a free slot before reclaiming the left one: $out"
[[ "$(cat "$(lock_dir agent-1)/lease")" == other ]] || fail "resume must leave the stale holder of the left slot alone"
[[ "$(git -C "$A2" status --porcelain)" == ' M a.txt' ]] || fail "origin resume must restore the dirty edit"
[[ -z "$(git ls-remote origin refs/heads/held/quiet)" ]] || fail "origin resume must delete origin's held branch"
echo "PASS: resume refuses an unsafe slot, prefers a free slot to a stale reclaim, and falls back to origin"
