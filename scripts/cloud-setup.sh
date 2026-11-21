#!/usr/bin/env bash
# Installs the toolchains this repo needs when Claude Code runs as a cloud session.
# Run by the SessionStart hook in .claude/settings.json. Does nothing on local machines.
set -euo pipefail

[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$ROOT"

say() { echo "[cloud-setup] $*"; }
persist() { [ -n "${CLAUDE_ENV_FILE:-}" ] && echo "$1" >> "$CLAUDE_ENV_FILE"; return 0; }

# .NET SDK pinned in global.json (10.0.301, rollForward latestFeature).
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.0\.'; then
  say "installing .NET SDK from global.json"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --jsonfile "$ROOT/global.json" --install-dir "$DOTNET_ROOT"
fi
persist "export DOTNET_ROOT=\"$DOTNET_ROOT\""
persist "export PATH=\"$DOTNET_ROOT:$DOTNET_ROOT/tools:\$PATH\""
persist "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
say "dotnet $(dotnet --version)"

# Node 24 + pnpm 9.15.9 (web/package.json packageManager).
if command -v node >/dev/null 2>&1; then
  say "node $(node --version)"
  case "$(node --version)" in v2[4-9]*|v[3-9][0-9]*) ;; *) say "WARNING: CI uses Node 24" ;; esac
  if ! pnpm --version 2>/dev/null | grep -q '^9\.15\.9$'; then
    corepack enable >/dev/null 2>&1 || npm install -g corepack >/dev/null
    corepack enable
    corepack prepare pnpm@9.15.9 --activate
  fi
  say "pnpm $(pnpm --version)"
else
  say "WARNING: node is not installed; web, tests/contract and tests/e2e cannot run"
fi

# uv for services/notification and Python tools.
if ! command -v uv >/dev/null 2>&1; then
  say "installing uv"
  curl -LsSf https://astral.sh/uv/install.sh | sh
fi
export PATH="$HOME/.local/bin:$PATH"
persist "export PATH=\"\$HOME/.local/bin:\$PATH\""
say "$(uv --version)"

# Go for services/inventory and services/cart (report only).
if command -v go >/dev/null 2>&1; then
  say "$(go version)"
else
  say "WARNING: go is not installed; Go checks cannot run (need $(grep -m1 '^go ' services/inventory/go.mod))"
fi

# Docker decides which checks can run (AGENTS.md, Cloud sessions).
if docker info >/dev/null 2>&1; then
  say "docker: available"
else
  say "docker: NOT available - Testcontainers, tests/contract, tests/system, AppHost and tests/e2e cannot run here"
fi
