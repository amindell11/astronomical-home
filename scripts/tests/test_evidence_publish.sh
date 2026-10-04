#!/usr/bin/env bash
set -euo pipefail
# covers: scripts/evidence_publish.sh

# End-to-end regression for scripts/evidence_publish.sh against a local bare remote: orphan
# creation with .gitattributes, append keeping the first pin an ancestor, --into and directory
# layout, LFS pointer plus uploaded object, the caller's repo left untouched, and a rejected push
# (remote tip moved) exiting non-zero with no SHA. A git shim on PATH moves the tip mid-run.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH="$SCRIPT_DIR/../evidence_publish.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

export GIT_CONFIG_NOSYSTEM=1
export GIT_CONFIG_GLOBAL="$TMP/gitconfig"
git config --global user.name test
git config --global user.email test@example.invalid
git config --global init.defaultBranch main

REMOTE="$TMP/remote.git"
REPO="$TMP/repo"
git init -q --bare "$REMOTE"
git init -q "$REPO"
git -C "$REPO" remote add origin "$REMOTE"
# A no-op pre-push stops git-lfs installing its own, so the upload under test is the script's.
printf '#!/bin/sh\nexit 0\n' > "$REPO/.git/hooks/pre-push"
chmod +x "$REPO/.git/hooks/pre-push"
printf 'ignored.log\n' > "$REPO/.gitignore"
printf 'tracked\n' > "$REPO/tracked.txt"
git -C "$REPO" add .gitignore tracked.txt
git -C "$REPO" commit -qm init
git -C "$REPO" push -q origin main
printf 'staged change\n' >> "$REPO/tracked.txt"
git -C "$REPO" add tracked.txt
printf 'untracked\n' > "$REPO/untracked.txt"
printf 'ignored\n' > "$REPO/ignored.log"

EV="$TMP/evidence"
mkdir -p "$EV/refs/sub"
head -c 4096 /dev/urandom > "$EV/shot.png"
printf 'caption\n' > "$EV/notes.txt"
printf 'nested\n' > "$EV/refs/sub/a.txt"
head -c 512 /dev/urandom > "$EV/refs/b.jpg"

snapshot() {
  local r="$REPO"
  {
    git -C "$r" symbolic-ref HEAD
    git -C "$r" rev-parse HEAD
    sha256sum "$r/.git/index" | cut -d' ' -f1
    git -C "$r" status --porcelain --ignored
    (cd "$r" && sha256sum tracked.txt untracked.txt ignored.log .gitignore)
    git -C "$r" worktree list --porcelain | grep -c '^worktree '
    git -C "$r" for-each-ref refs/heads/
  }
}
BEFORE="$(snapshot)"

publish() { (cd "$REPO" && "$PUBLISH" "$@"); }
sha_of() { sed -n 's/^SHA=//p' <<<"$1"; }
on_branch() { git -C "$REMOTE" cat-file -p "evidence/demo:$1"; }

# 1. First publish creates the orphan branch with the files and .gitattributes.
OUT1="$(publish demo "$EV/shot.png" "$EV/notes.txt")" || fail "first publish failed"
SHA1="$(sha_of "$OUT1")"
[[ -n "$SHA1" ]] || fail "first publish printed no SHA"
[[ "$(git -C "$REMOTE" rev-parse evidence/demo)" == "$SHA1" ]] || fail "remote tip is not the reported SHA"
[[ -z "$(git -C "$REMOTE" log --format=%P -1 "$SHA1")" ]] || fail "first commit is not an orphan"
on_branch .gitattributes | grep -qx '\*.png filter=lfs diff=lfs merge=lfs -text' || fail ".gitattributes lacks the png line"
[[ "$(on_branch notes.txt)" == caption ]] || fail "notes.txt missing or wrong at the branch root"

# 4. An LFS-tracked file arrives as a pointer with its object on the remote.
POINTER="$(on_branch shot.png)"
head -1 <<<"$POINTER" | grep -qx 'version https://git-lfs.github.com/spec/v1' || fail "shot.png is not an LFS pointer"
OID="$(sed -n 's/^oid sha256://p' <<<"$POINTER")"
[[ "$OID" == "$(sha256sum "$EV/shot.png" | cut -d' ' -f1)" ]] || fail "pointer oid does not match the file"
[[ -f "$REMOTE/lfs/objects/${OID:0:2}/${OID:2:2}/$OID" ]] || fail "LFS object not uploaded to the remote"

# 2. A second publish appends; --into and a directory argument keep their layout.
OUT2="$(publish demo --into history "$EV/refs")" || fail "second publish failed"
SHA2="$(sha_of "$OUT2")"
[[ -n "$SHA2" && "$SHA2" != "$SHA1" ]] || fail "second publish printed no new SHA"
git -C "$REMOTE" merge-base --is-ancestor "$SHA1" "$SHA2" || fail "first pin is not an ancestor of the second"
[[ "$(on_branch history/refs/sub/a.txt)" == nested ]] || fail "--into directory layout lost"
JPG_OID="$(on_branch history/refs/b.jpg | sed -n 's/^oid sha256://p')"
[[ -f "$REMOTE/lfs/objects/${JPG_OID:0:2}/${JPG_OID:2:2}/$JPG_OID" ]] || fail "jpg under --into: no LFS pointer or object"
[[ "$(on_branch notes.txt)" == caption ]] || fail "append dropped an earlier file"

# 3. The caller's repo is untouched: HEAD, index, every file, no worktree, no local branch.
[[ "$(snapshot)" == "$BEFORE" ]] || fail "caller repo changed (HEAD, index, files, worktrees or branches)"

# 5. Remote tip moves between fetch and push: non-zero exit, no SHA.
OTHER="$TMP/other"
git clone -q "$REMOTE" "$OTHER"
REAL_GIT="$(command -v git)"
mkdir -p "$TMP/bin"
cat > "$TMP/bin/git" <<EOF
#!/usr/bin/env bash
if [[ "\${1:-}" == push && ! -f "$TMP/moved" ]]; then
  touch "$TMP/moved"
  "$REAL_GIT" -C "$OTHER" fetch -q origin evidence/demo
  "$REAL_GIT" -C "$OTHER" commit-tree -p FETCH_HEAD -m race "FETCH_HEAD^{tree}" > "$TMP/race"
  "$REAL_GIT" -C "$OTHER" push -q origin "\$(cat "$TMP/race"):refs/heads/evidence/demo"
fi
exec "$REAL_GIT" "\$@"
EOF
chmod +x "$TMP/bin/git"
set +e
OUT3="$(cd "$REPO" && PATH="$TMP/bin:$PATH" "$PUBLISH" demo "$EV/notes.txt" 2>/dev/null)"
RC=$?
set -e
[[ -f "$TMP/moved" ]] || fail "race shim never fired"
[[ $RC -ne 0 ]] || fail "rejected push exited 0"
[[ -z "$(sha_of "$OUT3")" ]] || fail "rejected push reported a SHA"
[[ "$(git -C "$REMOTE" rev-parse evidence/demo)" == "$(cat "$TMP/race")" ]] || fail "racing commit was overwritten"

# Usage errors exit 2.
set +e
publish 2>/dev/null; [[ $? -eq 2 ]] || fail "no args did not exit 2"
publish demo "$TMP/nope" 2>/dev/null; [[ $? -eq 2 ]] || fail "missing path did not exit 2"
publish demo "$EV/notes.txt" "$EV/notes.txt" 2>/dev/null; [[ $? -eq 2 ]] || fail "colliding paths did not exit 2"
set -e

echo "PASS: test_evidence_publish"
