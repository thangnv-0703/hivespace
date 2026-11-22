---
name: start-feature
description: Reset the local repo for a new feature — switch to main, pull the latest code from origin, delete every other local branch, and optionally create the new feature branch (feat/NNN-<slug>). Use when the user wants to clean up local branches, sync with remote, or start the next feature ("start FEAT-003", "clean my branches and pull main", "fresh start for a new feature") or runs /start-feature [FEAT-NNN].
---

# Start a new feature from a clean main

Argument (optional): `FEAT-NNN` or a feature name. Without it, the skill only cleans up and syncs `main`. Run every git command from the repo root.

**Never:** `git reset --hard`, `git clean`, `git stash drop`/`clear`, `git push` of any kind, deleting a remote branch, or deleting a branch that holds commits found nowhere else without the user's explicit yes for that branch.

## 1. Look before acting

Run `git fetch origin --prune`, `git branch --show-current`, `git status --short`, and
`git for-each-ref --format="%(refname:short)|%(upstream:short)|%(upstream:track)" refs/heads`.
Also run `git worktree list`: a branch checked out in another worktree cannot be deleted; skip it and report it.

## 2. Handle uncommitted work

If `git status --short` shows modified, staged or deleted tracked files, list them and ask:
- **Stash** (default): `git stash push -m "start-feature <date>"` — report the stash name; it is kept.
- **Carry over** onto main (only if `git switch main` accepts it).
- **Stop** — so the user can commit first (suggest `/push-feature`).

Untracked files (`??`) carry over on their own; just mention them. Never discard anything.

## 3. Sync main

`git switch main` (or `git switch -c main origin/main` if there is no local `main`), then `git pull --ff-only origin main`.
If the fast-forward fails, local `main` has commits not on origin: show `git log --oneline origin/main..main` and stop — ask the user what to do. Do not reset.

## 4. Classify the other local branches

For each local branch except `main`, put it in one group:

- **Merged** — `git merge-base --is-ancestor <b> origin/main` succeeds. Safe.
- **Remote gone** — upstream shows `[gone]` (PR merged with squash and the remote branch deleted; this repo squash-merges). Safe in practice; the work is in `main` via the squash commit.
- **Pushed** — upstream exists and `git rev-list --count <upstream>..<b>` is 0. Safe; the work stays on origin.
- **Unpushed work** — no upstream, or commits ahead of the upstream. Show `git log --oneline origin/main..<b>` (first 10) for each. **At risk.**

## 5. Confirm and delete

Show the table (branch, group, last commit subject). Ask once:
- delete all **safe** branches (merged / remote gone / pushed), and
- for each **unpushed work** branch, separately: delete or keep (default keep).

Delete with `git branch -d <b>` for merged branches and `git branch -D <b>` for the others the user approved. Report any that git refuses, with the reason.

## 6. Create the feature branch (only with an argument)

- Normalize to `FEAT-NNN`. A name instead of an ID: match it against the feature table in `docs/implementation-plan.md` and confirm.
- Slug: from `docs/features/FEAT-NNN-<slug>/` if it exists, else the plan row's name in kebab-case.
- Branch: `feat/NNN-<slug>`. If it already exists on origin, `git switch <branch>` (tracks `origin/<branch>`); otherwise `git switch -c <branch> origin/main`.
- Do not push it; `/push-feature` does that on the first commit.

## 7. Report

`main` before → after (commit SHAs, number of new commits), branches deleted, branches kept (with reason), stash created (if any), the branch you are on now, and the next step (for a new feature: `/detailed-design FEAT-NNN`, or `/sdlc-orchestrator` to find the next one).
