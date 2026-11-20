# Project skills

Skills here load in every Claude Code session on this repo, local or cloud, and run as `/<name>` commands.

## Own skills
- `push-feature`: commit and push a feature's changes, then offer a PR.
- `sync-notion`: push `docs/` to Notion.
- `html-flow-diagram`: interactive HTML flow diagrams (used by `detailed-design`).

## From agentic-sdlc-kit
These 12 skills are copied from [agentic-sdlc-kit](https://github.com/zeeshanhanif/agentic-sdlc-kit) 2.0.7 (upstream commit `20f1c37`), Apache-2.0 (see `agentic-sdlc-kit.LICENSE` and `agentic-sdlc-kit.NOTICE`):
acceptance-verification, detailed-design, feature-implementation, implementation-planning, initial-deployment, pipeline-verify, project-scaffolding, requirements-engineering, sdlc-orchestrator, software-architecture, ui-design, ux-foundations.

They are copied so cloud sessions get the same skills as this project's local setup, for example `/feature-implementation FEAT-002`.

**Local changes** (not upstream): `detailed-design`, `ui-design` and `software-architecture` carry project changes (HTML flow diagrams, the design-canvas review artifact, and related references). Keep those when refreshing.

**Refreshing from upstream:** diff the upstream `skills/` folder against these copies, take the upstream changes, keep the local changes listed above, and update the version and commit here.

If the agentic-sdlc-kit plugin is also enabled on your machine, the same skills appear twice (`agentic-sdlc-kit:<name>` and `<name>`). Disable the plugin to keep one version.
