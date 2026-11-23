#!/usr/bin/env bash
# PreToolUse hook (Bash): block `git push` that would publish a claude/* branch, in cloud sessions only.
# Reads the tool call JSON on stdin. Exit 2 blocks the call and shows stderr to the agent.
set -uo pipefail

[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0

cmd="$(cat | sed -n 's/.*"command"[[:space:]]*:[[:space:]]*"\(\([^"\\]\|\\.\)*\)".*/\1/p' | head -n1)"
case "$cmd" in *"git push"*) ;; *) exit 0 ;; esac

branch="$(git branch --show-current 2>/dev/null || true)"
if printf '%s' "$cmd" | grep -q 'claude/' || { case "$branch" in claude/*) true ;; *) false ;; esac; }; then
  echo "Blocked: do not push claude/* branches. Work and push on the feat/NNN-<slug> branch (see AGENTS.md, Cloud sessions)." >&2
  exit 2
fi
exit 0
