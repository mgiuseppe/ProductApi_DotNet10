# Contributing

## GitHub Flow

`main` is protected by a ruleset versioned in `.github/rulesets/main.json`:

- Every change starts from `main` on a short-lived branch (`feature/`, `fix/`, `docs/`, `chore/`) or a worktree, and reaches `main` only through a pull request.
- No direct pushes, no force pushes and no deletion of `main`, with no bypass, administrators included.
- **0 required approvals**: GitHub does not let authors approve their own pull requests, so with one maintainer a required review would block every merge. The review is reading the diff, with the `architecture-reviewer` subagent.
- When the CI exists, its jobs become required checks and pull requests use auto-merge.

Why: the project has one maintainer and direct pushes would be enough, but it is meant to work like a real application, where changes reach `main` only through pull requests. Git Flow (`develop`, release branches) was not chosen: it is built for versioned releases, not for an app deployed from `main`.

A migration becomes shared when it is merged into `main` ([ADR-0010](docs/adr/0010-migrations-editable-until-pushed.md)).

## Squash merge only

Pull requests are merged with **squash merge only** (the other methods are disabled), and branches are deleted after the merge:

- `main` reads as one commit per pull request, whose message is the pull request title; undoing a change is one `git revert`.
- The branch's intermediate commits (often the agent's small steps) are lost from `git log`; they stay visible in the closed pull request.
- **Pull requests stay small**: a change that deserves its own commits deserves its own pull request.
- **A branch is not reused after its merge**: further commits would bring back the squashed ones. Start a new branch from `main`.
- **Formatting-only changes go in their own pull request**, so the squash commit can be added to `.git-blame-ignore-revs`.

Merge commits would keep meaningful intermediate commits (useful for long-lived branches); rebase merges would put every intermediate commit on the main line, so they would need cleaning up before each merge.

## Workflow

```bash
git switch main && git pull
git switch -c feature/<short-name>          # or fix/, docs/, chore/
# ...work, then in Claude Code: /verify-and-commit (ask it to push and open the PR)...
gh pr merge --squash --delete-branch        # after reading the diff in the pull request
```

Commits go through `/verify-and-commit`, so every commit passes the quality gate (format, build, tests, migration checks).

## Architecture decisions

Write an ADR in `docs/adr/` only for decisions that change the structure of the system or are expensive to reverse (layers, persistence, messaging, transaction boundaries, external integrations, infrastructure). Workflow rules and implementation details go in the topic documents (`CONTRIBUTING.md`, `docs/CICD.md`, `docs/TestingStrategy.md`).
