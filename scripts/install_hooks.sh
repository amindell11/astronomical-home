#!/usr/bin/env bash
#
# Point this repo's git hooks at the committed .githooks/ directory.
# Idempotent; safe to re-run. Applies to all linked worktrees (shared config).

set -euo pipefail

ROOT="$(git rev-parse --show-toplevel)"
git -C "$ROOT" config core.hooksPath .githooks
chmod +x "$ROOT/.githooks/"* 2>/dev/null || true

echo "Installed git hooks: core.hooksPath -> .githooks"
echo "Active hooks:"
ls -1 "$ROOT/.githooks/" 2>/dev/null | sed 's/^/  /'
