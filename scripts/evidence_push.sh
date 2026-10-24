#!/usr/bin/env bash
# Append files to the orphan evidence/<lease> branch and print SHA-pinned URLs.
#
# Usage: evidence_push.sh <lease> <file> [<file>...]
#
# stdout, after every URL's HEAD (or GET, if HEAD is not 200) returns 200:
#   URL=<https url>
#   SHA=<40-char commit>
# stderr carries progress. Exit 0 when every check is 200. Exit 2 on usage.
# Exit 1 when the push or a URL check fails.
#
# png, gif, mp4 and jpg are the LFS suffixes this repo's evidence branch tracks.
# Those URLs are https://media.githubusercontent.com/media/<owner>/<repo>/<sha>/<name>
# Anything else is https://github.com/<owner>/<repo>/raw/<sha>/<name>
#
# The slug is parsed from origin. EVIDENCE_PUSH_SLUG overrides it.
# EVIDENCE_PUSH_CURL overrides the client. It is invoked as
#   "$EVIDENCE_PUSH_CURL" <curl args>
# and must print the status code when -w "%{http_code}" is passed.
# A URL check retries 5 times, 2 seconds apart.
#
# git-lfs hook stubs written inside the evidence worktree (.githooks or dev/)
# are removed before the commit and again after the push. This repo's own
# .githooks directory is not touched. EVIDENCE_PUSH_PLANT_HOOK=1 writes a stub
# into the worktree just before that removal, so a test can see it was not committed.
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "usage: evidence_push.sh <lease> <file> [<file>...]" >&2
  exit 2
fi

lease="$1"
shift
if [[ ! "$lease" =~ ^[A-Za-z0-9_-]+$ ]]; then
  echo "lease must match [A-Za-z0-9_-], got '$lease'" >&2
  exit 2
fi

files=("$@")
names=()
for file in "${files[@]}"; do
  [[ -f "$file" ]] || { echo "not a file: $file" >&2; exit 2; }
  name="$(basename "$file")"
  for seen in "${names[@]+"${names[@]}"}"; do
    [[ "$seen" != "$name" ]] || { echo "two files share the name $name" >&2; exit 2; }
  done
  names+=("$name")
done

root="$(git rev-parse --show-toplevel)"
origin="$(git -C "$root" remote get-url origin)"
slug="${EVIDENCE_PUSH_SLUG:-}"
if [[ -z "$slug" ]]; then
  slug="$(printf '%s' "$origin" | sed -E 's#^git@github.com:##; s#^https://github.com/##; s#\.git$##')"
fi
if [[ ! "$slug" =~ ^[^/]+/[^/]+$ ]]; then
  echo "origin is not a github slug ($origin). Set EVIDENCE_PUSH_SLUG." >&2
  exit 1
fi

curl_bin="${EVIDENCE_PUSH_CURL:-curl}"
ev="$(mktemp -d)"
branch="evidence/$lease"

cleanup() {
  git -C "$root" worktree remove -f "$ev" >/dev/null 2>&1 || rm -rf "$ev"
  git -C "$root" branch -D "$branch" >/dev/null 2>&1 || true
}
trap cleanup EXIT

strip_hooks() {
  rm -rf "$ev/.githooks" "$ev/dev"
}

if [[ "${EVIDENCE_PUSH_PLANT_HOOK:-}" == 1 ]]; then
  plant_hooks() { mkdir -p "$ev/.githooks"; printf '%s\n' '#!/bin/sh' 'git lfs pre-push "$@"' > "$ev/.githooks/pre-push"; }
else
  plant_hooks() { :; }
fi

if git -C "$root" fetch -q origin "$branch" 2>/dev/null; then
  git -C "$root" worktree add -q -B "$branch" "$ev" FETCH_HEAD
else
  git -C "$root" worktree add -q --detach "$ev"
  git -C "$ev" checkout -q --orphan "$branch"
  git -C "$ev" rm -rfq . >/dev/null 2>&1 || true
  printf '%s filter=lfs diff=lfs merge=lfs -text\n' '*.png' '*.gif' '*.mp4' '*.jpg' > "$ev/.gitattributes"
fi

plant_hooks
strip_hooks
for index in "${!files[@]}"; do
  cp "${files[$index]}" "$ev/${names[$index]}"
done
plant_hooks
strip_hooks

if [[ -f "$ev/.gitattributes" ]]; then
  git -C "$ev" add -- .gitattributes
fi
for name in "${names[@]}"; do
  git -C "$ev" add -- "$name"
done
if git -C "$ev" diff --cached --quiet; then
  echo "no staged changes; reusing HEAD" >&2
else
  git -C "$ev" commit -qm "evidence: $lease"
  strip_hooks
  if git -C "$ev" lfs ls-files | grep -q .; then
    git -C "$ev" lfs push origin "$branch"
  fi
  git -C "$ev" push -q -u origin "$branch"
  strip_hooks
fi

sha="$(git -C "$ev" rev-parse HEAD)"
[[ ${#sha} -eq 40 ]] || { echo "commit sha is '$sha'" >&2; exit 1; }

status_code() {
  "$curl_bin" -sS -o /dev/null -w "%{http_code}" "$@"
}

for name in "${names[@]}"; do
  case "$name" in
    *.png|*.gif|*.mp4|*.jpg)
      url="https://media.githubusercontent.com/media/$slug/$sha/$name" ;;
    *)
      url="https://github.com/$slug/raw/$sha/$name" ;;
  esac
  code=""
  for _try in 1 2 3 4 5; do
    code="$(status_code -I -L "$url" || true)"
    if [[ "$code" != "200" ]]; then
      code="$(status_code -L "$url" || true)"
    fi
    [[ "$code" == "200" ]] && break
    [[ "$_try" == 5 ]] || sleep 2
  done
  if [[ "$code" != "200" ]]; then
    echo "HEAD $url returned ${code:-none}" >&2
    exit 1
  fi
  printf 'URL=%s\n' "$url"
done
printf 'SHA=%s\n' "$sha"
