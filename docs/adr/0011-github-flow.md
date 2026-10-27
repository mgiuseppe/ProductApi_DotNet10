# 0011. GitHub Flow with a protected `main`

- Status: Accepted
- Date: 2026-10-27

## Context
Until now every change was committed and pushed directly to `main`, with the quality gate run only locally by `/verify-and-commit`, which a manual `git push` can skip. The project has a single maintainer and runs only locally, so direct pushes would be enough. It is also a practice project meant to work like a real application, and a CI/CD pipeline to Azure is planned: in a real team, changes reach `main` only through reviewed pull requests with required checks.

## Decision
Adopt **GitHub Flow**, enforced on GitHub with a ruleset on `main`, versioned in `.github/rulesets/main.json`:
- every change starts from `main` on a short-lived branch (`feature/`, `fix/`, `docs/`, `chore/`) or a worktree, and reaches `main` only through a pull request;
- no direct pushes, no force pushes and no deletion of `main`, **with no bypass**, administrators included;
- **0 required approvals**: GitHub does not let authors approve their own pull requests, so with one maintainer a required review would block every merge. The review is reading the diff in the pull request, with the `architecture-reviewer` subagent;
- branches are deleted after the merge; the merge method is in [0012](0012-squash-merge-only.md);
- when the CI exists, its jobs become required status checks and pull requests use auto-merge.

## Consequences
- `main` changes only through pull requests: each change, including the agent's work, gets a diff to read before it lands.
- Every change costs a branch and a pull request more than a direct push, which is pure overhead for a single maintainer of a local app; accepted on purpose to practise the real workflow.
- Until the CI exists, nothing on GitHub checks the code: the gate is still `/verify-and-commit`, run before pushing the branch.
- Feature branches can be rewritten (rebase, force push) until the merge. A migration becomes shared when it is merged into `main`: the rule of [0010](0010-migrations-editable-until-pushed.md) ("on `origin/main`") is unchanged, and `main` is now reached only through a merge.
- Fixing a mistake on `main` (like the force pushes done before this ADR) also requires a pull request.

## Alternatives considered
- **Direct pushes to `main`, CI afterwards**: the simplest for one maintainer; the CI would detect a broken `main` instead of preventing it, and there is no point where the agent's work is reviewed before landing.
- **Git Flow** (`develop`, release and hotfix branches): built for versioned releases; too heavy for an application deployed from `main`.
