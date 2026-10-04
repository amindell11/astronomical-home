#!/usr/bin/env bash
set -euo pipefail

# Evidence publish: append one commit holding the given files to `evidence/<name>` on origin, so a
# PR body can pin URLs to that commit. Built with plumbing alone — a temp index outside the repo,
# blobs written to the object store, write-tree, commit-tree on the remote tip, a non-force push —
# so it creates no worktree and no local branch and never touches HEAD, the index or the tree.
#
# Usage: evidence_publish.sh <name> [--into <subdir>] <path>...
#   <name>            branch suffix; the commit goes to refs/heads/evidence/<name> on origin. The
#                     first publish creates it as an orphan carrying the .gitattributes below.
#   --into <subdir>   place the files under <subdir>/ on the branch (default: the branch root).
#   <path>            a file lands under its basename; a directory is added recursively under its
#                     own name, keeping its layout. Two paths landing on one name is a usage error.
# Runs against the repo at the working directory and its `origin` remote. Files matching the
# branch's LFS attributes (*.png *.gif *.mp4 *.jpg) are stored as LFS pointers and their objects
# are uploaded with `git lfs push --object-id` before the branch push.
# Exit: 0 published · 1 git/LFS failure, including a rejected push when the remote tip moved
#       (no SHA is printed; re-run to append on the new tip) · 2 usage.
# Stdout trailer: SHA=<commit> — pin URLs to it. Prose to stderr.

LFS_PATTERNS=('*.png' '*.gif' '*.mp4' '*.jpg')

usage() { echo "Usage: evidence_publish.sh <name> [--into <subdir>] <path>..." >&2; exit 2; }
fail() { echo "evidence_publish: $1" >&2; exit 1; }

[[ $# -ge 1 ]] || usage
NAME="$1"; shift
INTO=""
if [[ "${1:-}" == --into ]]; then
  [[ $# -ge 2 ]] || usage
  INTO="${2%/}"; shift 2
  [[ -n "$INTO" && "$INTO" != /* && "/$INTO/" != */../* ]] || usage
fi
[[ $# -ge 1 ]] || usage
BRANCH="evidence/$NAME"
REF="refs/heads/$BRANCH"
git check-ref-format "$REF" || usage

# Destination path on the branch -> source file on disk.
declare -A SRC=()
add_dest() {
  local dest="$1" file="$2"
  [[ -n "$INTO" ]] && dest="$INTO/$dest"
  [[ -z "${SRC[$dest]+x}" ]] || { echo "evidence_publish: two paths land on $dest" >&2; usage; }
  SRC[$dest]="$file"
}
for p in "$@"; do
  p="${p%/}"
  if [[ -f "$p" ]]; then
    add_dest "$(basename "$p")" "$p"
  elif [[ -d "$p" ]]; then
    parent="$(dirname "$p")"
    while IFS= read -r -d '' f; do
      add_dest "${f#"$parent"/}" "$f"
    done < <(find "$p" -type f -print0)
  else
    echo "evidence_publish: no such file or directory: $p" >&2; usage
  fi
done
[[ ${#SRC[@]} -gt 0 ]] || usage

git rev-parse --git-dir >/dev/null 2>&1 || fail "not inside a git repository"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
export GIT_INDEX_FILE="$TMP/index"

# ls-remote before fetch: the fetched tip is then the listed tip or a descendant of it, so the
# listed tip's objects are always present; a tip that moved in between rejects the push below.
TIP="$(git ls-remote origin "$REF" | cut -f1)" || fail "cannot reach origin"
if [[ -n "$TIP" ]]; then
  git fetch -q --no-write-fetch-head origin "$REF" || fail "fetch of $BRANCH failed"
  git read-tree "$TIP" || fail "read-tree of $TIP failed"
fi
if ! git cat-file -e ":.gitattributes" 2>/dev/null; then
  attrs="$(printf '%s filter=lfs diff=lfs merge=lfs -text\n' "${LFS_PATTERNS[@]}" | git hash-object -w --stdin)"
  git update-index --add --cacheinfo "100644,$attrs,.gitattributes"
fi

LFS_OIDS=()
for dest in "${!SRC[@]}"; do
  file="${SRC[$dest]}"
  if [[ "$(git check-attr --cached filter -- "$dest" | sed 's/.*: filter: //')" == lfs ]]; then
    pointer="$(git lfs clean -- "$dest" < "$file")" || fail "git lfs clean failed for $file"
    LFS_OIDS+=("$(sed -n 's/^oid sha256://p' <<<"$pointer")")
    blob="$(printf '%s\n' "$pointer" | git hash-object -w --stdin)"
  else
    blob="$(git hash-object -w --no-filters -- "$file")"
  fi
  git update-index --add --cacheinfo "100644,$blob,$dest"
done

TREE="$(git write-tree)"
PARENT=()
[[ -z "$TIP" ]] || PARENT=(-p "$TIP")
COMMIT="$(git commit-tree "$TREE" "${PARENT[@]}" -m "evidence: $NAME")" || fail "commit-tree failed"

if [[ ${#LFS_OIDS[@]} -gt 0 ]]; then
  git lfs push --object-id origin "${LFS_OIDS[@]}" >&2 || fail "LFS upload failed; nothing pushed"
fi
git push -q origin "$COMMIT:$REF" >&2 || fail "push to $BRANCH rejected (remote tip moved?); no SHA published"
echo "SHA=$COMMIT"
