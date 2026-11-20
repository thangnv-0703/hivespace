---
name: push-feature
description: Commit all pending changes for one feature (FEAT-NNN) on its feature branch and push it to origin, then offer to open or update the pull request to main. Use when the user asks to commit or push a feature's changes ("push FEAT-002", "commit everything for the profile feature", "ship feature 3 to remote") or runs /push-feature FEAT-NNN [commit message].
---

# Push a feature's changes

Arguments: `FEAT-NNN` (or a feature name) and an optional commit message. Run every git command from the repo root. This skill changes shared state (commits, pushes, PRs), so it shows what it will do before each outward step.

**Never:** `git reset --hard`, `git clean`, `git stash drop`, `git push --force` (or `--force-with-lease`), `--no-verify`, `git commit --amend`, a push to `main`, or a push to any remote other than `origin`. Never commit without first showing the file list.

## 1. Resolve the feature

- Normalize the argument to `FEAT-NNN` (three digits). A name instead of an ID: match it against the feature table in `docs/implementation-plan.md` and confirm the match.
- Slug: from the folder `docs/features/FEAT-NNN-<slug>/` if it exists, else the plan row's name in kebab-case.
- Branch: `feat/NNN-<slug>` (repo convention, e.g. `feat/001-register-and-sign-in`).
- No argument: take the feature from the current branch name (`feat/NNN-…`). Still unclear: ask which feature.
- Note the feature's FR IDs from its plan row; the PR title needs them (step 7).

## 2. Look before acting

Run `git fetch origin`, `git branch --show-current`, `git status --short`, and, if the branch already has an upstream, `git log --oneline @{u}..HEAD`.
Nothing to commit and nothing unpushed: say so and stop.

## 3. Get onto the feature branch

- **Already on it:** continue.
- **It exists** (local, or only as `origin/<branch>`): `git switch <branch>`. Uncommitted changes carry over. If git refuses (the switch would overwrite local changes), stop and report the files. Do not stash or discard anything.
- **It does not exist:**
  - If the current branch is another feature branch that is not yet in `origin/main` (`git merge-base --is-ancestor HEAD origin/main` fails), the pending work may build on it. Ask: **stack on `<current>`** (`git switch -c <branch>`, from the current HEAD; the PR base is then `<current>`) or **branch from main** (`git switch -c <branch> origin/main`).
  - Otherwise create it from `origin/main`: `git switch -c <branch> origin/main`.
  - If git refuses the switch from main because of conflicts with local changes, offer the stacked option instead or stop. Never force it.

## 4. Show and confirm the change set

List every modified, deleted and untracked file (`git status --porcelain=v1 -uall`), grouped by top-level folder, with counts.

Flag, and leave out unless the user says yes to each:
- **Secret-like files:** `.env*`, `*.pem`, `*.pfx`, `*.key`, `.claude/settings.local.json`, and any `appsettings*.json` or config whose diff adds a value under a key containing `Secret`, `Password`, `ApiKey` or `Token`.
- **Looks unrelated:** another feature's folder (`docs/features/FEAT-xxx-…` with a different number), build output (`bin/`, `obj/`, `dist/`, `node_modules/`, `.eslintcache`).

Ask once: commit everything listed (minus the flagged files), or exclude some. Stage the agreed files by explicit path: `git add -- <paths>` (this also stages deletions). Never `git add -A` or `git add .`.

## 5. Commit

- Message subject: the user's message if given, else `FEAT-NNN: <one-line summary drafted from the staged diff>` (repo history style, e.g. `FEAT-002: technical design, tasks and profile screen design`). Add the task ID when the change completes a task: `FEAT-NNN T5: …`.
- Body: a few lines on what changed, grouped by area, then the commit attribution lines the session's instructions give.
- Commit with a heredoc so the message keeps its lines.
- Hooks run. If one fails: report its output. Fix only what is inside the staged change and only with the user's agreement, re-stage, and make a **new** commit (no amend).

## 6. Push

- First push of the branch: `git push -u origin <branch>`. Later: `git push`.
- Rejected as non-fast-forward: show `git log --oneline HEAD..@{u}`, ask before `git pull --rebase`; on a no, stop and report. Never force.

## 7. Pull request (ask each time)

Check for an open PR: `gh pr list --head <branch> --state open --json number,url,title`.
Ask: **open PR** (or **update PR** when one exists) or **not now**. On yes:
- **Base:** `main`, or the parent branch when stacked in step 3.
- **Title** (dev-standards §4.2, CI checks it): `feat(<scope>): <feature name in lower case> [<FR IDs from the plan row>]`. Scope is the main service or folder touched (`identity`, `storefront`, `docs`…). For a docs-only change use `docs(<feature folder>)`.
- **Body:** what changed (short bullets), links to the feature's `technical-design.md`, `tasks.md`, `ui-design.md` when present, the dev-standards §4.4 checklist as unchecked boxes, then the PR attribution line the session's instructions give.
- `gh pr create --base <base> --head <branch> --title … --body-file <tmp>` (or `gh pr edit <n> --body-file <tmp>` to update). Write the body to a file in the scratchpad, never inline.
- Do not enable auto-merge.

## 8. Report

Branch (and whether it was created or stacked), commit SHA and subject, number of files committed, push result, PR link if any, and every file left uncommitted with the reason.
