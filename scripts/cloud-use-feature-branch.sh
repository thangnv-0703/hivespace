#!/usr/bin/env bash
# Cloud sessions start on a scratch claude/* branch cut from the branch the user picked. Put the session on that
# feat/NNN-<slug> branch instead, so commits and pushes land there and no claude/* branch reaches the remote.
# Run by the SessionStart hook in .claude/settings.json. Does nothing on local machines.
set -euo pipefail

[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"

say() { echo "[cloud-branch] $*"; }

current="$(git branch --show-current)"
case "$current" in
  claude/*) ;;
  *) say "on '$current', not a claude/* branch; nothing to do"; exit 0 ;;
esac

git fetch origin --quiet || { say "WARNING: git fetch failed; staying on $current"; exit 0; }

head_sha="$(git rev-parse HEAD)"
matches="$(git for-each-ref --format='%(objectname) %(refname:short)' 'refs/remotes/origin/feat/*' \
  | awk -v sha="$head_sha" '$1 == sha { sub("^origin/", "", $2); print $2 }')"
count="$(printf '%s' "$matches" | grep -c . || true)"

if [ "$count" -ne 1 ]; then
  say "WARNING: $count origin/feat/* branches match HEAD; staying on $current. Tell the agent which feature branch to use."
  exit 0
fi

git checkout -B "$matches" "origin/$matches" --quiet
git branch -D "$current" --quiet
say "working on $matches (tracks origin/$matches); local $current removed"
